// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying;

using System.Linq.Expressions;
using System.Reflection;

/// <summary>Builds the provider-neutral semantic query model from a graph LINQ expression.</summary>
public sealed class GraphQueryModelBuilder : ExpressionVisitor
{
    private static readonly HashSet<LinqOperator> SupportedAfterTraversePaths =
    [
        LinqOperator.ToListOrArray,
        LinqOperator.Direction,
        LinqOperator.WithDepth,
        LinqOperator.Where,
        LinqOperator.Select,
        LinqOperator.Take,
        LinqOperator.Skip,
        LinqOperator.Count,
        LinqOperator.Any,
    ];

    private readonly List<PredicateFragment> _predicates = [];
    private readonly List<PredicateFragment> _postPagingPredicates = [];
    private readonly List<TraversalStep> _traversal = [];
    private readonly List<OrderingKey> _ordering = [];
    private readonly List<OrderingKey> _postPagingOrdering = [];
    private readonly HashSet<string> _complexNavigationPaths = new(StringComparer.Ordinal);
    private QueryRoot? _root;
    private ProjectionShape? _projection;
    private JoinFragment? _join;
    private GroupByFragment? _groupBy;
    private SelectManyFragment? _selectMany;
    private UnionFragment? _union;
    private QueryPathShape? _pathShape;
    private object? _terminalOperand;
    private SearchRoot? _searchFilter;
    private TerminalOperation _terminal = TerminalOperation.ToListOrArray;
    private bool _distinct;
    private Type? _currentType;
    private string? _currentAlias;
    private int? _skip;
    private int? _take;
    private int? _postPagingSkip;
    private int? _postPagingTake;
    private bool _postPagingDistinct;
    private bool _hasPostPagingStage;
    private bool _isGraphPathResult;
    private int _explicitTraversalCount;
    private int _lastExplicitTraversalIndex = -1;

    private GraphQueryModelBuilder()
    {
    }

    /// <summary>Builds a semantic query model using the default expression-tree safety bounds.</summary>
    public static GraphQueryModel Build(Expression expression) => Build(expression, options: null);

    internal static GraphQueryModel Build(
        Expression expression,
        GraphQueryModelBuilderOptions? options)
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
            builder._searchFilter,
            builder._groupBy,
            builder._selectMany,
            builder._union,
            builder._hasPostPagingStage
                ? new PostPagingStage(
                    builder._postPagingPredicates,
                    builder._postPagingOrdering,
                    new Paging(builder._postPagingSkip, builder._postPagingTake),
                    builder._postPagingDistinct)
                : null);
    }

    /// <inheritdoc/>
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
                ThrowIfPathPredicateAfterProjectionOrPaging();
                BeginPostPagingStageIfNeeded(node.Method.Name);
                AddPredicate(node, "Where");
                break;
            case LinqOperator.Select:
                HandleSelect(node);
                break;
            case LinqOperator.OrderBy:
            case LinqOperator.ThenBy:
                BeginPostPagingStageIfNeeded(node.Method.Name);
                AddOrdering(node, descending: false);
                break;
            case LinqOperator.OrderByDescending:
            case LinqOperator.ThenByDescending:
                BeginPostPagingStageIfNeeded(node.Method.Name);
                AddOrdering(node, descending: true);
                break;
            case LinqOperator.Take:
                ComposeTake(EvaluateArgument<int>(node, 1, "Take count"));
                break;
            case LinqOperator.Skip:
                ComposeSkip(EvaluateArgument<int>(node, 1, "Skip count"));
                break;
            case LinqOperator.Distinct:
                BeginPostPagingStageIfNeeded(node.Method.Name);
                if (_hasPostPagingStage)
                {
                    _postPagingDistinct = true;
                }
                else
                {
                    _distinct = true;
                }
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
                HandleSelectMany(node);
                break;
            case LinqOperator.GroupBy:
                HandleGroupBy(node);
                break;
            case LinqOperator.Join:
                HandleJoin(node);
                break;
            case LinqOperator.Union:
                HandleUnion(node);
                break;
            default:
                throw Unsupported(node, $"Operator '{op}' is not supported by graph query translation.");
        }

        return node;
    }

    /// <inheritdoc/>
    protected override Expression VisitExtension(Expression node)
    {
        if (node is IGraphSearchRootExpression search)
        {
            _root = new SearchRoot(search.SearchQuery, search.Target,
                search.Target == SearchRootTarget.Entities ? null : search.EntityType);
            _currentType = search.EntityType;
            _currentAlias = SearchAlias(search.Target);
            return node;
        }

        if (node.CanReduce)
        {
            return base.VisitExtension(node);
        }

        throw new GraphQueryTranslationException(
            $"Expression '{node.GetType().Name}' is not a recognized graph query expression.");
    }

    /// <inheritdoc/>
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
        RejectIndexedLambda(node, lambda, operatorName);
        WidenRootScope(lambda);
        AddComplexPropertyTraversals(lambda);
        var predicate = new PredicateFragment(lambda, _currentAlias);
        if (_hasPostPagingStage)
        {
            _postPagingPredicates.Add(predicate);
        }
        else
        {
            _predicates.Add(predicate);
        }
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
        RejectIndexedLambda(node, selector, "Select");
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

        if (_isGraphPathResult && kind == ProjectionKind.Identity)
        {
            // Select(p => p) over TraversePaths is a no-op: storing it would make providers lower
            // a bare path projection (e.g. RETURN p) instead of the decomposed materialization
            // shape the IGraphPath wire contract requires.
            _projection = null;
            _currentType = selector.ReturnType;
            return;
        }

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
        if (previous is null)
        {
            return current;
        }

        if (previous.Parameters.Count != 1 || current.Parameters.Count != 1 ||
            current.Parameters[0].Type != previous.ReturnType)
        {
            throw new GraphQueryTranslationException(
                "Cannot compose chained Select projections: the later selector does not consume the earlier " +
                "selector's result. Combine the projections into a single Select.");
        }

        var body = new ParameterReplacementVisitor(current.Parameters[0], previous.Body)
            .Visit(current.Body) ?? current.Body;
        return Expression.Lambda(body, previous.Parameters);
    }

    private void AddOrdering(MethodCallExpression node, bool descending)
    {
        var selector = RequireLambda(node, 1, "ordering");
        AddComplexPropertyTraversals(selector);
        var ordering = new OrderingKey(selector, descending, _currentAlias);
        if (_hasPostPagingStage)
        {
            _postPagingOrdering.Add(ordering);
        }
        else
        {
            _ordering.Add(ordering);
        }
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
            if (_skip is not null || _take is not null || _hasPostPagingStage)
            {
                throw new GraphQueryTranslationException(
                    $"Predicate terminal operation '{terminal}' follows Skip/Take and cannot be represented " +
                    "without changing LINQ operator order. Materialize the paged query before applying it.");
            }

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

        _terminalOperand = index;
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
        AddExplicitTraversal(relationshipType, targetType, CreateDepthRange(node, minDepth, maxDepth));
        _isGraphPathResult = true;
        _pathShape = new QueryPathShape(sourceType, relationshipType, targetType);
        _currentType = typeof(IGraphPath);
        _currentAlias = "p";
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
            sourceAlias: sourceAlias,
            targetAlias: CurrentTargetAlias));

        _currentType = targetType;
        _currentAlias = CurrentTargetAlias;
    }

    private void HandleWithDepth(MethodCallExpression node)
    {
        var depth = node.Arguments.Count switch
        {
            2 => CreateDepthRange(node, 1, EvaluateArgument<int>(node, 1, "maximum traversal depth")),
            3 => CreateDepthRange(
                node,
                EvaluateArgument<int>(node, 1, "minimum traversal depth"),
                EvaluateArgument<int>(node, 2, "maximum traversal depth")),
            _ => throw Unsupported(node, "WithDepth must have one or two depth arguments."),
        };

        UpdateLastTraversal(depth: depth);
    }

    private static DepthRange CreateDepthRange(MethodCallExpression node, int min, int max)
    {
        if (min < 0 || max < min)
        {
            throw Unsupported(
                node,
                $"traversal depth range [{min}..{max}] is invalid: the minimum must be non-negative and the " +
                "maximum must be at least the minimum.");
        }

        return new DepthRange(min, max);
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
        _currentAlias = SearchAlias(target);
    }

    private static string SearchAlias(SearchRootTarget target) => target switch
    {
        SearchRootTarget.Nodes => "n",
        SearchRootTarget.Relationships => "r",
        SearchRootTarget.Entities => "entity",
        _ => throw new GraphQueryTranslationException($"Search target '{target}' is not supported."),
    };

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
            current.SourceAlias,
            current.TargetAlias);
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

    private void HandleGroupBy(MethodCallExpression node)
    {
        if (_groupBy is not null)
        {
            throw Unsupported(node, "chained GroupBy operations cannot be represented by a single query model.");
        }

        var keySelector = RequireLambda(node, 1, "GroupBy key");
        var second = TryGetLambda(node, 2);
        var third = TryGetLambda(node, 3);

        // Queryable.GroupBy overloads disambiguate by arity of the second lambda: an element
        // selector takes one parameter, a result selector takes the key and the group.
        var elementSelector = second is { Parameters.Count: 1 } ? second : null;
        var resultSelector = elementSelector is null ? second : third;

        // An identity projection carried into the grouping (e.g. the implicit `seg => seg` left by
        // PathSegments) contributes nothing: the group element is the current scope, reconstructible
        // from the traversal. Clearing it lets a following Select over the IGrouping be recognized
        // as the group projection instead of tripping chained-projection composition.
        if (_projection?.Selector is { } projectionSelector &&
            projectionSelector.Body == projectionSelector.Parameters[0])
        {
            _projection = null;
        }

        _currentType = keySelector.Parameters[0].Type;
        _groupBy = new GroupByFragment(keySelector, elementSelector, resultSelector);
    }

    private void HandleSelectMany(MethodCallExpression node)
    {
        if (_selectMany is not null)
        {
            throw Unsupported(node, "chained SelectMany operations cannot be represented by a single query model.");
        }

        var collectionSelector = RequireLambda(node, 1, "SelectMany collection selector");
        _selectMany = new SelectManyFragment(collectionSelector, TryGetLambda(node, 2));
    }

    private void HandleUnion(MethodCallExpression node)
    {
        if (_union is not null)
        {
            throw Unsupported(node, "chained Union operations cannot be represented by a single query model.");
        }

        if (node.Arguments.Count < 2)
        {
            throw Unsupported(node, "Union requires a second source.");
        }

        var elementType = node.Method.IsGenericMethod
            ? node.Method.GetGenericArguments()[0]
            : throw Unsupported(node, "Union must declare its element type.");
        _union = new UnionFragment(
            Build(node.Arguments[0]),
            Build(node.Arguments[1]),
            elementType);
    }

    private void HandleJoin(MethodCallExpression node)
    {
        if (node.Arguments.Count != 5)
        {
            throw Unsupported(node, "Join requires outer source, inner source, two key selectors, and a result selector.");
        }

        var inner = Build(node.Arguments[1]);
        if (inner.Predicates.Count > 0 || inner.Traversal.Count > 0 || inner.Ordering.Count > 0 ||
            inner.Projection is not null || inner.Paging.Skip is not null || inner.Paging.Take is not null ||
            inner.Distinct || inner.Join is not null || inner.SearchFilter is not null ||
            inner.PathShape is not null || inner.GroupBy is not null || inner.SelectMany is not null ||
            inner.Union is not null || inner.PostPaging is not null ||
            inner.Terminal != TerminalOperation.ToListOrArray)
        {
            throw Unsupported(
                node,
                "Join inner sources must be a bare node or relationship set; operators such as Where, Select, " +
                "OrderBy, Skip, or Take on the inner source are not translated. Apply them to the join result instead.");
        }

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

    /// <summary>
    /// Folds a <c>Take</c> into the paging window. The window already reflects every earlier
    /// paging operator, so a later Take can only tighten the remaining limit.
    /// </summary>
    private void ComposeTake(int take)
    {
        take = Math.Max(take, 0);
        if (_hasPostPagingStage)
        {
            _postPagingTake = _postPagingTake is { } existing ? Math.Min(existing, take) : take;
        }
        else
        {
            _take = _take is { } existing ? Math.Min(existing, take) : take;
        }
    }

    /// <summary>
    /// Folds a <c>Skip</c> into the paging window. Skipping after an earlier Take consumes part
    /// of that bounded window, so the remaining limit shrinks accordingly — <c>Take(2).Skip(1)</c>
    /// is SKIP 1 LIMIT 1, not SKIP 1 LIMIT 2.
    /// </summary>
    private void ComposeSkip(int skip)
    {
        skip = Math.Max(skip, 0);
        if (_hasPostPagingStage)
        {
            _postPagingSkip = (_postPagingSkip ?? 0) + skip;
            if (_postPagingTake is { } postPagingBounded)
            {
                _postPagingTake = Math.Max(postPagingBounded - skip, 0);
            }

            return;
        }

        _skip = (_skip ?? 0) + skip;
        if (_take is { } bounded)
        {
            _take = Math.Max(bounded - skip, 0);
        }
    }

    /// <summary>
    /// Starts the continuation stage when a sequence operator follows primary paging. A second
    /// paging boundary followed by more operators would require another continuation stage; reject
    /// that uncommon shape clearly instead of flattening it into the wrong order.
    /// </summary>
    private void BeginPostPagingStageIfNeeded(string operatorName)
    {
        if (_hasPostPagingStage)
        {
            if (_postPagingSkip is not null || _postPagingTake is not null)
            {
                throw new GraphQueryTranslationException(
                    $"'.{operatorName}(...)' follows a second paging window. Materialize the query after that " +
                    "Skip/Take and continue with LINQ-to-Objects; flattening a third sequence stage would change operator order.");
            }

            return;
        }

        _hasPostPagingStage = _skip is not null || _take is not null;
    }

    /// <summary>
    /// The query model applies path predicates before projection and pagination, so a
    /// <c>Where</c> that textually follows <c>Select</c>/<c>Take</c>/<c>Skip</c> on a
    /// <c>TraversePaths</c> query would silently filter a different row set than LINQ semantics
    /// require. Reject it instead of mistranslating.
    /// </summary>
    private void ThrowIfPathPredicateAfterProjectionOrPaging()
    {
        if (!_isGraphPathResult || (_projection is null && _take is null && _skip is null))
        {
            return;
        }

        throw new GraphQueryTranslationException(
            "'.Where(...)' on a 'TraversePaths(...)' query must precede '.Select(...)', '.Take(...)', and " +
            "'.Skip(...)': the query model applies path predicates before projection and pagination, so a " +
            "predicate added after them would silently change which paths are returned. Reorder the operators, " +
            "or materialize the paths first and filter client-side.");
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

    private static void RejectIndexedLambda(MethodCallExpression node, LambdaExpression lambda, string operatorName)
    {
        if (lambda.Parameters.Count != 1)
        {
            throw Unsupported(
                node,
                $"the indexed {operatorName} overload is not supported; graph queries have no positional " +
                "element index to translate.");
        }
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

        public static List<ComplexPropertyNavigation> Collect(Expression expression)
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
