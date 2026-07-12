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

    /// <summary>Materializes a relationship record as the given target type.</summary>
    public object MaterializeRelationship(RelationshipRecord record, Type targetType)
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
                    .Select(edge => ChildInfo(edge, state))
                    .OfType<EntityInfo>()
                    .ToList();

                if (propertySchema.PropertyType == PropertyType.ComplexCollection)
                {
                    complexProperties[name] = new Property(
                        propertySchema.PropertyInfo,
                        name,
                        propertySchema.IsNullable,
                        new EntityCollection(propertySchema.ElementType ?? typeof(object), children),
                        relationshipType);
                }
                else if (children.Count > 0)
                {
                    complexProperties[name] = new Property(
                        propertySchema.PropertyInfo,
                        name,
                        propertySchema.IsNullable,
                        children[0],
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
        var simpleProperties = SnapshotToProperties(record.Properties);
        OverrideLabels(simpleProperties, record.Labels);

        var complexProperties = new Dictionary<string, Property>();
        foreach (var group in state.ComplexEdges(record.Key).GroupBy(e => e.Type, StringComparer.Ordinal))
        {
            var children = group.OrderBy(e => e.SequenceNumber)
                .Select(edge => ChildInfo(edge, state))
                .OfType<EntityInfo>()
                .ToList();

            if (children.Count == 0)
            {
                continue;
            }

            var propertyName = GraphDataModel.RelationshipTypeNameToPropertyName(group.Key);
            complexProperties[propertyName] = new Property(
                PropertyInfo: null!,
                propertyName,
                IsNullable: false,
                children.Count == 1 ? children[0] : new EntityCollection(typeof(object), children),
                group.Key);
        }

        return new EntityInfo(
            typeof(DynamicNode),
            record.Label,
            [.. record.Labels],
            simpleProperties,
            complexProperties);
    }

    /// <summary>Rebuilds the serialized form of a relationship record.</summary>
    public static EntityInfo BuildRelationshipInfo(RelationshipRecord record, Type actualType)
    {
        var simpleProperties = SnapshotToProperties(record.Properties);

        // The stored identity fields are authoritative over whatever the caller serialized.
        SetSimple(simpleProperties, "Id", record.Id, typeof(string));
        SetSimple(simpleProperties, "Type", record.Type, typeof(string));
        SetSimple(simpleProperties, "StartNodeId", record.StartNodeId, typeof(string));
        SetSimple(simpleProperties, "EndNodeId", record.EndNodeId, typeof(string));
        SetSimple(simpleProperties, "Direction", record.Direction, typeof(RelationshipDirection));

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
        if (edge.EndKey is not { } childKey || !state.Nodes.TryGetValue(childKey, out var child))
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

    private static void SetSimple(Dictionary<string, Property> properties, string name, object value, Type type)
    {
        properties[name] = new Property(PropertyInfo: null!, name, IsNullable: false, new SimpleValue(value, type));
    }
}
