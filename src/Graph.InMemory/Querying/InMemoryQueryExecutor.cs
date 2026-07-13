// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.InMemory.Querying;

using System.Linq.Expressions;
using System.Reflection;
using Cvoya.Graph.Querying;

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
    CancellationToken cancellationToken)
{
    private const string NoElements = "Sequence contains no elements";
    private const string MoreThanOneElement = "Sequence contains more than one element";
    private static readonly object NullGroupKey = new();

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
        var complexTraversals = model.Traversal.Where(step => step.IsComplexPropertyTraversal).ToList();
        if (predicates.Count > 0)
        {
            rows = FilterRows(rows, predicates, deferred, complexTraversals);
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
            var end = _reader.MaterializeNode<INode>(hop.Target, _state);
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
        List<PredicateFragment> predicates,
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

        var replacements = new Dictionary<(string SourceId, Type SourceType, PropertyInfo Property), object>();
        return EvaluateCombinations(slotIndex: 0);

        bool EvaluateCombinations(int slotIndex)
        {
            if (slotIndex == slots.Count)
            {
                return predicates.All(EvaluateWithReplacements);
            }

            var slot = slots[slotIndex];
            var key = (slot.SourceId, slot.SourceType, slot.Property);
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

            var propertyReplacements = replacements
                .Where(pair => pair.Key.SourceId == sourceNode.Id && pair.Key.SourceType == predicate.Input.GetType())
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
        var slots = new Dictionary<(string SourceId, Type SourceType, PropertyInfo Property), ComplexTargetSlot>();
        foreach (var predicate in predicates)
        {
            if (predicate.Input is not INode sourceNode)
            {
                continue;
            }

            var sourceType = predicate.Input.GetType();
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

                var key = (sourceNode.Id, sourceType, property);
                if (slots.ContainsKey(key))
                {
                    continue;
                }

                var targetLabel = Labels.GetLabelFromType(targetType);
                var values = CollidingComplexTargets(sourceNode.Id, relationshipType, targetLabel)
                    .Select(target => _reader.MaterializeComplexValue(target, _state, targetType))
                    .ToList();
                if (values.Count > 0)
                {
                    slots.Add(key, new ComplexTargetSlot(sourceNode.Id, sourceType, property, values));
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
        string sourceId,
        string relationshipType,
        string targetLabel)
    {
        foreach (var relationship in _state.Relationships.Values)
        {
            if (!string.Equals(relationship.Type, relationshipType, StringComparison.Ordinal) ||
                relationship.PhysicalSourceId != sourceId)
            {
                continue;
            }

            var targets = relationship.EndKey is { } targetKey && _state.Nodes.TryGetValue(targetKey, out var target)
                ? [target]
                : NodeRecordsById(relationship.PhysicalTargetId);
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
        string SourceId,
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
    /// result-selector output). Nested <c>group.Select/Where/OrderBy/Count/Average/Max/Min</c> then
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
            GraphValueComparer.Instance);

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

        private static string PrimaryLabel(INode node) => node.Labels.Count == 0 ? string.Empty : node.Labels[0];
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
            var nullPropagating = (LambdaExpression)new NullPropagatingMemberAccessVisitor().Visit(lambda)!;
            compiled = nullPropagating.Compile();
            _compiled[lambda] = compiled;
        }

        return compiled;
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
