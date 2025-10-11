// Copyright 2025 Savas Parastatidis
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      https://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Reflection;

namespace Cvoya.Graph.Model;

/// <summary>
/// Extension methods for converting strongly-typed entities to dynamic entities.
/// </summary>
public static class DynamicEntityExtensions
{
    /// <summary>
    /// Converts a strongly-typed node to a dynamic node, preserving all properties and labels.
    /// </summary>
    /// <typeparam name="TNode">The type of the strongly-typed node.</typeparam>
    /// <param name="node">The strongly-typed node to convert.</param>
    /// <returns>A dynamic node with all properties and labels from the original node.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the node is null.</exception>
    public static DynamicNode ToDynamicNode<TNode>(this TNode node)
        where TNode : INode
    {
        ArgumentNullException.ThrowIfNull(node);

        // Get all properties from the node using reflection
        var properties = ExtractProperties(node);

        // Create dynamic node with labels and properties
        return new DynamicNode(
            labels: node.Labels,
            properties: properties
        )
        {
            Id = node.Id
        };
    }

    /// <summary>
    /// Converts a strongly-typed relationship to a dynamic relationship, preserving all properties and type.
    /// </summary>
    /// <typeparam name="TRelationship">The type of the strongly-typed relationship.</typeparam>
    /// <param name="relationship">The strongly-typed relationship to convert.</param>
    /// <returns>A dynamic relationship with all properties and type from the original relationship.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the relationship is null.</exception>
    public static DynamicRelationship ToDynamicRelationship<TRelationship>(this TRelationship relationship)
        where TRelationship : IRelationship
    {
        ArgumentNullException.ThrowIfNull(relationship);

        // Get all properties from the relationship using reflection
        var properties = ExtractProperties(relationship);

        // Create dynamic relationship with type and properties
        return new DynamicRelationship(
            startNodeId: relationship.StartNodeId,
            endNodeId: relationship.EndNodeId,
            type: relationship.Type,
            properties: properties,
            direction: relationship.Direction
        )
        {
            Id = relationship.Id
        };
    }

    /// <summary>
    /// Converts a strongly-typed node to a dynamic node, preserving all properties and labels.
    /// This is a generic overload that works with any INode implementation.
    /// </summary>
    /// <param name="node">The strongly-typed node to convert.</param>
    /// <returns>A dynamic node with all properties and labels from the original node.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the node is null.</exception>
    public static DynamicNode ToDynamic(this INode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        // Get all properties from the node using reflection
        var properties = ExtractProperties(node);

        // Create dynamic node with labels and properties
        return new DynamicNode(
            labels: node.Labels,
            properties: properties
        )
        {
            Id = node.Id
        };
    }

    /// <summary>
    /// Converts a strongly-typed relationship to a dynamic relationship, preserving all properties and type.
    /// This is a generic overload that works with any IRelationship implementation.
    /// </summary>
    /// <param name="relationship">The strongly-typed relationship to convert.</param>
    /// <returns>A dynamic relationship with all properties and type from the original relationship.</returns>
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
            type: relationship.Type,
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
            // Skip properties that are part of the base interfaces
            if (IsBaseProperty(propertyInfo))
                continue;

            try
            {
                var value = propertyInfo.GetValue(entity);
                properties[propertyInfo.Name] = value;
            }
            catch
            {
                // Skip properties that can't be read
                continue;
            }
        }

        return properties;
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
