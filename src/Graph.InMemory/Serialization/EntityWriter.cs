// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.InMemory;

using Cvoya.Graph.Serialization;

/// <summary>
/// Turns a serialized <see cref="EntityInfo"/> into store records: one main record with an
/// isolated snapshot of the simple properties, plus the decomposed complex-property subtree
/// (one value node per occurrence, linked by internal marker edges whose type is the property
/// name per ADR-0002). This mirrors the reference provider's storage shape, so complex-property
/// semantics come from real decomposition, not object aliasing.
/// </summary>
internal static class EntityWriter
{
    /// <summary>The result of decomposing one node entity.</summary>
    public sealed record DecomposedNode(
        NodeRecord Node,
        IReadOnlyList<NodeRecord> ComplexValueNodes,
        IReadOnlyList<RelationshipRecord> ComplexEdges)
    {
        /// <summary>Retargets the decomposed root to an existing private key for legacy updates.</summary>
        public DecomposedNode WithRootKey(Guid key)
        {
            var previous = Node.Key;
            return this with
            {
                Node = Node with { Key = key },
                ComplexEdges = [.. ComplexEdges.Select(edge =>
                    edge.StartKey == previous ? edge with { StartKey = key } : edge)],
            };
        }
    }

    /// <summary>Decomposes a serialized node into its store records.</summary>
    public static DecomposedNode DecomposeNode(EntityInfo entity)
    {
        var labels = EffectiveLabels(entity);
        var properties = SnapshotSimpleProperties(entity, labels);

        var node = new NodeRecord(
            Guid.NewGuid(),
            CompatibilityId(entity),
            labels.Count > 0 ? labels[0] : entity.Label,
            labels,
            entity.ActualType,
            properties,
            IsComplexValue: false);

        var valueNodes = new List<NodeRecord>();
        var edges = new List<RelationshipRecord>();
        DecomposeComplexProperties(entity, node, valueNodes, edges, depth: 1);

        return new DecomposedNode(node, valueNodes, edges);
    }

    /// <summary>
    /// Decomposes a serialized relationship into its store record. Relationships may only carry
    /// simple properties, matching the reference provider's contract.
    /// </summary>
    public static RelationshipRecord DecomposeRelationship(
        EntityInfo entity,
        Guid startKey,
        Guid endKey,
        RelationshipDirection direction,
        Guid? key = null)
    {
        if (entity.ComplexProperties.Any(p => p.Value.Value is not null))
        {
            throw new GraphException(
                $"Relationship type {entity.ActualType.Name} has complex properties; relationships may only have simple properties.");
        }

        return new RelationshipRecord(
            key ?? Guid.NewGuid(),
            CompatibilityId(entity),
            entity.Label,
            startKey,
            endKey,
            direction,
            entity.ActualType,
            SnapshotSimpleProperties(entity, labels: null),
            IsComplexProperty: false,
            SequenceNumber: 0);
    }

    private static void DecomposeComplexProperties(
        EntityInfo entity,
        NodeRecord parent,
        List<NodeRecord> valueNodes,
        List<RelationshipRecord> edges,
        int depth)
    {
        if (depth > GraphDataModel.DefaultDepthAllowed)
        {
            throw new GraphException(
                $"Complex property depth exceeds the maximum of {GraphDataModel.DefaultDepthAllowed}.");
        }

        foreach (var (name, property) in entity.ComplexProperties)
        {
            var relationshipType = property.RelationshipType
                ?? (property.PropertyInfo is not null
                    ? GraphDataModel.GetComplexPropertyRelationshipType(property.PropertyInfo)
                    : GraphDataModel.PropertyNameToRelationshipTypeName(name));

            switch (property.Value)
            {
                case null:
                    break;
                case EntityInfo single:
                    AddComplexValue(single, parent, relationshipType, sequenceNumber: 0, valueNodes, edges, depth);
                    break;
                case EntityCollection collection:
                    var sequence = 0;
                    foreach (var item in collection.Entities)
                    {
                        AddComplexValue(item, parent, relationshipType, sequence++, valueNodes, edges, depth);
                    }

                    break;
                default:
                    throw new GraphException(
                        $"Complex property '{name}' on {entity.ActualType.Name} has an unsupported serialized form.");
            }
        }
    }

    private static void AddComplexValue(
        EntityInfo entity,
        NodeRecord parent,
        string relationshipType,
        int sequenceNumber,
        List<NodeRecord> valueNodes,
        List<RelationshipRecord> edges,
        int depth)
    {
        var properties = SnapshotSimpleProperties(entity, labels: null);

        var valueNode = new NodeRecord(
            Guid.NewGuid(),
            CompatibilityId: null,
            entity.Label,
            [entity.Label],
            entity.ActualType,
            properties,
            IsComplexValue: true);

        valueNodes.Add(valueNode);
        edges.Add(new RelationshipRecord(
            Guid.NewGuid(),
            CompatibilityId: null,
            relationshipType,
            parent.Key,
            valueNode.Key,
            RelationshipDirection.Outgoing,
            ActualType: null,
            Properties: new Dictionary<string, StoredProperty>(),
            IsComplexProperty: true,
            SequenceNumber: sequenceNumber));

        DecomposeComplexProperties(entity, valueNode, valueNodes, edges, depth + 1);
    }

    private static Dictionary<string, StoredProperty> SnapshotSimpleProperties(
        EntityInfo entity,
        IReadOnlyList<string>? labels)
    {
        var snapshot = new Dictionary<string, StoredProperty>();
        foreach (var (name, property) in entity.SimpleProperties)
        {
            switch (property.Value)
            {
                case SimpleValue simple:
                    snapshot[name] = new StoredProperty(
                        name,
                        ValueSnapshot.Copy(simple.Object),
                        simple.Type,
                        property.IsNullable,
                        IsCollection: false,
                        ElementType: null);
                    break;
                case SimpleCollection collection:
                    snapshot[name] = new StoredProperty(
                        name,
                        ValueSnapshot.CopyList(collection.Values.Select(v => v.Object)),
                        collection.ElementType,
                        property.IsNullable,
                        IsCollection: true,
                        ElementType: collection.ElementType);
                    break;
                case null:
                    snapshot[name] = new StoredProperty(
                        name,
                        null,
                        property.PropertyInfo?.PropertyType ?? typeof(object),
                        IsNullable: true,
                        IsCollection: false,
                        ElementType: null);
                    break;
            }
        }

        if (labels is not null)
        {
            snapshot["Labels"] = new StoredProperty(
                "Labels",
                labels.Cast<object?>().ToList(),
                typeof(string),
                IsNullable: false,
                IsCollection: true,
                ElementType: typeof(string));
        }

        return snapshot;
    }

    private static IReadOnlyList<string> EffectiveLabels(EntityInfo entity)
    {
        if (entity.ActualLabels.Count > 0)
        {
            return [.. entity.ActualLabels];
        }

        var compatible = Labels.GetCompatibleLabels(entity.ActualType);
        return compatible.Count > 0 ? [.. compatible] : [entity.Label];
    }

    private static string? CompatibilityId(EntityInfo entity)
    {
        if (entity.SimpleProperties.TryGetValue("Id", out var property) &&
            property.Value is SimpleValue { Object: string id } &&
            !string.IsNullOrEmpty(id))
        {
            return id;
        }

        return null;
    }
}
