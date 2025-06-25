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

using System.Collections.Concurrent;
using System.Reflection;

namespace Cvoya.Graph.Model;

/// <summary>
/// Manages type-related operations for Neo4j entities.
/// </summary>
public static class Labels
{
    private static readonly ConcurrentDictionary<string, Type> LabelToTypeCache = new();
    private static readonly ConcurrentDictionary<Type, string> TypeToLabelCache = new();
    private static readonly ConcurrentDictionary<PropertyInfo, string> PropertyToLabelCache = new();
    private static readonly ConcurrentDictionary<(Type, string), PropertyInfo> LabelToPropertyCache = new();
    private static readonly ConcurrentDictionary<(Type targetType, string label), Type?> MostDerivedTypeCache = new();

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
        TypeToLabelCache.TryGetValue(type, out var label);

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

        TypeToLabelCache[type] = label;
        LabelToTypeCache[label] = type;
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
        ArgumentNullException.ThrowIfNull(propertyInfo.DeclaringType);

        PropertyToLabelCache.TryGetValue(propertyInfo, out var label);

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

        PropertyToLabelCache[propertyInfo] = label;
        LabelToPropertyCache[(propertyInfo.DeclaringType, label)] = propertyInfo;
        return label;
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

        LabelToTypeCache.TryGetValue(label, out var type);

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
                        LabelToTypeCache[label] = t;
                        TypeToLabelCache[t] = label;
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
    /// <param name="enclosingType">The type that contains the property</param>
    /// <returns>The .NET property associated with that label.</returns>
    /// <exception cref="GraphException">If no .NET property was found for the given label.</exception>
    public static PropertyInfo GetPropertyFromLabel(string label, Type enclosingType)
    {
        ArgumentNullException.ThrowIfNull(label);

        LabelToPropertyCache.TryGetValue((enclosingType, label), out var propertyInfo);

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
                            LabelToPropertyCache[(t, label)] = prop;
                            PropertyToLabelCache[prop] = label;
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

    /// <summary>
    /// Finds the most derived type that matches the given label and is assignable to the target type.
    /// </summary>
    /// <param name="targetType">The base type that the result must be assignable to (cannot be an interface)</param>
    /// <param name="label">The label to match</param>
    /// <returns>The type that matches the label and is assignable to targetType, 
    /// or null if no matching type is found</returns>
    public static Type? GetMostDerivedType(Type targetType, string label)
    {
        ArgumentNullException.ThrowIfNull(targetType);
        ArgumentNullException.ThrowIfNull(label);

        if (targetType.IsInterface)
        {
            throw new ArgumentException("Target type cannot be an interface", nameof(targetType));
        }

        var cacheKey = (targetType, label);

        MostDerivedTypeCache.TryGetValue(cacheKey, out var cachedType);

        if (cachedType is not null)
        {
            return cachedType;
        }

        // First try to get the type directly from the label (this uses caching internally)
        Type? typeFromLabel = null;
        try
        {
            typeFromLabel = GetTypeFromLabel(label);
        }
        catch (GraphException)
        {
            // Label not found, return null
            MostDerivedTypeCache[cacheKey] = null;
            return null;
        }

        // Check if this type is assignable to our target type
        if (typeFromLabel != null && targetType.IsAssignableFrom(typeFromLabel))
        {
            MostDerivedTypeCache[cacheKey] = typeFromLabel;
            return typeFromLabel;
        }

        // Not assignable, cache null result
        MostDerivedTypeCache[cacheKey] = null;
        return null;
    }

    /// <summary>
    /// Gets all labels that are compatible with the target type, considering inheritance hierarchies.
    /// This includes the target type's label and all labels of types that derive from the target type.
    /// </summary>
    /// <param name="targetType">The base type to find compatible labels for</param>
    /// <returns>A list of labels that represent types assignable to the target type</returns>
    public static List<string> GetCompatibleLabels(Type targetType)
    {
        ArgumentNullException.ThrowIfNull(targetType);

        var labels = new List<string>();

        // Always include the target type's own label
        labels.Add(GetLabelFromType(targetType));

        // Find all types in loaded assemblies that derive from the target type
        var derivedTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly =>
            {
                try
                {
                    return assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    // Handle cases where some types can't be loaded
                    return ex.Types.Where(t => t != null).Cast<Type>();
                }
                catch
                {
                    return Enumerable.Empty<Type>();
                }
            })
            .Where(t => t.IsClass && !t.IsAbstract &&
                       targetType.IsAssignableFrom(t) &&
                       t != targetType) // Exclude the target type itself since we already added it
            .ToList();

        // Add labels for all derived types
        foreach (var derivedType in derivedTypes)
        {
            var derivedLabel = GetLabelFromType(derivedType);
            if (!labels.Contains(derivedLabel))
            {
                labels.Add(derivedLabel);
            }
        }

        return labels;
    }
}