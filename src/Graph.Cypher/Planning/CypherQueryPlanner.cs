// Copyright CVOYA. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      https://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Linq.Expressions;
using Cvoya.Graph.Cypher.Ast;
using Cvoya.Graph.Cypher.Ast.Expressions;
using Cvoya.Graph.Cypher.Validation;
using Cvoya.Graph.Querying;
using AstBinaryExpression = Cvoya.Graph.Cypher.Ast.Expressions.BinaryExpression;
using AstDepthRange = Cvoya.Graph.Cypher.Ast.DepthRange;
using AstIndexExpression = Cvoya.Graph.Cypher.Ast.Expressions.IndexExpression;
using AstUnaryExpression = Cvoya.Graph.Cypher.Ast.Expressions.UnaryExpression;

namespace Cvoya.Graph.Cypher.Planning;

/// <summary>
/// Lowers a provider-independent <see cref="GraphQueryModel"/> into a typed Cypher statement.
/// </summary>
public sealed class CypherQueryPlanner
{
    /// <summary>
    /// Plans a graph query model as a typed Cypher statement.
    /// </summary>
    /// <param name="model">The provider-independent query model.</param>
    /// <returns>The planned Cypher statement.</returns>
    public CypherStatement Plan(GraphQueryModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        // Unconditional: the model validator guards user-reachable semantic errors that Cypher cannot
        // catch (it returns nulls for mis-bound references instead of erroring). One cheap pass per
        // query is negligible next to the network round-trip.
        GraphQueryModelValidator.Validate(model);

        var parameters = new CypherParameterRegistry();
        var lowerer = new ExpressionToCypherAstLowerer(parameters);
        var state = CreateState(model, parameters);

        var predicates = LowerPredicates(model, state, lowerer);
        var ordering = LowerOrdering(model, state, lowerer);
        var projection = LowerProjection(model, state, lowerer);

        var clauses = new List<ICypherClause>();
        AddRootAndTraversalClauses(clauses, model, state);
        AddSearchFilterClause(clauses, model.SearchFilter);
        clauses.AddRange(lowerer.NavigationMatches);

        if (predicates.Count > 0)
        {
            if (lowerer.NavigationMatches.Count > 0)
            {
                // A bare WHERE would attach to the last OPTIONAL MATCH above and stop filtering rows.
                clauses.Add(WithClause.All);
            }

            clauses.Add(new WhereClause(predicates.Count == 1
                ? predicates[0]
                : new ConjunctionExpression(predicates)));
        }

        if (model.PathShape is not null)
        {
            AddGraphPathProjection(clauses);
        }
        else
        {
            AddTerminalAndProjection(clauses, model, state, ordering, projection, parameters);
        }

        var pathTypes = model.PathShape is null
            ? null
            : new CypherPathTypes(
                model.PathShape.SourceType,
                model.PathShape.RelationshipType,
                model.PathShape.TargetType);
        var statement = new CypherStatement(clauses, parameters.Parameters, pathTypes);

#if DEBUG
        new CypherAstValidator().Run(statement);
#endif

        return statement;
    }

    private static PlanningState CreateState(GraphQueryModel model, CypherParameterRegistry parameters)
    {
        var rootType = GetRootType(model.Root);
        var rootAlias = model.Root switch
        {
            SearchRoot { Target: SearchRootTarget.Nodes } => "n",
            SearchRoot { Target: SearchRootTarget.Relationships } => "r",
            RelationshipRoot => "r",
            DynamicRoot { ElementType: { } type } when typeof(IRelationship).IsAssignableFrom(type) => "r",
            NodeRoot when rootType is { IsInterface: true } &&
                typeof(INode).IsAssignableFrom(rootType) &&
                model.Traversal.Any(step => !step.IsComplexPropertyTraversal) => "src_1",
            _ => "src",
        };

        var explicitTraversal = model.Traversal.Where(step => !step.IsComplexPropertyTraversal).ToArray();
        var targetAlias = explicitTraversal.Length switch
        {
            0 => rootAlias,
            1 => "tgt",
            _ => $"tgt_{explicitTraversal.Length}",
        };
        var targetType = explicitTraversal.LastOrDefault()?.TargetType ?? rootType;

        if (model.Root is SearchRoot search)
        {
            parameters.Add(search.Query);
        }
        else if (model.SearchFilter is { } searchFilter)
        {
            parameters.Add(searchFilter.Query);
        }

        return new PlanningState(rootAlias, rootType, targetAlias, targetType, explicitTraversal);
    }

    private static List<CypherExpression> LowerPredicates(
        GraphQueryModel model,
        PlanningState state,
        ExpressionToCypherAstLowerer lowerer)
    {
        var predicates = new List<CypherExpression>();

        if (model.Root is SearchRoot search)
        {
            predicates.AddRange(BuildSearchPredicates(search));
        }

        if (model.SearchFilter is { } searchFilter)
        {
            predicates.AddRange(BuildSearchFilterPredicates(searchFilter, state));
        }

        foreach (var predicate in model.Predicates)
        {
            var alias = predicate.Alias ?? state.CurrentAlias;
            if (alias == "src" && state.RootAlias == "src_1")
            {
                alias = state.RootAlias;
            }

            if (model.Root is SearchRoot { Target: SearchRootTarget.Nodes })
            {
                alias = "n";
            }

            predicates.Add(lowerer.LowerLambda(predicate.Predicate, alias));
        }

        if (model.Join is not null)
        {
            predicates.Add(LowerJoinCondition(model.Join, lowerer));
        }

        return predicates;
    }

    private static IReadOnlyList<CypherExpression> BuildSearchFilterPredicates(
        SearchRoot search,
        PlanningState state)
    {
        var searchAlias = search.Target switch
        {
            SearchRootTarget.Nodes => "searchedNode",
            SearchRootTarget.Relationships => "searchedRelationship",
            _ => throw new GraphQueryTranslationException(
                "Full-text search over mixed entities cannot be applied after a traversal."),
        };
        var predicates = new List<CypherExpression>
        {
            new AstBinaryExpression(
                CypherBinaryOperator.Equal,
                new VariableRef(state.CurrentAlias),
                new VariableRef(searchAlias))
        };

        if (search.Target == SearchRootTarget.Nodes &&
            search.ElementType is { } elementType &&
            elementType != typeof(INode) &&
            elementType != typeof(DynamicNode))
        {
            predicates.Add(new LabelTest(new VariableRef(searchAlias), Labels.GetCompatibleLabels(elementType)));
        }

        return predicates;
    }

    private static IReadOnlyList<CypherExpression> BuildSearchPredicates(SearchRoot search)
    {
        if (search.Target == SearchRootTarget.Nodes)
        {
            if (search.ElementType is null || search.ElementType == typeof(INode) || search.ElementType == typeof(DynamicNode))
            {
                return [];
            }

            return [new LabelTest(new VariableRef("n"), Labels.GetCompatibleLabels(search.ElementType))];
        }

        return [];
    }

    private static CypherExpression LowerJoinCondition(
        JoinFragment join,
        ExpressionToCypherAstLowerer lowerer)
    {
        var outerAliases = new Dictionary<ParameterExpression, string>
        {
            [join.OuterKeySelector.Parameters[0]] = typeof(IRelationship).IsAssignableFrom(join.OuterKeySelector.Parameters[0].Type)
                ? "r"
                : "src",
        };
        var innerAliases = new Dictionary<ParameterExpression, string>
        {
            [join.InnerKeySelector.Parameters[0]] = "joined",
        };

        return new AstBinaryExpression(
            CypherBinaryOperator.Equal,
            lowerer.Lower(join.OuterKeySelector.Body, outerAliases),
            lowerer.Lower(join.InnerKeySelector.Body, innerAliases));
    }

    private static IReadOnlyList<OrderByItem> LowerOrdering(
        GraphQueryModel model,
        PlanningState state,
        ExpressionToCypherAstLowerer lowerer)
    {
        return model.Ordering
            .Select(key => new OrderByItem(
                lowerer.LowerLambda(
                    RewriteOrderingAfterProjection(model.Projection?.Selector, key.KeySelector),
                    key.Alias ?? state.CurrentAlias),
                key.Descending))
            .ToArray();
    }

    private static LambdaExpression RewriteOrderingAfterProjection(
        LambdaExpression? projection,
        LambdaExpression ordering)
    {
        if (projection is null || ordering.Parameters.Count != 1 ||
            ordering.Parameters[0].Type != projection.ReturnType)
        {
            return ordering;
        }

        var orderingParameter = ordering.Parameters[0];
        var body = new ProjectedValueRewriter(orderingParameter, projection.Body).Visit(ordering.Body)
            ?? ordering.Body;
        if (ReferencesParameter(body, orderingParameter))
        {
            throw new GraphQueryTranslationException(
                "The ordering key cannot be mapped through the preceding Select projection; order before " +
                "projecting, or project the ordering key explicitly.");
        }

        return Expression.Lambda(body, projection.Parameters);
    }

    private static bool ReferencesParameter(Expression body, ParameterExpression parameter)
    {
        var finder = new ParameterReferenceFinder(parameter);
        finder.Visit(body);
        return finder.Found;
    }

    private sealed class ParameterReferenceFinder(ParameterExpression parameter) : ExpressionVisitor
    {
        public bool Found { get; private set; }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (node == parameter)
            {
                Found = true;
            }

            return node;
        }
    }

    private static ProjectionPlan LowerProjection(
        GraphQueryModel model,
        PlanningState state,
        ExpressionToCypherAstLowerer lowerer)
    {
        if (model.Join is { } join)
        {
            var joinBody = StripConvert(join.ResultSelector.Body);
            if (joinBody is ParameterExpression parameter)
            {
                var parameterIndex = join.ResultSelector.Parameters.IndexOf(parameter);
                return parameterIndex == 1
                    ? ProjectionPlan.Node("joined", join.ResultSelector.ReturnType, loadProperties: false)
                    : ProjectionPlan.PathSegment("src", "r", "tgt", null, null, loadProperties: false);
            }

            var aliases = new Dictionary<ParameterExpression, string>
            {
                [join.ResultSelector.Parameters[0]] = "r",
                [join.ResultSelector.Parameters[1]] = "joined",
            };
            return ProjectionPlan.Scalar([new ReturnItem(lowerer.Lower(joinBody, aliases), null)]);
        }

        if (model.Projection?.Selector is not { } selector)
        {
            return model.Root switch
            {
                SearchRoot { Target: SearchRootTarget.Entities } =>
                    ProjectionPlan.Scalar([new ReturnItem(new VariableRef("entity"), "entity")]),
                RelationshipRoot or SearchRoot { Target: SearchRootTarget.Relationships } =>
                    ProjectionPlan.PathSegment("src", "r", "tgt", null, null, loadProperties: true),
                DynamicRoot { ElementType: { } type } when typeof(IRelationship).IsAssignableFrom(type) =>
                    ProjectionPlan.PathSegment("src", "r", "tgt", null, null, loadProperties: true),
                _ => ProjectionPlan.Node(state.CurrentAlias, state.CurrentType, HasComplexProperties(state.CurrentType)),
            };
        }

        var body = StripConvert(selector.Body);
        if (body is NewExpression @new)
        {
            var items = @new.Arguments.Select((argument, index) => new ReturnItem(
                LowerProjectionItem(argument, selector, state, lowerer),
                @new.Members?[index].Name ?? $"Property{index}"));
            return ProjectionPlan.Scalar(items.ToArray());
        }

        if (model.Projection.Kind == ProjectionKind.Identity &&
            typeof(IRelationship).IsAssignableFrom(selector.ReturnType))
        {
            return ProjectionPlan.PathSegment("src", "r", "tgt", null, null, loadProperties: true);
        }

        if (model.Projection.Kind == ProjectionKind.PathSegment)
        {
            if (body is ParameterExpression)
            {
                return ProjectionPlan.PathSegment(
                    state.CurrentTraversalSourceAlias,
                    state.CurrentRelationshipAlias,
                    state.TargetAlias,
                    state.RootType,
                    state.CurrentType,
                    loadProperties: true);
            }

            if (body is MemberExpression member && TryGetDirectPathSegmentMember(member, out var component))
            {
                return component switch
                {
                    nameof(IGraphPathSegment.EndNode) =>
                        ProjectionPlan.Node(state.TargetAlias, member.Type, HasComplexProperties(member.Type)),
                    nameof(IGraphPathSegment.Relationship) =>
                        ProjectionPlan.PathSegment(
                            state.CurrentTraversalSourceAlias,
                            state.CurrentRelationshipAlias,
                            state.TargetAlias,
                            state.RootType,
                            state.CurrentType,
                            loadProperties: true),
                    nameof(IGraphPathSegment.StartNode) =>
                        ProjectionPlan.Node(
                            state.CurrentTraversalSourceAlias,
                            member.Type,
                            HasComplexProperties(member.Type)),
                    _ => throw new InvalidOperationException(),
                };
            }
        }

        var expression = lowerer.LowerLambda(selector, state.CurrentAlias);
        return ProjectionPlan.Scalar([new ReturnItem(expression, null)]);
    }

    private static CypherExpression LowerProjectionItem(
        Expression argument,
        LambdaExpression selector,
        PlanningState state,
        ExpressionToCypherAstLowerer lowerer)
    {
        var item = StripConvert(argument);
        if (item is ParameterExpression parameter &&
            typeof(IGraphPathSegment).IsAssignableFrom(parameter.Type))
        {
            return PathSegmentMap(state);
        }

        if (item is MemberExpression member && TryGetDirectPathSegmentMember(member, out var component))
        {
            return component switch
            {
                nameof(IGraphPathSegment.Relationship) => RelationshipMap(state),
                nameof(IGraphPathSegment.StartNode) => new EntityProjectionExpression(
                    state.CurrentTraversalSourceAlias,
                    HasComplexProperties(member.Type)),
                nameof(IGraphPathSegment.EndNode) => new EntityProjectionExpression(
                    state.TargetAlias,
                    HasComplexProperties(member.Type)),
                _ => throw new InvalidOperationException(),
            };
        }

        if (item is ParameterExpression relationship &&
            typeof(IRelationship).IsAssignableFrom(relationship.Type))
        {
            return RelationshipMap(state);
        }

        if (item is ParameterExpression node && typeof(INode).IsAssignableFrom(node.Type))
        {
            return new EntityProjectionExpression(state.CurrentAlias, HasComplexProperties(node.Type));
        }

        return lowerer.LowerLambda(Expression.Lambda(argument, selector.Parameters), state.CurrentAlias);
    }

    private static MapExpression RelationshipMap(PlanningState state) => new(
    [
        new MapEntry("StartNode", new VariableRef(state.CurrentTraversalSourceAlias)),
        new MapEntry("Relationship", new VariableRef(state.CurrentRelationshipAlias)),
        new MapEntry("EndNode", new VariableRef(state.CurrentEndNodeAlias)),
    ]);

    private static MapExpression PathSegmentMap(PlanningState state) => RelationshipMap(state);

    private static void AddRootAndTraversalClauses(
        List<ICypherClause> clauses,
        GraphQueryModel model,
        PlanningState state)
    {
        if (model.Join is { } join)
        {
            clauses.Add(BuildRootMatch(join.InnerRoot, "joined", compatibleLabels: false));
            clauses.Add(BuildRootMatch(model.Root, state.RootAlias));
            return;
        }

        if (model.Root is SearchRoot { Target: SearchRootTarget.Nodes })
        {
            clauses.Add(CallClause.WithAliasedYields(
                "search.nodes",
                [new Literal("node_fulltext_index"), new QueryParameter("p0")],
                [new CallYield("node", "n")]));
            return;
        }

        if (model.Root is SearchRoot { Target: SearchRootTarget.Relationships } searchRelationship)
        {
            clauses.Add(CallClause.WithAliasedYields(
                "search.relationships",
                [new Literal("rel_fulltext_index"), new QueryParameter("p0")],
                [new CallYield("relationship", "r")]));
            var elementType = searchRelationship.ElementType ?? typeof(IRelationship);
            QueryRoot root = elementType == typeof(IRelationship) || elementType == typeof(DynamicRelationship)
                ? new DynamicRoot(elementType)
                : new RelationshipRoot(elementType);
            clauses.Add(BuildRootMatch(root, "r"));
            return;
        }

        if (model.Root is SearchRoot { Target: SearchRootTarget.Entities })
        {
            clauses.Add(new CallClause(
                "search.entities",
                [
                    new Literal("node_fulltext_index"),
                    new Literal("rel_fulltext_index"),
                    new QueryParameter("p0")
                ],
                ["entity"]));
            return;
        }

        if (state.ExplicitTraversal.Count > 0 && model.Root is NodeRoot or DynamicRoot)
        {
            clauses.Add(BuildTraversalMatch(model.Root, state.ExplicitTraversal, model.PathShape is null ? null : "p"));
            return;
        }

        clauses.Add(BuildRootMatch(model.Root, state.RootAlias));
    }

    private static void AddSearchFilterClause(List<ICypherClause> clauses, SearchRoot? search)
    {
        if (search is null)
            return;

        var (procedure, index, yieldedName, alias) = search.Target switch
        {
            SearchRootTarget.Nodes => ("search.nodes", "node_fulltext_index", "node", "searchedNode"),
            SearchRootTarget.Relationships =>
                ("search.relationships", "rel_fulltext_index", "relationship", "searchedRelationship"),
            _ => throw new GraphQueryTranslationException(
                "Full-text search over mixed entities cannot be applied after a traversal."),
        };

        clauses.Add(CallClause.WithAliasedYields(
            procedure,
            [new Literal(index), new QueryParameter("p0")],
            [new CallYield(yieldedName, alias)]));
    }

    private static MatchClause BuildRootMatch(QueryRoot root, string alias, bool compatibleLabels = true)
    {
        PathPattern pattern = root switch
        {
            NodeRoot node => new PathPattern([new NodePattern(
                alias,
                compatibleLabels ? Labels.GetCompatibleLabels(node.ElementType) : [Labels.GetLabelFromType(node.ElementType)])]),
            RelationshipRoot relationship => new PathPattern(
            [
                new NodePattern("src", []),
                new RelationshipPattern("r", string.Join('|', Labels.GetCompatibleLabels(relationship.ElementType)), CypherDirection.Outgoing, null),
                new NodePattern("tgt", [])
            ]),
            DynamicRoot { ElementType: { } type } when typeof(IRelationship).IsAssignableFrom(type) => new PathPattern(
            [
                new NodePattern("src", []),
                new RelationshipPattern("r", null, CypherDirection.Outgoing, null),
                new NodePattern("tgt", [])
            ]),
            DynamicRoot => new PathPattern([new NodePattern(alias, [])]),
            _ => throw new GraphQueryTranslationException($"Query root '{root.GetType().Name}' cannot be lowered as a MATCH pattern."),
        };

        return new MatchClause([pattern], optional: false);
    }

    private static MatchClause BuildTraversalMatch(
        QueryRoot root,
        IReadOnlyList<TraversalStep> traversal,
        string? pathAlias)
    {
        var rootType = GetRootType(root);
        var rootAlias = rootType is { IsInterface: true } && typeof(INode).IsAssignableFrom(rootType)
            ? "src_1"
            : "src";
        var patterns = new List<PathPattern>();

        for (var index = 0; index < traversal.Count; index++)
        {
            var step = traversal[index];
            var relationshipAlias = index == 0 ? "r" : $"r_{index + 1}";
            var targetAlias = index == 0 ? "tgt" : $"tgt_{index + 1}";
            var sourceAlias = step.SourceAlias == "src" && rootAlias == "src_1"
                ? rootAlias
                : step.SourceAlias ?? (index == 0 ? rootAlias : index == 1 ? "tgt" : $"tgt_{index}");
            var depth = step.Depth is { Min: 1, Max: 1 }
                ? null
                : new AstDepthRange(step.Depth.Min, step.Depth.Max);

            patterns.Add(new PathPattern(
            [
                new NodePattern(
                    sourceAlias,
                    sourceAlias == rootAlias && rootType is not null ? Labels.GetCompatibleLabels(rootType) : []),
                new RelationshipPattern(
                    relationshipAlias,
                    step.RelationshipClrType is null
                        ? step.RelationshipType
                        : string.Join('|', Labels.GetCompatibleLabels(step.RelationshipClrType)),
                    step.Direction switch
                    {
                        GraphTraversalDirection.Outgoing => CypherDirection.Outgoing,
                        GraphTraversalDirection.Incoming => CypherDirection.Incoming,
                        GraphTraversalDirection.Both => CypherDirection.Both,
                        _ => throw new GraphQueryTranslationException($"Traversal direction '{step.Direction}' is not supported."),
                    },
                    depth),
                new NodePattern(
                    targetAlias,
                    step.TargetType is null ? [] : Labels.GetCompatibleLabels(step.TargetType))
            ], index == traversal.Count - 1 ? pathAlias : null));
        }

        return new MatchClause(patterns, optional: false);
    }

    private static void AddTerminalAndProjection(
        List<ICypherClause> clauses,
        GraphQueryModel model,
        PlanningState state,
        IReadOnlyList<OrderByItem> ordering,
        ProjectionPlan projection,
        CypherParameterRegistry parameters)
    {
        var aggregate = model.Terminal is TerminalOperation.Count or TerminalOperation.Sum or
            TerminalOperation.Average or TerminalOperation.Min or TerminalOperation.Max;
        var hasPaging = model.Paging.Skip is not null || model.Paging.Take is not null;

        if (aggregate && hasPaging)
        {
            clauses.Add(new WithClause([new ReturnItem(new VariableRef(state.CurrentAlias), null)], distinct: false));
            AddOrderingAndPaging(clauses, ordering, model.Paging, reverseOrdering: false);
            clauses.Add(BuildAggregateReturn(model, state, projection));
            return;
        }

        if (model.Terminal is TerminalOperation.Any or TerminalOperation.All)
        {
            clauses.Add(new ReturnClause(
            [
                new ReturnItem(
                    new AstBinaryExpression(
                        CypherBinaryOperator.GreaterThan,
                        Function("COUNT", new VariableRef(state.CurrentAlias)),
                        new Literal(0)),
                    "exists")
            ], distinct: false));
            return;
        }

        if (model.Terminal == TerminalOperation.Contains)
        {
            var source = projection.Items?.SingleOrDefault()?.Expression ?? new VariableRef(state.CurrentAlias);
            var item = parameters.Add(model.TerminalOperand);
            var sourceIsNull = new AstUnaryExpression(CypherUnaryOperator.IsNull, source);
            var nullCount = Function("count", new CaseExpression(sourceIsNull, new Literal(1)));
            var anyNull = new AstBinaryExpression(CypherBinaryOperator.GreaterThan, nullCount, new Literal(0));
            var contains = Function(
                "coalesce",
                new AstBinaryExpression(CypherBinaryOperator.In, item, Function("collect", source)),
                new Literal(false));
            clauses.Add(new ReturnClause(
            [
                new ReturnItem(
                    new CaseExpression(
                        new AstUnaryExpression(CypherUnaryOperator.IsNull, item),
                        anyNull,
                        contains),
                    "contains")
            ], distinct: false));
            return;
        }

        if (aggregate)
        {
            clauses.Add(BuildAggregateReturn(model, state, projection));
            return;
        }

        if (model.Distinct && projection.Items is { Count: > 0 } items &&
            (hasPaging || ordering.Count > 0))
        {
            AddDistinctScalarProjection(clauses, model, ordering, items);
            return;
        }

        var reverseOrdering = model.Terminal == TerminalOperation.Last;
        AddOrderingAndPaging(clauses, ordering, model.Paging, reverseOrdering);
        AddTerminalLimit(clauses, model);

        AddProjectionClause(clauses, model, projection);
    }

    /// <summary>
    /// Adds the row limit implied by a cardinality terminal. An explicit Take already bounds the
    /// query, so the terminal's limit would only narrow it further.
    /// </summary>
    private static void AddTerminalLimit(List<ICypherClause> clauses, GraphQueryModel model)
    {
        int? terminalLimit = model.Terminal switch
        {
            TerminalOperation.First or TerminalOperation.Last => 1,
            TerminalOperation.Single => 2,
            _ => null,
        };

        if (terminalLimit is not null && model.Paging.Take is null)
        {
            clauses.Add(new LimitClause(new Literal(terminalLimit.Value)));
        }
    }

    private static void AddDistinctScalarProjection(
        List<ICypherClause> clauses,
        GraphQueryModel model,
        IReadOnlyList<OrderByItem> ordering,
        IReadOnlyList<ReturnItem> items)
    {
        var aliases = items
            .Select((item, index) => item.Alias ?? $"__value{index}")
            .ToArray();
        clauses.Add(new WithClause(
            items.Select((item, index) => new ReturnItem(item.Expression, aliases[index])).ToArray(),
            distinct: true));

        var projectedOrdering = ordering.Select(order =>
        {
            var index = FindProjectionIndex(items, order.Expression);
            if (index < 0)
            {
                throw new GraphQueryTranslationException(
                    "Ordering a distinct projection requires the ordering key to be part of the projection.");
            }

            return new OrderByItem(new VariableRef(aliases[index]), order.Descending);
        }).ToArray();
        AddOrderingAndPaging(
            clauses,
            projectedOrdering,
            model.Paging,
            reverseOrdering: model.Terminal == TerminalOperation.Last);
        AddTerminalLimit(clauses, model);

        clauses.Add(new ReturnClause(
            items.Select((item, index) => new ReturnItem(new VariableRef(aliases[index]), item.Alias)).ToArray(),
            distinct: false));
    }

    private static int FindProjectionIndex(
        IReadOnlyList<ReturnItem> items,
        CypherExpression expression)
    {
        for (var index = 0; index < items.Count; index++)
        {
            if (items[index].Expression == expression)
                return index;
        }

        return -1;
    }

    private static ReturnClause BuildAggregateReturn(
        GraphQueryModel model,
        PlanningState state,
        ProjectionPlan projection)
    {
        var argument = projection.Items?.SingleOrDefault()?.Expression ?? new VariableRef(state.CurrentAlias);
        var function = model.Terminal switch
        {
            TerminalOperation.Count => "count",
            TerminalOperation.Sum => "sum",
            TerminalOperation.Average => "avg",
            TerminalOperation.Min => "min",
            TerminalOperation.Max => "max",
            _ => throw new InvalidOperationException($"Terminal '{model.Terminal}' is not an aggregate."),
        };
        return new ReturnClause([new ReturnItem(Function(function, argument), null)], distinct: false);
    }

    private static void AddOrderingAndPaging(
        List<ICypherClause> clauses,
        IReadOnlyList<OrderByItem> ordering,
        Paging paging,
        bool reverseOrdering)
    {
        if (ordering.Count > 0)
        {
            clauses.Add(new OrderByClause(reverseOrdering
                ? ordering.Select(item => new OrderByItem(item.Expression, !item.Descending)).ToArray()
                : ordering));
        }

        if (paging.Skip is { } skip)
        {
            clauses.Add(new SkipClause(new Literal(skip)));
        }

        if (paging.Take is { } take)
        {
            clauses.Add(new LimitClause(new Literal(take)));
        }
    }

    private static void AddProjectionClause(
        List<ICypherClause> clauses,
        GraphQueryModel model,
        ProjectionPlan projection)
    {
        if (projection.Items is not null)
        {
            clauses.Add(new ReturnClause(
                projection.Items,
                model.Distinct || model.Terminal == TerminalOperation.Distinct));
            return;
        }

        clauses.Add(new EntityProjectionClause(
            projection.Shape,
            projection.SourceAlias!,
            projection.RelationshipAlias,
            projection.TargetAlias,
            projection.LoadSourceProperties,
            projection.LoadTargetProperties));
    }

    private static void AddGraphPathProjection(List<ICypherClause> clauses)
    {
        clauses.Add(new WithClause(
            [new ReturnItem(Function("collect", new VariableRef("p")), "__paths")],
            distinct: false));
        clauses.Add(new UnwindClause(
            Function(
                "range",
                new Literal(0),
                new AstBinaryExpression(
                    CypherBinaryOperator.Subtract,
                    Function("size", new VariableRef("__paths")),
                    new Literal(1))),
            "pathIndex"));
        clauses.Add(new WithClause(
        [
            new ReturnItem(new AstIndexExpression(new VariableRef("__paths"), new VariableRef("pathIndex")), "p"),
            new ReturnItem(new VariableRef("pathIndex"), null)
        ], distinct: false));
        clauses.Add(new UnwindClause(
            Function(
                "range",
                new Literal(0),
                new AstBinaryExpression(
                    CypherBinaryOperator.Subtract,
                    Function("length", new VariableRef("p")),
                    new Literal(1))),
            "hopIndex"));
        clauses.Add(new WithClause(
        [
            new ReturnItem(new VariableRef("pathIndex"), null),
            new ReturnItem(new VariableRef("hopIndex"), null),
            new ReturnItem(
                new AstIndexExpression(Function("nodes", new VariableRef("p")), new VariableRef("hopIndex")),
                "src"),
            new ReturnItem(
                new AstIndexExpression(Function("relationships", new VariableRef("p")), new VariableRef("hopIndex")),
                "r"),
            new ReturnItem(
                new AstIndexExpression(
                    Function("nodes", new VariableRef("p")),
                    new AstBinaryExpression(CypherBinaryOperator.Add, new VariableRef("hopIndex"), new Literal(1))),
                "tgt")
        ], distinct: false));
        clauses.Add(new EntityProjectionClause(
            EntityProjectionShape.PathSegment,
            "src",
            "r",
            "tgt",
            loadSourceProperties: false,
            loadTargetProperties: false,
            includePathCoordinates: true));
        clauses.Add(new OrderByClause(
        [
            new OrderByItem(new VariableRef("pathIndex"), descending: false),
            new OrderByItem(new VariableRef("hopIndex"), descending: false)
        ]));
    }

    private static bool TryGetDirectPathSegmentMember(MemberExpression member, out string component)
    {
        component = string.Empty;
        if (member.Expression is not ParameterExpression parameter ||
            !typeof(IGraphPathSegment).IsAssignableFrom(parameter.Type))
        {
            return false;
        }

        component = member.Member.Name;
        return component is nameof(IGraphPathSegment.StartNode) or
            nameof(IGraphPathSegment.Relationship) or
            nameof(IGraphPathSegment.EndNode);
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, bool> ComplexPropertyCache = new();

    private static bool HasComplexProperties(Type? type)
    {
        if (type is null || type == typeof(DynamicNode))
            return true;

        return ComplexPropertyCache.GetOrAdd(type, static queried =>
            Labels.GetCompatibleLabels(queried).Any(label =>
            {
                try
                {
                    return GraphDataModel.GetComplexProperties(Labels.GetTypeFromLabel(label)).Any();
                }
                catch (GraphException)
                {
                    // "Could not resolve the label to a type" must not become "has no complex
                    // properties": that would silently skip loading and materialize nulls. Loading
                    // for a type that turns out to have none is only wasted work.
                    return true;
                }
            }));
    }

    private static Type? GetRootType(QueryRoot root)
    {
        return root switch
        {
            NodeRoot node => node.ElementType,
            RelationshipRoot relationship => relationship.ElementType,
            SearchRoot search => search.ElementType,
            DynamicRoot dynamic => dynamic.ElementType,
            _ => null,
        };
    }

    private static Expression StripConvert(Expression expression)
    {
        while (expression is System.Linq.Expressions.UnaryExpression
            { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
        {
            expression = unary.Operand;
        }

        return expression;
    }

    private static FunctionCall Function(string name, params CypherExpression[] arguments) => new(name, arguments);

    private sealed record PlanningState(
        string RootAlias,
        Type? RootType,
        string TargetAlias,
        Type? CurrentType,
        IReadOnlyList<TraversalStep> ExplicitTraversal)
    {
        public string CurrentAlias => ExplicitTraversal.Count == 0 ? RootAlias : TargetAlias;

        public string CurrentTraversalSourceAlias =>
            ExplicitTraversal.LastOrDefault()?.SourceAlias switch
            {
                "src" when RootAlias == "src_1" => RootAlias,
                { } sourceAlias => sourceAlias,
                _ => RootAlias == "r" ? "src" : RootAlias,
            };

        public string CurrentRelationshipAlias => ExplicitTraversal.Count <= 1
            ? "r"
            : $"r_{ExplicitTraversal.Count}";

        public string CurrentEndNodeAlias =>
            ExplicitTraversal.Count == 0 && RootAlias == "r" ? "tgt" : TargetAlias;
    }

    private sealed class ProjectedValueRewriter(ParameterExpression parameter, Expression projection) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node)
        {
            return node == parameter && projection is not NewExpression ? projection : base.VisitParameter(node);
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression == parameter && projection is NewExpression @new && @new.Members is not null)
            {
                var index = @new.Members.IndexOf(node.Member);
                if (index >= 0)
                    return @new.Arguments[index];
            }

            return base.VisitMember(node);
        }
    }

    private sealed record ProjectionPlan(
        EntityProjectionShape Shape,
        string? SourceAlias,
        string? RelationshipAlias,
        string? TargetAlias,
        bool LoadSourceProperties,
        bool LoadTargetProperties,
        IReadOnlyList<ReturnItem>? Items)
    {
        public static ProjectionPlan Node(string alias, Type? type, bool loadProperties) =>
            new(EntityProjectionShape.Node, alias, null, null, loadProperties, false, null);

        public static ProjectionPlan PathSegment(
            string source,
            string relationship,
            string target,
            Type? sourceType,
            Type? targetType,
            bool loadProperties) =>
            new(
                EntityProjectionShape.PathSegment,
                source,
                relationship,
                target,
                loadProperties && HasComplexProperties(sourceType),
                loadProperties && HasComplexProperties(targetType),
                null);

        public static ProjectionPlan Scalar(IReadOnlyList<ReturnItem> items) =>
            new(EntityProjectionShape.Node, null, null, null, false, false, items);
    }
}
