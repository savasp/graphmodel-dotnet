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

using System.Reflection;

/// <summary>
/// Registry for schema information that can be shared between strongly-typed and dynamic entities.
/// This registry discovers and manages schema information for all INode and IRelationship types.
/// </summary>
public class SchemaRegistry : IDisposable
{
    private readonly Dictionary<string, EntitySchemaInfo> _nodeSchemas = new();
    private readonly Dictionary<string, EntitySchemaInfo> _relationshipSchemas = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private volatile bool _isInitialized = false;

    /// <summary>
    /// Gets whether the schema registry has been initialized.
    /// </summary>
    public bool IsInitialized => _isInitialized;

    /// <summary>
    /// Asynchronously initializes the schema registry by discovering all INode and IRelationship types
    /// in all loaded assemblies and populating the registry with their schema information.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        // Quick check without lock for performance
        if (_isInitialized)
            return;

        // Use async-safe semaphore
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            // Double-check pattern
            if (_isInitialized)
                return;

            var (nodeTypes, relationshipTypes) = DiscoverGraphEntityTypes();

            foreach (var nodeType in nodeTypes)
            {
                RegisterNodeType(nodeType);
            }

            foreach (var relationshipType in relationshipTypes)
            {
                RegisterRelationshipType(relationshipType);
            }

            _isInitialized = true;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Gets the schema information for a node label.
    /// </summary>
    /// <param name="label">The node label.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The schema information for the node label, or null if not found.</returns>
    public async Task<EntitySchemaInfo?> GetNodeSchemaAsync(string label, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        // Quick read without lock since dictionary reads are thread-safe after initialization
        if (_isInitialized)
        {
            return _nodeSchemas.TryGetValue(label, out var schema) ? schema : null;
        }

        // If not initialized, use semaphore for safety
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            return _nodeSchemas.TryGetValue(label, out var schema) ? schema : null;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Gets the schema information for a relationship type.
    /// </summary>
    /// <param name="type">The relationship type.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The schema information for the relationship type, or null if not found.</returns>
    public async Task<EntitySchemaInfo?> GetRelationshipSchemaAsync(string type, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);

        // Quick read without lock since dictionary reads are thread-safe after initialization
        if (_isInitialized)
        {
            return _relationshipSchemas.TryGetValue(type, out var schema) ? schema : null;
        }

        // If not initialized, use semaphore for safety
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            return _relationshipSchemas.TryGetValue(type, out var schema) ? schema : null;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Gets the schema information for a node label (synchronous version for use after initialization).
    /// </summary>
    /// <param name="label">The node label.</param>
    /// <returns>The schema information for the node label, or null if not found.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the registry is not initialized.</exception>
    public EntitySchemaInfo? GetNodeSchema(string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        if (!_isInitialized)
            throw new InvalidOperationException("Schema registry must be initialized before accessing schema information synchronously.");

        return _nodeSchemas.TryGetValue(label, out var schema) ? schema : null;
    }

    /// <summary>
    /// Gets the schema information for a relationship type (synchronous version for use after initialization).
    /// </summary>
    /// <param name="type">The relationship type.</param>
    /// <returns>The schema information for the relationship type, or null if not found.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the registry is not initialized.</exception>
    public EntitySchemaInfo? GetRelationshipSchema(string type)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);

        if (!_isInitialized)
            throw new InvalidOperationException("Schema registry must be initialized before accessing schema information synchronously.");

        return _relationshipSchemas.TryGetValue(type, out var schema) ? schema : null;
    }

    /// <summary>
    /// Gets all registered node labels.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>An enumerable of registered node labels.</returns>
    public async Task<IEnumerable<string>> GetRegisteredNodeLabelsAsync(CancellationToken cancellationToken = default)
    {
        // Quick read without lock since dictionary reads are thread-safe after initialization
        if (_isInitialized)
        {
            return _nodeSchemas.Keys.ToList();
        }

        // If not initialized, use semaphore for safety
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            return _nodeSchemas.Keys.ToList();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Gets all registered relationship types.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>An enumerable of registered relationship types.</returns>
    public async Task<IEnumerable<string>> GetRegisteredRelationshipTypesAsync(CancellationToken cancellationToken = default)
    {
        // Quick read without lock since dictionary reads are thread-safe after initialization
        if (_isInitialized)
        {
            return _relationshipSchemas.Keys.ToList();
        }

        // If not initialized, use semaphore for safety
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            return _relationshipSchemas.Keys.ToList();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Asynchronously clears all schema information and resets the initialization state.
    /// </summary>
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            _nodeSchemas.Clear();
            _relationshipSchemas.Clear();
            _isInitialized = false;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private void RegisterNodeType(Type nodeType)
    {
        var label = GetLabelFromType(nodeType);
        var schema = CreateEntitySchemaInfo(nodeType, label);
        _nodeSchemas[label] = schema;
    }

    private void RegisterRelationshipType(Type relationshipType)
    {
        var type = GetLabelFromType(relationshipType);
        var schema = CreateEntitySchemaInfo(relationshipType, type);
        _relationshipSchemas[type] = schema;
    }

    private EntitySchemaInfo CreateEntitySchemaInfo(Type entityType, string label)
    {
        var properties = new Dictionary<string, PropertySchemaInfo>();
        var allProperties = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in allProperties)
        {
            var propertySchema = CreatePropertySchemaInfo(property);
            properties[property.Name] = propertySchema;
        }

        return new EntitySchemaInfo
        {
            Type = entityType,
            Label = label,
            Properties = properties
        };
    }

    private PropertySchemaInfo CreatePropertySchemaInfo(PropertyInfo property)
    {
        var attribute = property.GetCustomAttribute<PropertyAttribute>();
        var isKey = attribute?.IsKey ?? false;

        var includeInFullTextSearch = attribute?.IncludeInFullTextSearch switch
        {
            true => property.PropertyType == typeof(string),
            false => false,
            null when property.PropertyType == typeof(string) => true, // Default for string properties
            null => false // Default for non-string properties
        };

        return new PropertySchemaInfo
        {
            PropertyInfo = property,
            Name = GetPropertyName(property, attribute),
            IsIndexed = isKey || (attribute?.IsIndexed ?? false),
            IsKey = isKey,
            IsUnique = isKey || (attribute?.IsUnique ?? false),
            IsRequired = isKey || (attribute?.IsRequired ?? false),
            Ignore = attribute?.Ignore ?? false,
            IncludeInFullTextSearch = includeInFullTextSearch,
            Validation = new PropertyValidation
            {
                MinLength = attribute?.MinLength,
                MaxLength = attribute?.MaxLength,
                Pattern = attribute?.Pattern
            }
        };
    }

    private string GetLabelFromType(Type type)
    {
        // Priority: NodeAttribute/RelationshipAttribute → Labels.GetLabelFromType
        var nodeAttr = type.GetCustomAttribute<NodeAttribute>();
        if (nodeAttr?.Label is { Length: > 0 })
        {
            return nodeAttr.Label;
        }

        var relAttr = type.GetCustomAttribute<RelationshipAttribute>();
        if (relAttr?.Label is { Length: > 0 })
        {
            return relAttr.Label;
        }

        // Fall back to Labels utility
        return Labels.GetLabelFromType(type);
    }

    private string GetPropertyName(PropertyInfo property, PropertyAttribute? attribute)
    {
        // Priority: PropertyAttribute.Label → Labels.GetLabelFromProperty
        if (attribute?.Label is { Length: > 0 })
        {
            return attribute.Label;
        }

        return Labels.GetLabelFromProperty(property);
    }

    private (List<Type> nodeTypes, List<Type> relationshipTypes) DiscoverGraphEntityTypes()
    {
        var nodeTypes = new List<Type>();
        var relationshipTypes = new List<Type>();
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();

        foreach (var assembly in assemblies)
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch
            {
                continue;
            }

            foreach (var type in types)
            {
                if (type.IsAbstract || type.IsInterface) continue;

                if (typeof(INode).IsAssignableFrom(type))
                {
                    // Exclude DynamicNode from schema discovery
                    if (type != typeof(DynamicNode))
                        nodeTypes.Add(type);
                }
                else if (typeof(IRelationship).IsAssignableFrom(type))
                {
                    // Exclude DynamicRelationship from schema discovery
                    if (type != typeof(DynamicRelationship))
                        relationshipTypes.Add(type);
                }
            }
        }

        return (nodeTypes, relationshipTypes);
    }

    /// <summary>
    /// Disposes the semaphore used for concurrency control.
    /// </summary>
    public void Dispose()
    {
        _semaphore?.Dispose();
    }
}