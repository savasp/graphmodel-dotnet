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
    /// Converts a strongly-typed node to a dynamic node, preserving all properties and stored labels.
    /// When the node has no stored labels, compatible labels are derived from its runtime type.
    /// </summary>
    /// <typeparam name="TNode">The type of the strongly-typed node.</typeparam>
    /// <param name="node">The strongly-typed node to convert.</param>
    /// <returns>A dynamic node with all properties and resolved labels from the original node.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the node is null.</exception>
    public static DynamicNode ToDynamicNode<TNode>(this TNode node)
        where TNode : class, INode
    {
        ArgumentNullException.ThrowIfNull(node);

        // Get all properties from the node using reflection
        var properties = ExtractProperties(node);

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
    /// Converts a strongly-typed relationship to a dynamic relationship, preserving all properties and its stored type.
    /// When the relationship has no stored type, the physical type is derived from its runtime type.
    /// </summary>
    /// <typeparam name="TRelationship">The type of the strongly-typed relationship.</typeparam>
    /// <param name="relationship">The strongly-typed relationship to convert.</param>
    /// <returns>A dynamic relationship with all properties and the resolved type from the original relationship.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the relationship is null.</exception>
    public static DynamicRelationship ToDynamicRelationship<TRelationship>(this TRelationship relationship)
        where TRelationship : class, IRelationship
    {
        ArgumentNullException.ThrowIfNull(relationship);

        // Get all properties from the relationship using reflection
        var properties = ExtractProperties(relationship);

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
    /// Converts a strongly-typed node to a dynamic node, preserving all properties and stored labels.
    /// When the node has no stored labels, compatible labels are derived from its runtime type.
    /// This is a generic overload that works with any INode implementation.
    /// </summary>
    /// <param name="node">The strongly-typed node to convert.</param>
    /// <returns>A dynamic node with all properties and resolved labels from the original node.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the node is null.</exception>
    public static DynamicNode ToDynamic(this INode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        // Get all properties from the node using reflection
        var properties = ExtractProperties(node);

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
    /// Converts a strongly-typed relationship to a dynamic relationship, preserving all properties and its stored type.
    /// When the relationship has no stored type, the physical type is derived from its runtime type.
    /// This is a generic overload that works with any IRelationship implementation.
    /// </summary>
    /// <param name="relationship">The strongly-typed relationship to convert.</param>
    /// <returns>A dynamic relationship with all properties and the resolved type from the original relationship.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the relationship is null.</exception>
    public static DynamicRelationship ToDynamic(this IRelationship relationship)
    {
        ArgumentNullException.ThrowIfNull(relationship);

        // Get all properties from the relationship using reflection
        var properties = ExtractProperties(relationship);

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
    /// <returns>A dictionary of property names and values.</returns>
    private static Dictionary<string, object?> ExtractProperties(object entity)
    {
        var properties = new Dictionary<string, object?>();
        var type = entity.GetType();

        // Get all public properties
        var propertyInfos = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var propertyInfo in propertyInfos)
        {
            // Only public, readable, non-indexed model properties participate in conversion.
            if (IsBaseProperty(propertyInfo) ||
                propertyInfo.GetMethod is not { IsPublic: true } ||
                propertyInfo.GetIndexParameters().Length != 0)
            {
                continue;
            }

            var value = GetPropertyValue(propertyInfo, entity);
            properties[Labels.GetLabelFromProperty(propertyInfo)] = value;
        }

        return properties;
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
    /// <returns>True if the property is part of the base interfaces, false otherwise.</returns>
    private static bool IsBaseProperty(PropertyInfo propertyInfo)
    {
        var propertyName = propertyInfo.Name;

        // Check for base interface properties
        return propertyName == nameof(IEntity.Id) ||
               propertyName == nameof(INode.Labels) ||
               propertyName == nameof(IRelationship.Type) ||
               propertyName == nameof(IRelationship.Direction) ||
               propertyName == nameof(IRelationship.StartNodeId) ||
               propertyName == nameof(IRelationship.EndNodeId);
    }
}
