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
using Cvoya.Graph.Model;

namespace Cvoya.Graph.Provider.Neo4j.Schema;

/// <summary>
/// Manages type-related operations for Neo4j entities.
/// </summary>
internal static class Neo4jTypeManager
{
    /// <summary>
    /// Gets the Neo4j label for a .NET type.
    /// </summary>
    /// <param name="type">The .NET type</param>
    /// <returns>The Neo4j label to use</returns>
    /// <exception cref="GraphException">Thrown when the type doesn't have a valid name</exception>
    public static string GetLabel(Type type)
    {
        // Check for custom label from attribute
        var nodeAttr = type.GetCustomAttribute<NodeAttribute>(inherit: false);
        if (nodeAttr?.Label is { Length: > 0 }) return nodeAttr.Label;

        var relAttr = type.GetCustomAttribute<RelationshipAttribute>(inherit: false);
        if (relAttr?.Label is { Length: > 0 }) return relAttr.Label;

        var propertyAttr = type.GetCustomAttribute<PropertyAttribute>(inherit: false);
        if (propertyAttr?.Label is { Length: > 0 }) return propertyAttr.Label;

        // Fall back to the type name with backticks removed
        var label = type.Name.Replace("`", "");
        return label ?? throw new GraphException($"Type '{type}' does not have a valid name.");
    }

    /// <summary>
    /// Finds the .NET type for a given Neo4j label that is assignable to the specified base type.
    /// </summary>
    /// <param name="label">The Neo4j label</param>
    /// <param name="baseType">The base type the result must be assignable to</param>
    /// <returns>The matching .NET type</returns>
    /// <exception cref="GraphException">Thrown when no matching type is found</exception>
    public static Type GetTypeForLabel(string label, Type baseType)
    {
        var match = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(baseType.IsAssignableFrom)
            .FirstOrDefault(t => GetLabel(t) == label);

        if (match is null)
            throw new GraphException($"No .NET type found for label '{label}' assignable to {baseType.FullName}");

        return match;
    }
}