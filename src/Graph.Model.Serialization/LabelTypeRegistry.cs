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

namespace Cvoya.Graph.Model.Serialization;

using System.Collections.Concurrent;
using System.Reflection;

/// <summary>
/// Registry that maps relationship/node labels to their concrete C# types.
/// This enables polymorphic type resolution based on stored graph labels.
/// </summary>
public class LabelTypeRegistry
{
    private readonly ConcurrentDictionary<string, Type> _labelToTypeMap = new();
    private readonly ConcurrentDictionary<Type, string> _typeToLabelMap = new();

    // Proper singleton instance
    private static readonly Lazy<LabelTypeRegistry> _instance = new(() => new LabelTypeRegistry());

    private LabelTypeRegistry()
    {
        // Private constructor to enforce singleton pattern
    }

    /// <summary>
    /// Gets the singleton instance of the registry
    /// </summary>
    public static LabelTypeRegistry Instance => _instance.Value;

    /// <summary>
    /// Registers a type with its corresponding graph label
    /// </summary>
    /// <param name="label">The graph label (e.g., "WORKS_REALLY_WELL_WITH")</param>
    /// <param name="type">The concrete C# type (e.g., typeof(KnowsWell))</param>
    public void Register(string label, Type type)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(type);

        _labelToTypeMap[label] = type;
        _typeToLabelMap[type] = label;
    }

    /// <summary>
    /// Gets the concrete type for a given graph label
    /// </summary>
    /// <param name="label">The graph label to look up</param>
    /// <returns>The concrete type, or null if not found</returns>
    public Type? GetTypeByLabel(string label)
    {
        if (string.IsNullOrWhiteSpace(label)) return null;

        // First check if we already have it
        if (_labelToTypeMap.TryGetValue(label, out var type))
        {
            return type;
        }

        // If not found, try to discover and register types from all currently loaded assemblies
        DiscoverAndRegisterTypesFromCurrentAssemblies();

        // Try again after discovery
        return _labelToTypeMap.TryGetValue(label, out type) ? type : null;
    }

    /// <summary>
    /// Gets the graph label for a given concrete type
    /// </summary>
    /// <param name="type">The type to look up</param>
    /// <returns>The graph label, or null if not found</returns>
    public string? GetLabelByType(Type type)
    {
        return type == null ? null : 
               _typeToLabelMap.TryGetValue(type, out var label) ? label : null;
    }

    /// <summary>
    /// Finds the most specific concrete type from a list of inheritance labels
    /// </summary>
    /// <param name="inheritanceLabels">List of type names in inheritance hierarchy</param>
    /// <returns>The most specific concrete type that has a registered serializer</returns>
    public Type? FindMostSpecificType(IEnumerable<string> inheritanceLabels)
    {
        if (inheritanceLabels == null) return null;

        // Collect all candidate types that have serializers registered
        var serializerRegistry = EntitySerializerRegistry.Instance;
        var candidateTypes = new List<Type>();
        
        foreach (var label in inheritanceLabels)
        {
            // Try direct label lookup first
            var type = GetTypeByLabel(label);
            if (type != null && serializerRegistry.ContainsType(type))
            {
                candidateTypes.Add(type);
            }

            // Try type name lookup (for inheritance_labels that contain type names)
            var typeByName = FindTypeByName(label);
            if (typeByName != null && serializerRegistry.ContainsType(typeByName))
            {
                candidateTypes.Add(typeByName);
            }
        }

        if (candidateTypes.Count == 0) return null;
        if (candidateTypes.Count == 1) return candidateTypes[0];

        // Find the most specific (most derived) type among candidates
        // A type is more specific if it is assignable to another candidate type
        Type? mostSpecificType = null;
        
        foreach (var candidate in candidateTypes)
        {
            bool isMoreSpecific = true;
            
            // Check if this candidate is more specific than our current best
            foreach (var other in candidateTypes)
            {
                if (candidate != other && candidate.IsAssignableFrom(other))
                {
                    // There's another type that derives from this candidate,
                    // so this candidate is not the most specific
                    isMoreSpecific = false;
                    break;
                }
            }
            
            if (isMoreSpecific)
            {
                // This candidate is not a base type of any other candidate,
                // so it's more specific than what we had before
                if (mostSpecificType == null || mostSpecificType.IsAssignableFrom(candidate))
                {
                    mostSpecificType = candidate;
                }
            }
        }

        return mostSpecificType ?? candidateTypes[0]; // Fallback to first if logic fails
    }

    /// <summary>
    /// Auto-discovers and registers all relationship and node types in loaded assemblies
    /// </summary>
    public void AutoDiscoverTypes()
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !IsSystemAssembly(a))
            .ToList();

        foreach (var assembly in assemblies)
        {
            try
            {
                var types = assembly.GetTypes()
                    .Where(t => !t.IsAbstract && !t.IsInterface)
                    .Where(t => typeof(INode).IsAssignableFrom(t) || typeof(IRelationship).IsAssignableFrom(t))
                    .ToList();

                foreach (var type in types)
                {
                    RegisterTypeWithAttributes(type);
                }
            }
            catch (ReflectionTypeLoadException)
            {
                // Skip assemblies that can't be loaded
                continue;
            }
        }
    }

    /// <summary>
    /// Discovers and registers types from all currently loaded assemblies
    /// </summary>
    private void DiscoverAndRegisterTypesFromCurrentAssemblies()
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !IsSystemAssembly(a))
            .ToList();

        foreach (var assembly in assemblies)
        {
            try
            {
                var types = assembly.GetTypes()
                    .Where(t => !t.IsAbstract && !t.IsInterface)
                    .Where(t => typeof(INode).IsAssignableFrom(t) || typeof(IRelationship).IsAssignableFrom(t))
                    .ToList();

                foreach (var type in types)
                {
                    // Only register if we don't already have this type registered
                    var existingLabel = GetLabelByType(type);
                    if (existingLabel == null)
                    {
                        RegisterTypeWithAttributes(type);
                    }
                }
            }
            catch (ReflectionTypeLoadException)
            {
                // Skip assemblies that can't be loaded
                continue;
            }
        }
    }

    private void RegisterTypeWithAttributes(Type type)
    {
        // Handle Node types
        if (typeof(INode).IsAssignableFrom(type))
        {
            var nodeAttr = type.GetCustomAttribute<NodeAttribute>();
            if (nodeAttr != null && !string.IsNullOrWhiteSpace(nodeAttr.Label))
            {
                Register(nodeAttr.Label, type);
            }
        }

        // Handle Relationship types
        if (typeof(IRelationship).IsAssignableFrom(type))
        {
            var relAttr = type.GetCustomAttribute<RelationshipAttribute>();
            if (relAttr != null && !string.IsNullOrWhiteSpace(relAttr.Label))
            {
                Register(relAttr.Label, type);
            }
        }
    }

    private Type? FindTypeByName(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName)) return null;

        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !IsSystemAssembly(a));

        foreach (var assembly in assemblies)
        {
            try
            {
                var type = assembly.GetTypes()
                    .FirstOrDefault(t => t.Name == typeName || t.FullName == typeName);
                
                if (type != null)
                {
                    return type;
                }
            }
            catch (ReflectionTypeLoadException)
            {
                continue;
            }
        }

        return null;
    }

    private static bool IsSystemAssembly(Assembly assembly)
    {
        var name = assembly.GetName().Name;
        return name != null && (
            name.StartsWith("System.") ||
            name.StartsWith("Microsoft.") ||
            name.StartsWith("mscorlib") ||
            name.StartsWith("netstandard") ||
            name == "System" ||
            name == "Microsoft.Extensions.Logging.Abstractions" ||
            name.StartsWith("NuGet.")
        );
    }
}