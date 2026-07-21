// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.InMemory.Querying;

using System.Linq.Expressions;
using System.Reflection;
using Cvoya.Graph.Querying;
using Cvoya.Graph.Querying.Commands;
using Cvoya.Graph.Serialization;

/// <summary>
/// Interprets a <see cref="GraphQueryModel"/> with LINQ-to-objects over one store snapshot.
/// Entities are fully hydrated (complex properties included) before any user lambda runs, so
/// predicates, orderings, and projections execute exactly as written. Complex-property traversal
/// hints are skipped for materialization, while predicates also consider colliding domain edges
/// with the same relationship type and target label.
/// </summary>
internal sealed class InMemoryQueryExecutor(
    EntityReader reader,
    StoreState state,
    SchemaRegistry schemaRegistry,
    CancellationToken cancellationToken)
{
    private const string NoElements = "Sequence contains no elements";
    private const string MoreThanOneElement = "Sequence contains more than one element";
    private static readonly object NullGroupKey = new();

    private readonly EntityReader _reader = reader;
    private readonly StoreState _state = state;
    private readonly CancellationToken _cancellationToken = cancellationToken;
    private readonly InMemoryFullTextMatcher _fullTextMatcher = new(schemaRegistry);
    private readonly Dictionary<LambdaExpression, Delegate> _compiled = [];
    private readonly Dictionary<object, ElementBinding> _elementBindings =
        new(GraphDataModel.ReferenceEqualityComparer.Instance);
    private readonly Dictionary<object, Guid> _nodeKeys =
        new(GraphDataModel.ReferenceEqualityComparer.Instance);
    private GraphValueComparer? _graphValueComparer;

    private GraphValueComparer ValueComparer => _graphValueComparer ??= new GraphValueComparer(_elementBindings);

    /// <summary>Executes the model and returns the raw terminal result.</summary>
    public object? Execute(GraphQueryModel model, TerminalHints hints)
    {
        _cancellationToken.ThrowIfCancellationRequested();

        if (model.Union is { } setOperation)
        {
            var first = (IEnumerable<object?>)(Execute(setOperation.First, new TerminalHints(false)) ?? Array.Empty<object?>());
            var second = (IEnumerable<object?>)(Execute(setOperation.Second, new TerminalHints(false)) ?? Array.Empty<object?>());
            var combined = first.Concat(second);
            if (setOperation.Operation == SetOperationKind.Union)
            {
                combined = combined.Distinct(ValueComparer);
            }

            return ApplyTerminal(combined.ToList(), model, hints);
        }

        var rows = RootRows(model.Root);
        rows = ApplyTraversal(rows, model);
        rows = ApplyLabelFilters(rows, model.LabelFilters);
        rows = ApplyRelationshipExistence(rows, model.RelationshipExistence);
        rows = ApplyJoin(rows, model);

        // The All terminal predicate is universal quantification over the effective source, not a
        // source filter: it stays in model.TerminalPredicate and is evaluated once every surviving
        // row is projected. Only the preceding Where predicates filter here.
        var predicates = model.Predicates;
        var deferred = new List<PredicateFragment>();
        var complexTraversals = model.Traversal.Where(step => step.IsComplexPropertyTraversal).ToList();
        if (predicates.Count > 0)
        {
            rows = FilterRows(rows, predicates, deferred, complexTraversals);
        }

        if (model.SearchFilter is { } searchFilter)
        {
            rows = ApplySearchFilter(rows, searchFilter);
        }

        var projectionSource = model.GroupBy is { } groupBy ? GroupRows(rows, groupBy) : rows;
        var pairs = Project(projectionSource, model);

        foreach (var predicate in deferred)
        {
            pairs = pairs.Where(p => EvaluatePredicate(predicate.Predicate, p.Value));
        }

        if (model.Distinct)
        {
            pairs = DistinctByValue(pairs);
        }

        pairs = ApplyOrdering(pairs, model.Ordering);

        var materialized = pairs.ToList();
        _cancellationToken.ThrowIfCancellationRequested();

        var paging = model.Terminal is TerminalOperation.ElementAt or TerminalOperation.ElementAtOrDefault
            ? new Paging((int)model.TerminalOperand!, 1)
            : model.Paging;

        if (paging.Skip is { } skip)
        {
            materialized = [.. materialized.Skip(skip)];
        }

        if (paging.Take is { } take)
        {
            materialized = [.. materialized.Take(take)];
        }

        if (model.PostPaging is { } postPaging)
        {
            foreach (var predicate in postPaging.Predicates)
            {
                materialized = [.. materialized.Where(pair => EvaluatePostPagingPredicate(pair, predicate))];
            }

            if (postPaging.Distinct)
            {
                materialized = [.. DistinctByValue(materialized)];
            }

            materialized = [.. ApplyOrdering(materialized, postPaging.Ordering)];

            if (postPaging.Paging.Skip is { } postPagingSkip)
            {
                materialized = [.. materialized.Skip(postPagingSkip)];
            }

            if (postPaging.Paging.Take is { } postPagingTake)
            {
                materialized = [.. materialized.Take(postPagingTake)];
            }
        }

        var values = materialized.Select(p => p.Value).ToList();
        return ApplyTerminal(values, model, hints);
    }

    /// <summary>Selects opaque store keys with command ordering/windowing followed by deduplication.</summary>
    public IReadOnlyList<SelectedGraphElement> SelectNative(GraphElementSelectionModel selection)
    {
        GraphElementSelectionModelValidator.Validate(selection);
        var model = selection.Query;
        var rows = RootRows(model.Root);
        rows = ApplyLabelFilters(rows, model.LabelFilters);
        rows = ApplyRelationshipExistence(rows, model.RelationshipExistence);

        var deferred = new List<PredicateFragment>();
        if (model.Predicates.Count > 0)
        {
            rows = FilterRows(rows, model.Predicates, deferred, complexTraversals: []);
        }

        if (model.SearchFilter is { } searchFilter)
        {
            rows = ApplySearchFilter(rows, searchFilter);
        }

        var pairs = ApplyOrdering(rows.Select(row => (row, row.Current)), model.Ordering).ToList();
        if (deferred.Count > 0)
        {
            // ExecuteCore re-applies deferred fragments to projected values, but a command
            // selection has no projection stage; dropping them would widen the mutation target
            // set past the query's own filter, so the selection must fail instead.
            throw new GraphQueryTranslationException(
                "Cannot use the query as a graph command selection: a predicate does not bind to the selected graph element.");
        }

        if (model.Paging.Skip is { } skip)
        {
            pairs = [.. pairs.Skip(skip)];
        }

        if (model.Paging.Take is { } take)
        {
            pairs = [.. pairs.Take(take)];
        }

        var selected = pairs
            .Select(pair => pair.Row.NativeIdentity ?? throw new GraphException(
                "The in-memory command selection lost its private record key."))
            .Distinct()
            .Select(identity => new SelectedGraphElement(selection.ElementKind, identity))
            .ToArray();
        if (selection.Mode == GraphElementSelectionMode.ExactOne)
        {
            return selected.Take(2).ToArray();
        }

        return selected;
    }

    private object? ApplyTerminal(
        List<object?> values,
        GraphQueryModel model,
        TerminalHints hints)
    {
        switch (model.Terminal)
        {
            case TerminalOperation.ToListOrArray:
                return values;
            case TerminalOperation.First:
                if (values.Count == 0)
                {
                    return hints.OrDefault ? DefaultResult.Instance : throw new InvalidOperationException(NoElements);
                }

                return values[0];
            case TerminalOperation.Last:
                if (values.Count == 0)
                {
                    return hints.OrDefault ? DefaultResult.Instance : throw new InvalidOperationException(NoElements);
                }

                return values[^1];
            case TerminalOperation.Single:
                if (values.Count > 1)
                {
                    throw new InvalidOperationException(MoreThanOneElement);
                }

                if (values.Count == 0)
                {
                    return hints.OrDefault ? DefaultResult.Instance : throw new InvalidOperationException(NoElements);
                }

                return values[0];
            case TerminalOperation.ElementAt:
#pragma warning disable CA2208 // "index" is the public ElementAt parameter encoded in the query model.
                return values.Count > 0
                    ? values[0]
                    : throw new ArgumentOutOfRangeException("index", "Index was out of range.");
#pragma warning restore CA2208
            case TerminalOperation.ElementAtOrDefault:
                return values.Count > 0 ? values[0] : DefaultResult.Instance;
            case TerminalOperation.Any:
                return values.Count > 0;
            case TerminalOperation.All:
                // Universal quantification over the effective source: vacuously true when empty.
                var allPredicate = model.TerminalPredicate ?? throw new GraphException(
                    $"Terminal operation '{TerminalOperation.All}' requires a terminal predicate.");
                return values.All(v => EvaluatePredicate(allPredicate.Predicate, v));
            case TerminalOperation.Count:
                return values.Count;
            case TerminalOperation.Contains:
                return values.Contains(model.TerminalOperand);
            case TerminalOperation.Sum:
            case TerminalOperation.Average:
            case TerminalOperation.Min:
            case TerminalOperation.Max:
                return Aggregate(model.Terminal, values, AggregateInputType(model));
            default:
                throw new GraphException($"Terminal operation '{model.Terminal}' is not supported by the in-memory provider.");
        }
    }

    private static Type AggregateInputType(GraphQueryModel model) =>
        model.Projection?.Selector?.ReturnType ?? typeof(object);

    private static object? Aggregate(TerminalOperation operation, List<object?> values, Type inputType)
    {
        var nonNull = values.Where(v => v is not null).ToList();
        var nullable = !inputType.IsValueType || Nullable.GetUnderlyingType(inputType) is not null;
        var numericType = Nullable.GetUnderlyingType(inputType) ?? inputType;

        switch (operation)
        {
            case TerminalOperation.Min:
            case TerminalOperation.Max:
                if (nonNull.Count == 0)
                {
                    return nullable ? null : throw new InvalidOperationException(NoElements);
                }

                var comparer = Comparer<object>.Default;
                return operation == TerminalOperation.Min
                    ? nonNull.Aggregate((a, b) => comparer.Compare(a, b) <= 0 ? a : b)
                    : nonNull.Aggregate((a, b) => comparer.Compare(a, b) >= 0 ? a : b);
            case TerminalOperation.Sum:
                if (nonNull.Count == 0)
                {
                    return Convert.ChangeType(0, numericType == typeof(object) ? typeof(int) : numericType,
                        System.Globalization.CultureInfo.InvariantCulture);
                }

                return SumValues(nonNull, numericType);
            case TerminalOperation.Average:
                // LINQ Average ignores nulls: a nullable input averages to a nullable result and
                // returns null for an empty or all-null sequence, while a non-nullable input throws
                // on empty. Integer and long inputs average to double, decimal stays decimal, and
                // float/double compute in double (the API result type shapes float back to float).
                if (nonNull.Count == 0)
                {
                    return nullable ? null : throw new InvalidOperationException(NoElements);
                }

                return numericType == typeof(decimal)
                    ? nonNull.Average(v => Convert.ToDecimal(v, System.Globalization.CultureInfo.InvariantCulture))
                    : nonNull.Average(v => Convert.ToDouble(v, System.Globalization.CultureInfo.InvariantCulture));
            default:
                throw new GraphException($"Aggregate '{operation}' is not supported.");
        }
    }

    private static object SumValues(List<object?> values, Type numericType)
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        return Type.GetTypeCode(numericType) switch
        {
            TypeCode.Decimal => values.Sum(v => Convert.ToDecimal(v, culture)),
            TypeCode.Double => values.Sum(v => Convert.ToDouble(v, culture)),
            TypeCode.Single => (object)values.Sum(v => Convert.ToSingle(v, culture)),
            TypeCode.Int64 or TypeCode.UInt32 => values.Sum(v => Convert.ToInt64(v, culture)),
            _ => values.Sum(v => Convert.ToInt32(v, culture)),
        };
    }

    // ---- rows ----

    private sealed class Row
    {
        public required Dictionary<string, object?> Bindings { get; init; }

        public object? Current { get; set; }

        public object? NativeIdentity { get; set; }

        public List<StepTrace> Traces { get; init; } = [];

        public StepTrace? LastStep => Traces.Count > 0 ? Traces[^1] : null;

        public Dictionary<object, IReadOnlyDictionary<string, StoredProperty>> Sources { get; init; } =
            new(GraphDataModel.ReferenceEqualityComparer.Instance);

        public List<IGraphPathSegment>? PathHops { get; set; }

        public INode? PathStart { get; set; }

        public Row Clone() => new()
        {
            Bindings = new Dictionary<string, object?>(Bindings, StringComparer.Ordinal),
            Current = Current,
            NativeIdentity = NativeIdentity,
            Traces = [.. Traces],
            Sources = new Dictionary<object, IReadOnlyDictionary<string, StoredProperty>>(
                Sources, GraphDataModel.ReferenceEqualityComparer.Instance),
            PathHops = PathHops,
            PathStart = PathStart,
        };
    }

    private sealed record ElementBinding(
        GraphElementKind Kind,
        Guid Key,
        IReadOnlyDictionary<string, StoredProperty> Properties);

    private sealed record StepTrace(
        INode Start,
        IRelationship LastRelationship,
        INode End,
        RelationshipDirection Direction);

    private IEnumerable<Row> RootRows(QueryRoot root)
    {
        switch (root)
        {
            case NodeRoot nodeRoot:
                return MaterializeNodeRoot(nodeRoot.ElementType, "src");
            case RelationshipRoot relationshipRoot:
                return MaterializeRelationshipRoot(relationshipRoot.ElementType, "r");
            case DynamicRoot { ElementType: { } elementType } when elementType == typeof(DynamicNode):
                return MaterializeNodeRoot(typeof(DynamicNode), "src", includeComplexValues: true);
            case DynamicRoot { ElementType: { } elementType } when elementType == typeof(DynamicRelationship):
                return MaterializeRelationshipRoot(typeof(DynamicRelationship), "r");
            case SearchRoot searchRoot:
                return SearchRootRows(searchRoot);
            default:
                throw new GraphException($"Query root '{root.GetType().Name}' is not supported by the in-memory provider.");
        }
    }

    // ---- full-text search ----

    private IEnumerable<Row> SearchRootRows(SearchRoot searchRoot)
    {
        var terms = FullTextQueryTokenizer.Tokenize(searchRoot.Query);
        if (terms.Count == 0)
        {
            yield break;
        }

        // Candidate rows come from the normal materialization path, so subtype narrowing (e.g.
        // SearchNodes<Person> returning Manager rows) and complex-property hydration already work.
        var candidates = searchRoot.Target switch
        {
            SearchRootTarget.Nodes => MaterializeNodeRoot(searchRoot.ElementType ?? typeof(INode), "src"),
            SearchRootTarget.Relationships => MaterializeRelationshipRoot(searchRoot.ElementType ?? typeof(IRelationship), "r"),
            SearchRootTarget.Entities => MaterializeNodeRoot(typeof(INode), "src")
                .Concat(MaterializeRelationshipRoot(typeof(IRelationship), "r")),
            _ => throw new GraphException($"Unsupported full-text search target '{searchRoot.Target}'."),
        };

        foreach (var row in candidates)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            if (row.Current is { } entity && _fullTextMatcher.Matches(entity, terms))
            {
                var searchAlias = searchRoot.Target switch
                {
                    SearchRootTarget.Nodes => "n",
                    SearchRootTarget.Relationships => "r",
                    SearchRootTarget.Entities => "entity",
                    _ => throw new GraphException($"Unsupported full-text search target '{searchRoot.Target}'."),
                };
                row.Bindings[searchAlias] = entity;
                yield return row;
            }
        }
    }

    private IEnumerable<Row> ApplySearchFilter(IEnumerable<Row> rows, SearchRoot searchFilter)
    {
        var terms = FullTextQueryTokenizer.Tokenize(searchFilter.Query);
        var narrowing = searchFilter.ElementType is { } elementType
            && elementType != typeof(INode)
            && elementType != typeof(IRelationship)
            && elementType != typeof(IEntity)
            && elementType != typeof(DynamicNode)
            && elementType != typeof(DynamicRelationship)
                ? elementType
                : null;

        foreach (var row in rows)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            if (row.Current is not { } entity)
            {
                continue;
            }

            // Element-type narrowing mirrors the shared planner's semi-join scope (subtypes
            // included via IsInstanceOfType); then the current entity must match the query.
            if (narrowing is not null && !narrowing.IsInstanceOfType(entity))
            {
                continue;
            }

            if (_fullTextMatcher.Matches(entity, terms))
            {
                yield return row;
            }
        }
    }

    private IEnumerable<Row> MaterializeNodeRoot(Type elementType, string alias, bool includeComplexValues = false)
    {
        foreach (var record in _state.Nodes.Values)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            if (record.IsComplexValue && !includeComplexValues)
            {
                continue;
            }

            var resolved = EntityReader.ResolveNodeType(record, elementType);
            if (!elementType.IsAssignableFrom(resolved))
            {
                continue;
            }

            var entity = _reader.MaterializeNode(record, _state, elementType);
            yield return NewRow(alias, entity, GraphElementKind.Node, record.Properties, record.Key);
        }
    }

    private IEnumerable<Row> MaterializeRelationshipRoot(Type elementType, string alias)
    {
        foreach (var record in _state.Relationships.Values)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            if (record.IsComplexProperty)
            {
                continue;
            }

            var resolved = EntityReader.ResolveRelationshipType(record, elementType);
            if (!elementType.IsAssignableFrom(resolved))
            {
                continue;
            }

            var entity = _reader.MaterializeRelationship(record, elementType);
            yield return NewRow(alias, entity, GraphElementKind.Relationship, record.Properties, record.Key);
        }
    }

    private Row NewRow(
        string alias,
        object entity,
        GraphElementKind kind,
        IReadOnlyDictionary<string, StoredProperty> source,
        Guid nativeIdentity)
    {
        TrackEntity(entity, kind, nativeIdentity, source);
        return new()
        {
            Bindings = new Dictionary<string, object?>(StringComparer.Ordinal) { [alias] = entity },
            Current = entity,
            NativeIdentity = nativeIdentity,
            Sources = new Dictionary<object, IReadOnlyDictionary<string, StoredProperty>>(
                GraphDataModel.ReferenceEqualityComparer.Instance)
            {
                [entity] = source,
            },
        };
    }

    private void TrackEntity(
        object entity,
        GraphElementKind kind,
        Guid key,
        IReadOnlyDictionary<string, StoredProperty> properties)
    {
        _elementBindings[entity] = new ElementBinding(kind, key, properties);
        if (kind == GraphElementKind.Node)
        {
            _nodeKeys[entity] = key;
        }
    }

    private Guid RequireNodeKey(INode node) =>
        _elementBindings.TryGetValue(node, out var binding) && binding.Kind == GraphElementKind.Node
            ? binding.Key
            : throw new GraphException("The in-memory query row lost its private node binding.");

    // ---- traversal ----

    private IEnumerable<Row> ApplyTraversal(IEnumerable<Row> rows, GraphQueryModel model)
    {
        var explicitIndex = 0;
        foreach (var step in model.Traversal)
        {
            if (step.IsComplexPropertyTraversal)
            {
                // Matching hint for query-language planners; hydrated entities already carry
                // their complex properties, so the original lambdas evaluate directly.
                continue;
            }

            explicitIndex++;
            var targetAlias = explicitIndex == 1 ? "tgt" : $"tgt_{explicitIndex}";
            var currentStep = step;
            var isPathStep = model.PathShape is not null;
            rows = rows.SelectMany(row => ExpandStep(row, currentStep, targetAlias, isPathStep));
        }

        return rows;
    }

    private IEnumerable<Row> ExpandStep(Row row, TraversalStep step, string targetAlias, bool buildPath)
    {
        var sourceAlias = step.SourceAlias ?? "src";
        if (!row.Bindings.TryGetValue(sourceAlias, out var sourceValue) || sourceValue is not INode sourceNode)
        {
            sourceValue = row.Current;
            if (sourceValue is not INode fallbackNode)
            {
                yield break;
            }

            sourceNode = fallbackNode;
        }

        var sourceKey = RequireNodeKey(sourceNode);

        // Depth bounds are validated to be positive and ordered before a model reaches a provider,
        // so there is no zero-hop branch to expand here.
        var minDepth = step.Depth.Min;
        var maxDepth = step.Depth.Max;

        // Selection over all candidates only materializes for shortest-path grouping; a plain
        // traversal streams so a bounded terminal can stop the expansion early.
        IEnumerable<(List<Hop> Path, NodeRecord TargetRecord, INode TargetEntity)> selected =
            step.PathSelection switch
            {
                TraversalPathSelection.All => Candidates(),
                TraversalPathSelection.Shortest => Candidates()
                    .GroupBy(candidate => candidate.TargetRecord.Key)
                    .Select(group => group.OrderBy(candidate => candidate.Path.Count).First()),
                TraversalPathSelection.AllShortest => Candidates()
                    .GroupBy(candidate => candidate.TargetRecord.Key)
                    .SelectMany(group =>
                    {
                        var minimum = group.Min(candidate => candidate.Path.Count);
                        return group.Where(candidate => candidate.Path.Count == minimum);
                    }),
                _ => throw new GraphException($"Path selection '{step.PathSelection}' is not supported."),
            };

        var matched = false;
        foreach (var candidate in selected)
        {
            matched = true;
            var path = candidate.Path;
            var finalRecord = candidate.TargetRecord;
            var targetEntity = candidate.TargetEntity;
            var lastRelationship = (IRelationship)_reader.MaterializeRelationship(
                path[^1].Relationship,
                step.RelationshipClrType ?? typeof(IRelationship));
            TrackEntity(
                lastRelationship,
                GraphElementKind.Relationship,
                path[^1].Relationship.Key,
                path[^1].Relationship.Properties);
            TrackEntity(targetEntity, GraphElementKind.Node, finalRecord.Key, finalRecord.Properties);
            var lastHopStart = path.Count == 1
                ? sourceNode
                : MaterializeTrackedNode(path[^2].Target);
            var segmentDirection = GetPathSegmentDirection(
                path[^1].Relationship,
                lastHopStart,
                targetEntity);

            var newRow = row.Clone();
            newRow.Bindings[targetAlias] = targetEntity;
            newRow.Bindings["r"] = lastRelationship;
            newRow.Current = targetEntity;
            newRow.NativeIdentity = finalRecord.Key;
            newRow.Traces.Add(new StepTrace(sourceNode, lastRelationship, targetEntity, segmentDirection));
            newRow.Sources[targetEntity] = finalRecord.Properties;
            newRow.Sources[lastRelationship] = path[^1].Relationship.Properties;

            if (buildPath)
            {
                newRow.PathStart = sourceNode;
                newRow.PathHops = BuildHops(sourceNode, path, step);
            }

            yield return newRow;
        }

        if (step.IsOptional && !matched)
        {
            var optionalRow = row.Clone();
            optionalRow.Bindings[targetAlias] = null;
            optionalRow.Current = null;
            yield return optionalRow;
        }

        IEnumerable<(List<Hop> Path, NodeRecord TargetRecord, INode TargetEntity)> Candidates()
        {
            // Shortest-path selection never returns the source itself, matching the shared contract.
            Guid? sourceRecordKey = step.PathSelection == TraversalPathSelection.All ? null : sourceKey;
            foreach (var path in ExpandPaths(sourceKey, step, maxDepth))
            {
                _cancellationToken.ThrowIfCancellationRequested();
                if (path.Count < Math.Max(minDepth, 1))
                {
                    continue;
                }

                var finalRecord = path[^1].Target;
                if (step.PathSelection != TraversalPathSelection.All && finalRecord.Key == sourceRecordKey)
                {
                    continue;
                }

                var resolved = EntityReader.ResolveNodeType(finalRecord, step.TargetType ?? typeof(INode));
                if (step.TargetType is { } targetType && !targetType.IsAssignableFrom(resolved))
                {
                    continue;
                }

                var targetEntity = (INode)_reader.MaterializeNode(finalRecord, _state, step.TargetType ?? typeof(INode));
                TrackEntity(targetEntity, GraphElementKind.Node, finalRecord.Key, finalRecord.Properties);
                if (step.TargetPredicates.Any(predicate => !EvaluatePredicate(predicate.Predicate, targetEntity)))
                {
                    continue;
                }

                yield return (path, finalRecord, targetEntity);
            }
        }
    }

    private List<IGraphPathSegment> BuildHops(INode start, List<Hop> path, TraversalStep step)
    {
        var segments = new List<IGraphPathSegment>();
        INode current = start;
        foreach (var hop in path)
        {
            var end = MaterializeTrackedNode(hop.Target);
            var relationship = (IRelationship)_reader.MaterializeRelationship(
                hop.Relationship,
                step.RelationshipClrType ?? typeof(IRelationship));
            TrackEntity(
                relationship,
                GraphElementKind.Relationship,
                hop.Relationship.Key,
                hop.Relationship.Properties);
            segments.Add(new InMemoryPathHopSegment(
                current,
                relationship,
                end,
                GetPathSegmentDirection(hop.Relationship, current, end)));
            current = end;
        }

        return segments;
    }

    private sealed record Hop(RelationshipRecord Relationship, NodeRecord Target);

    private RelationshipDirection GetPathSegmentDirection(
        RelationshipRecord relationship,
        INode start,
        INode end)
    {
        var startKey = RequireNodeKey(start);
        var endKey = RequireNodeKey(end);
        if (relationship.PhysicalSourceKey == startKey && relationship.PhysicalTargetKey == endKey)
        {
            return RelationshipDirection.Outgoing;
        }

        if (relationship.PhysicalSourceKey == endKey && relationship.PhysicalTargetKey == startKey)
        {
            return RelationshipDirection.Incoming;
        }

        throw new GraphException("Relationship endpoints do not match the materialized path segment.");
    }

    private IEnumerable<List<Hop>> ExpandPaths(Guid sourceKey, TraversalStep step, int maxDepth)
    {
        var stack = new Stack<(Guid NodeKey, List<Hop> Path)>();
        stack.Push((sourceKey, []));

        while (stack.Count > 0)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            var (nodeKey, path) = stack.Pop();
            if (path.Count >= maxDepth)
            {
                continue;
            }

            foreach (var relationship in _state.Relationships.Values)
            {
                if (!RelationshipMatches(relationship, step))
                {
                    continue;
                }

                if (!RelationshipPredicatesMatch(relationship, step.RelationshipPredicates, step.RelationshipClrType))
                {
                    continue;
                }

                if (path.Any(h => h.Relationship.Key == relationship.Key))
                {
                    continue;
                }

                foreach (var neighborKey in Neighbors(relationship, nodeKey, step.Direction))
                {
                    if (_state.Nodes.TryGetValue(neighborKey, out var neighbor))
                    {
                        var extended = new List<Hop>(path) { new(relationship, neighbor) };
                        yield return extended;
                        stack.Push((neighbor.Key, extended));
                    }
                }
            }
        }
    }

    private INode MaterializeTrackedNode(NodeRecord record)
    {
        var entity = _reader.MaterializeNode<INode>(record, _state);
        TrackEntity(entity, GraphElementKind.Node, record.Key, record.Properties);
        return entity;
    }

    private IEnumerable<Row> ApplyLabelFilters(
        IEnumerable<Row> rows,
        IReadOnlyList<LabelFilterFragment> filters)
    {
        foreach (var filter in filters)
        {
            rows = rows.Where(row => MatchesLabelFilter(row, filter));
        }

        return rows;
    }

    private bool MatchesLabelFilter(Row row, LabelFilterFragment filter)
    {
        var node = (row.Bindings.TryGetValue(filter.Alias, out var bound)
            ? bound as INode
            : null) ?? row.Current as INode;
        if (node is null)
        {
            return false;
        }

        if (!_elementBindings.TryGetValue(node, out var binding) ||
            binding.Kind != GraphElementKind.Node ||
            !_state.Nodes.TryGetValue(binding.Key, out var record) ||
            record.IsComplexValue)
        {
            return false;
        }

        IReadOnlyList<string> storedLabels = record.ActualType == typeof(DynamicNode)
            ? record.Labels
            : [record.Label];
        return filter.Match switch
        {
            GraphLabelMatch.Any => filter.Labels.Any(label => storedLabels.Contains(label, StringComparer.Ordinal)),
            GraphLabelMatch.All => filter.Labels.All(label => storedLabels.Contains(label, StringComparer.Ordinal)),
            _ => false,
        };
    }

    private static bool RelationshipMatches(RelationshipRecord relationship, TraversalStep step)
    {
        var clrType = step.RelationshipClrType;

        if (relationship.IsComplexProperty)
        {
            // Internal complex-property edges are reachable only through dynamic traversal; a
            // typed traversal never sees them.
            if (clrType is not null && clrType != typeof(DynamicRelationship))
            {
                return false;
            }

            return step.RelationshipType is not { } complexLabel ||
                string.Equals(relationship.Type, complexLabel, StringComparison.Ordinal);
        }

        if (clrType is not null && (clrType.IsInterface || clrType.IsAbstract))
        {
            // No concrete relationship type was named: the label the builder derived from the
            // interface/abstract CLR type names no stored type, so match by assignability alone.
            return relationship.ActualType is not null && clrType.IsAssignableFrom(relationship.ActualType);
        }

        if (step.RelationshipType is { } label &&
            !string.Equals(relationship.Type, label, StringComparison.Ordinal))
        {
            return false;
        }

        return clrType is null ||
            (relationship.ActualType is not null && clrType.IsAssignableFrom(relationship.ActualType));
    }

    private bool RelationshipPredicatesMatch(
        RelationshipRecord relationship,
        IReadOnlyList<PredicateFragment> predicates,
        Type? relationshipType)
    {
        if (predicates.Count == 0)
            return true;

        var entity = _reader.MaterializeRelationship(
            relationship,
            relationshipType ?? relationship.ActualType ?? typeof(IRelationship));
        return predicates.All(predicate => EvaluatePredicate(predicate.Predicate, entity));
    }

    private IEnumerable<Row> ApplyRelationshipExistence(
        IEnumerable<Row> rows,
        IReadOnlyList<RelationshipExistenceFragment> filters)
    {
        foreach (var filter in filters)
        {
            rows = rows.Where(row => HasMatchingRelationship(row, filter));
        }

        return rows;
    }

    private bool HasMatchingRelationship(Row row, RelationshipExistenceFragment filter)
    {
        var sourceNode = (row.Bindings.TryGetValue(filter.SourceAlias, out var source)
            ? source as INode
            : null) ?? row.Current as INode;

        if (sourceNode is null)
            return false;

        var sourceKey = RequireNodeKey(sourceNode);

        var step = new TraversalStep(
            Labels.GetLabelFromType(filter.RelationshipType),
            filter.Direction,
            new DepthRange(1, 1),
            filter.Predicate is null ? [] : [filter.Predicate],
            targetType: null,
            relationshipClrType: filter.RelationshipType);
        return _state.Relationships.Values.Any(relationship =>
            RelationshipMatches(relationship, step) &&
            RelationshipPredicatesMatch(relationship, step.RelationshipPredicates, filter.RelationshipType) &&
            Neighbors(relationship, sourceKey, filter.Direction).Any());
    }

    private static IEnumerable<Guid> Neighbors(
        RelationshipRecord relationship,
        Guid nodeKey,
        GraphTraversalDirection direction)
    {
        var matchesOutgoing = direction is GraphTraversalDirection.Outgoing or GraphTraversalDirection.Both &&
            relationship.PhysicalSourceKey == nodeKey;
        var matchesIncoming = direction is GraphTraversalDirection.Incoming or GraphTraversalDirection.Both &&
            relationship.PhysicalTargetKey == nodeKey;

        if (matchesOutgoing)
        {
            yield return relationship.PhysicalTargetKey;
        }

        // Both matches can only be true together when the relationship is a self-loop on nodeKey, and
        // that is one physical edge, not two. Emitting it once keeps Both traversal consistent with the
        // degree projection, which already counts a self-loop once for Both. Parallel relationships stay
        // distinct because this is scoped to a single relationship record, not to neighbor identity.
        if (matchesIncoming && !matchesOutgoing)
        {
            yield return relationship.PhysicalSourceKey;
        }
    }

    // ---- join ----

    private IEnumerable<Row> ApplyJoin(IEnumerable<Row> rows, GraphQueryModel model)
    {
        if (model.Join is not { } join)
        {
            return rows;
        }

        var inner = RootRows(join.InnerRoot).Select(r => r.Current).ToList();
        var outerKey = Compile(join.OuterKeySelector);
        var innerKey = Compile(join.InnerKeySelector);
        var result = Compile(join.ResultSelector);

        return rows.Join(
            inner,
            row => Invoke(outerKey, ResolveInput(row, null, join.OuterKeySelector.Parameters[0].Type)),
            item => Invoke(innerKey, item),
            (row, item) =>
            {
                var joined = row.Clone();
                joined.Current = Invoke(result, ResolveInput(row, null, join.ResultSelector.Parameters[0].Type), item);
                joined.Bindings["joined"] = item;
                return joined;
            },
            ValueComparer);
    }

    // ---- predicates ----

    private IEnumerable<Row> FilterRows(
        IEnumerable<Row> rows,
        IReadOnlyList<PredicateFragment> predicates,
        List<PredicateFragment> deferred,
        IReadOnlyList<TraversalStep> complexTraversals)
    {
        return rows.Where(row =>
        {
            var boundPredicates = new List<BoundPredicate>(predicates.Count);
            foreach (var predicate in predicates)
            {
                var parameterType = predicate.Predicate.Parameters[0].Type;
                var input = ResolveInput(row, predicate.Alias, parameterType);
                if (input is null && !RowCanBind(row, predicate.Alias, parameterType))
                {
                    // Applies to the projected value; evaluated after projection. Deferring inside
                    // the first row that fails to bind keeps binding decisions per-query.
                    if (!deferred.Contains(predicate))
                    {
                        deferred.Add(predicate);
                    }

                    continue;
                }

                boundPredicates.Add(new BoundPredicate(predicate.Predicate, input));
            }

            return boundPredicates.All(predicate => EvaluatePredicate(predicate.Predicate, predicate.Input)) ||
                EvaluatePredicatesAgainstCollidingComplexTargets(boundPredicates, complexTraversals);
        });
    }

    private bool EvaluatePredicatesAgainstCollidingComplexTargets(
        IReadOnlyList<BoundPredicate> predicates,
        IReadOnlyList<TraversalStep> complexTraversals)
    {
        var slots = ComplexTargetSlots(predicates, complexTraversals);
        if (slots.Count == 0)
        {
            return false;
        }

        var replacements = new Dictionary<(Guid SourceKey, Type SourceType, PropertyInfo Property), object>();
        return EvaluateCombinations(slotIndex: 0);

        bool EvaluateCombinations(int slotIndex)
        {
            if (slotIndex == slots.Count)
            {
                return predicates.All(EvaluateWithReplacements);
            }

            var slot = slots[slotIndex];
            var key = (slot.SourceKey, slot.SourceType, slot.Property);
            foreach (var value in slot.Values)
            {
                replacements[key] = value;
                if (EvaluateCombinations(slotIndex + 1))
                {
                    return true;
                }
            }

            replacements.Remove(key);
            return false;
        }

        bool EvaluateWithReplacements(BoundPredicate predicate)
        {
            if (predicate.Input is not INode sourceNode)
            {
                return EvaluatePredicate(predicate.Predicate, predicate.Input);
            }

            var sourceKey = RequireNodeKey(sourceNode);
            var propertyReplacements = replacements
                .Where(pair =>
                    pair.Key.SourceKey == sourceKey &&
                    pair.Key.SourceType == predicate.Input.GetType())
                .ToDictionary(pair => pair.Key.Property, pair => pair.Value);
            var rewrittenBody = new ComplexPropertyValueRewriter(
                predicate.Predicate.Parameters[0],
                propertyReplacements).Visit(predicate.Predicate.Body);
            var rewritten = Expression.Lambda(rewrittenBody!, predicate.Predicate.Parameters);
            return EvaluatePredicate(rewritten, predicate.Input);
        }
    }

    private IReadOnlyList<ComplexTargetSlot> ComplexTargetSlots(
        IReadOnlyList<BoundPredicate> predicates,
        IReadOnlyList<TraversalStep> complexTraversals)
    {
        var slots = new Dictionary<(Guid SourceKey, Type SourceType, PropertyInfo Property), ComplexTargetSlot>();
        foreach (var predicate in predicates)
        {
            if (predicate.Input is not INode sourceNode)
            {
                continue;
            }

            var sourceType = predicate.Input.GetType();
            var sourceKey = RequireNodeKey(sourceNode);
            foreach (var step in complexTraversals)
            {
                if (step.RelationshipType is not { } relationshipType || step.TargetType is not { } targetType)
                {
                    continue;
                }

                var property = GraphDataModel.GetComplexProperties(sourceType).FirstOrDefault(candidate =>
                    candidate.PropertyType == targetType &&
                    string.Equals(
                        GraphDataModel.GetComplexPropertyRelationshipType(candidate),
                        relationshipType,
                        StringComparison.Ordinal));
                if (property is null || !ReferencesParameterProperty(predicate.Predicate, property))
                {
                    continue;
                }

                var key = (sourceKey, sourceType, property);
                if (slots.ContainsKey(key))
                {
                    continue;
                }

                var targetLabel = Labels.GetLabelFromType(targetType);
                var values = CollidingComplexTargets(sourceKey, relationshipType, targetLabel)
                    .Select(target => _reader.MaterializeComplexValue(target, _state, targetType))
                    .ToList();
                if (values.Count > 0)
                {
                    slots.Add(key, new ComplexTargetSlot(sourceKey, sourceType, property, values));
                }
            }
        }

        return [.. slots.Values];
    }

    private static bool ReferencesParameterProperty(LambdaExpression predicate, PropertyInfo property)
    {
        var finder = new ParameterPropertyAccessFinder(predicate.Parameters[0], property);
        finder.Visit(predicate.Body);
        return finder.Found;
    }

    private IEnumerable<NodeRecord> CollidingComplexTargets(
        Guid sourceKey,
        string relationshipType,
        string targetLabel)
    {
        foreach (var relationship in _state.Relationships.Values)
        {
            if (!string.Equals(relationship.Type, relationshipType, StringComparison.Ordinal) ||
                relationship.PhysicalSourceKey != sourceKey)
            {
                continue;
            }

            var targets = _state.Nodes.TryGetValue(relationship.PhysicalTargetKey, out var target)
                ? new[] { target }
                : [];
            foreach (var candidate in targets)
            {
                if (candidate.Labels.Contains(targetLabel, StringComparer.Ordinal))
                {
                    yield return candidate;
                }
            }
        }
    }

    private sealed class ComplexPropertyValueRewriter(
        ParameterExpression parameter,
        IReadOnlyDictionary<PropertyInfo, object> replacements) : ExpressionVisitor
    {
        protected override Expression VisitMember(MemberExpression node) =>
            node.Expression == parameter &&
            node.Member is PropertyInfo property &&
            replacements.TryGetValue(property, out var value)
                ? Expression.Constant(value, property.PropertyType)
                : base.VisitMember(node);
    }

    private sealed class ParameterPropertyAccessFinder(
        ParameterExpression parameter,
        PropertyInfo property) : ExpressionVisitor
    {
        public bool Found { get; private set; }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression == parameter && node.Member == property)
            {
                Found = true;
            }

            return Found ? node : base.VisitMember(node);
        }
    }

    private sealed record BoundPredicate(LambdaExpression Predicate, object? Input);

    private sealed record ComplexTargetSlot(
        Guid SourceKey,
        Type SourceType,
        PropertyInfo Property,
        IReadOnlyList<object> Values);

    private static bool RowCanBind(Row row, string? alias, Type parameterType)
    {
        if (parameterType == typeof(IGraphPath))
        {
            return row.PathHops is { Count: > 0 };
        }

        if (typeof(IGraphPathSegment).IsAssignableFrom(parameterType))
        {
            return FindTrace(row, parameterType) is not null;
        }

        if (alias is not null && row.Bindings.TryGetValue(alias, out var bound) &&
            bound is not null && parameterType.IsInstanceOfType(bound))
        {
            return true;
        }

        if (row.Current is not null && parameterType.IsInstanceOfType(row.Current))
        {
            return true;
        }

        return row.Bindings.Values.Any(v => v is not null && parameterType.IsInstanceOfType(v));
    }

    private static object? ResolveInput(Row row, string? alias, Type parameterType)
    {
        if (parameterType == typeof(IGraphPath))
        {
            return row.PathHops is { Count: > 0 } ? BuildGraphPath(row) : null;
        }

        if (typeof(IGraphPathSegment).IsAssignableFrom(parameterType))
        {
            return FindTrace(row, parameterType) is { } trace ? BuildTypedSegment(trace, parameterType) : null;
        }

        if (alias is not null && row.Bindings.TryGetValue(alias, out var bound) &&
            bound is not null && parameterType.IsInstanceOfType(bound))
        {
            return bound;
        }

        if (row.Current is not null && parameterType.IsInstanceOfType(row.Current))
        {
            return row.Current;
        }

        return row.Bindings.Values.FirstOrDefault(v => v is not null && parameterType.IsInstanceOfType(v));
    }

    /// <summary>
    /// Picks the traversal step a segment-typed lambda binds to: the latest step whose start,
    /// relationship, and end instances fit the requested segment's type arguments. Chained
    /// <c>PathSegments</c> calls each leave a trace; a predicate written against the first
    /// segment shape must not bind to the second.
    /// </summary>
    private static StepTrace? FindTrace(Row row, Type segmentType)
    {
        if (row.Traces.Count == 0)
        {
            return null;
        }

        if (!segmentType.IsGenericType ||
            segmentType.GetGenericTypeDefinition() != typeof(IGraphPathSegment<,,>))
        {
            return row.LastStep;
        }

        var arguments = segmentType.GetGenericArguments();
        for (var i = row.Traces.Count - 1; i >= 0; i--)
        {
            var trace = row.Traces[i];
            if (arguments[0].IsInstanceOfType(trace.Start) &&
                arguments[1].IsInstanceOfType(trace.LastRelationship) &&
                arguments[2].IsInstanceOfType(trace.End))
            {
                return trace;
            }
        }

        return null;
    }

    private static object BuildTypedSegment(StepTrace step, Type segmentType)
    {
        if (segmentType.IsGenericType &&
            segmentType.GetGenericTypeDefinition() == typeof(IGraphPathSegment<,,>))
        {
            var arguments = segmentType.GetGenericArguments();
            var concrete = typeof(InMemoryPathSegment<,,>).MakeGenericType(arguments);
            return Activator.CreateInstance(
                concrete,
                step.Start,
                step.LastRelationship,
                step.End,
                step.Direction)!;
        }

        return new InMemoryPathHopSegment(
            step.Start,
            step.LastRelationship,
            step.End,
            step.Direction);
    }

    private bool EvaluatePredicate(LambdaExpression predicate, object? input)
    {
        var result = Invoke(Compile(predicate), input);
        return result is true;
    }

    private bool EvaluatePostPagingPredicate(
        (Row Row, object? Value) pair,
        PredicateFragment predicate)
    {
        var parameterType = predicate.Predicate.Parameters[0].Type;
        var input = ResolveInput(pair.Row, predicate.Alias, parameterType);
        if (input is null && pair.Value is not null && parameterType.IsInstanceOfType(pair.Value))
        {
            input = pair.Value;
        }

        return EvaluatePredicate(predicate.Predicate, input);
    }

    // ---- grouping ----

    /// <summary>
    /// Groups the post-traversal/post-filter rows by the compiled key selector and yields one row
    /// per group whose <see cref="Row.Current"/> is an <see cref="IGrouping{TKey,TElement}"/> (or the
    /// result-selector output). Nested
    /// <c>group.Select/Where/OrderBy/GroupBy/Count/LongCount/Sum/Average/Min/Max</c> operations then
    /// materialize natively when the projection lambda runs.
    /// </summary>
    private IEnumerable<Row> GroupRows(IEnumerable<Row> rows, GroupByFragment groupBy)
    {
        var keySelector = groupBy.KeySelector;
        var sourceType = keySelector.Parameters[0].Type;
        var keyType = keySelector.ReturnType;
        var elementType = groupBy.ElementSelector?.ReturnType ?? sourceType;
        var keyDelegate = Compile(keySelector);
        var elementDelegate = groupBy.ElementSelector is null ? null : Compile(groupBy.ElementSelector);
        var groupingType = typeof(Grouping<,>).MakeGenericType(keyType, elementType);
        var listType = typeof(List<>).MakeGenericType(elementType);

        var order = new List<object>();
        var groups = new Dictionary<object, (object? Key, System.Collections.IList Elements, Row Row)>(
            ValueComparer);

        foreach (var row in rows)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            var source = ResolveInput(row, null, sourceType);
            var key = Invoke(keyDelegate, source);
            var element = elementDelegate is null ? source : Invoke(elementDelegate, source);
            var boxedKey = key ?? NullGroupKey;
            if (!groups.TryGetValue(boxedKey, out var group))
            {
                group = (key, (System.Collections.IList)Activator.CreateInstance(listType)!, row);
                groups[boxedKey] = group;
                order.Add(boxedKey);
            }

            group.Elements.Add(element);
        }

        foreach (var boxedKey in order)
        {
            var group = groups[boxedKey];
            var grouping = Activator.CreateInstance(groupingType, group.Key, group.Elements)!;
            var newRow = group.Row.Clone();
            newRow.Current = groupBy.ResultSelector is { } resultSelector
                ? Invoke(Compile(resultSelector), group.Key, grouping)
                : grouping;
            yield return newRow;
        }
    }

    private sealed class Grouping<TKey, TElement>(TKey key, List<TElement> elements)
        : IGrouping<TKey, TElement>
    {
        public TKey Key => key;

        public IEnumerator<TElement> GetEnumerator() => elements.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => elements.GetEnumerator();
    }

    // ---- projection ----

    private IEnumerable<(Row Row, object? Value)> Project(IEnumerable<Row> rows, GraphQueryModel model)
    {
        foreach (var row in rows)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            yield return (row, ProjectRow(row, model));
        }
    }

    private static InMemoryGraphPath BuildGraphPath(Row row)
    {
        var hops = row.PathHops ?? [];
        if (hops.Count == 0)
        {
            throw new GraphException("A traversal path must contain at least one segment.");
        }

        return new InMemoryGraphPath(row.PathStart ?? hops[0].StartNode, hops[^1].EndNode, hops);
    }

    private object? ProjectRow(Row row, GraphQueryModel model)
    {
        var projection = model.Projection;
        if (projection?.Selector is not { } selector)
        {
            return model.PathShape is not null ? BuildGraphPath(row) : row.Current;
        }

        if (model.Traversal.Any(item => item.IsOptional) && selector.Parameters.Count == 2)
        {
            var step = model.Traversal.Last(item => item.IsOptional);
            row.Bindings.TryGetValue(step.SourceAlias ?? "src", out var optionalSource);
            row.Bindings.TryGetValue(step.TargetAlias ?? "tgt", out var target);
            return Invoke(Compile(selector), optionalSource, target);
        }

        // A two-parameter selector is the Join result selector, already applied at the join.
        if (selector.Parameters.Count != 1)
        {
            return row.Current;
        }

        if (selector.Body == selector.Parameters[0])
        {
            return ResolveInput(row, null, selector.Parameters[0].Type) ?? row.Current;
        }

        var input = model.PathShape is not null && selector.Parameters[0].Type == typeof(IGraphPath)
            ? BuildGraphPath(row)
            : ResolveInput(row, null, selector.Parameters[0].Type);

        // Constructor projections must read required simple arguments from the stored record just
        // like scalar projections do below. Invoking the compiled selector against a hydrated entity
        // would otherwise observe the deserializer's default for an absent property and silently
        // construct a projection whose corresponding parameter cannot hold that value.
        if (StripConvert(selector.Body) is NewExpression construction &&
            input is not null &&
            row.Sources.TryGetValue(input, out var constructorSource))
        {
            var parameters = construction.Constructor?.GetParameters() ?? [];
            for (var index = 0; index < construction.Arguments.Count && index < parameters.Length; index++)
            {
                if (StripConvert(construction.Arguments[index]) is not MemberExpression
                    {
                        Expression: ParameterExpression parameter,
                        Member: PropertyInfo constructionProperty,
                    } constructionMember ||
                    parameter != selector.Parameters[0] ||
                    (!GraphDataModel.IsSimple(constructionMember.Type) &&
                     !GraphDataModel.IsCollectionOfSimple(constructionMember.Type)) ||
                    NullabilityDerivation.IsParameterNullable(parameters[index]))
                {
                    continue;
                }

                if (!constructorSource.TryGetValue(Labels.GetLabelFromProperty(constructionProperty), out var constructionStored) ||
                    constructionStored.Value is null)
                {
                    throw new GraphException(
                        $"Cannot materialize null into non-nullable type '{parameters[index].ParameterType.FullName}' " +
                        $"for '{parameters[index].Name ?? $"param{index}"}'.");
                }
            }
        }

        // A scalar projection of one simple member reads the stored value: an entity hydrated
        // from a record that never stored the member (e.g. created dynamically) must surface
        // "null into non-nullable" as a materialization error, not the CLR default the
        // deserializer filled in.
        if (StripConvert(selector.Body) is MemberExpression { } member &&
            member.Expression == selector.Parameters[0] &&
            member.Member is PropertyInfo property &&
            input is not null &&
            row.Sources.TryGetValue(input, out var source) &&
            (GraphDataModel.IsSimple(member.Type) || GraphDataModel.IsCollectionOfSimple(member.Type)) &&
            (!source.TryGetValue(Labels.GetLabelFromProperty(property), out var stored) || stored.Value is null))
        {
            if (member.Type.IsValueType && Nullable.GetUnderlyingType(member.Type) is null)
            {
                throw new GraphException(
                    $"Cannot materialize null into non-nullable type '{member.Type.FullName}' for '{member.Member.Name}'.");
            }

            return null;
        }

        return Invoke(Compile(selector), input);
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

    private IEnumerable<(Row Row, object? Value)> DistinctByValue(
        IEnumerable<(Row Row, object? Value)> pairs)
    {
        var seen = new HashSet<object?>(ValueComparer);
        return pairs.Where(pair => seen.Add(pair.Value));
    }

    private sealed class GraphValueComparer(
        IReadOnlyDictionary<object, ElementBinding> elementBindings) : IEqualityComparer<object?>
    {
        public new bool Equals(object? x, object? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null || y is null)
            {
                return false;
            }

            var leftIsElement = elementBindings.TryGetValue(x, out var left);
            var rightIsElement = elementBindings.TryGetValue(y, out var right);
            if (leftIsElement != rightIsElement)
            {
                // A bound graph element hashes by its private key, so it can never equal a
                // detached value hashing by CLR semantics.
                return false;
            }

            if (leftIsElement)
            {
                return left!.Kind == right!.Kind && left.Key == right.Key;
            }

            return EqualityComparer<object?>.Default.Equals(x, y);
        }

        public int GetHashCode(object? value)
        {
            if (value is not null && elementBindings.TryGetValue(value, out var binding))
            {
                return HashCode.Combine(binding.Kind, binding.Key);
            }

            return value?.GetHashCode() ?? 0;
        }
    }

    private sealed class GraphOrderingComparer : Comparer<object?>
    {
        public static GraphOrderingComparer Instance { get; } = new();

        public override int Compare(object? x, object? y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            return (x, y) switch
            {
                (null, _) => -1,
                (_, null) => 1,
                (IEntity, _) or (_, IEntity) => throw new GraphQueryTranslationException(
                    "Whole-entity ordering is not supported by the in-memory provider. " +
                    "Order by one or more mapped scalar properties instead."),
                _ => Comparer<object?>.Default.Compare(x, y),
            };
        }
    }

    private IEnumerable<(Row Row, object? Value)> ApplyOrdering(
        IEnumerable<(Row Row, object? Value)> pairs,
        IReadOnlyList<OrderingKey> ordering)
    {
        if (ordering.Count == 0)
        {
            return pairs;
        }

        IOrderedEnumerable<(Row Row, object? Value)>? ordered = null;
        foreach (var key in ordering)
        {
            object? KeyOf((Row Row, object? Value) pair)
            {
                var parameterType = key.KeySelector.Parameters[0].Type;
                var input = ResolveInput(pair.Row, key.Alias, parameterType);
                if (input is null && pair.Value is not null && parameterType.IsInstanceOfType(pair.Value))
                {
                    input = pair.Value;
                }

                return Invoke(Compile(key.KeySelector), input);
            }

            ordered = (ordered, key.Descending) switch
            {
                (null, false) => pairs.OrderBy(KeyOf, GraphOrderingComparer.Instance),
                (null, true) => pairs.OrderByDescending(KeyOf, GraphOrderingComparer.Instance),
                ({ } previous, false) => previous.ThenBy(KeyOf, GraphOrderingComparer.Instance),
                ({ } previous, true) => previous.ThenByDescending(KeyOf, GraphOrderingComparer.Instance),
            };
        }

        return ordered!;
    }

    // ---- lambda plumbing ----

    private Delegate Compile(LambdaExpression lambda)
    {
        if (!_compiled.TryGetValue(lambda, out var compiled))
        {
            // CountRelationships is a translation marker whose body throws; rewrite each call into a
            // degree computation over the store snapshot before the lambda is compiled and invoked.
            var rewritten = (LambdaExpression)new CountRelationshipsRewriter(_state, _nodeKeys).Visit(lambda)!;
            var nullPropagating = (LambdaExpression)new NullPropagatingMemberAccessVisitor().Visit(rewritten)!;
            compiled = nullPropagating.Compile();
            _compiled[lambda] = compiled;
        }

        return compiled;
    }

    private sealed class CountRelationshipsRewriter(
        StoreState state,
        IReadOnlyDictionary<object, Guid> nodeKeys) : ExpressionVisitor
    {
        private static readonly MethodInfo CountMethod =
            typeof(InMemoryDegreeCounter).GetMethod(
                nameof(InMemoryDegreeCounter.Count),
                BindingFlags.Static | BindingFlags.NonPublic)!;

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType != typeof(GraphDegreeExtensions) ||
                node.Method.Name != nameof(GraphDegreeExtensions.CountRelationships))
            {
                return base.VisitMethodCall(node);
            }

            if (node.Arguments[0] is not ParameterExpression)
            {
                throw new GraphQueryTranslationException(
                    "CountRelationships must be called on a query parameter (e.g. p.CountRelationships<Rel>(...)).");
            }

            if (node.Arguments[1] is not ConstantExpression { Value: GraphTraversalDirection direction } ||
                !Enum.IsDefined(direction))
            {
                throw new GraphQueryTranslationException(
                    "The direction argument to CountRelationships must be a constant GraphTraversalDirection value.");
            }

            var relationshipType = node.Method.GetGenericArguments()[0];

            return Expression.Call(
                CountMethod,
                Expression.Constant(state),
                Expression.Convert(Visit(node.Arguments[0]), typeof(INode)),
                Expression.Constant(nodeKeys),
                Expression.Constant(relationshipType, typeof(Type)),
                Expression.Constant(direction));
        }
    }

    private sealed class NullPropagatingMemberAccessVisitor : ExpressionVisitor
    {
        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression is null)
            {
                return base.VisitMember(node);
            }

            var instance = Visit(node.Expression);
            var access = Expression.MakeMemberAccess(instance, node.Member);
            if (!CanBeNull(instance.Type) || !CanBeNull(access.Type))
            {
                return access;
            }

            return Expression.Condition(
                Expression.Equal(instance, Expression.Constant(null, instance.Type)),
                Expression.Default(access.Type),
                access);
        }

        private static bool CanBeNull(Type type) =>
            !type.IsValueType || Nullable.GetUnderlyingType(type) is not null;
    }

    private static object? Invoke(Delegate compiled, params object?[] arguments)
    {
        try
        {
            return compiled.DynamicInvoke(arguments);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is NullReferenceException)
        {
            // Query languages propagate null through property access; a null-dereference inside
            // a user lambda is the LINQ-to-objects equivalent, so the value is null / no match.
            return null;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }
}
