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

namespace Cvoya.Graph.Model;

using System.Collections;
using System.Reflection;


/// <summary>
/// Checks to enforce behavior expected by the graph model.
/// </summary>
public static class GraphDataModel
{
    /// <summary>
    /// The default maximum depth allowed for traversing complex properties of an entity.
    /// This is used to prevent infinite recursion when traversing complex properties that may reference other entities.
    /// The default value is set to 5, which means that the traversal will stop after 5 levels of depth.
    /// </summary>
    public const int DefaultDepthAllowed = 5;

    /// <summary>
    /// Converts a property name to a relationship type name.
    /// This is used to create a relationship type for properties of an entity.
    /// The relationship type name is prefixed and suffixed with special strings to avoid conflicts with other relationship types.
    /// The resulting relationship type name will be in the format: "__PROPERTY__{propertyName}__".
    /// </summary>
    /// <param name="propertyName"> The name of the property to convert</param>
    /// <returns>>The relationship type name for the property</returns>
    public static string PropertyNameToRelationshipTypeName(string propertyName) =>
        $"{PropertyRelationshipTypeNamePrefix}{propertyName}{PropertyRelationshipTypeNameSuffix}";

    /// <summary>
    /// Prefix and suffix used to create relationship type names for properties.
    /// </summary>
    public const string PropertyRelationshipTypeNamePrefix = "__PROPERTY__";

    /// <summary>
    /// Suffix used to create relationship type names for properties.
    /// </summary>
    public const string PropertyRelationshipTypeNameSuffix = "__";

    /// <summary>
    /// Converts a relationship type name back to a property name.
    /// This is used to extract the property name from a relationship type name that was created using <see cref="PropertyNameToRelationshipTypeName"/>.
    /// The relationship type name is expected to be in the format: "__PROPERTY__{propertyName}__".
    /// If the relationship type name does not match this format, it is returned unchanged.
    /// </summary>
    /// <param name="relationshipTypeName"> The relationship type name to convert</param>
    /// <returns>The property name extracted from the relationship type name</returns>
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
            .Where(p => IsSimple(p.PropertyType) || IsCollectionOfSimple(p.PropertyType));

    /// <summary>
    /// Gets the complex properties of a type.
    /// </summary>
    /// <param name="type">The type to examine</param>
    /// <returns>An enumerable of complex properties</returns>
    public static IEnumerable<PropertyInfo> GetComplexProperties(Type type) =>
        type.GetProperties()
            .Where(p => IsComplex(p.PropertyType));

    /// <summary>
    /// Gets the simple and complex properties of an object.
    /// </summary>
    /// <param name="obj">The object to examine</param>
    /// <returns>A tuple containing dictionaries of simple and complex properties</returns>
    public static (IDictionary<PropertyInfo, object?>, IDictionary<PropertyInfo, object?>) GetSimpleAndComplexProperties(object obj)
    {
        var type = obj.GetType();
        var simpleProperties = GetSimpleProperties(type).ToDictionary(p => p, p => p.GetValue(obj));
        var complexProperties = GetComplexProperties(type).ToDictionary(p => p, p => p.GetValue(obj));

        return (simpleProperties, complexProperties);
    }

    /// <summary>
    /// Ensures that an entity has no reference cycles.
    /// </summary>
    /// <param name="entity">The entity to check</param>
    /// <exception cref="GraphException">Thrown if a reference cycle is detected</exception>
    public static void EnsureNoReferenceCycle(this IEntity entity)
    {
        if (HasReferenceCycle(entity))
        {
            throw new GraphException($"Reference cycle detected in the entity with ID '{entity.Id}'");
        }
    }

    /// <summary>
    /// Enforces representation constraints for an <see cref="Model.INode"/>.
    /// </summary>
    /// <param name="node">The node to enforce constraints for</param>
    /// <typeparam name="T">The type of the node</typeparam>
    /// <exception cref="GraphException">Thrown if the given node violates any of the constraints</exception>
    public static void EnforceGraphConstraintsForNode<T>(T node)
        where T : INode
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
        where T : INode
    {
        // Enforce additional graph constraints as needed
        // TODO: Check the property types
    }

    /// <summary>
    /// Enforces representation constraints for an <see cref="Model.IEntity"/>.
    /// </summary>
    public static void EnforceGraphConstraintsForEntity<T>(T entity)
        where T : IEntity
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
        where T : IRelationship
    {
        EnforceGraphConstraintsForEntity(relationship);

        if (string.IsNullOrEmpty(relationship.StartNodeId) || string.IsNullOrEmpty(relationship.EndNodeId))
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
        where T : IRelationship
    {
        // Enforce additional graph constraints as needed
        // TODO: Check the property types
    }

    /// <summary>
    /// Checks if a type is considered to be a "simple" type in the context of the graph model.
    /// A "simple" type is one that can be used as the type of the property of an <see cref="INode"/> .
    /// The full list of simple types includes:
    /// <list type="bullet">
    /// <item>All .NET primitive types (e.g., <see cref="int"/>, <see cref="long"/>, <see cref="double"/>, etc.)</item>
    /// <item>All .NET enum types</item>
    /// <item><see cref="string"/></item>
    /// <item><see cref="Point"/></item>
    /// <item><see cref="DateTime"/></item>
    /// <item><see cref="DateTimeOffset"/></item>
    /// <item><see cref="TimeSpan"/></item>
    /// <item><see cref="TimeOnly"/></item>
    /// <item><see cref="DateOnly"/></item>
    /// <item><see cref="Guid"/></item>
    /// <item>Byte arrays</item>
    /// <item><see cref="Uri"/></item>
    /// </list>
    /// </summary>
    public static bool IsSimple(Type type)
    {
        // Handle nullable value types
        type = Nullable.GetUnderlyingType(type) ?? type;

        return type switch
        {
            _ when type.IsPrimitive => true,
            _ when type.IsEnum => true,
            _ when type == typeof(string) => true,
            _ when type == typeof(Point) => true,
            _ when type == typeof(DateTime) => true,
            _ when type == typeof(DateTimeOffset) => true,
            _ when type == typeof(TimeSpan) => true,
            _ when type == typeof(TimeOnly) => true,
            _ when type == typeof(DateOnly) => true,
            _ when type == typeof(decimal) => true,
            _ when type == typeof(Guid) => true,
            _ when type == typeof(byte[]) => true,
            _ when type == typeof(Uri) => true,
            _ => false
        };
    }

    /// <summary>
    /// Checks if a type is considered to be a "complex" type in the context of the graph model.
    /// A "complex" type is one that is not a simple type and can be used as the type of the property of an <see cref="INode"/>.
    /// Complex types are typically user-defined classes or structs that do not fall into the simple type categories.
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public static bool IsComplex(Type type) => IsComplex(type, DefaultDepthAllowed);

    /// <summary>
    /// Checks if a type is a collection of simple types. See <see cref="IsSimple(Type)"/> for what is considered a simple type.
    /// </summary>
    public static bool IsCollectionOfSimple(Type type) =>
        type != typeof(string)
        && typeof(IEnumerable).IsAssignableFrom(type) is true
        && typeof(IDictionary).IsAssignableFrom(type) is false // Exclude IDictionary as it is not a simple collection
        && type switch
        {
            { IsArray: true } => IsSimple(type.GetElementType()!),
            { IsGenericType: true } => type.GetGenericArguments().FirstOrDefault() is { } arg && IsSimple(arg),
            _ => false
        };

    /// <summary>
    /// Checks if a type is a collection of complex types. See <see cref="IsComplex(Type)"/> for what is considered a complex type.
    /// </summary>
    public static bool IsCollectionOfComplex(Type type) =>
        type != typeof(string)
        && typeof(IEnumerable).IsAssignableFrom(type) is true
        && typeof(IDictionary).IsAssignableFrom(type) is false // Exclude IDictionary as it is not a supported collection
        && type switch
        {
            { IsArray: true } => IsComplex(type.GetElementType()!),
            { IsGenericType: true } => type.GetGenericArguments().FirstOrDefault() is { } arg && IsComplex(arg),
            _ => false
        };

    /// <summary>
    /// Checks if an object has reference cycles (true cycles, not just shared references).
    /// </summary>
    /// <param name="obj">The object to check</param>
    /// <returns>True if a reference cycle is detected, false otherwise</returns>
    public static bool HasReferenceCycle(object obj)
    {
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var currentPath = new HashSet<object>(ReferenceEqualityComparer.Instance);

        return CheckForCycle(obj, visited, currentPath);
    }

    private static bool CheckForCycle(object obj, HashSet<object> visited, HashSet<object> currentPath)
    {
        // Early exit for null
        if (obj == null)
            return false;

        var type = obj.GetType();

        // Skip value types and common immutable types
        if (type.IsValueType || // This covers DateTime, int, bool, etc.
            type == typeof(string) ||
            type == typeof(Type) ||
            type.IsEnum ||
            type.IsPrimitive)
        {
            return false;
        }

        // Skip collections of simple types
        if (IsCollectionOfSimple(type))
        {
            return false;
        }

        // Skip system types that we know don't have cycles
        if (type.Namespace?.StartsWith("System") == true &&
            !type.Namespace.StartsWith("System.Collections"))
        {
            return false;
        }

        // Now do the actual cycle detection for reference types
        if (currentPath.Contains(obj))
            return true;

        if (visited.Contains(obj))
            return false;

        visited.Add(obj);
        currentPath.Add(obj);

        try
        {
            if (obj is IDictionary objDict)
            {
                // Handle IDictionary objects
                foreach (var value in objDict.Values)
                {
                    if (value != null && CheckForCycle(value, visited, currentPath))
                        return true;
                }
            }
            else if (obj is IEnumerable enumerable && !(obj is string))
            {
                // Handle IEnumerable objects
                foreach (var item in enumerable)
                {
                    if (item != null && CheckForCycle(item, visited, currentPath))
                        return true;
                }
            }
            else
            {
                // Check all properties recursively
                foreach (var prop in type.GetProperties())
                {
                    if (prop.CanRead && !prop.GetIndexParameters().Any())
                    {
                        var value = prop.GetValue(obj);
                        if (value != null && CheckForCycle(value, visited, currentPath))
                            return true;
                    }
                }
            }
            return false;
        }
        finally
        {
            // CRITICAL: Remove from current path when backtracking!
            currentPath.Remove(obj);
        }
    }

    // Reference equality comparer for HashSet
    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static ReferenceEqualityComparer Instance { get; } = new ReferenceEqualityComparer();
        bool IEqualityComparer<object>.Equals(object? x, object? y) => ReferenceEquals(x, y);
        public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }

    private static bool IsComplex(Type type, int depth)
    {
        if (depth <= 0)
            return false;

        if (IsSimple(type) || IsCollectionOfSimple(type))
            return false;

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            return IsComplex(type.GetGenericArguments()[0], depth - 1);

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (IsSimple(prop.PropertyType) || IsCollectionOfSimple(prop.PropertyType))
            {
                continue;
            }

            if (IsComplex(prop.PropertyType, depth - 1))
            {
                return true;
            }
        }

        return false;
    }
}