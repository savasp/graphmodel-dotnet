// Copyright CVOYA. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.InMemory.Querying;

using System.Linq.Expressions;
using System.Reflection;
using Cvoya.Graph.Querying;

/// <summary>
/// Interprets a <see cref="GraphQueryModel"/> with LINQ-to-objects over one store snapshot.
/// Entities are fully hydrated (complex properties included) before any user lambda runs, so
/// predicates, orderings, and projections execute exactly as written; the complex-property
/// traversal steps the builder injects as query-language matching hints are therefore skipped.
/// </summary>
internal sealed class InMemoryQueryExecutor(
    EntityReader reader,
    StoreState state,
    CancellationToken cancellationToken)
{
    private const string NoElements = "Sequence contains no elements";
    private const string MoreThanOneElement = "Sequence contains more than one element";

    private readonly EntityReader _reader = reader;
    private readonly StoreState _state = state;
    private readonly CancellationToken _cancellationToken = cancellationToken;
    private readonly Dictionary<LambdaExpression, Delegate> _compiled = [];

    /// <summary>Executes the model and returns the raw terminal result.</summary>
    public object? Execute(GraphQueryModel model, TerminalHints hints)
    {
        _cancellationToken.ThrowIfCancellationRequested();

        if (model.Root is SearchRoot || model.SearchFilter is not null)
        {
            throw new GraphException(
                "Full-text search is not supported by the in-memory provider (the FullTextSearch capability is not declared).");
        }

        var rows = RootRows(model.Root);
        rows = ApplyTraversal(rows, model);
        rows = ApplyJoin(rows, model);

        var predicates = model.Predicates.ToList();
        PredicateFragment? allPredicate = null;
        if (model.Terminal == TerminalOperation.All && predicates.Count > 0)
        {
            allPredicate = predicates[^1];
            predicates.RemoveAt(predicates.Count - 1);
        }

        var deferred = new List<PredicateFragment>();
        foreach (var predicate in predicates)
        {
            rows = FilterRows(rows, predicate, deferred);
        }

        var pairs = Project(rows, model);

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

        if (model.Paging.Skip is { } skip)
        {
            materialized = [.. materialized.Skip(skip)];
        }

        if (model.Paging.Take is { } take)
        {
            materialized = [.. materialized.Take(take)];
        }

        var values = materialized.Select(p => p.Value).ToList();
        return ApplyTerminal(values, model, hints, allPredicate);
    }

    private object? ApplyTerminal(
        List<object?> values,
        GraphQueryModel model,
        TerminalHints hints,
        PredicateFragment? allPredicate)
    {
        switch (model.Terminal)
        {
            case TerminalOperation.ToListOrArray:
            case TerminalOperation.Distinct:
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
                return values.Count > 0
                    ? values[0]
                    : throw new ArgumentOutOfRangeException("index", "Index was out of range.");
            case TerminalOperation.ElementAtOrDefault:
                return values.Count > 0 ? values[0] : DefaultResult.Instance;
            case TerminalOperation.Any:
                return values.Count > 0;
            case TerminalOperation.All:
                return allPredicate is null ||
                    values.All(v => EvaluatePredicate(allPredicate.Predicate, v));
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
                if (nonNull.Count == 0)
                {
                    return nullable && values.Count == 0 && Nullable.GetUnderlyingType(inputType) is not null
                        ? null
                        : throw new InvalidOperationException(NoElements);
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
            Traces = [.. Traces],
            Sources = new Dictionary<object, IReadOnlyDictionary<string, StoredProperty>>(
                Sources, GraphDataModel.ReferenceEqualityComparer.Instance),
            PathHops = PathHops,
            PathStart = PathStart,
        };
    }

    private sealed record StepTrace(INode Start, IRelationship? LastRelationship, INode End);

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
            default:
                throw new GraphException($"Query root '{root.GetType().Name}' is not supported by the in-memory provider.");
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
            yield return NewRow(alias, entity, record.Properties);
        }
    }

    private IEnumerable<Row> MaterializeRelationshipRoot(Type elementType, string alias)
    {
        foreach (var record in _state.Relationships.Values)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            var resolved = EntityReader.ResolveRelationshipType(record, elementType);
            if (!elementType.IsAssignableFrom(resolved))
            {
                continue;
            }

            var entity = _reader.MaterializeRelationship(record, elementType);
            yield return NewRow(alias, entity, record.Properties);
        }
    }

    private static Row NewRow(string alias, object entity, IReadOnlyDictionary<string, StoredProperty> source) => new()
    {
        Bindings = new Dictionary<string, object?>(StringComparer.Ordinal) { [alias] = entity },
        Current = entity,
        Sources = new Dictionary<object, IReadOnlyDictionary<string, StoredProperty>>(
            GraphDataModel.ReferenceEqualityComparer.Instance)
        {
            [entity] = source,
        },
    };

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

        var minDepth = Math.Max(step.Depth.Min, 0);
        var maxDepth = Math.Max(step.Depth.Max, minDepth);

        if (minDepth == 0)
        {
            var zeroRow = row.Clone();
            zeroRow.Bindings[targetAlias] = sourceNode;
            zeroRow.Current = sourceNode;
            zeroRow.Traces.Add(new StepTrace(sourceNode, null, sourceNode));
            yield return zeroRow;
        }

        foreach (var path in ExpandPaths(sourceNode.Id, step, maxDepth))
        {
            _cancellationToken.ThrowIfCancellationRequested();
            if (path.Count < Math.Max(minDepth, 1))
            {
                continue;
            }

            var finalRecord = path[^1].Target;
            var resolved = EntityReader.ResolveNodeType(finalRecord, step.TargetType ?? typeof(INode));
            if (step.TargetType is { } targetType && !targetType.IsAssignableFrom(resolved))
            {
                continue;
            }

            var targetEntity = (INode)_reader.MaterializeNode(finalRecord, _state, step.TargetType ?? typeof(INode));
            var lastRelationship = (IRelationship)_reader.MaterializeRelationship(
                path[^1].Relationship,
                step.RelationshipClrType ?? typeof(IRelationship));

            var newRow = row.Clone();
            newRow.Bindings[targetAlias] = targetEntity;
            newRow.Bindings["r"] = lastRelationship;
            newRow.Current = targetEntity;
            newRow.Traces.Add(new StepTrace(sourceNode, lastRelationship, targetEntity));
            newRow.Sources[targetEntity] = finalRecord.Properties;
            newRow.Sources[lastRelationship] = path[^1].Relationship.Properties;

            if (buildPath)
            {
                newRow.PathStart = sourceNode;
                newRow.PathHops = BuildHops(sourceNode, path, step);
            }

            yield return newRow;
        }
    }

    private List<IGraphPathSegment> BuildHops(INode start, List<Hop> path, TraversalStep step)
    {
        var segments = new List<IGraphPathSegment>();
        INode current = start;
        foreach (var hop in path)
        {
            var end = (INode)_reader.MaterializeNode(hop.Target, _state, typeof(INode));
            var relationship = (IRelationship)_reader.MaterializeRelationship(
                hop.Relationship,
                step.RelationshipClrType ?? typeof(IRelationship));
            segments.Add(new InMemoryPathHopSegment(current, relationship, end));
            current = end;
        }

        return segments;
    }

    private sealed record Hop(RelationshipRecord Relationship, NodeRecord Target);

    private IEnumerable<List<Hop>> ExpandPaths(string sourceId, TraversalStep step, int maxDepth)
    {
        var stack = new Stack<(string NodeId, List<Hop> Path)>();
        stack.Push((sourceId, []));

        while (stack.Count > 0)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            var (nodeId, path) = stack.Pop();
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

                if (path.Any(h => h.Relationship.Id == relationship.Id))
                {
                    continue;
                }

                foreach (var neighborId in Neighbors(relationship, nodeId, step.Direction))
                {
                    foreach (var neighbor in NodeRecordsById(neighborId))
                    {
                        var extended = new List<Hop>(path) { new(relationship, neighbor) };
                        yield return extended;
                        stack.Push((neighbor.Id, extended));
                    }
                }
            }
        }
    }

    private IEnumerable<NodeRecord> NodeRecordsById(string id) =>
        _state.Nodes.Values.Where(n => n.Id == id);

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

    private static IEnumerable<string> Neighbors(
        RelationshipRecord relationship,
        string nodeId,
        GraphTraversalDirection direction)
    {
        if (direction is GraphTraversalDirection.Outgoing or GraphTraversalDirection.Both &&
            relationship.PhysicalSourceId == nodeId)
        {
            yield return relationship.PhysicalTargetId;
        }

        if (direction is GraphTraversalDirection.Incoming or GraphTraversalDirection.Both &&
            relationship.PhysicalTargetId == nodeId)
        {
            yield return relationship.PhysicalSourceId;
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
            });
    }

    // ---- predicates ----

    private IEnumerable<Row> FilterRows(
        IEnumerable<Row> rows,
        PredicateFragment predicate,
        List<PredicateFragment> deferred)
    {
        var parameterType = predicate.Predicate.Parameters[0].Type;

        return rows.Where(row =>
        {
            var input = ResolveInput(row, predicate.Alias, parameterType);
            if (input is null && !RowCanBind(row, predicate.Alias, parameterType))
            {
                // Applies to the projected value; evaluated after projection. Deferring inside
                // the first row that fails to bind keeps binding decisions per-query.
                if (!deferred.Contains(predicate))
                {
                    deferred.Add(predicate);
                }

                return true;
            }

            return EvaluatePredicate(predicate.Predicate, input);
        });
    }

    private bool RowCanBind(Row row, string? alias, Type parameterType)
    {
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

    private object? ResolveInput(Row row, string? alias, Type parameterType)
    {
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
            if (trace.LastRelationship is not null &&
                arguments[0].IsInstanceOfType(trace.Start) &&
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
        if (step.LastRelationship is null)
        {
            throw new GraphException("A zero-hop traversal has no relationship to project into a path segment.");
        }

        if (segmentType.IsGenericType &&
            segmentType.GetGenericTypeDefinition() == typeof(IGraphPathSegment<,,>))
        {
            var arguments = segmentType.GetGenericArguments();
            var concrete = typeof(InMemoryPathSegment<,,>).MakeGenericType(arguments);
            return Activator.CreateInstance(concrete, step.Start, step.LastRelationship, step.End)!;
        }

        return new InMemoryPathHopSegment(step.Start, step.LastRelationship, step.End);
    }

    private bool EvaluatePredicate(LambdaExpression predicate, object? input)
    {
        var result = Invoke(Compile(predicate), input);
        return result is true;
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

    private object? ProjectRow(Row row, GraphQueryModel model)
    {
        if (model.PathShape is not null)
        {
            var hops = row.PathHops ?? [];
            if (hops.Count == 0)
            {
                throw new GraphException("A traversal path must contain at least one segment.");
            }

            return new InMemoryGraphPath(row.PathStart ?? hops[0].StartNode, hops[^1].EndNode, hops);
        }

        var projection = model.Projection;
        if (projection?.Selector is not { } selector)
        {
            return row.Current;
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

        var input = ResolveInput(row, null, selector.Parameters[0].Type);

        // A scalar projection of one simple member reads the stored value: an entity hydrated
        // from a record that never stored the member (e.g. created dynamically) must surface
        // "null into non-nullable" as a materialization error, not the CLR default the
        // deserializer filled in.
        if (StripConvert(selector.Body) is MemberExpression { } member &&
            member.Expression == selector.Parameters[0] &&
            input is not null &&
            row.Sources.TryGetValue(input, out var source) &&
            (GraphDataModel.IsSimple(member.Type) || GraphDataModel.IsCollectionOfSimple(member.Type)) &&
            (!source.TryGetValue(member.Member.Name, out var stored) || stored.Value is null))
        {
            if (member.Type.IsValueType && Nullable.GetUnderlyingType(member.Type) is null)
            {
                throw new InvalidOperationException(
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

    private static IEnumerable<(Row Row, object? Value)> DistinctByValue(
        IEnumerable<(Row Row, object? Value)> pairs)
    {
        var seen = new HashSet<object?>(GraphValueComparer.Instance);
        return pairs.Where(pair => seen.Add(pair.Value));
    }

    private sealed class GraphValueComparer : IEqualityComparer<object?>
    {
        public static GraphValueComparer Instance { get; } = new();

        public new bool Equals(object? x, object? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            return (x, y) switch
            {
                (INode left, INode right) =>
                    string.Equals(left.Id, right.Id, StringComparison.Ordinal) &&
                    string.Equals(PrimaryLabel(left), PrimaryLabel(right), StringComparison.Ordinal),
                (IRelationship left, IRelationship right) =>
                    string.Equals(left.Id, right.Id, StringComparison.Ordinal),
                _ => EqualityComparer<object?>.Default.Equals(x, y),
            };
        }

        public int GetHashCode(object? value) => value switch
        {
            INode node => HashCode.Combine(
                StringComparer.Ordinal.GetHashCode(node.Id),
                StringComparer.Ordinal.GetHashCode(PrimaryLabel(node))),
            IRelationship relationship => StringComparer.Ordinal.GetHashCode(relationship.Id),
            null => 0,
            _ => value.GetHashCode(),
        };

        private static string PrimaryLabel(INode node) => node.Labels.FirstOrDefault() ?? string.Empty;
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
                (null, false) => pairs.OrderBy(KeyOf, Comparer<object?>.Default),
                (null, true) => pairs.OrderByDescending(KeyOf, Comparer<object?>.Default),
                ({ } previous, false) => previous.ThenBy(KeyOf, Comparer<object?>.Default),
                ({ } previous, true) => previous.ThenByDescending(KeyOf, Comparer<object?>.Default),
            };
        }

        return ordered!;
    }

    // ---- lambda plumbing ----

    private Delegate Compile(LambdaExpression lambda)
    {
        if (!_compiled.TryGetValue(lambda, out var compiled))
        {
            compiled = lambda.Compile();
            _compiled[lambda] = compiled;
        }

        return compiled;
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
