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
    /// Checks if an object has reference cycles.
    /// </summary>
    /// <param name="obj">The object to check</param>
    /// <param name="visited">Set of visited objects</param>
    /// <returns>True if a reference cycle is detected, false otherwise</returns>
    public static bool HasReferenceCycle(object obj, HashSet<object>? visited = null)
    {
        visited ??= new HashSet<object>(ReferenceComparer);

        if (obj == null || obj is string || obj.GetType().IsValueType)
            return false;

        if (!visited.Add(obj))
            return true;

        var type = obj.GetType();
        foreach (var prop in type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            if (prop.GetIndexParameters().Length > 0) continue; // Skip indexers

            var propType = prop.PropertyType;
            if (IsRelationshipType(propType) || IsCollectionOfRelationshipType(propType))
            {
                // Ignore navigation properties of type IRelationship or collections of IRelationship
                continue;
            }

            if (propType.IsAssignableTo(typeof(Model.INode)))
            {
                // Ignore properties of type INode
                continue;
            }

            var value = prop.GetValue(obj);
            if (value == null) continue;

            if (value is IEnumerable enumerable && !(value is string))
            {
                foreach (var item in enumerable)
                {
                    if (item != null && HasReferenceCycle(item, visited))
                        return true;
                }
            }
            else if (!prop.PropertyType.IsValueType && prop.PropertyType != typeof(string))
            {
                if (HasReferenceCycle(value, visited))
                    return true;
            }
        }
        // Do not remove from visited here!
        return false;
    }

    /// <summary>
    /// Checks if a type is a relationship type.
    /// </summary>
    public static bool IsRelationshipType(this Type type) =>
        typeof(Model.IRelationship).IsAssignableFrom(type);

    /// <summary>
    /// Checks if a type is a collection of relationship types.
    /// </summary>
    public static bool IsCollectionOfRelationshipType(this Type type) =>
        type != typeof(string)
        && typeof(IEnumerable).IsAssignableFrom(type)
        && type switch
        {
            { IsArray: true } => type.GetElementType()!.IsRelationshipType(),
            { IsGenericType: true } => type.GetGenericArguments().FirstOrDefault() is { } arg && arg.IsRelationshipType(),
            _ => false
        };
}