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

namespace Cvoya.Graph.Model.Age.Core.Entities;

using System.Text.RegularExpressions;
using Cvoya.Graph.Model;

/// <summary>
/// Validates property-level rules (required, min/max length, pattern, min/max value)
/// for entities before persisting changes.
/// </summary>
internal static class PropertyRuleValidator
{
    public static void ValidatePropertyRules(object entity, EntitySchemaInfo schema, string entityDisplayName)
    {
        foreach (var propertySchema in schema.Properties.Values)
        {
            if (propertySchema.Ignore)
            {
                continue;
            }

            var propertyInfo = propertySchema.PropertyInfo;

            // Safety check: the PropertyInfo must be usable on this entity type.
            // SchemaRegistry may have cached PropertyInfo from a different type
            // with the same schema label (e.g., multiple TestNode classes in tests).
            if (propertyInfo.DeclaringType != null && !propertyInfo.DeclaringType.IsAssignableFrom(entity.GetType()))
            {
                continue;
            }

            var value = propertyInfo.GetValue(entity);
            var propertyDisplayName = propertyInfo.Name;

            if (propertySchema.IsRequired)
            {
                if (value is null || (value is string stringValue && string.IsNullOrWhiteSpace(stringValue)))
                {
                    throw new GraphException($"Property '{propertyDisplayName}' on {entityDisplayName} is required and cannot be null or empty.");
                }
            }

            if (value is null)
            {
                continue;
            }

            var validation = propertySchema.Validation;
            if (validation.MinLength is null && validation.MaxLength is null && string.IsNullOrWhiteSpace(validation.Pattern)
                && validation.MinValue is null && validation.MaxValue is null)
            {
                continue;
            }

            ValidatePropertyValue(propertyDisplayName, value, validation, entityDisplayName);
        }
    }

    public static void ValidateDynamicPropertyRules(
        IReadOnlyDictionary<string, object?> properties,
        EntitySchemaInfo schema,
        string entityDisplayName)
    {
        // Known system properties that don't need schema entries
        var knownSystemProperties = new HashSet<string>(StringComparer.Ordinal)
        {
            nameof(INode.Labels), "user_id",
            nameof(IRelationship.StartNodeId),
            nameof(IRelationship.EndNodeId),
            nameof(IRelationship.Type)
        };

        // Track which schema properties we've seen in properties dictionary
        var seenProperties = new HashSet<string>(StringComparer.Ordinal);

        // Single pass: validate all schema properties against the provided dictionary
        // and check for unknown properties simultaneously.
        foreach (var propertySchema in schema.Properties.Values)
        {
            if (propertySchema.Ignore)
                continue;

            var graphName = propertySchema.Name;
            var exists = properties.TryGetValue(graphName, out var value);

            if (propertySchema.IsRequired)
            {
                if (!exists || value is null || (value is string stringValue && string.IsNullOrWhiteSpace(stringValue)))
                {
                    // For required properties not provided, check if there's a CLR default value
                    if (!exists)
                    {
                        var propInfo = propertySchema.PropertyInfo;
                        if (propInfo != null && propInfo.DeclaringType != null)
                        {
                            try
                            {
                                var defaultInstance = Activator.CreateInstance(propInfo.DeclaringType);
                                var defaultValue = propInfo.GetValue(defaultInstance);
                                if (defaultValue != null && (defaultValue is not string ds || !string.IsNullOrWhiteSpace(ds)))
                                {
                                    seenProperties.Add(graphName);
                                    continue;
                                }
                            }
                            catch { }
                        }
                    }

                    throw new GraphException($"Property '{graphName}' on {entityDisplayName} is required but not provided.");
                }
            }

            if (!exists || value is null)
            {
                // Property not provided but not required — still track it as handled
                seenProperties.Add(graphName);
                continue;
            }

            seenProperties.Add(graphName);

            // Validate enum values: if the CLR property type is an enum and the
            // dynamic value is a string, check it parses to a valid enum member.
            var enumPropInfo = propertySchema.PropertyInfo;
            if (enumPropInfo != null && enumPropInfo.PropertyType.IsEnum && value is string enumStr)
            {
                try
                {
                    var _ = Enum.Parse(enumPropInfo.PropertyType, enumStr, ignoreCase: false);
                }
                catch
                {
                    throw new GraphException($"Property '{graphName}' on {entityDisplayName} has value '{enumStr}' which is not a valid {enumPropInfo.PropertyType.Name} enum value.");
                }
            }

            var validation = propertySchema.Validation;
            if (validation.MinLength is null && validation.MaxLength is null && string.IsNullOrWhiteSpace(validation.Pattern)
                && validation.MinValue is null && validation.MaxValue is null)
                continue;

            ValidatePropertyValue(graphName, value, validation, entityDisplayName);
        }

        // Check for extra/unknown properties not defined in schema.
        // Dynamic entities use attribute Label values, so match only against
        // PropertySchemaInfo.Name (the graph property name), NOT C# property names.
        foreach (var (propName, _) in properties)
        {
            if (knownSystemProperties.Contains(propName) || seenProperties.Contains(propName))
                continue;

            throw new GraphException($"Property '{propName}' is not defined in the schema for {entityDisplayName}.");
        }
    }

    private static void ValidatePropertyValue(string propertyName, object value, PropertyValidation validation, string entityDisplayName)
    {
        if (validation.MinLength is int minLength && value is string stringValue && stringValue.Length < minLength)
        {
            throw new GraphException($"Property '{propertyName}' on {entityDisplayName} must have a minimum length of {minLength}. Current length: {stringValue.Length}.");
        }

        if (validation.MaxLength is int maxLength && value is string stringValueMax && stringValueMax.Length > maxLength)
        {
            throw new GraphException($"Property '{propertyName}' on {entityDisplayName} must have a maximum length of {maxLength}. Current length: {stringValueMax.Length}.");
        }

        if (!string.IsNullOrEmpty(validation.Pattern) && value is string patternValue)
        {
            if (!Regex.IsMatch(patternValue, validation.Pattern))
            {
                throw new GraphException($"Property '{propertyName}' on {entityDisplayName} must match the pattern '{validation.Pattern}'. Current value: {patternValue}");
            }
        }
    }
}
