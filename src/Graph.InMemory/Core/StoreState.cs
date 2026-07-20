// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.InMemory;

using System.Collections.Immutable;

/// <summary>
/// One immutable snapshot of the whole store. Mutations are pure functions from state to state,
/// which is what makes transactions cheap: a transaction is a base snapshot plus a replayable
/// list of these functions, and a query is a read over whichever snapshot is current.
/// </summary>
/// <param name="Nodes">All node records keyed by store-internal key.</param>
/// <param name="Relationships">All relationship records (user relationships and internal
/// complex-property edges) keyed by relationship id.</param>
internal sealed record StoreState(
    ImmutableDictionary<Guid, NodeRecord> Nodes,
    ImmutableDictionary<string, RelationshipRecord> Relationships)
{
    /// <summary>Gets the empty store.</summary>
    public static StoreState Empty { get; } = new(
        ImmutableDictionary<Guid, NodeRecord>.Empty,
        ImmutableDictionary<string, RelationshipRecord>.Empty);

    /// <summary>
    /// Gets the user-deletable root records (everything except decomposed complex-property value
    /// nodes) carrying the given caller-visible id.
    /// </summary>
    public IReadOnlyList<NodeRecord> RootNodes(string id) =>
        [.. Nodes.Values.Where(n => !n.IsComplexValue && n.Id == id)];

    /// <summary>
    /// Gets the internal complex-property edges leaving the given parent record, for the given
    /// relationship type, ordered by collection sequence number.
    /// </summary>
    public IEnumerable<RelationshipRecord> ComplexEdges(Guid parentKey, string relationshipType) =>
        Relationships.Values
            .Where(r => r.IsComplexProperty && r.StartKey == parentKey &&
                        string.Equals(r.Type, relationshipType, StringComparison.Ordinal))
            .OrderBy(r => r.SequenceNumber);

    /// <summary>
    /// Gets all internal complex-property edges leaving the given parent record.
    /// </summary>
    public IEnumerable<RelationshipRecord> ComplexEdges(Guid parentKey) =>
        Relationships.Values.Where(r => r.IsComplexProperty && r.StartKey == parentKey);

    /// <summary>
    /// Adds a node together with its decomposed complex-property subtree. Throws when a node
    /// with the same id and primary label already exists.
    /// </summary>
    public StoreState AddNode(
        NodeRecord node,
        IReadOnlyList<NodeRecord> complexValueNodes,
        IReadOnlyList<RelationshipRecord> complexEdges)
    {
        if (Nodes.Values.Any(n => n.Id == node.Id && string.Equals(n.Label, node.Label, StringComparison.Ordinal)))
        {
            throw new GraphException($"A node with ID {node.Id} and label {node.Label} already exists.");
        }

        var nodes = Nodes.Add(node.Key, node);
        foreach (var child in complexValueNodes)
        {
            nodes = nodes.Add(child.Key, child);
        }

        var relationships = Relationships;
        foreach (var edge in complexEdges)
        {
            relationships = relationships.Add(edge.Id, edge);
        }

        return new StoreState(nodes, relationships);
    }

    /// <summary>
    /// Replaces the node with the given id and primary label: its simple properties are replaced
    /// and its complex-property subtree is deleted and recreated. User relationships attached to
    /// the node are untouched. Throws <see cref="EntityNotFoundException"/> when no such node
    /// exists.
    /// </summary>
    public StoreState UpdateNode(
        NodeRecord replacement,
        IReadOnlyList<NodeRecord> complexValueNodes,
        IReadOnlyList<RelationshipRecord> complexEdges)
    {
        var existing = Nodes.Values.FirstOrDefault(n =>
            !n.IsComplexValue && n.Id == replacement.Id &&
            string.Equals(n.Label, replacement.Label, StringComparison.Ordinal))
            ?? throw new EntityNotFoundException($"Node with ID {replacement.Id} not found for update");

        var state = RemoveComplexSubtree(existing.Key);
        var nodes = state.Nodes.Remove(existing.Key).Add(replacement.Key, replacement);
        foreach (var child in complexValueNodes)
        {
            nodes = nodes.Add(child.Key, child);
        }

        var relationships = state.Relationships;
        foreach (var edge in complexEdges)
        {
            relationships = relationships.Add(edge.Id, edge);
        }

        return new StoreState(nodes, relationships);
    }

    /// <summary>
    /// Deletes the node with the given id, applying the public cascade contract: missing node
    /// throws <see cref="EntityNotFoundException"/>, the same id under more than one label
    /// throws and leaves everything untouched, user relationships block the delete unless
    /// <paramref name="cascadeDelete"/> is set, and the complex-property subtree always goes
    /// with the node.
    /// </summary>
    public StoreState DeleteNode(string id, bool cascadeDelete)
    {
        var roots = RootNodes(id);
        if (roots.Count == 0)
        {
            throw new EntityNotFoundException($"Node with ID {id} not found for deletion");
        }

        if (roots.Count > 1)
        {
            throw new GraphException(
                $"Node ID {id} matches {roots.Count} nodes under different labels; refusing an ambiguous delete.");
        }

        var root = roots[0];
        var userRelationships = Relationships.Values
            .Where(r => !r.IsComplexProperty && (r.StartNodeId == id || r.EndNodeId == id))
            .ToList();

        if (userRelationships.Count > 0 && !cascadeDelete)
        {
            throw new GraphException(
                $"Node with ID {id} has {userRelationships.Count} relationship(s); delete them first or use cascade delete.");
        }

        var state = RemoveComplexSubtree(root.Key);
        var relationships = state.Relationships;
        foreach (var relationship in userRelationships)
        {
            relationships = relationships.Remove(relationship.Id);
        }

        return new StoreState(state.Nodes.Remove(root.Key), relationships);
    }

    /// <summary>Deletes one frozen set of nodes addressed by private record key.</summary>
    public StoreState DeleteNodes(IReadOnlyCollection<Guid> keys, bool cascadeDelete)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var distinctKeys = keys.Distinct().ToArray();
        var targets = distinctKeys.Select(key => Nodes.TryGetValue(key, out var node)
            ? node
            : throw new GraphException("A frozen in-memory node target no longer exists in the transaction view."))
            .ToArray();
        var targetIds = targets.Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
        var userRelationships = Relationships.Values
            .Where(relationship => !relationship.IsComplexProperty &&
                (targetIds.Contains(relationship.StartNodeId) || targetIds.Contains(relationship.EndNodeId)))
            .ToArray();
        if (!cascadeDelete && userRelationships.Length > 0)
        {
            throw new GraphException(
                $"Cannot delete the selected nodes because they have {userRelationships.Length} incident user relationship(s). " +
                "Delete those relationships first or use cascade delete.");
        }

        var state = this;
        foreach (var key in distinctKeys)
        {
            state = state.RemoveComplexSubtree(key);
        }

        var nodes = state.Nodes.RemoveRange(distinctKeys);
        var relationships = state.Relationships;
        if (cascadeDelete)
        {
            relationships = relationships.RemoveRange(userRelationships.Select(relationship => relationship.Id));
        }

        return new StoreState(nodes, relationships);
    }

    /// <summary>Deletes one frozen set of relationships addressed by private record key.</summary>
    public StoreState DeleteRelationships(IReadOnlyCollection<string> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var distinctKeys = keys.Distinct(StringComparer.Ordinal).ToArray();
        if (distinctKeys.Any(key => !Relationships.ContainsKey(key)))
        {
            throw new GraphException("A frozen in-memory relationship target no longer exists in the transaction view.");
        }

        return this with { Relationships = Relationships.RemoveRange(distinctKeys) };
    }

    /// <summary>
    /// Adds a user relationship. Throws when the id already exists or either endpoint does not.
    /// </summary>
    public StoreState AddRelationship(RelationshipRecord relationship)
    {
        if (Relationships.ContainsKey(relationship.Id))
        {
            throw new GraphException($"A relationship with ID {relationship.Id} already exists.");
        }

        if (RootNodes(relationship.StartNodeId).Count == 0)
        {
            throw new GraphException(
                $"Cannot create relationship {relationship.Id}: start node {relationship.StartNodeId} does not exist.");
        }

        if (RootNodes(relationship.EndNodeId).Count == 0)
        {
            throw new GraphException(
                $"Cannot create relationship {relationship.Id}: end node {relationship.EndNodeId} does not exist.");
        }

        return this with { Relationships = Relationships.Add(relationship.Id, relationship) };
    }

    /// <summary>
    /// Replaces the properties of the user relationship with the given id. The stored endpoints,
    /// type, concrete CLR type, and direction are identity: an identity change throws, and
    /// endpoints are kept as stored. Throws <see cref="EntityNotFoundException"/> when no such
    /// relationship exists.
    /// </summary>
    public StoreState UpdateRelationship(RelationshipRecord replacement)
    {
        if (!Relationships.TryGetValue(replacement.Id, out var existing) || existing.IsComplexProperty)
        {
            throw new EntityNotFoundException($"Relationship with ID {replacement.Id} not found for update");
        }

        if (existing.Direction != replacement.Direction)
        {
            throw new GraphException(
                "Direction cannot be changed on update; delete and recreate the relationship. " +
                $"Stored direction is {existing.Direction}; incoming direction is {replacement.Direction}.");
        }

        if (!string.Equals(existing.Type, replacement.Type, StringComparison.Ordinal) ||
            existing.ActualType != replacement.ActualType)
        {
            throw new GraphException(
                "Relationship type or concrete CLR type cannot be changed on update; delete and recreate the relationship. " +
                $"Stored relationship type is '{existing.Type}' and CLR type is '{existing.ActualType?.FullName}'; " +
                $"incoming relationship type is '{replacement.Type}' and CLR type is '{replacement.ActualType?.FullName}'.");
        }

        var updated = existing with { Properties = replacement.Properties };
        return this with { Relationships = Relationships.SetItem(existing.Id, updated) };
    }

    /// <summary>
    /// Deletes the user relationship with the given id. Throws
    /// <see cref="EntityNotFoundException"/> when no such relationship exists.
    /// </summary>
    public StoreState DeleteRelationship(string id)
    {
        if (!Relationships.TryGetValue(id, out var existing) || existing.IsComplexProperty)
        {
            throw new EntityNotFoundException($"Relationship with ID {id} not found for deletion");
        }

        return this with { Relationships = Relationships.Remove(id) };
    }

    private StoreState RemoveComplexSubtree(Guid parentKey)
    {
        var nodes = Nodes;
        var relationships = Relationships;
        var pending = new Stack<Guid>();
        pending.Push(parentKey);

        while (pending.Count > 0)
        {
            var key = pending.Pop();
            foreach (var edge in relationships.Values.Where(r => r.IsComplexProperty && r.StartKey == key).ToList())
            {
                relationships = relationships.Remove(edge.Id);
                if (edge.EndKey is { } childKey && nodes.ContainsKey(childKey))
                {
                    nodes = nodes.Remove(childKey);
                    pending.Push(childKey);
                }
            }
        }

        return new StoreState(nodes, relationships);
    }
}
