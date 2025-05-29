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

using System.Collections;
using System.Reflection;
using Cvoya.Graph.Model;

namespace Cvoya.Graph.Provider.Neo4j;

/// <summary>
/// Helper methods for Neo4j operations.
/// </summary>
internal static class Helpers
{
    /// <summary>
    /// Ensures that an entity has no reference cycles.
    /// </summary>
    /// <param name="entity">The entity to check</param>
    /// <exception cref="GraphException">Thrown if a reference cycle is detected</exception>
    public static void EnsureNoReferenceCycle(this IEntity entity)
    {
        if (HasReferenceCycle(entity, []))
        {
            throw new GraphException($"Reference cycle detected in the entity with ID '{entity.Id}'");
        }
    }

    private static readonly IEqualityComparer<object> ReferenceComparer = ReferenceEqualityComparer.Instance;

    // Reference equality comparer for HashSet
    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static ReferenceEqualityComparer Instance { get; } = new ReferenceEqualityComparer();
        bool IEqualityComparer<object>.Equals(object? x, object? y) => ReferenceEquals(x, y);
        public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }

    /// <summary>
    /// Enforces representation constraints for an <see cref="Model.INode"/>.
    /// </summary>
    /// <param name="node">The node to enforce constraints for</param>
    /// <typeparam name="T">The type of the node</typeparam>
    /// <exception cref="GraphException">Thrown if the given node violates any of the constraints</exception>
    public static void EnforceGraphConstraintsForNode<T>(T node)
        where T : class, Model.INode
    {
        EnforceGraphConstraintsForEntity(node);

        EnforceGraphConstraintsForNodeType<T>();
        // Enforce additional graph constraints as needed
        // TODO: Check the property types

        // TODO: in case of an IEnumerable, it MUST be generic.
        // Otherwise, we cannot determine the type of the elements in the collection
        // when deserializing the node.

        // TODO: Prevent the use of IDictionary.
    }

    /// <summary>
    /// Enforces representation constraints for an <see cref="Model.INode"/>.
    /// </summary>
    /// <typeparam name="T">The type of the node</typeparam>
    /// <exception cref="GraphException">Thrown if the given node violates any of the constraints</exception>
    public static void EnforceGraphConstraintsForNodeType<T>()
        where T : class, Model.INode
    {
        // Enforce additional graph constraints as needed
        // TODO: Check the property types
    }

    /// <summary>
    /// Enforces representation constraints for an <see cref="Model.IEntity"/>.
    /// </summary>
    public static void EnforceGraphConstraintsForEntity<T>(T entity)
        where T : class, IEntity
    {
        // Ensure the entity is not null
        ArgumentNullException.ThrowIfNull(entity, nameof(entity));

        if (string.IsNullOrEmpty(entity.Id))
        {
            var ex = new ArgumentException("Entity ID cannot be null or empty");
            throw new GraphException(ex.Message, ex);
        }

        // Ensure the entity has no reference cycles
        entity.EnsureNoReferenceCycle();
    }

    /// <summary>
    /// Enforces representation constraints for an <see cref="Model.IRelationship"/>.
    /// </summary>
    /// <typeparam name="T">The type of the relationship</typeparam>
    /// <param name="relationship">The relationship to enforce constraints for</param>
    /// <exception cref="GraphException">Thrown if the given relationship violates any of the constraints</exception>
    public static void EnforceGraphConstraintsForRelationship<T>(T relationship)
        where T : class, Model.IRelationship
    {
        EnforceGraphConstraintsForEntity(relationship);

        if (string.IsNullOrEmpty(relationship.SourceId) || string.IsNullOrEmpty(relationship.TargetId))
        {
            var ex = new ArgumentException("Relationship source and target IDs cannot be null or empty");
            throw new GraphException(ex.Message, ex);
        }

        EnforceGraphConstraintsForRelationshipType<T>();
    }

    /// <summary>
    /// Enforces representation constraints for an <see cref="Model.IRelationship"/>.
    /// </summary>
    /// <typeparam name="T">The type of the relationship</typeparam>
    /// <exception cref="GraphException">Thrown if the given relationship violates any of the constraints</exception>
    public static void EnforceGraphConstraintsForRelationshipType<T>()
        where T : class, Model.IRelationship
    {
        // Enforce additional graph constraints as needed
        // TODO: Check the property types
    }

    public static string PropertyNameToRelationshipTypeName(string propertyName) =>
        $"{PropertyRelationshipTypeNamePrefix}{propertyName}{PropertyRelationshipTypeNameSuffix}";

    public const string PropertyRelationshipTypeNamePrefix = "__PROPERTY__";
    public const string PropertyRelationshipTypeNameSuffix = "__";

    public static string RelationshipTypeNameToPropertyName(string relationshipTypeName) =>
        relationshipTypeName.StartsWith(PropertyRelationshipTypeNamePrefix) && relationshipTypeName.EndsWith(PropertyRelationshipTypeNameSuffix)
            ? relationshipTypeName[12..^2]
            : relationshipTypeName;

    /// <summary>
    /// Gets the simple properties of a type.
    /// </summary>
    /// <param name="type">The type to examine</param>
    /// <returns>>An enumerable of simple properties</returns>
    public static IEnumerable<PropertyInfo> GetSimpleProperties(Type type) =>
        type.GetProperties()
            .Where(p => IsPrimitiveOrSimple(p.PropertyType) || IsCollectionOfSimple(p.PropertyType));

    /// <summary>
    /// Gets the complex properties of a type.
    /// </summary>
    /// <param name="type">The type to examine</param>
    /// <returns>An enumerable of complex properties</returns>
    public static IEnumerable<PropertyInfo> GetComplexProperties(Type type) =>
        type.GetProperties()
            .Where(p => !IsPrimitiveOrSimple(p.PropertyType) && !IsCollectionOfSimple(p.PropertyType) && !IsRelationshipProperty(p));

    /// <summary>
    /// Gets the simple and complex properties of an object.
    /// </summary>
    /// <param name="obj">The object to examine</param>
    /// <returns>A tuple containing dictionaries of simple and complex properties</returns>
    public static (Dictionary<PropertyInfo, object?>, Dictionary<PropertyInfo, object?>) GetSimpleAndComplexProperties(object obj)
    {
        var simpleProperties = new Dictionary<PropertyInfo, object?>();
        var complexProperties = new Dictionary<PropertyInfo, object?>();

        var properties = obj.GetType().GetProperties();

        foreach (var property in properties)
        {
            if (IsPrimitiveOrSimple(property.PropertyType) || IsCollectionOfSimple(property.PropertyType))
            {
                simpleProperties[property] = property.GetValue(obj);
            }
            else
            {
                complexProperties[property] = property.GetValue(obj);
            }
        }

        return (simpleProperties, complexProperties);
    }

    /// <summary>
    /// Checks if a type is a collection of relationship types.
    /// </summary>
    public static bool IsCollectionOfRelationshipType(Type type) =>
        type != typeof(string)
        && typeof(IEnumerable).IsAssignableFrom(type)
        && type switch
        {
            { IsArray: true } => typeof(Model.IRelationship).IsAssignableFrom(type.GetElementType()!),
            { IsGenericType: true } => type.GetGenericArguments().FirstOrDefault() is { } arg && typeof(Model.IRelationship).IsAssignableFrom(arg),
            _ => false
        };

    /// <summary>
    /// Checks if a type is a primitive or simple type.
    /// </summary>
    public static bool IsPrimitiveOrSimple(Type type) => type switch
    {
        _ when type.IsPrimitive => true,
        _ when type.IsEnum => true,
        _ when type == typeof(string) => true,
        _ when type.IsValueType => true,
        _ when type == typeof(decimal) => true,
        _ when type == typeof(Model.Point) => true,
        _ when type == typeof(DateTime) => true,
        _ when type == typeof(DateTimeOffset) => true,
        _ when type == typeof(TimeSpan) => true,
        _ when type == typeof(Guid) => true,
        _ when type == typeof(TimeOnly) => true,
        _ when type == typeof(DateOnly) => true,
        _ when type == typeof(decimal) => true,
        _ => false
    };

    /// <summary>
    /// Checks if a type is a collection of simple types.
    /// </summary>
    public static bool IsCollectionOfSimple(Type type) =>
        type != typeof(string)
        && typeof(IEnumerable).IsAssignableFrom(type)
        && type switch
        {
            { IsArray: true } => type.GetElementType()!.IsPrimitiveOrSimple(),
            { IsGenericType: true } => type.GetGenericArguments().FirstOrDefault() is { } arg && arg.IsPrimitiveOrSimple(),
            _ => false
        };

    /// <summary>
    /// Checks if an object has reference cycles (true cycles, not just shared references).
    /// </summary>
    /// <param name="obj">The object to check</param>
    /// <param name="path">Set of objects in the current traversal path</param>
    /// <returns>True if a reference cycle is detected, false otherwise</returns>
    public static bool HasReferenceCycle(object obj, HashSet<object>? path = null)
    {
        path ??= new HashSet<object>(ReferenceComparer);

        if (obj == null || obj is string || obj.GetType().IsValueType)
            return false;

        if (!path.Add(obj))
            return true;

        var type = obj.GetType();
        foreach (var prop in type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            if (prop.GetIndexParameters().Length > 0) continue; // Skip indexers

            var value = prop.GetValue(obj);
            if (value == null) continue;

            if (value is IEnumerable enumerable && !(value is string))
            {
                foreach (var item in enumerable)
                {
                    if (item != null && HasReferenceCycle(item, path))
                    {
                        path.Remove(obj);
                        return true;
                    }
                }
            }
            else if (!prop.PropertyType.IsValueType && prop.PropertyType != typeof(string))
            {
                if (HasReferenceCycle(value, path))
                {
                    path.Remove(obj);
                    return true;
                }
            }
        }

        path.Remove(obj);
        return false;
    }

    /// <summary>
    /// Checks if a type is a relationship type.
    /// </summary>
    public static bool IsRelationshipType(this Type type) =>
        typeof(Model.IRelationship).IsAssignableFrom(type);

    public static bool IsRelationshipProperty(PropertyInfo property)
    {
        var type = property.PropertyType;

        if (typeof(Cvoya.Graph.Model.IRelationship).IsAssignableFrom(type))
            return true;

        if (type.IsGenericType &&
            typeof(IEnumerable<object>).IsAssignableFrom(type) &&
            !type.IsAssignableTo(typeof(string)))
        {
            var elementType = type.GetGenericArguments().FirstOrDefault();
            return elementType != null && typeof(Cvoya.Graph.Model.IRelationship).IsAssignableFrom(elementType);
        }

        return false;
    }
}