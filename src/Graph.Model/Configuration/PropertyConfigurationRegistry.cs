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

namespace Cvoya.Graph.Model.Configuration;
using Cvoya.Graph.Model;

/// <summary>
/// Registry for property configurations that can be shared between strongly-typed and dynamic entities.
/// </summary>
public class PropertyConfigurationRegistry
{
    private readonly Dictionary<string, EntityPropertyConfiguration> _nodeConfigs = new();
    private readonly Dictionary<string, EntityPropertyConfiguration> _relationshipConfigs = new();
    private readonly object _lock = new();

    /// <summary>
    /// Configures properties for a node label.
    /// </summary>
    /// <param name="label">The node label to configure.</param>
    /// <param name="configure">The configuration action.</param>
    /// <returns>This registry instance for method chaining.</returns>
    public PropertyConfigurationRegistry ConfigureNode(string label, Action<EntityPropertyConfiguration> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(configure);

        lock (_lock)
        {
            var config = new EntityPropertyConfiguration();
            configure(config);
            _nodeConfigs[label] = config;
        }
        return this;
    }

    /// <summary>
    /// Configures properties for a relationship type.
    /// </summary>
    /// <param name="type">The relationship type to configure.</param>
    /// <param name="configure">The configuration action.</param>
    /// <returns>This registry instance for method chaining.</returns>
    public PropertyConfigurationRegistry ConfigureRelationship(string type, Action<EntityPropertyConfiguration> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentNullException.ThrowIfNull(configure);

        lock (_lock)
        {
            var config = new EntityPropertyConfiguration();
            configure(config);
            _relationshipConfigs[type] = config;
        }
        return this;
    }

    /// <summary>
    /// Gets the configuration for a node label.
    /// </summary>
    /// <param name="label">The node label.</param>
    /// <returns>The configuration for the node label, or null if not found.</returns>
    public EntityPropertyConfiguration? GetNodeConfiguration(string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        lock (_lock)
        {
            return _nodeConfigs.TryGetValue(label, out var config) ? config : null;
        }
    }

    /// <summary>
    /// Gets the configuration for a relationship type.
    /// </summary>
    /// <param name="type">The relationship type.</param>
    /// <returns>The configuration for the relationship type, or null if not found.</returns>
    public EntityPropertyConfiguration? GetRelationshipConfiguration(string type)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);

        lock (_lock)
        {
            return _relationshipConfigs.TryGetValue(type, out var config) ? config : null;
        }
    }

    /// <summary>
    /// Clears all configurations.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _nodeConfigs.Clear();
            _relationshipConfigs.Clear();
        }
    }

    /// <summary>
    /// Gets all configured node labels.
    /// </summary>
    /// <returns>An enumerable of configured node labels.</returns>
    public IEnumerable<string> GetConfiguredNodeLabels()
    {
        lock (_lock)
        {
            return _nodeConfigs.Keys.ToList();
        }
    }

    /// <summary>
    /// Gets all configured relationship types.
    /// </summary>
    /// <returns>An enumerable of configured relationship types.</returns>
    public IEnumerable<string> GetConfiguredRelationshipTypes()
    {
        lock (_lock)
        {
            return _relationshipConfigs.Keys.ToList();
        }
    }
}

/// <summary>
/// Configuration for entity properties.
/// </summary>
public class EntityPropertyConfiguration
{
    /// <summary>
    /// Gets the property configurations.
    /// </summary>
    public IDictionary<string, PropertyConfiguration> Properties { get; } = new Dictionary<string, PropertyConfiguration>();

    /// <summary>
    /// Configures a specific property.
    /// </summary>
    /// <param name="propertyName">The name of the property to configure.</param>
    /// <param name="configure">The configuration action.</param>
    /// <returns>This configuration instance for method chaining.</returns>
    public EntityPropertyConfiguration Property(string propertyName, Action<PropertyConfiguration> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        ArgumentNullException.ThrowIfNull(configure);

        var config = new PropertyConfiguration();
        configure(config);
        Properties[propertyName] = config;
        return this;
    }

    /// <summary>
    /// Gets the configuration for a specific property.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    /// <returns>The property configuration, or null if not found.</returns>
    public PropertyConfiguration? GetPropertyConfiguration(string propertyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        return Properties.TryGetValue(propertyName, out var config) ? config : null;
    }
}

/// <summary>
/// Configuration for a single property.
/// </summary>
public class PropertyConfiguration
{
    /// <summary>
    /// Gets or sets whether this property should be indexed for query performance.
    /// </summary>
    public bool IsIndexed { get; set; } = false;

    /// <summary>
    /// Gets or sets whether this property should be used as a key for the entity.
    /// </summary>
    public bool IsKey { get; set; } = false;

    /// <summary>
    /// Gets or sets whether this property should have unique values across entities with the same label/type.
    /// </summary>
    public bool IsUnique { get; set; } = false;

    /// <summary>
    /// Gets or sets whether this property is required (cannot be null).
    /// </summary>
    public bool IsRequired { get; set; } = false;

    /// <summary>
    /// Gets or sets the custom name for this property in the graph.
    /// </summary>
    public string? CustomName { get; set; } = null;

    /// <summary>
    /// Gets or sets validation rules for this property.
    /// </summary>
    public PropertyValidation? Validation { get; set; } = null;
}