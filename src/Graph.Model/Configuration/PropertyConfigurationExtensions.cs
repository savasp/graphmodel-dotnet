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

using System.Reflection;

/// <summary>
/// Extensions to automatically configure the registry from attribute-based configurations.
/// </summary>
public static class PropertyConfigurationExtensions
{
    /// <summary>
    /// Configures the registry based on PropertyAttribute configurations in a type.
    /// </summary>
    /// <typeparam name="T">The entity type to configure.</typeparam>
    /// <param name="registry">The registry to configure.</param>
    /// <returns>The registry instance for method chaining.</returns>
    public static PropertyConfigurationRegistry ConfigureFromAttributes<T>(
        this PropertyConfigurationRegistry registry)
        where T : IEntity
    {
        ArgumentNullException.ThrowIfNull(registry);

        var type = typeof(T);
        var label = Labels.GetLabelFromType(type);

        if (typeof(INode).IsAssignableFrom(type))
        {
            registry.ConfigureNode(label, config =>
            {
                ConfigurePropertiesFromAttributes(type, config);
            });
        }
        else if (typeof(IRelationship).IsAssignableFrom(type))
        {
            registry.ConfigureRelationship(label, config =>
            {
                ConfigurePropertiesFromAttributes(type, config);
            });
        }

        return registry;
    }

    /// <summary>
    /// Configures the registry based on PropertyAttribute configurations in a type.
    /// </summary>
    /// <param name="registry">The registry to configure.</param>
    /// <param name="type">The entity type to configure.</param>
    /// <returns>The registry instance for method chaining.</returns>
    public static PropertyConfigurationRegistry ConfigureFromAttributes(
        this PropertyConfigurationRegistry registry,
        Type type)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(type);

        if (!typeof(IEntity).IsAssignableFrom(type))
        {
            throw new ArgumentException($"Type {type.Name} must implement IEntity", nameof(type));
        }

        var label = Labels.GetLabelFromType(type);

        if (typeof(INode).IsAssignableFrom(type))
        {
            registry.ConfigureNode(label, config =>
            {
                ConfigurePropertiesFromAttributes(type, config);
            });
        }
        else if (typeof(IRelationship).IsAssignableFrom(type))
        {
            registry.ConfigureRelationship(label, config =>
            {
                ConfigurePropertiesFromAttributes(type, config);
            });
        }

        return registry;
    }

    /// <summary>
    /// Configures the registry from all entity types in an assembly.
    /// </summary>
    /// <param name="registry">The registry to configure.</param>
    /// <param name="assembly">The assembly to scan for entity types.</param>
    /// <returns>The registry instance for method chaining.</returns>
    public static PropertyConfigurationRegistry ConfigureFromAssembly(
        this PropertyConfigurationRegistry registry,
        Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(assembly);

        var entityTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(IEntity).IsAssignableFrom(t))
            .ToList();

        foreach (var type in entityTypes)
        {
            registry.ConfigureFromAttributes(type);
        }

        return registry;
    }

    /// <summary>
    /// Configures the registry from all entity types in the current app domain.
    /// </summary>
    /// <param name="registry">The registry to configure.</param>
    /// <returns>The registry instance for method chaining.</returns>
    public static PropertyConfigurationRegistry ConfigureFromAllAssemblies(
        this PropertyConfigurationRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach (var assembly in assemblies)
        {
            try
            {
                registry.ConfigureFromAssembly(assembly);
            }
            catch (ReflectionTypeLoadException)
            {
                // Skip assemblies that can't be fully loaded
                continue;
            }
        }

        return registry;
    }

    private static void ConfigurePropertiesFromAttributes(Type type, EntityPropertyConfiguration config)
    {
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties)
        {
            var attribute = property.GetCustomAttribute<PropertyAttribute>();
            if (attribute != null)
            {
                config.Property(property.Name, propConfig =>
                {
                    propConfig.IsIndexed = attribute.IsIndexed;
                    propConfig.IsUnique = attribute.IsUnique;
                    propConfig.IsRequired = attribute.IsRequired;
                    propConfig.Validation = attribute.Validation;

                    if (!string.IsNullOrEmpty(attribute.Label))
                    {
                        propConfig.CustomName = attribute.Label;
                    }
                });
            }
        }
    }
}