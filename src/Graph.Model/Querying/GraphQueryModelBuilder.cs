// Copyright 2025 Savas Parastatidis
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

namespace Cvoya.Graph.Model.Querying;

using System.Linq.Expressions;
using System.Reflection;

internal sealed class GraphQueryModelBuilder : ExpressionVisitor
{
    private static readonly HashSet<LinqOperator> SupportedAfterTraversePaths =
    [
        LinqOperator.ToListOrArray,
        LinqOperator.Direction,
        LinqOperator.WithDepth,
    ];

    private readonly List<PredicateFragment> _predicates = [];
    private readonly List<TraversalStep> _traversal = [];
    private readonly List<OrderingKey> _ordering = [];
    private readonly HashSet<string> _complexNavigationPaths = new(StringComparer.Ordinal);
    private QueryRoot? _root;
    private ProjectionShape? _projection;
    private JoinFragment? _join;
    private QueryPathShape? _pathShape;
    private object? _terminalOperand;
    private SearchRoot? _searchFilter;
    private TerminalOperation _terminal = TerminalOperation.ToListOrArray;
    private bool _distinct;
    private Type? _currentType;
    private string? _currentAlias;
    private int? _skip;
    private int? _take;
    private bool _isGraphPathResult;
    private int _explicitTraversalCount;
    private int _lastExplicitTraversalIndex = -1;

    private GraphQueryModelBuilder()
    {
    }

    public static GraphQueryModel Build(
        Expression expression,
        GraphQueryModelBuilderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(expression);

        var effectiveOptions = options ?? new GraphQueryModelBuilderOptions();
        ExpressionTreeBoundsValidator.Validate(expression, effectiveOptions);

        var builder = new GraphQueryModelBuilder();
        builder.Visit(expression);

        if (builder._root is null)
        {
            throw new GraphQueryTranslationException(
                $"The expression does not contain a graph query root: {expression}.");
        }

        return new GraphQueryModel(
            builder._root,
            builder._predicates,
            builder._traversal,
            builder._projection,
            builder._ordering,
            new Paging(builder._skip, builder._take),
            builder._terminal,
            builder._distinct,
            builder._terminalOperand,
            builder._pathShape,
            builder._join,
            builder._searchFilter);
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        var op = LinqOperatorDispatch.Resolve(node.Method);
        if (op is null)
        {
            throw new GraphQueryTranslationException(
                $"Method '{node.Method.DeclaringType?.FullName}.{node.Method.Name}' is not a recognized graph query operator.");
        }

        var source = GetSource(node);
        if (source is not null)
        {
            Visit(source);
        }

        ThrowIfUnsupportedAfterTraversePaths(op.Value, node.Method.Name);

        switch (op.Value)
        {
            case LinqOperator.Where:
                AddPredicate(node, "Where");
                break;
            case LinqOperator.Select:
                HandleSelect(node);
                break;
            case LinqOperator.OrderBy:
            case LinqOperator.ThenBy:
                AddOrdering(node, descending: false);
                break;
            case LinqOperator.OrderByDescending:
            case LinqOperator.ThenByDescending:
                AddOrdering(node, descending: true);
                break;
            case LinqOperator.Take:
                _take = EvaluateArgument<int>(node, 1, "Take count");
                break;
            case LinqOperator.Skip:
                _skip = EvaluateArgument<int>(node, 1, "Skip count");
                break;
            case LinqOperator.Distinct:
                _distinct = true;
                _terminal = TerminalOperation.Distinct;
                break;
            case LinqOperator.ToListOrArray:
                _terminal = TerminalOperation.ToListOrArray;
                break;
            case LinqOperator.First:
                HandlePredicateTerminal(node, TerminalOperation.First);
                break;
            case LinqOperator.Single:
                HandlePredicateTerminal(node, TerminalOperation.Single);
                break;
            case LinqOperator.Last:
                HandleLast(node);
                break;
            case LinqOperator.Any:
                HandlePredicateTerminal(node, TerminalOperation.Any);
                break;
            case LinqOperator.All:
                HandlePredicateTerminal(node, TerminalOperation.All);
                break;
            case LinqOperator.Count:
                HandlePredicateTerminal(node, TerminalOperation.Count);
                break;
            case LinqOperator.Sum:
                HandleSelectorTerminal(node, TerminalOperation.Sum);
                break;
            case LinqOperator.Average:
                HandleSelectorTerminal(node, TerminalOperation.Average);
                break;
            case LinqOperator.Min:
                HandleSelectorTerminal(node, TerminalOperation.Min);
                break;
            case LinqOperator.Max:
                HandleSelectorTerminal(node, TerminalOperation.Max);
                break;
            case LinqOperator.Contains:
                _terminalOperand = EvaluateArgument<object?>(node, 1, "Contains item");
                _terminal = TerminalOperation.Contains;
                break;
            case LinqOperator.ElementAt:
                HandleElementAt(node, TerminalOperation.ElementAt);
                break;
            case LinqOperator.ElementAtOrDefault:
                HandleElementAt(node, TerminalOperation.ElementAtOrDefault);
                break;
            case LinqOperator.PathSegments:
                HandlePathSegments(node);
                break;
            case LinqOperator.TraversePaths:
                HandleTraversePaths(node);
                break;
            case LinqOperator.Direction:
                UpdateLastTraversal(direction: EvaluateArgument<GraphTraversalDirection>(node, 1, "traversal direction"));
                break;
            case LinqOperator.WithDepth:
                HandleWithDepth(node);
                break;
            case LinqOperator.Search:
                HandleSearch(node);
                break;
            case LinqOperator.SelectMany:
                throw Unsupported(node, "SelectMany is not supported by graph query translation yet; see #100.");
            case LinqOperator.GroupBy:
                throw Unsupported(node, "GroupBy is not supported by graph query translation yet; see #100.");
            case LinqOperator.Join:
                HandleJoin(node);
                break;
            case LinqOperator.Union:
                throw Unsupported(node, "Union is not supported by graph query translation yet.");
            default:
                throw Unsupported(node, $"Operator '{op}' is not supported by graph query translation.");
        }

        return node;
    }

    protected override Expression VisitExtension(Expression node)
    {
        if (node is IGraphSearchRootExpression search)
        {
            _root = new SearchRoot(search.SearchQuery, search.Target,
                search.Target == SearchRootTarget.Entities ? null : search.EntityType);
            _currentType = search.EntityType;
            _currentAlias = search.Target == SearchRootTarget.Relationships ? "r" : "n";
            return node;
        }

        return node.CanReduce ? base.VisitExtension(node) : node;
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        if (_root is not null || node.Value is not IQueryable queryable)
        {
            return node;
        }

        var elementType = queryable.ElementType;
        _currentType = elementType;

        if (elementType == typeof(DynamicNode) || elementType == typeof(DynamicRelationship))
        {
            _root = new DynamicRoot(elementType);
            _currentAlias = elementType == typeof(DynamicRelationship) ? "r" : "src";
        }
        else if (typeof(INode).IsAssignableFrom(elementType))
        {
            _root = new NodeRoot(elementType);
            _currentAlias = "src";
        }
        else if (typeof(IRelationship).IsAssignableFrom(elementType))
        {
            _root = new RelationshipRoot(elementType);
            _currentAlias = "r";
        }
        else
        {
            _root = new DynamicRoot(elementType);
            _currentAlias = null;
        }

        return node;
    }

    private static Expression? GetSource(MethodCallExpression node)
    {
        if (node.Method.IsStatic)
        {
            return node.Arguments.Count > 0 ? node.Arguments[0] : null;
        }

        return node.Object;
    }

    private void AddPredicate(MethodCallExpression node, string operatorName)
    {
        var lambda = RequireLambda(node, 1, operatorName);
        WidenRootScope(lambda);
        AddComplexPropertyTraversals(lambda);
        _predicates.Add(new PredicateFragment(lambda, _currentAlias));
    }

    private void WidenRootScope(LambdaExpression lambda)
    {
        if (_explicitTraversalCount != 0 || _root is not NodeRoot root || lambda.Parameters.Count != 1)
            return;

        var parameterType = lambda.Parameters[0].Type;
        if (parameterType != root.ElementType &&
            typeof(INode).IsAssignableFrom(parameterType) &&
            parameterType.IsAssignableFrom(root.ElementType))
        {
            _root = new NodeRoot(parameterType);
            _currentType = parameterType;
        }
    }

    private void HandleSelect(MethodCallExpression node)
    {
        var selector = RequireLambda(node, 1, "Select");
        AddComplexPropertyTraversals(selector);
        selector = ComposeProjection(_projection?.Selector, selector);

        var body = StripConvert(selector.Body);
        var kind = selector.Parameters.Any(parameter => typeof(IGraphPathSegment).IsAssignableFrom(parameter.Type))
            ? ProjectionKind.PathSegment
            : body switch
            {
                ParameterExpression => ProjectionKind.Identity,
                NewExpression or MemberInitExpression => ProjectionKind.Anonymous,
                _ => ProjectionKind.Scalar,
            };

        _projection = new ProjectionShape(kind, selector);

        if (kind == ProjectionKind.PathSegment && body is MemberExpression member)
        {
            _currentType = member.Type;
            _currentAlias = member.Member.Name switch
            {
                nameof(IGraphPathSegment.StartNode) => "src",
                nameof(IGraphPathSegment.Relationship) => "r",
                nameof(IGraphPathSegment.EndNode) => CurrentTargetAlias,
                _ => _currentAlias,
            };
        }
        else
        {
            _currentType = selector.ReturnType;
        }
    }

    private static LambdaExpression ComposeProjection(
        LambdaExpression? previous,
        LambdaExpression current)
    {
        if (previous is null || previous.Parameters.Count != 1 || current.Parameters.Count != 1 ||
            current.Parameters[0].Type != previous.ReturnType)
        {
            return current;
        }

        var body = new ParameterReplacementVisitor(current.Parameters[0], previous.Body)
            .Visit(current.Body) ?? current.Body;
        return Expression.Lambda(body, previous.Parameters);
    }

    private void AddOrdering(MethodCallExpression node, bool descending)
    {
        var selector = RequireLambda(node, 1, "ordering");
        AddComplexPropertyTraversals(selector);
        _ordering.Add(new OrderingKey(selector, descending, _currentAlias));
    }

    private sealed class ParameterReplacementVisitor(
        ParameterExpression parameter,
        Expression replacement) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) =>
            node == parameter ? replacement : base.VisitParameter(node);
    }

    private void HandlePredicateTerminal(MethodCallExpression node, TerminalOperation terminal)
    {
        if (TryGetLambda(node, 1) is { } predicate)
        {
            AddComplexPropertyTraversals(predicate);
            _predicates.Add(new PredicateFragment(predicate, _currentAlias));
        }

        _terminal = terminal;
    }

    private void HandleLast(MethodCallExpression node)
    {
        if (_ordering.Count == 0)
        {
            throw new GraphQueryTranslationException(
                $"{node.Method.Name} requires an explicit OrderBy before LastAsync or LastOrDefaultAsync so providers can translate deterministic ordering.");
        }

        HandlePredicateTerminal(node, TerminalOperation.Last);
    }

    private void HandleSelectorTerminal(MethodCallExpression node, TerminalOperation terminal)
    {
        if (TryGetLambda(node, 1) is { } selector)
        {
            AddComplexPropertyTraversals(selector);
            _projection = new ProjectionShape(ProjectionKind.Scalar, selector);
        }

        _terminal = terminal;
    }

    private void HandleElementAt(MethodCallExpression node, TerminalOperation terminal)
    {
        var index = EvaluateArgument<int>(node, 1, "element index");
        if (index < 0)
        {
            throw new GraphQueryTranslationException("ElementAt index must be non-negative.");
        }

        _skip = index;
        _take = 1;
        _terminal = terminal;
    }

    private void HandlePathSegments(MethodCallExpression node)
    {
        if (!node.Method.IsGenericMethod || node.Method.GetGenericArguments() is not { Length: 3 } types)
        {
            throw Unsupported(node, "PathSegments must have start, relationship, and target type arguments.");
        }

        var relationshipType = types[1];
        var targetType = types[2];
        if (_currentType is not null &&
            !_currentType.IsAssignableFrom(types[0]) &&
            !types[0].IsAssignableFrom(_currentType))
        {
            throw Unsupported(
                node,
                $"PathSegments source type '{types[0].FullName}' does not match the current scope '{_currentType.FullName}'.");
        }

        AddExplicitTraversal(relationshipType, targetType, new DepthRange(1, 1));

        var parameter = Expression.Parameter(node.Type.GetGenericArguments()[0], "segment");
        _projection = new ProjectionShape(
            ProjectionKind.PathSegment,
            Expression.Lambda(parameter, parameter));
    }

    private void HandleTraversePaths(MethodCallExpression node)
    {
        if (!node.Method.IsGenericMethod)
        {
            throw Unsupported(node, "TraversePaths must be generic.");
        }

        var types = node.Method.GetGenericArguments();
        var relationshipType = types.Length switch
        {
            2 => types[0],
            3 => types[1],
            _ => throw Unsupported(node, "TraversePaths must have relationship and target type arguments."),
        };
        var targetType = types.Length switch
        {
            2 => types[1],
            3 => types[2],
            _ => throw Unsupported(node, "TraversePaths must have relationship and target type arguments."),
        };
        var minDepth = EvaluateArgument<int>(node, 1, "minimum traversal depth");
        var maxDepth = EvaluateArgument<int>(node, 2, "maximum traversal depth");

        var sourceType = _currentType ?? typeof(INode);
        AddExplicitTraversal(relationshipType, targetType, new DepthRange(minDepth, maxDepth));
        _isGraphPathResult = true;
        _pathShape = new QueryPathShape(sourceType, relationshipType, targetType);
        _currentType = typeof(IGraphPath);
        _projection = null;
    }

    private void AddExplicitTraversal(Type relationshipType, Type targetType, DepthRange depth)
    {
        var sourceAlias = _currentAlias ?? (_explicitTraversalCount == 0 ? "src" : CurrentTargetAlias);
        _explicitTraversalCount++;
        _lastExplicitTraversalIndex = _traversal.Count;
        _traversal.Add(new TraversalStep(
            Labels.GetLabelFromType(relationshipType),
            GraphTraversalDirection.Outgoing,
            depth,
            [],
            targetType,
            relationshipType,
            isComplexPropertyTraversal: false,
            sourceAlias: sourceAlias));

        _currentType = targetType;
        _currentAlias = CurrentTargetAlias;
    }

    private void HandleWithDepth(MethodCallExpression node)
    {
        var depth = node.Arguments.Count switch
        {
            2 => new DepthRange(1, EvaluateArgument<int>(node, 1, "maximum traversal depth")),
            3 => new DepthRange(
                EvaluateArgument<int>(node, 1, "minimum traversal depth"),
                EvaluateArgument<int>(node, 2, "maximum traversal depth")),
            _ => throw Unsupported(node, "WithDepth must have one or two depth arguments."),
        };

        UpdateLastTraversal(depth: depth);
    }

    private void HandleSearch(MethodCallExpression node)
    {
        if (_explicitTraversalCount > 0)
        {
            var filterQuery = EvaluateArgument<string>(node, 1, "search query");
            var filterTarget = _currentType switch
            {
                { } type when typeof(INode).IsAssignableFrom(type) => SearchRootTarget.Nodes,
                { } type when typeof(IRelationship).IsAssignableFrom(type) => SearchRootTarget.Relationships,
                _ => SearchRootTarget.Entities,
            };
            _searchFilter = new SearchRoot(
                filterQuery,
                filterTarget,
                filterTarget == SearchRootTarget.Entities ? null : _currentType);
            return;
        }

        var query = EvaluateArgument<string>(node, 1, "search query");
        var elementType = _currentType;
        var target = elementType switch
        {
            { } type when typeof(INode).IsAssignableFrom(type) => SearchRootTarget.Nodes,
            { } type when typeof(IRelationship).IsAssignableFrom(type) => SearchRootTarget.Relationships,
            _ => SearchRootTarget.Entities,
        };

        _root = new SearchRoot(query, target, target == SearchRootTarget.Entities ? null : elementType);
    }

    private void UpdateLastTraversal(
        GraphTraversalDirection? direction = null,
        DepthRange? depth = null)
    {
        if (_lastExplicitTraversalIndex < 0)
        {
            throw new GraphQueryTranslationException("Traversal modifiers require a preceding PathSegments or TraversePaths operator.");
        }

        var current = _traversal[_lastExplicitTraversalIndex];
        _traversal[_lastExplicitTraversalIndex] = new TraversalStep(
            current.RelationshipType,
            direction ?? current.Direction,
            depth ?? current.Depth,
            current.RelationshipPredicates,
            current.TargetType,
            current.RelationshipClrType,
            current.IsComplexPropertyTraversal,
            current.SourceAlias);
    }

    private void AddComplexPropertyTraversals(LambdaExpression lambda)
    {
        foreach (var navigation in ComplexPropertyNavigationCollector.Collect(lambda.Body))
        {
            if (!_complexNavigationPaths.Add(navigation.Path))
            {
                continue;
            }

            _traversal.Add(new TraversalStep(
                GraphDataModel.GetComplexPropertyRelationshipType(navigation.Property),
                GraphTraversalDirection.Outgoing,
                new DepthRange(1, 1),
                [],
                navigation.TargetType,
                relationshipClrType: null,
                isComplexPropertyTraversal: true));
        }
    }

    private void HandleJoin(MethodCallExpression node)
    {
        if (node.Arguments.Count != 5)
        {
            throw Unsupported(node, "Join requires outer source, inner source, two key selectors, and a result selector.");
        }

        var inner = Build(node.Arguments[1]);
        var outerKey = RequireLambda(node, 2, "Join outer key");
        var innerKey = RequireLambda(node, 3, "Join inner key");
        var result = RequireLambda(node, 4, "Join result selector");
        _join = new JoinFragment(inner.Root, outerKey, innerKey, result);

        _projection = new ProjectionShape(
            StripConvert(result.Body) is ParameterExpression ? ProjectionKind.Identity : ProjectionKind.Scalar,
            result);
        _currentType = result.ReturnType;
        _currentAlias = StripConvert(result.Body) is ParameterExpression parameter &&
            result.Parameters.IndexOf(parameter) == 1
                ? "joined"
                : _currentAlias;
    }

    private void ThrowIfUnsupportedAfterTraversePaths(LinqOperator op, string methodName)
    {
        if (!_isGraphPathResult || SupportedAfterTraversePaths.Contains(op))
        {
            return;
        }

        throw new GraphQueryTranslationException(
            $"'.{methodName}(...)' chained after 'TraversePaths(...)' is not supported: TraversePaths returns IGraphPath, " +
            "whose per-hop result shape has no single scope for that operator. Materialize the paths first and continue client-side.");
    }

    private static LambdaExpression RequireLambda(MethodCallExpression node, int argumentIndex, string operatorName)
    {
        return TryGetLambda(node, argumentIndex)
            ?? throw Unsupported(node, $"{operatorName} requires a lambda expression.");
    }

    private static LambdaExpression? TryGetLambda(MethodCallExpression node, int argumentIndex)
    {
        if (node.Arguments.Count <= argumentIndex)
        {
            return null;
        }

        return node.Arguments[argumentIndex] switch
        {
            LambdaExpression lambda => lambda,
            UnaryExpression { NodeType: ExpressionType.Quote, Operand: LambdaExpression lambda } => lambda,
            _ => null,
        };
    }

    private static T EvaluateArgument<T>(MethodCallExpression node, int argumentIndex, string description)
    {
        if (node.Arguments.Count <= argumentIndex)
        {
            throw Unsupported(node, $"{node.Method.Name} is missing its {description} argument.");
        }

        return QueryExpressionEvaluator.Evaluate<T>(node.Arguments[argumentIndex], description);
    }

    private static Expression StripConvert(Expression expression)
    {
        while (expression is UnaryExpression
            {
                NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked,
            } unary)
        {
            expression = unary.Operand;
        }

        return expression;
    }

    private static GraphQueryTranslationException Unsupported(MethodCallExpression node, string message)
    {
        return new GraphQueryTranslationException(
            $"Cannot translate '{node.Method.DeclaringType?.Name}.{node.Method.Name}': {message}");
    }

    private string CurrentTargetAlias => _explicitTraversalCount == 1
        ? "tgt"
        : $"tgt_{_explicitTraversalCount}";

    private sealed class ComplexPropertyNavigationCollector : ExpressionVisitor
    {
        private readonly List<ComplexPropertyNavigation> _navigations = [];

        public static IReadOnlyList<ComplexPropertyNavigation> Collect(Expression expression)
        {
            var collector = new ComplexPropertyNavigationCollector();
            collector.Visit(expression);
            return collector._navigations;
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            var chain = GetMemberChain(node);
            for (var i = 0; i < chain.Count; i++)
            {
                var member = chain[i];
                var memberType = member.Type;
                var targetType = GraphDataModel.IsCollectionOfComplex(memberType)
                    ? memberType.GetElementType() ?? memberType.GetGenericArguments().FirstOrDefault()
                    : memberType;
                if (targetType is null ||
                    (!GraphDataModel.IsComplex(targetType) && !GraphDataModel.IsCollectionOfComplex(memberType)) ||
                    typeof(IEntity).IsAssignableFrom(targetType))
                {
                    continue;
                }

                var path = string.Join('.', chain.Take(i + 1).Select(item => item.Member.Name));
                if (member.Member is PropertyInfo property)
                {
                    _navigations.Add(new ComplexPropertyNavigation(path, property, targetType));
                }
            }

            return base.VisitMember(node);
        }

        private static IReadOnlyList<MemberExpression> GetMemberChain(MemberExpression leaf)
        {
            var chain = new Stack<MemberExpression>();
            Expression? current = leaf;
            while (current is MemberExpression member)
            {
                chain.Push(member);
                current = member.Expression;
            }

            return current is ParameterExpression ? [.. chain] : [];
        }
    }

    private sealed record ComplexPropertyNavigation(string Path, PropertyInfo Property, Type TargetType);
}
