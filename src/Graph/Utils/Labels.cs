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

namespace Cvoya.Graph;

using System.Collections.Concurrent;
using System.Reflection;


/// <summary>
/// Manages type-related operations for Neo4j entities.
/// </summary>
public static class Labels
{
    // Case-insensitive: a type maps to exactly one label and no two loaded types may share one
    // (case-insensitive), matching the resolver's case-insensitive label comparison. See SchemaRegistry.
    private static readonly ConcurrentDictionary<string, Type> LabelToTypeCache = new(StringComparer.OrdinalIgnoreCase);
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

        label = ResolveLabelFromType(type);

        // CG008/CG009 mirror: two distinct node types (or two distinct relationship types) must not resolve
        // to the same label. Re-resolving the same type - including a different closed form of the same
        // generic definition, see NormalizeForGenericIdentity - is always a silent cache hit. A node label
        // colliding with a relationship type of the same name is explicitly NOT a collision (they are
        // different namespaces in the graph model), so the check only fires within the same kind.
        if (LabelToTypeCache.TryGetValue(label, out var existingType) &&
            !AreSameTypeIdentity(existingType, type) &&
            AreSameLabelKind(existingType, type))
        {
            throw new GraphException(
                $"Label '{label}' is used by both '{existingType.FullName}' and '{type.FullName}'.");
        }

        TypeToLabelCache[type] = label;
        LabelToTypeCache[label] = type;
        return label;
    }

    /// <summary>
    /// Determines whether <paramref name="left"/> and <paramref name="right"/> should be treated as the same
    /// type identity for label-collision purposes: either they are literally the same <see cref="Type"/>, or
    /// they are different closed constructions of the same generic type definition (e.g. <c>MyNode&lt;int&gt;</c>
    /// and <c>MyNode&lt;string&gt;</c> both normalize to <c>MyNode&lt;&gt;</c>).
    /// </summary>
    private static bool AreSameTypeIdentity(Type left, Type right)
        => NormalizeForGenericIdentity(left) == NormalizeForGenericIdentity(right);

    private static Type NormalizeForGenericIdentity(Type type)
        => type.IsConstructedGenericType ? type.GetGenericTypeDefinition() : type;

    private static string ResolveLabelFromType(Type type)
    {
        string? label = null;

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
        return label ?? type.Name.Replace("`", "") ?? throw new GraphException($"Type '{type}' does not have a valid name.");
    }

    /// <summary>
    /// Determines whether <paramref name="left"/> and <paramref name="right"/> occupy the same "kind"
    /// namespace for label collisions: both implement <see cref="INode"/>, or both implement
    /// <see cref="IRelationship"/>. A node and a relationship sharing a label/type string is explicitly not a
    /// collision (they are different namespaces in the graph model - e.g. Neo4j <c>:Person</c> vs
    /// <c>-[:FOLLOWS]-&gt;</c>), so this returns false for any node/relationship pairing, regardless of label.
    /// </summary>
    private static bool AreSameLabelKind(Type left, Type right)
    {
        var leftIsNode = typeof(INode).IsAssignableFrom(left);
        var rightIsNode = typeof(INode).IsAssignableFrom(right);
        if (leftIsNode && rightIsNode)
        {
            return true;
        }

        var leftIsRelationship = typeof(IRelationship).IsAssignableFrom(left);
        var rightIsRelationship = typeof(IRelationship).IsAssignableFrom(right);
        return leftIsRelationship && rightIsRelationship;
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
        label = ResolveLabelFromProperty(propertyInfo, propertyAttr);

        var cacheKey = (propertyInfo.DeclaringType, label);

        // CG007 mirror (partial - see remarks): re-resolving the same property is always a silent cache
        // hit; a different property on the same declaring type resolving to the same label is a collision.
        // This only sees collisions between properties that share a declaring type, since PropertyInfo
        // .DeclaringType for an inherited property is the base type that declares it, not whatever derived
        // type it is queried through - SchemaRegistry.CreateEntitySchemaInfo is the authoritative,
        // full-inheritance-chain mirror of CG007; this is a narrower, complementary check.
        if (LabelToPropertyCache.TryGetValue(cacheKey, out var existingProperty) &&
            !AreSameProperty(existingProperty, propertyInfo))
        {
            throw new GraphException(
                $"Property label '{label}' on '{propertyInfo.DeclaringType.FullName}' is used by both " +
                $"'{propertyInfo.DeclaringType.FullName}.{existingProperty.Name}' and " +
                $"'{propertyInfo.DeclaringType.FullName}.{propertyInfo.Name}'.");
        }

        PropertyToLabelCache[propertyInfo] = label;
        LabelToPropertyCache[cacheKey] = propertyInfo;
        return label;
    }

    private static bool AreSameProperty(PropertyInfo left, PropertyInfo right)
        => left == right || (left.DeclaringType == right.DeclaringType && left.Name == right.Name);

    private static string ResolveLabelFromProperty(PropertyInfo propertyInfo)
    {
        var propertyAttr = propertyInfo.GetCustomAttribute<PropertyAttribute>(inherit: false);
        return ResolveLabelFromProperty(propertyInfo, propertyAttr);
    }

    private static string ResolveLabelFromProperty(PropertyInfo propertyInfo, PropertyAttribute? propertyAttr)
    {
        if (propertyAttr?.Label is { Length: > 0 })
        {
            return propertyAttr.Label;
        }

        // Fall back to the property name with backticks removed
        return propertyInfo.Name.Replace("`", "") ?? throw new GraphException($"Property '{propertyInfo}' does not have a valid name.");
    }

    /// <summary>
    /// Finds the .NET type for a given label.
    /// </summary>
    /// <param name="label">The label</param>
    /// <returns>The .NET associated with that label.</returns>
    /// <exception cref="GraphException">If no .NET type was found for the given label.</exception>
    /// <remarks>
    /// A type maps to exactly one label, and <see cref="SchemaRegistry"/> forbids two loaded types from
    /// sharing a label (case-insensitive), so this reverse lookup is deterministic; matching is
    /// case-insensitive. This is a Model-layer utility for recovering a type from a stored label - the
    /// portability path when a node's stored metadata type is not loadable (a different app, or the type was
    /// renamed/moved). A provider may use it or implement its own equivalent resolution; the Neo4j provider
    /// does the latter.
    /// </remarks>
    public static Type GetTypeFromLabel(string label)
    {
        ArgumentNullException.ThrowIfNull(label);

        LabelToTypeCache.TryGetValue(label, out var type);

        if (type is not null)
        {
            return type;
        }

        var candidates = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(GetLoadableTypes)
            .Where(IsGraphEntityType)
            // Case-insensitive to match the cache (LabelToTypeCache) and the process-wide uniqueness rule,
            // so a cold-cache lookup resolves the same type a warm one would regardless of label casing.
            .Where(candidate => string.Equals(ResolveLabelFromType(candidate), label, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (candidates.Count == 0)
        {
            throw new GraphException($"No type found for label '{label}'.");
        }

        type = SelectTypeFromReverseLookupCandidates(label, candidates);
        LabelToTypeCache[label] = type;
        TypeToLabelCache[type] = label;
        return type;
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
        ArgumentNullException.ThrowIfNull(enclosingType);

        LabelToPropertyCache.TryGetValue((enclosingType, label), out var propertyInfo);

        if (propertyInfo is not null)
        {
            return propertyInfo;
        }

        // Graph entities persist public instance properties; keep reverse lookup on that same boundary.
        var candidates = enclosingType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(prop => ResolveLabelFromProperty(prop) == label)
            .ToList();

        if (candidates.Count == 0)
        {
            throw new GraphException($"No property found for label '{label}' on '{enclosingType.FullName}'.");
        }

        propertyInfo = SelectPropertyFromReverseLookupCandidates(label, enclosingType, candidates);
        LabelToPropertyCache[(enclosingType, label)] = propertyInfo;
        PropertyToLabelCache[propertyInfo] = label;
        return propertyInfo;
    }

    private static Type SelectTypeFromReverseLookupCandidates(string label, IReadOnlyCollection<Type> candidates)
    {
        var distinctCandidates = candidates
            .GroupBy(NormalizeForGenericIdentity)
            .Select(group => group.First())
            .ToList();

        var collidingCandidates = distinctCandidates
            .Where(candidate => distinctCandidates.Any(other =>
                !AreSameTypeIdentity(candidate, other) && AreSameLabelKind(candidate, other)))
            .ToList();

        if (collidingCandidates.Count > 0)
        {
            throw new GraphException(
                $"Label '{label}' is used by multiple graph entity types: " +
                string.Join(", ", collidingCandidates.Select(FormatTypeName)) + ".");
        }

        // A node and relationship may legitimately share the same graph label/type string. With no kind
        // parameter on this API, prefer the node candidate to preserve the historical node-first contract.
        return distinctCandidates.FirstOrDefault(static candidate => typeof(INode).IsAssignableFrom(candidate))
            ?? distinctCandidates[0];
    }

    private static PropertyInfo SelectPropertyFromReverseLookupCandidates(
        string label,
        Type enclosingType,
        IReadOnlyCollection<PropertyInfo> candidates)
    {
        var distinctCandidates = new List<PropertyInfo>();
        foreach (var candidate in candidates.Where(candidate =>
            !distinctCandidates.Any(existing => AreSameProperty(existing, candidate))))
        {
            distinctCandidates.Add(candidate);
        }

        if (distinctCandidates.Count > 1)
        {
            throw new GraphException(
                $"Property label '{label}' on '{enclosingType.FullName}' is used by multiple properties: " +
                string.Join(", ", distinctCandidates.Select(FormatPropertyName)) + ".");
        }

        return distinctCandidates[0];
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(static type => type is not null).Cast<Type>();
        }
        catch (NotSupportedException)
        {
            return [];
        }
        catch (FileNotFoundException)
        {
            return [];
        }
        catch (FileLoadException)
        {
            return [];
        }
        catch (BadImageFormatException)
        {
            return [];
        }
        catch (TypeLoadException)
        {
            return [];
        }
    }

    // Include abstract graph types to match GetLabelFromType, which resolves labels for abstract bases too.
    private static bool IsGraphEntityType(Type type)
        => !type.IsInterface &&
           (typeof(INode).IsAssignableFrom(type) || typeof(IRelationship).IsAssignableFrom(type));

    private static string FormatTypeName(Type type)
        => $"'{type.FullName}'";

    private static string FormatPropertyName(PropertyInfo property)
        => $"'{property.DeclaringType?.FullName}.{property.Name}'";

    internal static void ClearCachesForTesting()
    {
        LabelToTypeCache.Clear();
        TypeToLabelCache.Clear();
        PropertyToLabelCache.Clear();
        LabelToPropertyCache.Clear();
        MostDerivedTypeCache.Clear();
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
    /// <remarks>
    /// Returns one label per type (<see cref="GetLabelFromType"/>) - the target type's label plus the label
    /// of each concrete type that derives from it. This is how a polymorphic query
    /// (e.g. <c>Nodes&lt;Person&gt;()</c>) matches subtypes: the class hierarchy is expanded to the set of
    /// compatible labels at query-construction time. Only subtypes the registry has discovered
    /// (their assembly loaded) are included.
    /// </remarks>
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
