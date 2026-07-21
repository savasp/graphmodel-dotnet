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
/// complex-property edges) keyed by private record key.</param>
internal sealed record StoreState(
    ImmutableDictionary<Guid, NodeRecord> Nodes,
    ImmutableDictionary<Guid, RelationshipRecord> Relationships)
{
    /// <summary>Gets the empty store.</summary>
    public static StoreState Empty { get; } = new(
        ImmutableDictionary<Guid, NodeRecord>.Empty,
        ImmutableDictionary<Guid, RelationshipRecord>.Empty);

    /// <summary>
    /// Gets the user-deletable root records (everything except decomposed complex-property value
    /// nodes) carrying the given caller-visible id.
    /// </summary>
    public IReadOnlyList<NodeRecord> RootNodes(string id) =>
        [.. Nodes.Values.Where(n => !n.IsComplexValue && n.CompatibilityId == id)];

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
    /// Adds a node together with its decomposed complex-property subtree.
    /// </summary>
    public StoreState AddNode(
        NodeRecord node,
        IReadOnlyList<NodeRecord> complexValueNodes,
        IReadOnlyList<RelationshipRecord> complexEdges)
    {
        var nodes = Nodes.Add(node.Key, node);
        foreach (var child in complexValueNodes)
        {
            nodes = nodes.Add(child.Key, child);
        }

        var relationships = Relationships;
        foreach (var edge in complexEdges)
        {
            relationships = relationships.Add(edge.Key, edge);
        }

        return new StoreState(nodes, relationships);
    }

    /// <summary>
    /// Replaces the node selected by private record key: its simple properties are replaced and
    /// its complex-property subtree is deleted and recreated. User relationships attached to the
    /// node are untouched. Throws <see cref="EntityNotFoundException"/> when the selected record
    /// no longer exists.
    /// </summary>
    public StoreState UpdateNode(
        NodeRecord replacement,
        IReadOnlyList<NodeRecord> complexValueNodes,
        IReadOnlyList<RelationshipRecord> complexEdges)
    {
        if (!Nodes.TryGetValue(replacement.Key, out var existing) || existing.IsComplexValue)
        {
            throw new EntityNotFoundException("The node selected for update no longer exists.");
        }

        var state = RemoveComplexSubtree(existing.Key);
        var nodes = state.Nodes.Remove(existing.Key).Add(replacement.Key, replacement);
        foreach (var child in complexValueNodes)
        {
            nodes = nodes.Add(child.Key, child);
        }

        var relationships = state.Relationships;
        foreach (var edge in complexEdges)
        {
            relationships = relationships.Add(edge.Key, edge);
        }

        return new StoreState(nodes, relationships);
    }

    /// <summary>
    /// Replaces selected owned complex-property subtrees below one root while leaving every other
    /// complex property and all user graph elements untouched.
    /// </summary>
    public StoreState ReplaceComplexProperties(
        Guid parentKey,
        IReadOnlyCollection<string> relationshipTypes,
        IReadOnlyList<NodeRecord> complexValueNodes,
        IReadOnlyList<RelationshipRecord> complexEdges)
    {
        ArgumentNullException.ThrowIfNull(relationshipTypes);
        ArgumentNullException.ThrowIfNull(complexValueNodes);
        ArgumentNullException.ThrowIfNull(complexEdges);
        if (!Nodes.TryGetValue(parentKey, out var parent) || parent.IsComplexValue)
        {
            throw new GraphException(
                "A frozen in-memory node target no longer exists in the transaction view.");
        }

        var selectedTypes = relationshipTypes.ToHashSet(StringComparer.Ordinal);
        var state = RemoveComplexSubtrees(parentKey, selectedTypes);
        var nodes = state.Nodes;
        foreach (var child in complexValueNodes)
        {
            nodes = nodes.Add(child.Key, child);
        }

        var relationships = state.Relationships;
        foreach (var edge in complexEdges)
        {
            relationships = relationships.Add(edge.Key, edge);
        }

        return new StoreState(nodes, relationships);
    }

    /// <summary>
    /// Deletes the node resolved by the transitional public-ID API, applying the public cascade
    /// contract: a missing or ambiguous ID throws and leaves everything untouched, user
    /// relationships block the delete unless <paramref name="cascadeDelete"/> is set, and the
    /// complex-property subtree always goes with the node.
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
                $"Node ID {id} matches {roots.Count} nodes; refusing an ambiguous delete.");
        }

        var root = roots[0];
        var userRelationships = Relationships.Values
            .Where(r => !r.IsComplexProperty && (r.StartKey == root.Key || r.EndKey == root.Key))
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
            relationships = relationships.Remove(relationship.Key);
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
        var targetKeys = targets.Select(node => node.Key).ToHashSet();
        var userRelationships = Relationships.Values
            .Where(relationship => !relationship.IsComplexProperty &&
                (targetKeys.Contains(relationship.StartKey) || targetKeys.Contains(relationship.EndKey)))
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
            relationships = relationships.RemoveRange(userRelationships.Select(relationship => relationship.Key));
        }

        return new StoreState(nodes, relationships);
    }

    /// <summary>Deletes one frozen set of relationships addressed by private record key.</summary>
    public StoreState DeleteRelationships(IReadOnlyCollection<Guid> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var distinctKeys = keys.Distinct().ToArray();
        if (distinctKeys.Any(key => !Relationships.ContainsKey(key)))
        {
            throw new GraphException("A frozen in-memory relationship target no longer exists in the transaction view.");
        }

        return this with { Relationships = Relationships.RemoveRange(distinctKeys) };
    }

    /// <summary>
    /// Adds a relationship whose endpoint keys must already exist.
    /// </summary>
    public StoreState AddRelationship(RelationshipRecord relationship)
    {
        if (!Nodes.TryGetValue(relationship.StartKey, out var start) || start.IsComplexValue)
        {
            throw new GraphException("Cannot create a relationship because its source endpoint does not exist.");
        }

        if (!Nodes.TryGetValue(relationship.EndKey, out var end) || end.IsComplexValue)
        {
            throw new GraphException("Cannot create a relationship because its target endpoint does not exist.");
        }

        return this with { Relationships = Relationships.Add(relationship.Key, relationship) };
    }

    /// <summary>
    /// Replaces the properties of the user relationship selected by private record key. The
    /// stored endpoints, type, concrete CLR type, and direction are immutable: changing one
    /// throws. Throws <see cref="EntityNotFoundException"/> when the selected record no longer
    /// exists.
    /// </summary>
    public StoreState UpdateRelationship(RelationshipRecord replacement)
    {
        if (!Relationships.TryGetValue(replacement.Key, out var existing) || existing.IsComplexProperty)
        {
            throw new EntityNotFoundException("The relationship selected for update no longer exists.");
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

        if (existing.StartKey != replacement.StartKey || existing.EndKey != replacement.EndKey)
        {
            throw new GraphException(
                "Relationship endpoints cannot be changed on update; delete and recreate the relationship.");
        }

        var updated = existing with { Properties = replacement.Properties };
        return this with { Relationships = Relationships.SetItem(existing.Key, updated) };
    }

    /// <summary>
    /// Deletes the user relationship resolved by the transitional public-ID API. A missing or
    /// ambiguous ID throws and leaves the state untouched.
    /// </summary>
    public StoreState DeleteRelationship(string id)
    {
        var matches = Relationships.Values
            .Where(relationship => !relationship.IsComplexProperty && relationship.CompatibilityId == id)
            .ToArray();
        if (matches.Length == 0)
        {
            throw new EntityNotFoundException($"Relationship with ID {id} not found for deletion");
        }

        if (matches.Length > 1)
        {
            throw new GraphException(
                $"Relationship ID {id} matches {matches.Length} relationships; refusing an ambiguous delete.");
        }

        return this with { Relationships = Relationships.Remove(matches[0].Key) };
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
                relationships = relationships.Remove(edge.Key);
                if (nodes.ContainsKey(edge.EndKey))
                {
                    nodes = nodes.Remove(edge.EndKey);
                    pending.Push(edge.EndKey);
                }
            }
        }

        return new StoreState(nodes, relationships);
    }

    private StoreState RemoveComplexSubtrees(Guid parentKey, HashSet<string> relationshipTypes)
    {
        var nodes = Nodes;
        var relationships = Relationships;
        var roots = relationships.Values
            .Where(relationship => relationship.IsComplexProperty &&
                relationship.StartKey == parentKey &&
                relationshipTypes.Contains(relationship.Type))
            .ToArray();

        foreach (var root in roots)
        {
            relationships = relationships.Remove(root.Key);
            var subtree = new StoreState(nodes, relationships).RemoveComplexSubtree(root.EndKey);
            nodes = subtree.Nodes.Remove(root.EndKey);
            relationships = subtree.Relationships;
        }

        return new StoreState(nodes, relationships);
    }
}
