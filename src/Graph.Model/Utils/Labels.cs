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
/// Manages type-related operations for Neo4j entities.
/// </summary>
public static class Labels
{
    private static readonly Dictionary<Type, string> LabelCache = [];
    private static readonly Dictionary<PropertyInfo, string> PropertyCache = [];
    private static readonly Dictionary<string, Type> TypeCache = [];
    private static readonly Dictionary<string, PropertyInfo> PropertyTypeCache = [];
    private static readonly object CacheLock = new();

    /// <summary>
    /// Gets the label associated with an object. It returns the label of the object's actual type,
    /// not the type of the variable used to hold the object when this method is called.
    /// </summary>
    /// <param name="obj">The object</param>
    /// <returns>The label</returns>
    /// <exception cref="GraphException">Thrown when the type doesn't have a valid name</exception>
    public static string GetLabelFromObject(object obj)
    {
        ArgumentNullException.ThrowIfNull(obj);

        var type = obj.GetType();
        return GetLabelFromType(type);
    }

    /// <summary>
    /// Gets the label associated with a .NET type.
    /// </summary>
    /// <param name="type">The .NET type</param>
    /// <returns>The label</returns>
    /// <exception cref="GraphException">Thrown when the type doesn't have a valid name</exception>
    public static string GetLabelFromType(Type type)
    {
        var label = GetFromCache(type);

        if (label is not null)
        {
            return label;
        }

        // Check for custom label from Node attribute
        var nodeAttr = type.GetCustomAttribute<NodeAttribute>(inherit: false);
        if (nodeAttr?.Label is { Length: > 0 })
        {
            label = nodeAttr.Label;
        }

        // Check for custom label from Relationship attribute
        var relAttr = type.GetCustomAttribute<RelationshipAttribute>(inherit: false);
        if (relAttr?.Label is { Length: > 0 })
        {
            label = relAttr.Label;
        }

        // Fall back to the type name with backticks removed
        label ??= type.Name.Replace("`", "") ?? throw new GraphException($"Type '{type}' does not have a valid name.");

        PutInCache(type, label);
        return label;
    }

    /// <summary>
    /// Gets the label associated with a property.
    /// </summary>
    /// <param name="propertyInfo">The .NET property</param>
    /// <returns>The label</returns>
    /// <exception cref="GraphException">Thrown when the property doesn't have a valid name</exception>
    public static string GetLabelFromProperty(PropertyInfo propertyInfo)
    {
        var label = GetFromCache(propertyInfo);

        if (label is not null)
        {
            return label;
        }

        var propertyAttr = propertyInfo.GetCustomAttribute<PropertyAttribute>(inherit: false);
        if (propertyAttr?.Label is { Length: > 0 })
        {
            label = propertyAttr.Label;
        }

        // Fall back to the property name with backticks removed
        label ??= propertyInfo.Name.Replace("`", "") ?? throw new GraphException($"Property '{propertyInfo}' does not have a valid name.");

        PutInCache(propertyInfo, label);
        return label;
    }

    /// <summary>
    /// Gets all labels for types that are assignable to the specified base type.
    /// This includes the base type itself and all derived types.
    /// The types cannot be interfaces or abstract classes.
    /// </summary>
    /// <param name="baseType">The base type to find assignable types for</param>
    /// <returns>A collection of labels for all types assignable to the base type.
    /// There is no predefined order in the collection of labels.</returns>
    public static IEnumerable<string> GetLabelsForAssignableTypes(Type baseType)
    {
        // Find all types that are assignable to the base type
        var assignableTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a =>
            {
                try { return a.GetTypes(); }
                catch { return []; }
            })
            .Where(t => baseType.IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);

        return assignableTypes.Select(GetLabelFromType).Distinct();
    }

    /// <summary>
    /// Finds the .NET type for a given label.
    /// </summary>
    /// <param name="label">The label</param>
    /// <returns>The .NET associated with that label.</returns>
    /// <exception cref="GraphException">If no .NET type was found for the given label.</exception>
    public static Type GetTypeFromLabel(string label)
    {
        ArgumentNullException.ThrowIfNull(label);

        var type = GetFromCacheLabelToType(label);
        if (type is not null)
        {
            return type;
        }

        // Check for custom label from Node attribute
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                foreach (var t in assembly.GetTypes())
                {
                    var nodeAttr = t.GetCustomAttribute<NodeAttribute>(inherit: false);
                    if (nodeAttr?.Label == label)
                    {
                        PutInCache(label, t);
                        return t;
                    }
                }
            }
            catch
            {
                // Ignore types that cannot be loaded
            }
        }

        throw new GraphException($"No type found for label '{label}'.");
    }

    /// <summary>
    /// Finds the .NET property for a given label.
    /// </summary>
    /// <param name="label">The label</param>
    /// <returns>The .NET property associated with that label.</returns>
    /// <exception cref="GraphException">If no .NET property was found for the given label.</exception>
    public static PropertyInfo GetPropertyFromLabel(string label)
    {
        ArgumentNullException.ThrowIfNull(label);

        var propertyInfo = GetFromCacheLabelToProperty(label);
        if (propertyInfo is not null)
        {
            return propertyInfo;
        }

        // Check for custom label from Property attribute
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                foreach (var t in assembly.GetTypes())
                {
                    foreach (var prop in t.GetProperties())
                    {
                        var propertyAttr = prop.GetCustomAttribute<PropertyAttribute>(inherit: false);
                        if (propertyAttr?.Label == label)
                        {
                            PutInCache(label, prop);
                            return prop;
                        }
                    }
                }
            }
            catch
            {
                // Ignore types that cannot be loaded
            }
        }

        throw new GraphException($"No property found for label '{label}'.");
    }

    private static void PutInCache(Type type, string label)
    {
        lock (CacheLock)
        {
            LabelCache[type] = label;
        }
    }

    private static void PutInCache(PropertyInfo propertyInfo, string label)
    {
        lock (CacheLock)
        {
            PropertyCache[propertyInfo] = label;
        }
    }

    private static void PutInCache(string label, Type type)
    {
        lock (CacheLock)
        {
            TypeCache[label] = type;
        }
    }

    private static void PutInCache(string label, PropertyInfo propertyInfo)
    {
        lock (CacheLock)
        {
            PropertyTypeCache[label] = propertyInfo;
        }
    }

    private static string? GetFromCache(Type type)
    {
        lock (CacheLock)
        {
            return LabelCache.TryGetValue(type, out var label) ? label : null;
        }
    }

    private static string? GetFromCache(PropertyInfo propertyInfo)
    {
        lock (CacheLock)
        {
            return PropertyCache.TryGetValue(propertyInfo, out var label) ? label : null;
        }
    }

    private static Type? GetFromCacheLabelToType(string label)
    {
        lock (CacheLock)
        {
            return TypeCache.TryGetValue(label, out var type) ? type : null;
        }
    }

    private static PropertyInfo? GetFromCacheLabelToProperty(string label)
    {
        lock (CacheLock)
        {
            return PropertyTypeCache.TryGetValue(label, out var property) ? property : null;
        }
    }
}