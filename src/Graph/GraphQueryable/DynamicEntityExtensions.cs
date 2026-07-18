// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using System.Reflection;

namespace Cvoya.Graph;

/// <summary>
/// Extension methods for converting strongly-typed entities to dynamic entities.
/// </summary>
public static class DynamicEntityExtensions
{
    /// <summary>
    /// Converts a strongly-typed node to a dynamic node, preserving public, readable, non-indexed modeled
    /// properties and stored labels.
    /// When the node has no stored labels, compatible labels are derived from its runtime type.
    /// </summary>
    /// <typeparam name="TNode">The type of the strongly-typed node.</typeparam>
    /// <param name="node">The strongly-typed node to convert.</param>
    /// <returns>A dynamic node with modeled properties and resolved labels from the original node.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the node is null.</exception>
    /// <exception cref="GraphException">
    /// Thrown when a node or property label is invalid or collides with another physical label.
    /// </exception>
    /// <remarks>
    /// Indexers and properties without public getters are excluded. Exceptions thrown by modeled property
    /// getters propagate unchanged.
    /// </remarks>
    public static DynamicNode ToDynamicNode<TNode>(this TNode node)
        where TNode : class, INode
    {
        ArgumentNullException.ThrowIfNull(node);

        // Get all properties from the node using reflection
        var properties = ExtractProperties(node, EntityKind.Node);

        // Create dynamic node with labels and properties
        return new DynamicNode(
            labels: GetNodeLabels(node),
            properties: properties
        )
        {
            Id = node.Id
        };
    }

    /// <summary>
    /// Converts a strongly-typed relationship to a dynamic relationship, preserving public, readable, non-indexed
    /// modeled properties and its stored type.
    /// When the relationship has no stored type, the physical type is derived from its runtime type.
    /// </summary>
    /// <typeparam name="TRelationship">The type of the strongly-typed relationship.</typeparam>
    /// <param name="relationship">The strongly-typed relationship to convert.</param>
    /// <returns>A dynamic relationship with modeled properties and the resolved type from the original relationship.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the relationship is null.</exception>
    /// <exception cref="GraphException">
    /// Thrown when a relationship type or property label is invalid or collides with another physical label.
    /// </exception>
    /// <remarks>
    /// Indexers and properties without public getters are excluded. Exceptions thrown by modeled property
    /// getters propagate unchanged.
    /// </remarks>
    public static DynamicRelationship ToDynamicRelationship<TRelationship>(this TRelationship relationship)
        where TRelationship : class, IRelationship
    {
        ArgumentNullException.ThrowIfNull(relationship);

        // Get all properties from the relationship using reflection
        var properties = ExtractProperties(relationship, EntityKind.Relationship);

        // Create dynamic relationship with type and properties
        return new DynamicRelationship(
            startNodeId: relationship.StartNodeId,
            endNodeId: relationship.EndNodeId,
            type: GetRelationshipType(relationship),
            properties: properties,
            direction: relationship.Direction
        )
        {
            Id = relationship.Id
        };
    }

    /// <summary>
    /// Converts a strongly-typed node to a dynamic node, preserving public, readable, non-indexed modeled
    /// properties and stored labels.
    /// When the node has no stored labels, compatible labels are derived from its runtime type.
    /// This is a generic overload that works with any INode implementation.
    /// </summary>
    /// <param name="node">The strongly-typed node to convert.</param>
    /// <returns>A dynamic node with modeled properties and resolved labels from the original node.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the node is null.</exception>
    /// <exception cref="GraphException">
    /// Thrown when a node or property label is invalid or collides with another physical label.
    /// </exception>
    /// <remarks>
    /// Indexers and properties without public getters are excluded. Exceptions thrown by modeled property
    /// getters propagate unchanged.
    /// </remarks>
    public static DynamicNode ToDynamic(this INode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        // Get all properties from the node using reflection
        var properties = ExtractProperties(node, EntityKind.Node);

        // Create dynamic node with labels and properties
        return new DynamicNode(
            labels: GetNodeLabels(node),
            properties: properties
        )
        {
            Id = node.Id
        };
    }

    /// <summary>
    /// Converts a strongly-typed relationship to a dynamic relationship, preserving public, readable, non-indexed
    /// modeled properties and its stored type.
    /// When the relationship has no stored type, the physical type is derived from its runtime type.
    /// This is a generic overload that works with any IRelationship implementation.
    /// </summary>
    /// <param name="relationship">The strongly-typed relationship to convert.</param>
    /// <returns>A dynamic relationship with modeled properties and the resolved type from the original relationship.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the relationship is null.</exception>
    /// <exception cref="GraphException">
    /// Thrown when a relationship type or property label is invalid or collides with another physical label.
    /// </exception>
    /// <remarks>
    /// Indexers and properties without public getters are excluded. Exceptions thrown by modeled property
    /// getters propagate unchanged.
    /// </remarks>
    public static DynamicRelationship ToDynamic(this IRelationship relationship)
    {
        ArgumentNullException.ThrowIfNull(relationship);

        // Get all properties from the relationship using reflection
        var properties = ExtractProperties(relationship, EntityKind.Relationship);

        // Create dynamic relationship with type and properties
        return new DynamicRelationship(
            startNodeId: relationship.StartNodeId,
            endNodeId: relationship.EndNodeId,
            type: GetRelationshipType(relationship),
            properties: properties,
            direction: relationship.Direction
        )
        {
            Id = relationship.Id
        };
    }

    /// <summary>
    /// Extracts all properties from an entity using reflection.
    /// </summary>
    /// <param name="entity">The entity to extract properties from.</param>
    /// <param name="entityKind">The graph entity kind whose base metadata properties are excluded.</param>
    /// <returns>A dictionary of property names and values.</returns>
    private static Dictionary<string, object?> ExtractProperties(object entity, EntityKind entityKind)
    {
        var properties = new Dictionary<string, object?>();
        var type = entity.GetType();

        var propertyInfos = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(propertyInfo =>
                propertyInfo.GetMethod is { IsPublic: true } &&
                propertyInfo.GetIndexParameters().Length == 0)
            .Select(propertyInfo =>
                (Property: propertyInfo, Label: Labels.GetLabelFromProperty(propertyInfo)))
            .ToList();

        ValidateUniquePropertyLabels(type, propertyInfos);

        foreach (var (propertyInfo, label) in propertyInfos)
        {
            if (IsBaseProperty(propertyInfo, entityKind))
            {
                continue;
            }

            var value = GetPropertyValue(propertyInfo, entity);
            properties.Add(label, value);
        }

        return properties;
    }

    private static void ValidateUniquePropertyLabels(
        Type entityType,
        IEnumerable<(PropertyInfo Property, string Label)> properties)
    {
        var resolvedLabels = new Dictionary<string, PropertyInfo>(StringComparer.Ordinal);

        foreach (var (property, label) in properties)
        {
            if (resolvedLabels.TryGetValue(label, out var existingProperty))
            {
                throw new GraphException(
                    $"Property label '{label}' on '{entityType.FullName}' is used by both " +
                    $"'{existingProperty.DeclaringType?.FullName}.{existingProperty.Name}' and " +
                    $"'{property.DeclaringType?.FullName}.{property.Name}'.");
            }

            resolvedLabels[label] = property;
        }
    }

    private static object? GetPropertyValue(PropertyInfo propertyInfo, object entity)
    {
        try
        {
            return propertyInfo.GetValue(entity);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    private static IReadOnlyList<string> GetNodeLabels(INode node)
    {
        if (node.Labels.Count > 0)
        {
            return node.Labels;
        }

        return Labels.GetCompatibleLabels(node.GetType());
    }

    private static string GetRelationshipType(IRelationship relationship)
    {
        if (!string.IsNullOrEmpty(relationship.Type))
        {
            return relationship.Type;
        }

        return Labels.GetLabelFromType(relationship.GetType());
    }

    /// <summary>
    /// Determines if a property is part of the base entity interfaces.
    /// </summary>
    /// <param name="propertyInfo">The property information.</param>
    /// <param name="entityKind">The graph entity kind being converted.</param>
    /// <returns>True if the property is part of the base interfaces, false otherwise.</returns>
    private static bool IsBaseProperty(PropertyInfo propertyInfo, EntityKind entityKind)
    {
        var propertyName = propertyInfo.Name;

        if (propertyName == nameof(IEntity.Id))
        {
            return true;
        }

        return entityKind switch
        {
            EntityKind.Node => propertyName == nameof(INode.Labels),
            EntityKind.Relationship =>
                propertyName == nameof(IRelationship.Type) ||
                propertyName == nameof(IRelationship.Direction) ||
                propertyName == nameof(IRelationship.StartNodeId) ||
                propertyName == nameof(IRelationship.EndNodeId),
            _ => false,
        };
    }

    private enum EntityKind
    {
        Node,
        Relationship,
    }
}
