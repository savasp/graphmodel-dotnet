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

using Cvoya.Graph.Model;

namespace Cvoya.Graph.Provider.Neo4j.Entities;

/// <summary>
/// Extension methods for Neo4j graph operations.
/// </summary>
internal static class Neo4jGraphExtensions
{
    /// <summary>
    /// Ensures that an entity has no reference cycles.
    /// </summary>
    /// <param name="entity">The entity to check</param>
    /// <exception cref="GraphException">Thrown if a reference cycle is detected</exception>
    public static void EnsureNoReferenceCycle(this IEntity entity)
    {
        if (Helpers.HasReferenceCycle(entity, []))
        {
            throw new GraphException($"Reference cycle detected in the entity with ID '{entity.Id}'");
        }
    }
    
    /// <summary>
    /// Gets the generic interface type from a relationship.
    /// </summary>
    /// <param name="relationType">The relationship type</param>
    /// <returns>The generic interface type, or null if not found</returns>
    public static Type? GetGenericRelationshipInterface(this Type relationType)
    {
        return relationType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType &&
                              i.GetGenericTypeDefinition() == typeof(Model.IRelationship<,>));
    }
}