// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.InMemory;

using Cvoya.Graph.Serialization;

/// <summary>
/// Rebuilds a fresh <see cref="EntityInfo"/> from store records (recomposing the decomposed
/// complex-property subtree) and materializes entities through the shared
/// <see cref="EntityFactory"/>. Every read produces brand-new dictionaries, values, and object
/// graphs: mutating a returned entity can never reach the store.
/// </summary>
internal sealed class EntityReader(EntityFactory entityFactory)
{
    private readonly EntityFactory _entityFactory = entityFactory;

    /// <summary>Materializes a node record as the given node type.</summary>
    public T MaterializeNode<T>(NodeRecord record, StoreState state) where T : class, INode =>
        (T)MaterializeNode(record, state, typeof(T));

    /// <summary>Materializes a node record as the given target type.</summary>
    public object MaterializeNode(NodeRecord record, StoreState state, Type targetType)
    {
        var actualType = ResolveNodeType(record, targetType);
        if (actualType == typeof(DynamicNode))
        {
            return _entityFactory.Deserialize(BuildDynamicNodeInfo(record, state));
        }

        return _entityFactory.Deserialize(BuildEntityInfo(record, state, actualType));
    }

    /// <summary>Materializes a stored node shape as a complex-property value type.</summary>
    public object MaterializeComplexValue(NodeRecord record, StoreState state, Type targetType) =>
        _entityFactory.Deserialize(BuildEntityInfo(record, state, targetType));

    /// <summary>Materializes a relationship record as the given target type.</summary>
    public object MaterializeRelationship(
        RelationshipRecord record,
        Type targetType)
    {
        var actualType = ResolveRelationshipType(record, targetType);
        var info = BuildRelationshipInfo(record, actualType);
        return _entityFactory.Deserialize(info);
    }

    /// <summary>
    /// Rebuilds the serialized form of a node record, recursing into its decomposed
    /// complex-property subtree.
    /// </summary>
    public EntityInfo BuildEntityInfo(NodeRecord record, StoreState state, Type actualType)
    {
        var simpleProperties = SnapshotToProperties(record.Properties);
        OverrideLabels(simpleProperties, record.Labels);

        var complexProperties = new Dictionary<string, Property>();
        var schema = _entityFactory.GetSchema(actualType);
        if (schema is not null)
        {
            foreach (var (name, propertySchema) in schema.ComplexProperties)
            {
                var relationshipType = propertySchema.RelationshipType
                    ?? GraphDataModel.GetComplexPropertyRelationshipType(propertySchema.PropertyInfo);

                var children = state.ComplexEdges(record.Key, relationshipType)
                    .Select(edge => (edge.SequenceNumber, Entity: ChildInfo(edge, state)))
                    .Where(item => item.Entity is not null)
                    .Select(item => (item.SequenceNumber, item.Entity!))
                    .ToList();

                if (propertySchema.PropertyType == PropertyType.ComplexCollection)
                {
                    var elementType = propertySchema.ElementType ?? typeof(object);
                    EntityCollection collection;
                    if (record.ComplexCollections.TryGetValue(name, out var storedCollection))
                    {
                        if (!string.Equals(
                                storedCollection.RelationshipType,
                                relationshipType,
                                StringComparison.Ordinal))
                        {
                            throw new GraphException(
                                $"Invalid complex-collection storage for property '{name}': " +
                                $"the stored relationship type '{storedCollection.RelationshipType}' does not match " +
                                $"the declared relationship type '{relationshipType}'.");
                        }

                        collection = ComplexCollectionStorageCodec.Rehydrate(
                            name,
                            elementType,
                            storedCollection.Length,
                            storedCollection.NullIndexes,
                            storedCollection.ElementType,
                            children);
                    }
                    else
                    {
                        collection = new EntityCollection(
                            elementType,
                            [.. children
                                .OrderBy(item => item.SequenceNumber)
                                .Select(item => (EntityInfo?)item.Item2)]);
                    }

                    complexProperties[name] = new Property(
                        propertySchema.PropertyInfo,
                        name,
                        propertySchema.IsNullable,
                        collection,
                        relationshipType);
                }
                else if (children.Count > 0)
                {
                    complexProperties[name] = new Property(
                        propertySchema.PropertyInfo,
                        name,
                        propertySchema.IsNullable,
                        children[0].Item2,
                        relationshipType);
                }
            }
        }

        return new EntityInfo(actualType, record.Label, [.. record.Labels], simpleProperties, complexProperties);
    }

    /// <summary>
    /// Rebuilds the serialized form of a node record for dynamic materialization: complex
    /// children are grouped by edge type, with a single child read back as a single value and
    /// multiple children as a collection.
    /// </summary>
    public EntityInfo BuildDynamicNodeInfo(NodeRecord record, StoreState state)
    {
        var entityInfo = BuildDynamicEntityInfo(record, state, includeStructuralProperties: true);
        return entityInfo with { ActualType = typeof(DynamicNode) };
    }

    private EntityInfo BuildDynamicEntityInfo(
        NodeRecord record,
        StoreState state,
        bool includeStructuralProperties)
    {
        var simpleProperties = SnapshotToProperties(record.Properties);
        if (includeStructuralProperties)
        {
            OverrideLabels(simpleProperties, record.Labels);
        }
        else
        {
            simpleProperties.Remove(nameof(INode.Labels));
        }

        var complexProperties = new Dictionary<string, Property>();
        var relationshipGroups = state.ComplexEdges(record.Key)
            .GroupBy(edge => edge.Type, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
        var consumedRelationshipTypes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (propertyName, storedCollection) in record.ComplexCollections
            .OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            if (!consumedRelationshipTypes.Add(storedCollection.RelationshipType))
            {
                throw new GraphException(
                    $"Complex collection metadata assigns relationship type '{storedCollection.RelationshipType}' to more than one property.");
            }

            var edges = relationshipGroups.GetValueOrDefault(storedCollection.RelationshipType) ?? [];
            var children = edges
                .Select(edge => (edge.SequenceNumber, Entity: DynamicChildInfo(edge, state)))
                .Where(item => item.Entity is not null)
                .Select(item => (item.SequenceNumber, item.Entity!))
                .ToList();
            var value = ComplexCollectionStorageCodec.Rehydrate(
                propertyName,
                typeof(object),
                storedCollection.Length,
                storedCollection.NullIndexes,
                storedCollection.ElementType,
                children);

            complexProperties[propertyName] = new Property(
                PropertyInfo: null!,
                propertyName,
                IsNullable: false,
                value,
                storedCollection.RelationshipType);
        }

        foreach (var (relationshipType, edges) in relationshipGroups
            .Where(group => !consumedRelationshipTypes.Contains(group.Key))
            .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var propertyName = GraphDataModel.RelationshipTypeNameToPropertyName(relationshipType);
            if (complexProperties.ContainsKey(propertyName))
            {
                throw new GraphException(
                    $"Complex relationship type '{relationshipType}' conflicts with collection metadata for property '{propertyName}'.");
            }

            var denseChildren = edges
                .Select(edge => (edge.SequenceNumber, Entity: DynamicChildInfo(edge, state)))
                .Where(item => item.Entity is not null)
                .OrderBy(item => item.SequenceNumber)
                .Select(item => item.Entity!)
                .ToList();
            if (denseChildren.Count == 0)
            {
                continue;
            }

            Serialized value = denseChildren.Count == 1
                ? denseChildren[0]
                : new EntityCollection(typeof(object), denseChildren);
            complexProperties[propertyName] = new Property(
                PropertyInfo: null!,
                propertyName,
                IsNullable: false,
                value,
                relationshipType);
        }

        return new EntityInfo(
            record.ActualType,
            record.Label,
            includeStructuralProperties ? [.. record.Labels] : [],
            simpleProperties,
            complexProperties);
    }

    private EntityInfo? DynamicChildInfo(RelationshipRecord edge, StoreState state)
    {
        if (!state.Nodes.TryGetValue(edge.EndKey, out var child))
        {
            return null;
        }

        return BuildDynamicEntityInfo(child, state, includeStructuralProperties: false);
    }

    /// <summary>Rebuilds the serialized form of a relationship record.</summary>
    public static EntityInfo BuildRelationshipInfo(
        RelationshipRecord record,
        Type actualType)
    {
        var simpleProperties = SnapshotToProperties(record.Properties);
        simpleProperties[nameof(IRelationship.Type)] = new Property(
            PropertyInfo: null!,
            nameof(IRelationship.Type),
            IsNullable: false,
            new SimpleValue(record.Type, typeof(string)));

        return new EntityInfo(actualType, record.Type, [], simpleProperties, new Dictionary<string, Property>());
    }

    /// <summary>
    /// Picks the CLR type to materialize a node record as: the stored concrete type when it fits
    /// the target, a label-resolved type when the record was stored dynamically but is being read
    /// as a typed node, and <see cref="DynamicNode"/> when reading dynamically.
    /// </summary>
    public static Type ResolveNodeType(NodeRecord record, Type targetType)
    {
        if (targetType == typeof(DynamicNode))
        {
            return typeof(DynamicNode);
        }

        if (record.ActualType != typeof(DynamicNode) && targetType.IsAssignableFrom(record.ActualType))
        {
            return record.ActualType;
        }

        if (record.ActualType == typeof(DynamicNode))
        {
            var resolved = Labels.GetMostDerivedType(targetType, record.Label);
            if (resolved is not null)
            {
                return resolved;
            }
        }

        return record.ActualType;
    }

    /// <summary>
    /// Picks the CLR type to materialize a relationship record as, mirroring
    /// <see cref="ResolveNodeType"/>.
    /// </summary>
    public static Type ResolveRelationshipType(RelationshipRecord record, Type targetType)
    {
        if (targetType == typeof(DynamicRelationship))
        {
            return typeof(DynamicRelationship);
        }

        var stored = record.ActualType ?? typeof(DynamicRelationship);
        if (stored != typeof(DynamicRelationship) && targetType.IsAssignableFrom(stored))
        {
            return stored;
        }

        if (stored == typeof(DynamicRelationship))
        {
            var resolved = Labels.GetMostDerivedType(targetType, record.Type);
            if (resolved is not null)
            {
                return resolved;
            }
        }

        return stored;
    }

    private EntityInfo? ChildInfo(RelationshipRecord edge, StoreState state)
    {
        if (!state.Nodes.TryGetValue(edge.EndKey, out var child))
        {
            return null;
        }

        return BuildEntityInfo(child, state, child.ActualType);
    }

    private static Dictionary<string, Property> SnapshotToProperties(
        IReadOnlyDictionary<string, StoredProperty> properties)
    {
        var result = new Dictionary<string, Property>();
        foreach (var (name, stored) in properties)
        {
            Serialized? value;
            if (stored.IsCollection && stored.Value is IEnumerable<object?> items)
            {
                var elementType = stored.ElementType ?? typeof(object);
                value = new SimpleCollection(
                    [.. items.Select(item => new SimpleValue(ValueSnapshot.Copy(item)!, item?.GetType() ?? elementType))],
                    elementType);
            }
            else if (stored.Value is null)
            {
                value = new SimpleValue(null!, stored.Type);
            }
            else
            {
                value = new SimpleValue(ValueSnapshot.Copy(stored.Value)!, stored.Type);
            }

            result[name] = new Property(PropertyInfo: null!, name, stored.IsNullable, value);
        }

        return result;
    }

    private static void OverrideLabels(Dictionary<string, Property> properties, IReadOnlyList<string> labels)
    {
        properties["Labels"] = new Property(
            PropertyInfo: null!,
            "Labels",
            IsNullable: false,
            new SimpleCollection([.. labels.Select(l => new SimpleValue(l, typeof(string)))], typeof(string)));
    }

}
