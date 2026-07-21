// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.InMemory.Querying.Commands;

using System.Collections;
using System.Linq.Expressions;
using Cvoya.Graph.Querying;
using Cvoya.Graph.Querying.Commands;
using Cvoya.Graph.Serialization;

/// <summary>Executes in-memory graph commands against one active transaction view.</summary>
internal sealed class InMemoryGraphCommandExecutionContext(
    InMemoryTransaction transaction,
    InMemoryGraph graph,
    EntityReader reader,
    SchemaRegistry schemaRegistry) : IGraphCommandExecutionContext
{
    public IGraphTransaction Transaction => transaction;

    public Task<IReadOnlyList<SelectedGraphElement>> SelectAsync(
        GraphElementSelectionModel selection,
        Expression sourceExpression,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceExpression);
        cancellationToken.ThrowIfCancellationRequested();
        var executor = new InMemoryQueryExecutor(reader, transaction.View, schemaRegistry, cancellationToken);
        return Task.FromResult(executor.SelectNative(selection));
    }

    public Task<int> ApplyAsync(
        GraphMutationModel mutation,
        Expression mutationExpression,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mutationExpression);
        cancellationToken.ThrowIfCancellationRequested();
        GraphMutationModelValidator.Validate(mutation);

        var executor = new InMemoryQueryExecutor(reader, transaction.View, schemaRegistry, cancellationToken);
        var selected = executor.SelectNative(mutation.Selection);
        if (selected.Count == 0)
        {
            return Task.FromResult(0);
        }

        if (mutation.Kind == GraphMutationKind.Delete)
        {
            ApplyDelete(mutation, selected);
            return Task.FromResult(selected.Count);
        }

        var constraintPlan = GraphMutationConstraintPlan.Create(mutation, schemaRegistry);
        var updates = PrepareUpdates(mutation, selected, cancellationToken);
        transaction.Apply(state => ApplyUpdates(state, updates, constraintPlan));
        return Task.FromResult(selected.Count);
    }

    public Task CreateRelationshipAsync(
        GraphCommandEndpoint source,
        IRelationship relationship,
        GraphCommandEndpoint target,
        RelationshipDirection direction,
        GraphRelationshipCreationMode mode,
        CancellationToken cancellationToken) =>
        graph.CreateCommandRelationshipAsync(
            transaction,
            source,
            relationship,
            target,
            direction,
            mode,
            cancellationToken);

    private void ApplyDelete(GraphMutationModel mutation, IReadOnlyList<SelectedGraphElement> selected)
    {
        if (mutation.Selection.ElementKind == GraphElementKind.Node)
        {
            var keys = selected.Select(item => RequireIdentity<Guid>(item)).ToArray();
            transaction.Apply(state => state.DeleteNodes(keys, mutation.CascadeDelete));
            return;
        }

        var relationshipKeys = selected.Select(item => RequireIdentity<Guid>(item)).ToArray();
        transaction.Apply(state => state.DeleteRelationships(relationshipKeys));
    }

    private List<PreparedUpdate> PrepareUpdates(
        GraphMutationModel mutation,
        IReadOnlyList<SelectedGraphElement> selected,
        CancellationToken cancellationToken)
    {
        var state = transaction.View;
        var entityType = GetEntityType(mutation.Selection.Query.Root);
        var compiled = mutation.Assignments.ToDictionary(
            assignment => assignment,
            assignment => assignment is GraphComputedPropertyAssignment computed
                ? computed.ValueExpression.Compile()
                : null);
        var updates = new List<PreparedUpdate>(selected.Count);

        foreach (var item in selected)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entity = Materialize(item, state, entityType);
            var values = new Dictionary<GraphPropertyAssignment, object?>();
            foreach (var assignment in mutation.Assignments)
            {
                values[assignment] = assignment switch
                {
                    GraphConstantPropertyAssignment constant => constant.Value,
                    GraphComputedPropertyAssignment => compiled[assignment]!.DynamicInvoke(entity),
                    _ => throw new GraphException(
                        $"Graph property assignment '{assignment.GetType().Name}' is not supported."),
                };
            }

            updates.Add(new PreparedUpdate(item, values));
        }

        return updates;
    }

    private object Materialize(SelectedGraphElement selected, StoreState state, Type entityType) =>
        selected.Kind switch
        {
            GraphElementKind.Node => reader.MaterializeNode(
                state.Nodes[RequireIdentity<Guid>(selected)], state, entityType),
            GraphElementKind.Relationship => reader.MaterializeRelationship(
                state.Relationships[RequireIdentity<Guid>(selected)], entityType),
            _ => throw new GraphException($"Graph element kind '{selected.Kind}' is not supported."),
        };

    private static StoreState ApplyUpdates(
        StoreState state,
        IReadOnlyList<PreparedUpdate> updates,
        GraphMutationConstraintPlan constraintPlan)
    {
        foreach (var update in updates)
        {
            if (update.Target.Kind == GraphElementKind.Node)
            {
                var key = RequireIdentity<Guid>(update.Target);
                if (!state.Nodes.TryGetValue(key, out var node))
                {
                    throw new GraphException("A frozen in-memory node target no longer exists in the transaction view.");
                }

                state = state with
                {
                    Nodes = state.Nodes.SetItem(node.Key, node with
                    {
                        Properties = ApplyAssignments(node.Properties, update.Values),
                    }),
                };
            }
            else
            {
                var key = RequireIdentity<Guid>(update.Target);
                if (!state.Relationships.TryGetValue(key, out var relationship) || relationship.IsComplexProperty)
                {
                    throw new GraphException(
                        "A frozen in-memory relationship target no longer exists in the transaction view.");
                }

                state = state with
                {
                    Relationships = state.Relationships.SetItem(key, relationship with
                    {
                        Properties = ApplyAssignments(relationship.Properties, update.Values),
                    }),
                };
            }
        }

        ValidateConstraints(state, updates, constraintPlan);
        return state;
    }

    private static void ValidateConstraints(
        StoreState state,
        IReadOnlyList<PreparedUpdate> updates,
        GraphMutationConstraintPlan constraintPlan)
    {
        if (constraintPlan.IsEmpty)
        {
            return;
        }

        var selectedKeys = updates
            .Select(update => RequireIdentity<Guid>(update.Target))
            .ToHashSet();
        var rows = updates.Select(update =>
        {
            var key = RequireIdentity<Guid>(update.Target);
            var properties = update.Target.Kind == GraphElementKind.Node
                ? state.Nodes[key].Properties
                : state.Relationships[key].Properties;
            return new GraphMutationConstraintRow(
                key,
                constraintPlan.Properties.ToDictionary(
                    property => property.StorageName,
                    property => properties.GetValueOrDefault(property.StorageName)?.Value,
                    StringComparer.Ordinal));
        }).ToArray();
        var proposed = constraintPlan.ValidateFinalValues(rows);

        IEnumerable<IReadOnlyDictionary<string, StoredProperty>> unselected =
            constraintPlan.ElementKind == GraphElementKind.Node
                ? state.Nodes.Values
                    .Where(node => !node.IsComplexValue &&
                        !selectedKeys.Contains(node.Key) &&
                        string.Equals(node.Label, constraintPlan.LabelOrType, StringComparison.Ordinal))
                    .Select(node => node.Properties)
                : state.Relationships.Values
                    .Where(relationship => !relationship.IsComplexProperty &&
                        !selectedKeys.Contains(relationship.Key) &&
                        string.Equals(relationship.Type, constraintPlan.LabelOrType, StringComparison.Ordinal))
                    .Select(relationship => relationship.Properties);

        var stored = unselected.ToArray();
        foreach (var candidate in proposed)
        {
            if (stored.Any(properties => GraphMutationConstraint.Matches(
                    candidate.Values,
                    candidate.Constraint.Properties.Select(property =>
                        properties.GetValueOrDefault(property.StorageName)?.Value).ToArray())))
            {
                throw constraintPlan.CreateViolation(candidate.Constraint);
            }
        }
    }

    private static Dictionary<string, StoredProperty> ApplyAssignments(
        IReadOnlyDictionary<string, StoredProperty> properties,
        IReadOnlyDictionary<GraphPropertyAssignment, object?> assignments)
    {
        var result = new Dictionary<string, StoredProperty>(properties, StringComparer.Ordinal);
        foreach (var (assignment, value) in assignments)
        {
            result[assignment.StorageName] = SnapshotProperty(
                assignment,
                value,
                result.GetValueOrDefault(assignment.StorageName));
        }

        return result;
    }

    private static StoredProperty SnapshotProperty(
        GraphPropertyAssignment assignment,
        object? value,
        StoredProperty? existing)
    {
        // Dynamic-bag types follow the assigned value (matching the entity writer); the previous
        // snapshot only fills in when the new value is null and carries no type of its own.
        var declaredType = assignment.Property?.PropertyType
            ?? (assignment.Dynamic ? value?.GetType() : null)
            ?? existing?.Type
            ?? value?.GetType()
            ?? typeof(object);
        var isCollection = GraphDataModel.IsCollectionOfSimple(declaredType) ||
            assignment.Dynamic && value is IEnumerable and not string && value is not byte[];
        Type? elementType = null;
        if (isCollection)
        {
            elementType = GetElementType(declaredType) ?? GetRuntimeElementType(value) ?? existing?.ElementType;
        }

        return new StoredProperty(
            assignment.StorageName,
            isCollection && value is IEnumerable enumerable
                ? ValueSnapshot.CopyList(enumerable)
                : ValueSnapshot.Copy(value),
            isCollection ? elementType ?? typeof(object) : declaredType,
            existing?.IsNullable ?? value is null || !declaredType.IsValueType || Nullable.GetUnderlyingType(declaredType) is not null,
            isCollection,
            elementType);
    }

    private static Type? GetElementType(Type type) => type switch
    {
        { IsArray: true } => type.GetElementType(),
        { IsGenericType: true } => type.GetGenericArguments().FirstOrDefault(),
        _ => null,
    };

    private static Type? GetRuntimeElementType(object? value) => value is IEnumerable enumerable
        ? enumerable.Cast<object?>().FirstOrDefault(item => item is not null)?.GetType()
        : null;

    private static Type GetEntityType(QueryRoot root) => root switch
    {
        NodeRoot node => node.ElementType,
        RelationshipRoot relationship => relationship.ElementType,
        SearchRoot { ElementType: { } elementType } => elementType,
        SearchRoot { Target: SearchRootTarget.Nodes } => typeof(INode),
        SearchRoot { Target: SearchRootTarget.Relationships } => typeof(IRelationship),
        DynamicRoot { ElementType: { } elementType } => elementType,
        _ => throw new GraphQueryTranslationException(
            $"Query root '{root.GetType().Name}' does not have one materializable graph element type."),
    };

    private static T RequireIdentity<T>(SelectedGraphElement selected) => selected.NativeIdentity is T identity
        ? identity
        : throw new GraphException(
            $"The in-memory {selected.Kind.ToString().ToLowerInvariant()} command target has an invalid private key.");

    private sealed record PreparedUpdate(
        SelectedGraphElement Target,
        IReadOnlyDictionary<GraphPropertyAssignment, object?> Values);
}
