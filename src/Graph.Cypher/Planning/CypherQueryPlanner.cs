// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

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
    private readonly ICypherDialect dialect;

    /// <summary>Initializes a planner for a specific Cypher dialect.</summary>
    /// <param name="dialect">The dialect whose capabilities constrain translation.</param>
    public CypherQueryPlanner(ICypherDialect dialect)
    {
        this.dialect = dialect ?? throw new ArgumentNullException(nameof(dialect));
        ArgumentException.ThrowIfNullOrWhiteSpace(dialect.Name);
    }

    /// <summary>
    /// Plans a graph query model as a typed Cypher statement.
    /// </summary>
    /// <param name="model">The provider-independent query model.</param>
    /// <returns>The planned Cypher statement.</returns>
    public CypherStatement Plan(GraphQueryModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (model.Root is SearchRoot || model.SearchFilter is not null)
        {
            RequireCapability(GraphCapability.FullTextSearch, "FullTextSearch");
        }

        // Representation and support are distinct: the model can carry these operations, but this
        // planner cannot lower them yet. Reject before validation so the error is stable.
        if (model.SelectMany is not null)
        {
            throw new GraphQueryTranslationException(
                "Cannot plan the query: SelectMany is not supported by graph query translation yet; see #100.");
        }

        if (model.GroupBy is not null)
        {
            RequireCapability(GraphCapability.CallSubqueries, "GroupBy");
            throw new GraphQueryTranslationException(
                "Cannot plan the query: GroupBy is not supported by graph query translation yet; see #100.");
        }

        if (model.Union is not null)
        {
            throw new GraphQueryTranslationException(
                "Cannot plan the query: Union is not supported by graph query translation yet.");
        }

        // Unconditional: the model validator guards user-reachable semantic errors that Cypher cannot
        // catch (it returns nulls for mis-bound references instead of erroring). One cheap pass per
        // query is negligible next to the network round-trip.
        GraphQueryModelValidator.Validate(model);

        var parameters = new CypherParameterRegistry();
        var lowerer = new ExpressionToCypherAstLowerer(parameters, dialect);
        var state = CreateState(model, parameters);

        var predicates = LowerPredicates(model, state, lowerer);
        var ordering = LowerOrdering(model, state, lowerer);
        var projection = model.PathShape is null
            ? LowerProjection(model, state, lowerer)
            : null;
        var postPagingLowerer = model.PostPaging is not null
            ? new ExpressionToCypherAstLowerer(parameters, dialect)
            : null;
        var postPagingPredicates = model.PostPaging is { } postPaging
            ? postPaging.Predicates
                .Select(predicate => postPagingLowerer!.LowerLambda(
                    RewriteLambdaAfterProjection(
                        model.Projection?.Selector,
                        predicate.Predicate,
                        "post-paging predicate"),
                    ResolveLambdaAlias(model, state, predicate.Alias)))
                .ToArray()
            : [];
        var postPagingOrdering = model.PostPaging is { } postPagingOrderingStage
            ? postPagingOrderingStage.Ordering
                .Select(key => new OrderByItem(
                    postPagingLowerer!.LowerLambda(
                        RewriteLambdaAfterProjection(
                            model.Projection?.Selector,
                            key.KeySelector,
                            "post-paging ordering key"),
                        ResolveLambdaAlias(model, state, key.Alias)),
                    key.Descending))
                .ToArray()
            : [];

        var clauses = new List<ICypherClause>();
        AddRootAndTraversalClauses(clauses, model, state);
        AddSearchFilterClause(clauses, model.SearchFilter, state.SearchParameter);
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
            AddGraphPathTerminalAndProjection(clauses, model, lowerer);
        }
        else
        {
            AddTerminalAndProjection(
                clauses,
                model,
                state,
                ordering,
                projection!,
                parameters,
                postPagingLowerer,
                postPagingPredicates,
                postPagingOrdering);
        }

        var pathTypes = model.PathShape is null ||
            model.Projection is not null ||
            model.Terminal != TerminalOperation.ToListOrArray
            ? null
            : new CypherPathTypes(
                model.PathShape.SourceType,
                model.PathShape.RelationshipType,
                model.PathShape.TargetType);
        var statement = new CypherStatement(clauses, parameters.Parameters, pathTypes);
        ValidateCapabilities(statement);

#if DEBUG
        new CypherAstValidator().Run(statement);
#endif

        return statement;
    }

    private void RequireCapability(GraphCapability capability, string construct)
    {
        if (dialect.Capabilities.Has(capability))
        {
            return;
        }

        throw new GraphQueryTranslationException(
            $"Cypher construct '{construct}' requires capability '{capability}', " +
            $"which dialect '{dialect.Name}' does not declare.");
    }

    private void ValidateCapabilities(CypherStatement statement)
    {
        foreach (var clause in statement.Clauses)
        {
            switch (clause)
            {
                case MatchClause match:
                    if (match.Optional)
                    {
                        RequireCapability(GraphCapability.OptionalTraversal, "OptionalTraversal");
                    }

                    if (match.Patterns.Any(pattern =>
                            pattern.Elements.OfType<NodePattern>().Any(node => node.Labels.Count > 1) ||
                            pattern.Elements.OfType<RelationshipPattern>().Any(relationship => relationship.Types.Count > 1)))
                    {
                        RequireCapability(GraphCapability.MultiLabelMatch, "MultiLabelMatch");
                    }

                    break;
                case OrderByClause orderBy when orderBy.Items.Any(item => item.Expression is VariableRef):
                    RequireCapability(GraphCapability.OrderByEntity, "OrderByEntity");
                    break;
            }

            foreach (var expression in ClauseExpressions(clause))
            {
                ValidateExpressionCapabilities(expression);
            }
        }
    }

    private void ValidateExpressionCapabilities(CypherExpression expression)
    {
        if (expression is LabelTest { Labels.Count: > 1 })
        {
            RequireCapability(GraphCapability.MultiLabelMatch, "MultiLabelMatch");
        }

        if (expression is PatternSubqueryExpression { Kind: PatternSubqueryKind.Count })
        {
            RequireCapability(GraphCapability.PatternSizeProjection, "PatternSizeProjection");
        }

        foreach (var child in ChildExpressions(expression))
        {
            ValidateExpressionCapabilities(child);
        }
    }

    private static IEnumerable<CypherExpression> ClauseExpressions(ICypherClause clause)
    {
        return clause switch
        {
            WhereClause where => [where.Predicate],
            WithClause with => with.Items.Select(item => item.Expression),
            ReturnClause @return => @return.Items.Select(item => item.Expression),
            CallClause call => call.Arguments,
            FullTextSearchClause search => [search.Query],
            UnwindClause unwind => [unwind.Source],
            OrderByClause orderBy => orderBy.Items.Select(item => item.Expression),
            SkipClause skip => [skip.Count],
            LimitClause limit => [limit.Count],
            _ => [],
        };
    }

    private static IEnumerable<CypherExpression> ChildExpressions(CypherExpression expression)
    {
        return expression switch
        {
            PropertyAccess property => [property.Target],
            EscapedPropertyAccess property => [property.Target],
            FunctionCall function => function.Arguments,
            AstBinaryExpression binary => [binary.Left, binary.Right],
            AstUnaryExpression unary => [unary.Operand],
            LabelTest label => [label.Target],
            ListExpression list => list.Items,
            MapExpression map => map.Entries.Select(entry => entry.Value),
            EntityProjectionExpression => [],
            AstIndexExpression index => [index.Target, index.Index],
            CaseExpression @case => @case.WhenFalse is null
                ? [@case.Condition, @case.WhenTrue]
                : [@case.Condition, @case.WhenTrue, @case.WhenFalse],
            ConjunctionExpression conjunction => conjunction.Predicates,
            PatternSubqueryExpression subquery => subquery.Predicate is null ? [] : [subquery.Predicate],
            PatternComprehensionExpression comprehension => comprehension.Predicate is null
                ? [comprehension.Projection]
                : [comprehension.Predicate, comprehension.Projection],
            _ => [],
        };
    }

    private static PlanningState CreateState(GraphQueryModel model, CypherParameterRegistry parameters)
    {
        var rootType = GetRootType(model.Root);
        var rootAlias = model.Root switch
        {
            SearchRoot { Target: SearchRootTarget.Nodes } => "n",
            SearchRoot { Target: SearchRootTarget.Relationships } => "r",
            SearchRoot { Target: SearchRootTarget.Entities } => "entity",
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
            _ => explicitTraversal[^1].TargetAlias ??
                (explicitTraversal.Length == 1 ? "tgt" : $"tgt_{explicitTraversal.Length}"),
        };
        var targetType = explicitTraversal.LastOrDefault()?.TargetType ?? rootType;

        var searchParameter = model.Root is SearchRoot search
            ? parameters.Add(search.Query)
            : model.SearchFilter is { } searchFilter
                ? parameters.Add(searchFilter.Query)
                : null;

        return new PlanningState(rootAlias, rootType, targetAlias, targetType, explicitTraversal, searchParameter);
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
            var alias = ResolveLambdaAlias(model, state, predicate.Alias);

            predicates.Add(lowerer.LowerLambda(predicate.Predicate, alias));
        }

        if (model.Join is not null)
        {
            predicates.Add(LowerJoinCondition(model.Join, lowerer));
        }

        return predicates;
    }

    private static string ResolveLambdaAlias(
        GraphQueryModel model,
        PlanningState state,
        string? declaredAlias)
    {
        if (model.Root is SearchRoot { Target: SearchRootTarget.Nodes })
        {
            return "n";
        }

        if (model.Root is SearchRoot { Target: SearchRootTarget.Entities })
        {
            return "entity";
        }

        var alias = declaredAlias ?? state.CurrentAlias;
        return alias == "src" && state.RootAlias == "src_1"
            ? state.RootAlias
            : alias;
    }

    private static List<CypherExpression> BuildSearchFilterPredicates(
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

    private static AstBinaryExpression LowerJoinCondition(
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

    private static OrderByItem[] LowerOrdering(
        GraphQueryModel model,
        PlanningState state,
        ExpressionToCypherAstLowerer lowerer)
    {
        return model.Ordering
            .Select(key => new OrderByItem(
                lowerer.LowerLambda(
                    RewriteOrderingAfterProjection(model.Projection?.Selector, key.KeySelector),
                    ResolveLambdaAlias(model, state, key.Alias)),
                key.Descending))
            .ToArray();
    }

    private static LambdaExpression RewriteOrderingAfterProjection(
        LambdaExpression? projection,
        LambdaExpression ordering) =>
        RewriteLambdaAfterProjection(projection, ordering, "ordering key");

    private static LambdaExpression RewriteLambdaAfterProjection(
        LambdaExpression? projection,
        LambdaExpression lambda,
        string description)
    {
        if (projection is null || lambda.Parameters.Count != 1 ||
            lambda.Parameters[0].Type != projection.ReturnType)
        {
            return lambda;
        }

        var parameter = lambda.Parameters[0];
        var body = new ProjectedValueRewriter(parameter, projection.Body).Visit(lambda.Body)
            ?? lambda.Body;
        if (ReferencesParameter(body, parameter))
        {
            throw new GraphQueryTranslationException(
                $"The {description} cannot be mapped through the preceding Select projection; apply it before " +
                "projecting, or project every referenced value explicitly.");
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
            clauses.Add(new FullTextSearchClause(
                SearchRootTarget.Nodes,
                state.SearchParameter ?? throw MissingSearchParameter(),
                "n"));
            return;
        }

        if (model.Root is SearchRoot { Target: SearchRootTarget.Relationships } searchRelationship)
        {
            clauses.Add(new FullTextSearchClause(
                SearchRootTarget.Relationships,
                state.SearchParameter ?? throw MissingSearchParameter(),
                "r"));
            var elementType = searchRelationship.ElementType ?? typeof(IRelationship);
            QueryRoot root = elementType == typeof(IRelationship) || elementType == typeof(DynamicRelationship)
                ? new DynamicRoot(elementType)
                : new RelationshipRoot(elementType);
            clauses.Add(BuildRootMatch(root, "r"));
            return;
        }

        if (model.Root is SearchRoot { Target: SearchRootTarget.Entities })
        {
            clauses.Add(new FullTextSearchClause(
                SearchRootTarget.Entities,
                state.SearchParameter ?? throw MissingSearchParameter(),
                "entity"));
            return;
        }

        if (state.ExplicitTraversal.Count > 0 && model.Root is NodeRoot or DynamicRoot)
        {
            clauses.Add(BuildTraversalMatch(model.Root, state.ExplicitTraversal, model.PathShape is null ? null : "p"));
            return;
        }

        clauses.Add(BuildRootMatch(model.Root, state.RootAlias));
    }

    private static void AddSearchFilterClause(
        List<ICypherClause> clauses,
        SearchRoot? search,
        QueryParameter? searchParameter)
    {
        if (search is null)
            return;

        var alias = search.Target switch
        {
            SearchRootTarget.Nodes => "searchedNode",
            SearchRootTarget.Relationships => "searchedRelationship",
            _ => throw new GraphQueryTranslationException(
                "Full-text search over mixed entities cannot be applied after a traversal."),
        };

        clauses.Add(new FullTextSearchClause(
            search.Target,
            searchParameter ?? throw MissingSearchParameter(),
            alias));
    }

    private static GraphQueryTranslationException MissingSearchParameter() => new(
        "The full-text search query parameter was not registered during planning.");

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
                new RelationshipPattern(
                    "r",
                    CypherDirection.Outgoing,
                    depth: null,
                    Labels.GetCompatibleLabels(relationship.ElementType)),
                new NodePattern("tgt", [])
            ]),
            DynamicRoot { ElementType: { } type } when typeof(IRelationship).IsAssignableFrom(type) => new PathPattern(
            [
                new NodePattern("src", []),
                new RelationshipPattern("r", CypherDirection.Outgoing, depth: null, types: []),
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
            var targetAlias = step.TargetAlias ?? (index == 0 ? "tgt" : $"tgt_{index + 1}");
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
                    step.Direction switch
                    {
                        GraphTraversalDirection.Outgoing => CypherDirection.Outgoing,
                        GraphTraversalDirection.Incoming => CypherDirection.Incoming,
                        GraphTraversalDirection.Both => CypherDirection.Both,
                        _ => throw new GraphQueryTranslationException($"Traversal direction '{step.Direction}' is not supported."),
                    },
                    depth,
                    step.RelationshipClrType is null
                        ? step.RelationshipType is { } relationshipType ? [relationshipType] : []
                        : Labels.GetCompatibleLabels(step.RelationshipClrType)),
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
        OrderByItem[] ordering,
        ProjectionPlan projection,
        CypherParameterRegistry parameters,
        ExpressionToCypherAstLowerer? postPagingLowerer,
        IReadOnlyList<CypherExpression> postPagingPredicates,
        IReadOnlyList<OrderByItem> postPagingOrdering)
    {
        if (model.PostPaging is not null)
        {
            AddPostPagingStageAndProjection(
                clauses,
                model,
                ordering,
                projection,
                postPagingLowerer!,
                postPagingPredicates,
                postPagingOrdering);
            return;
        }

        var aggregate = model.Terminal is TerminalOperation.Count or TerminalOperation.Sum or
            TerminalOperation.Average or TerminalOperation.Min or TerminalOperation.Max;
        var paging = EffectivePaging(model);
        var hasPaging = paging.Skip is not null || paging.Take is not null;

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

        var distinctApplied = false;
        IReadOnlyList<OrderByItem> effectiveOrdering = ordering;
        if (ShouldPipeDistinct(model, projection, aggregate, hasPaging, ordering.Length > 0))
        {
            (projection, effectiveOrdering) = AddDistinctProjection(
                clauses,
                projection,
                ordering,
                rejectMultiValueProjection: aggregate);
            distinctApplied = true;
        }

        if (aggregate && hasPaging)
        {
            if (!distinctApplied)
            {
                clauses.Add(new WithClause(
                    [new ReturnItem(new VariableRef(state.CurrentAlias), null)],
                    distinct: false));
            }

            AddOrderingAndPaging(clauses, effectiveOrdering, paging, reverseOrdering: false);
            clauses.Add(BuildAggregateReturn(model, state, projection));
            return;
        }

        if (aggregate)
        {
            clauses.Add(BuildAggregateReturn(model, state, projection));
            return;
        }

        var reverseOrdering = model.Terminal == TerminalOperation.Last;
        AddOrderingAndPaging(clauses, effectiveOrdering, paging, reverseOrdering);
        AddTerminalLimit(clauses, model, paging);

        AddProjectionClause(clauses, model, projection, distinctApplied);
    }

    private static void AddPostPagingStageAndProjection(
        List<ICypherClause> clauses,
        GraphQueryModel model,
        IReadOnlyList<OrderByItem> ordering,
        ProjectionPlan projection,
        ExpressionToCypherAstLowerer postPagingLowerer,
        IReadOnlyList<CypherExpression> postPagingPredicates,
        IReadOnlyList<OrderByItem> postPagingOrdering)
    {
        if (model.Terminal != TerminalOperation.ToListOrArray)
        {
            throw new GraphQueryTranslationException(
                $"Terminal operation '{model.Terminal}' after a post-paging sequence stage is not supported yet; " +
                "materialize the paged and filtered query before applying the terminal operation.");
        }

        if (model.Distinct && projection.Items is not null &&
            (postPagingPredicates.Count > 0 || postPagingOrdering.Count > 0 ||
             postPagingLowerer.NavigationMatches.Count > 0))
        {
            throw new GraphQueryTranslationException(
                "Filtering or ordering after Distinct and paging a scalar projection is not supported yet; " +
                "materialize the paged distinct values before continuing.");
        }

        var distinctApplied = false;
        var effectiveOrdering = ordering;
        if (model.Distinct)
        {
            (projection, effectiveOrdering) = AddDistinctProjection(
                clauses,
                projection,
                ordering,
                rejectMultiValueProjection: false);
            distinctApplied = true;
        }
        else
        {
            // Primary paging must belong to a WITH pipeline so the following WHERE filters the
            // paged rows instead of attaching to the root MATCH.
            clauses.Add(WithClause.All);
        }

        AddOrderingAndPaging(clauses, effectiveOrdering, model.Paging, reverseOrdering: false);
        var postPaging = model.PostPaging!;
        if (!postPaging.Distinct || postPagingPredicates.Count > 0 ||
            postPagingLowerer.NavigationMatches.Count > 0)
        {
            // A separate WITH makes the continuation a real row-pipeline stage for every Cypher
            // dialect. Keeping WHERE on the primary WITH lets some implementations evaluate it
            // before that WITH's ORDER BY/SKIP/LIMIT even though it renders textually afterward.
            clauses.Add(WithClause.All);
        }

        if (postPagingLowerer.NavigationMatches.Count > 0)
        {
            clauses.AddRange(postPagingLowerer.NavigationMatches);
        }

        if (postPagingPredicates.Count > 0)
        {
            if (postPagingLowerer.NavigationMatches.Count > 0)
            {
                clauses.Add(WithClause.All);
            }

            clauses.Add(new WhereClause(postPagingPredicates.Count == 1
                ? postPagingPredicates[0]
                : new ConjunctionExpression(postPagingPredicates)));
        }

        var effectivePostPagingOrdering = postPagingOrdering;
        if (postPaging.Distinct)
        {
            (projection, effectivePostPagingOrdering) = AddDistinctProjection(
                clauses,
                projection,
                postPagingOrdering,
                rejectMultiValueProjection: false);
            distinctApplied = true;
        }
        else if ((postPagingPredicates.Count > 0 || postPagingLowerer.NavigationMatches.Count > 0) &&
            (postPagingOrdering.Count > 0 ||
             postPaging.Paging.Skip is not null || postPaging.Paging.Take is not null))
        {
            // ORDER BY/SKIP/LIMIT after a WITH ... WHERE stage require a new WITH boundary.
            clauses.Add(WithClause.All);
        }

        AddOrderingAndPaging(
            clauses,
            effectivePostPagingOrdering,
            postPaging.Paging,
            reverseOrdering: false);
        AddProjectionClause(clauses, model, projection, distinctApplied);
    }

    /// <summary>
    /// The paging a terminal implies. ElementAt carries its index as a first-class terminal
    /// operand; it lowers to SKIP index / LIMIT 1, superseding any explicit paging operators.
    /// </summary>
    private static Paging EffectivePaging(GraphQueryModel model)
    {
        return model.Terminal is TerminalOperation.ElementAt or TerminalOperation.ElementAtOrDefault
            ? new Paging((int)model.TerminalOperand!, 1)
            : model.Paging;
    }

    private static bool ShouldPipeDistinct(
        GraphQueryModel model,
        ProjectionPlan projection,
        bool aggregate,
        bool hasPaging,
        bool hasOrdering)
    {
        if (!model.Distinct)
        {
            return false;
        }

        // Scalar materialization can keep the compact RETURN DISTINCT form when nothing follows
        // it. Entity projection has no DISTINCT form of its own, while aggregates, ordering,
        // paging, and cardinality terminals must all observe the distinct row set first.
        return aggregate || projection.Items is null || hasPaging || hasOrdering ||
            model.Terminal is TerminalOperation.First or TerminalOperation.Last or TerminalOperation.Single;
    }

    private static (ProjectionPlan Projection, IReadOnlyList<OrderByItem> Ordering) AddDistinctProjection(
        List<ICypherClause> clauses,
        ProjectionPlan projection,
        IReadOnlyList<OrderByItem> ordering,
        bool rejectMultiValueProjection)
    {
        if (projection.Items is { Count: > 0 } items)
        {
            if (rejectMultiValueProjection && items.Count > 1)
            {
                // Piping the tuple through WITH DISTINCT and aggregating a single column would
                // silently aggregate the wrong thing; reject until tuple aggregation is modeled.
                throw new GraphQueryTranslationException(
                    "Distinct over a multi-value projection cannot be combined with an aggregate terminal; " +
                    "project a single value before Distinct, or materialize the query first.");
            }

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
            var projectedItems = items
                .Select((item, index) => new ReturnItem(new VariableRef(aliases[index]), item.Alias))
                .ToArray();
            return (ProjectionPlan.Scalar(projectedItems), projectedOrdering);
        }

        IReadOnlyList<ReturnItem> entityItems = projection.Shape switch
        {
            EntityProjectionShape.Node =>
                [new ReturnItem(new VariableRef(projection.SourceAlias!), null)],
            EntityProjectionShape.PathSegment =>
            [
                new ReturnItem(new VariableRef(projection.SourceAlias!), null),
                new ReturnItem(new VariableRef(projection.RelationshipAlias!), null),
                new ReturnItem(new VariableRef(projection.TargetAlias!), null),
            ],
            _ => throw new GraphQueryTranslationException(
                $"Distinct entity projection shape '{projection.Shape}' is not supported."),
        };
        clauses.Add(new WithClause(entityItems, distinct: true));
        return (projection, ordering);
    }

    /// <summary>
    /// Adds the row limit implied by a cardinality terminal. An explicit Take already bounds the
    /// query, so the terminal's limit would only narrow it further.
    /// </summary>
    private static void AddTerminalLimit(List<ICypherClause> clauses, GraphQueryModel model, Paging paging)
    {
        int? terminalLimit = model.Terminal switch
        {
            TerminalOperation.First or TerminalOperation.Last => 1,
            TerminalOperation.Single => 2,
            _ => null,
        };

        if (terminalLimit is not null && paging.Take is null)
        {
            clauses.Add(new LimitClause(new Literal(terminalLimit.Value)));
        }
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
        ProjectionPlan projection,
        bool distinctApplied)
    {
        if (projection.Items is not null)
        {
            clauses.Add(new ReturnClause(projection.Items, model.Distinct && !distinctApplied));
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

    private static void AddGraphPathProjection(
        List<ICypherClause> clauses,
        GraphQueryModel model)
    {
        var pathShape = model.PathShape!;
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
        // A variable-length pattern only label-constrains the path's first and last nodes; when
        // more than one hop is possible, every hop column can also hold an unconstrained
        // intermediate node, whose runtime type is unknowable at planning time — load complex
        // properties rather than silently under-hydrating it. At exactly one hop the columns are
        // the label-constrained endpoints, so their static types decide.
        var intermediateNodesPossible =
            model.Traversal.LastOrDefault(step => !step.IsComplexPropertyTraversal) is not { Depth.Max: <= 1 };
        var loadSourceProperties = intermediateNodesPossible || HasComplexProperties(pathShape.SourceType);
        var loadTargetProperties = intermediateNodesPossible || HasComplexProperties(pathShape.TargetType);
        clauses.Add(new ReturnClause(
        [
            new ReturnItem(new VariableRef("pathIndex"), null),
            new ReturnItem(new VariableRef("hopIndex"), null),
            new ReturnItem(
                new MapExpression(
                [
                    new MapEntry("StartNode", new EntityProjectionExpression("src", loadSourceProperties)),
                    new MapEntry("Relationship", new VariableRef("r")),
                    new MapEntry("EndNode", new EntityProjectionExpression("tgt", loadTargetProperties)),
                ]),
                "PathSegment"),
        ], distinct: false));
        clauses.Add(new OrderByClause(
        [
            new OrderByItem(new VariableRef("pathIndex"), descending: false),
            new OrderByItem(new VariableRef("hopIndex"), descending: false)
        ]));
    }

    private static void AddGraphPathTerminalAndProjection(
        List<ICypherClause> clauses,
        GraphQueryModel model,
        ExpressionToCypherAstLowerer lowerer)
    {
        if (model.Terminal is TerminalOperation.Count or TerminalOperation.Any)
        {
            if (model.Paging.Skip is not null || model.Paging.Take is not null)
            {
                clauses.Add(new WithClause([new ReturnItem(new VariableRef("p"), null)], distinct: false));
            }

            AddOrderingAndPaging(clauses, [], model.Paging, reverseOrdering: false);
            var count = Function("count", new VariableRef("p"));
            clauses.Add(new ReturnClause(
                [new ReturnItem(
                    model.Terminal == TerminalOperation.Any
                        ? new AstBinaryExpression(CypherBinaryOperator.GreaterThan, count, new Literal(0))
                        : count,
                    model.Terminal == TerminalOperation.Any ? "exists" : null)],
                distinct: false));
            return;
        }

        if (model.Terminal != TerminalOperation.ToListOrArray)
        {
            throw new GraphQueryTranslationException(
                $"Terminal '{model.Terminal}' after TraversePaths is not supported yet.");
        }

        AddOrderingAndPaging(clauses, [], model.Paging, reverseOrdering: false);

        if (model.Projection?.Selector is not { } selector)
        {
            AddGraphPathProjection(clauses, model);
            return;
        }

        var body = StripConvert(selector.Body);
        if (body is MemberExpression
            {
                Expression: ParameterExpression parameter,
                Member.Name: nameof(IGraphPath.Start) or nameof(IGraphPath.End),
            } member && parameter.Type == typeof(IGraphPath))
        {
            var entity = lowerer.LowerLambda(selector, "p");
            clauses.Add(new WithClause([new ReturnItem(entity, "__pathEntity")], distinct: false));
            // Start/End are declared INode, but the pattern label-constrains them to the path
            // shape's endpoint types — use those rather than the interface, which always reports
            // complex properties.
            var entityType = member.Member.Name == nameof(IGraphPath.Start)
                ? model.PathShape!.SourceType
                : model.PathShape!.TargetType;
            clauses.Add(new EntityProjectionClause(
                EntityProjectionShape.Node,
                "__pathEntity",
                relationshipAlias: null,
                targetAlias: null,
                loadSourceProperties: HasComplexProperties(entityType),
                loadTargetProperties: false));
            return;
        }

        clauses.Add(new ReturnClause(
            [new ReturnItem(lowerer.LowerLambda(selector, "p"), null)],
            distinct: false));
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
        IReadOnlyList<TraversalStep> ExplicitTraversal,
        QueryParameter? SearchParameter)
    {
        public string CurrentAlias => ExplicitTraversal.Count == 0 ? RootAlias : TargetAlias;

        public string CurrentTraversalSourceAlias =>
            (ExplicitTraversal.Count == 0 ? null : ExplicitTraversal[^1].SourceAlias) switch
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
