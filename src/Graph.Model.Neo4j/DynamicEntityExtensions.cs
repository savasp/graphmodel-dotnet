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

namespace Cvoya.Graph.Model.Neo4j;

using Cvoya.Graph.Model;
using Cvoya.Graph.Model.Neo4j.Serialization;

/// <summary>
/// Extension methods for dynamic entities.
/// </summary>
public static class DynamicEntityExtensions
{
    /// <summary>
    /// Gets a property value from a dynamic node with type safety.
    /// </summary>
    /// <typeparam name="T">The expected type of the property value.</typeparam>
    /// <param name="node">The dynamic node.</param>
    /// <param name="propertyName">The name of the property.</param>
    /// <returns>The property value if found and of the correct type, otherwise default(T).</returns>
    public static T? GetProperty<T>(this DynamicNode node, string propertyName)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        if (!node.Properties.TryGetValue(propertyName, out var value) || value is null)
        {
            return default;
        }

        if (value is T typedValue)
        {
            return typedValue;
        }

        // Special handling for collections
        if (typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(IList<>) && value is IList<object> objectList)
        {
            var elementType = typeof(T).GetGenericArguments()[0];
            var resultList = new List<object?>();

            foreach (var item in objectList)
            {
                if (item == null)
                {
                    resultList.Add(null);
                }
                else
                {
                    try
                    {
                        // Use SerializationBridge for proper conversion from Neo4j types
                        var converted = SerializationBridge.FromNeo4jValue(item, elementType);
                        resultList.Add(converted);
                    }
                    catch
                    {
                        // If conversion fails, try direct cast
                        resultList.Add(item);
                    }
                }
            }

            // Create the appropriate collection type
            if (typeof(T) == typeof(IList<string>))
            {
                return (T)(object)resultList.Where(x => x != null).Cast<string>().ToList();
            }
            else if (typeof(T) == typeof(IList<int>))
            {
                return (T)(object)resultList.Where(x => x != null).Cast<int>().ToList();
            }
            else if (typeof(T) == typeof(IList<double>))
            {
                return (T)(object)resultList.Where(x => x != null).Cast<double>().ToList();
            }
            else if (typeof(T) == typeof(IList<bool>))
            {
                return (T)(object)resultList.Where(x => x != null).Cast<bool>().ToList();
            }
            else
            {
                // Generic fallback
                var genericListType = typeof(List<>).MakeGenericType(elementType);
                var genericList = Activator.CreateInstance(genericListType);
                var addMethod = genericListType.GetMethod("Add");

                foreach (var item in resultList)
                {
                    if (item != null)
                    {
                        addMethod?.Invoke(genericList, new[] { item });
                    }
                }

                return (T?)genericList!;
            }
        }

        // For value types, always use conversion logic
        if (typeof(T).IsValueType)
        {
            var underlyingType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
            try
            {
                if (underlyingType == typeof(bool))
                {
                    if (value is bool b)
                    {
                        var result = (T)(object)b;
                        return result;
                    }
                    if (value is string s)
                    {
                        var result = (T)(object)bool.Parse(s);
                        return result;
                    }
                    if (value is long l)
                    {
                        var result = (T)(object)(l != 0);
                        return result;
                    }
                    if (value is int i)
                    {
                        var result = (T)(object)(i != 0);
                        return result;
                    }
                    if (value is double d)
                    {
                        var result = (T)(object)(Math.Abs(d) > 0.00001);
                        return result;
                    }
                }
                // Use SerializationBridge for proper conversion from Neo4j types
                var converted = SerializationBridge.FromNeo4jValue(value, underlyingType);
                return (T?)converted;
            }
            catch
            {
                try
                {
                    var direct = (T)(object)value;
                    return direct;
                }
                catch
                {
                    return default;
                }
            }
        }

        // For reference types, use direct cast if possible
        if (value is T typedValue2)
        {
            return typedValue2;
        }

        try
        {
            var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
            if (targetType == typeof(bool))
            {
                if (value is bool b) return (T)(object)b;
                if (value is string s) return (T)(object)bool.Parse(s);
                if (value is long l) return (T)(object)(l != 0);
                if (value is int i) return (T)(object)(i != 0);
                if (value is double d) return (T)(object)(Math.Abs(d) > 0.00001);
            }
            // Use SerializationBridge for proper conversion from Neo4j types
            var convertedValue = SerializationBridge.FromNeo4jValue(value, targetType);
            return (T?)convertedValue;
        }
        catch
        {
            return default;
        }
    }

    /// <summary>
    /// Gets a property value from a dynamic relationship with type safety.
    /// </summary>
    /// <typeparam name="T">The expected type of the property value.</typeparam>
    /// <param name="relationship">The dynamic relationship.</param>
    /// <param name="propertyName">The name of the property.</param>
    /// <returns>The property value if found and of the correct type, otherwise default(T).</returns>
    public static T? GetProperty<T>(this DynamicRelationship relationship, string propertyName)
    {
        ArgumentNullException.ThrowIfNull(relationship);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        if (!relationship.Properties.TryGetValue(propertyName, out var value) || value is null)
            return default;

        if (value is T typedValue)
            return typedValue;

        try
        {
            var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
            if (targetType == typeof(bool))
            {
                if (value is bool b) return (T)(object)b;
                if (value is string s) return (T)(object)bool.Parse(s);
                if (value is long l) return (T)(object)(l != 0);
                if (value is int i) return (T)(object)(i != 0);
                if (value is double d) return (T)(object)(Math.Abs(d) > 0.00001);
            }
            return (T)Convert.ChangeType(value, targetType);
        }
        catch
        {
            return default;
        }
    }

    /// <summary>
    /// Checks if a dynamic node has a specific label.
    /// </summary>
    /// <param name="node">The dynamic node.</param>
    /// <param name="label">The label to check for.</param>
    /// <returns>True if the node has the specified label, otherwise false.</returns>
    public static bool HasLabel(this DynamicNode node, string label)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        return node.Labels.Contains(label);
    }

    /// <summary>
    /// Checks if a dynamic relationship has a specific type.
    /// </summary>
    /// <param name="relationship">The dynamic relationship.</param>
    /// <param name="type">The type to check for.</param>
    /// <returns>True if the relationship has the specified type, otherwise false.</returns>
    public static bool HasType(this DynamicRelationship relationship, string type)
    {
        ArgumentNullException.ThrowIfNull(relationship);
        ArgumentException.ThrowIfNullOrWhiteSpace(type);

        return relationship.Type == type;
    }

    /// <summary>
    /// Checks if a dynamic node has any of the specified labels.
    /// </summary>
    /// <param name="node">The dynamic node.</param>
    /// <param name="labels">The labels to check for.</param>
    /// <returns>True if the node has any of the specified labels, otherwise false.</returns>
    public static bool HasAnyLabel(this DynamicNode node, params string[] labels)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(labels);

        return node.Labels.Any(label => labels.Contains(label));
    }

    /// <summary>
    /// Checks if a dynamic node has all of the specified labels.
    /// </summary>
    /// <param name="node">The dynamic node.</param>
    /// <param name="labels">The labels to check for.</param>
    /// <returns>True if the node has all of the specified labels, otherwise false.</returns>
    public static bool HasAllLabels(this DynamicNode node, params string[] labels)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(labels);

        return labels.All(label => node.Labels.Contains(label));
    }

    /// <summary>
    /// Gets all property names from a dynamic node.
    /// </summary>
    /// <param name="node">The dynamic node.</param>
    /// <returns>An enumerable of property names.</returns>
    public static IEnumerable<string> GetPropertyNames(this DynamicNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return node.Properties.Keys;
    }

    /// <summary>
    /// Gets all property names from a dynamic relationship.
    /// </summary>
    /// <param name="relationship">The dynamic relationship.</param>
    /// <returns>An enumerable of property names.</returns>
    public static IEnumerable<string> GetPropertyNames(this DynamicRelationship relationship)
    {
        ArgumentNullException.ThrowIfNull(relationship);
        return relationship.Properties.Keys;
    }

    /// <summary>
    /// Checks if a dynamic node has a specific property.
    /// </summary>
    /// <param name="node">The dynamic node.</param>
    /// <param name="propertyName">The name of the property to check for.</param>
    /// <returns>True if the node has the specified property, otherwise false.</returns>
    public static bool HasProperty(this DynamicNode node, string propertyName)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        return node.Properties.ContainsKey(propertyName);
    }

    /// <summary>
    /// Checks if a dynamic relationship has a specific property.
    /// </summary>
    /// <param name="relationship">The dynamic relationship.</param>
    /// <param name="propertyName">The name of the property to check for.</param>
    /// <returns>True if the relationship has the specified property, otherwise false.</returns>
    public static bool HasProperty(this DynamicRelationship relationship, string propertyName)
    {
        ArgumentNullException.ThrowIfNull(relationship);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        return relationship.Properties.ContainsKey(propertyName);
    }
}