// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.InMemory;

/// <summary>
/// Application-level property validation mirroring the reference provider: required properties,
/// declarative validation rules from <c>[Property]</c>, and schema conformance for dynamic
/// entities. Hostile identifier strings are inert in an in-memory store, so labels and types are
/// accepted verbatim except where they are never legitimate (empty or whitespace).
/// </summary>
internal sealed class EntityValidator(SchemaRegistry schemaRegistry)
{
    private readonly SchemaRegistry _schemaRegistry = schemaRegistry;

    /// <summary>Validates a node before create or update.</summary>
    public void ValidateNode<TNode>(TNode node) where TNode : class, INode
    {
        if (node is DynamicNode dynamicNode)
        {
            if (dynamicNode.Labels.Any(string.IsNullOrWhiteSpace))
            {
                throw new GraphException("Dynamic node labels cannot be null, empty, or whitespace.");
            }

            ValidateDynamicNode(dynamicNode);
            return;
        }

        var label = Labels.GetLabelFromType(node.GetType());
        var schema = _schemaRegistry.GetNodeSchema(label);
        if (schema is null)
        {
            return;
        }

        foreach (var (propertyName, propertySchema) in schema.Properties)
        {
            var property = node.GetType().GetProperty(propertyName);
            if (property is null)
            {
                continue;
            }

            var value = property.GetValue(node);
            ValidateRequired(propertyName, value, propertySchema.IsRequired, label);
            if (value is not null)
            {
                ValidateRules(propertyName, value, propertySchema.Validation, label);
            }
        }
    }

    /// <summary>Validates a relationship before create or update.</summary>
    public void ValidateRelationship<TRelationship>(TRelationship relationship)
        where TRelationship : class, IRelationship
    {
        if (relationship is DynamicRelationship dynamicRelationship)
        {
            if (string.IsNullOrWhiteSpace(dynamicRelationship.Type))
            {
                throw new GraphException("Dynamic relationship types cannot be null, empty, or whitespace.");
            }

            ValidateDynamicRelationship(dynamicRelationship);
            return;
        }

        var type = Labels.GetLabelFromType(relationship.GetType());
        var schema = _schemaRegistry.GetRelationshipSchema(type);
        if (schema is null)
        {
            return;
        }

        foreach (var (propertyName, propertySchema) in schema.Properties)
        {
            var property = relationship.GetType().GetProperty(propertyName);
            if (property is null)
            {
                continue;
            }

            var value = property.GetValue(relationship);
            ValidateRequired(propertyName, value, propertySchema.IsRequired, type);
            if (value is not null)
            {
                ValidateRules(propertyName, value, propertySchema.Validation, type);
            }
        }
    }

    private void ValidateDynamicNode(DynamicNode node)
    {
        foreach (var label in node.Labels)
        {
            var schema = _schemaRegistry.GetNodeSchema(label);
            if (schema is null)
            {
                continue;
            }

            ValidateDynamicProperties(node.Properties, schema.Properties, label);
        }
    }

    private void ValidateDynamicRelationship(DynamicRelationship relationship)
    {
        var schema = _schemaRegistry.GetRelationshipSchema(relationship.Type);
        if (schema is null)
        {
            return;
        }

        ValidateDynamicProperties(relationship.Properties, schema.Properties, relationship.Type);
    }

    private static void ValidateDynamicProperties(
        IReadOnlyDictionary<string, object?> properties,
        IDictionary<string, PropertySchemaInfo> schemaProperties,
        string label)
    {
        var validated = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (_, propertySchema) in schemaProperties)
        {
            var mappedName = propertySchema.Name;
            if (!properties.TryGetValue(mappedName, out var value))
            {
                if (propertySchema.IsRequired)
                {
                    throw new GraphException(
                        $"Property '{mappedName}' on {label} is required but not provided in the dynamic entity.");
                }

                continue;
            }

            validated.Add(mappedName);
            ValidateRequired(mappedName, value, propertySchema.IsRequired, label);
            if (value is not null)
            {
                ValidateRules(mappedName, value, propertySchema.Validation, label);
                ValidateEnum(mappedName, value, propertySchema.PropertyInfo.PropertyType, label);
            }
        }

        foreach (var propertyName in properties.Keys.Where(propertyName => !validated.Contains(propertyName)))
        {
            throw new GraphException(
                $"Property '{propertyName}' on {label} is not defined in the schema and cannot be used.");
        }
    }

    private static void ValidateRequired(string propertyName, object? value, bool isRequired, string label)
    {
        if (isRequired && (value is null || (value is string text && string.IsNullOrWhiteSpace(text))))
        {
            throw new GraphException($"Property '{propertyName}' on {label} is required and cannot be null or empty.");
        }
    }

    private static void ValidateRules(string propertyName, object value, PropertyValidation validation, string label)
    {
        if (validation.MinValue is not null && value is IComparable belowComparable &&
            belowComparable.CompareTo(validation.MinValue) < 0)
        {
            throw new GraphException(
                $"Property '{propertyName}' on {label} must be greater than or equal to {validation.MinValue}. Current value: {value}");
        }

        if (validation.MaxValue is not null && value is IComparable aboveComparable &&
            aboveComparable.CompareTo(validation.MaxValue) > 0)
        {
            throw new GraphException(
                $"Property '{propertyName}' on {label} must be less than or equal to {validation.MaxValue}. Current value: {value}");
        }

        if (value is string stringValue)
        {
            if (validation.MinLength is { } minLength && stringValue.Length < minLength)
            {
                throw new GraphException(
                    $"Property '{propertyName}' on {label} must have a minimum length of {minLength}. Current length: {stringValue.Length}");
            }

            if (validation.MaxLength is { } maxLength && stringValue.Length > maxLength)
            {
                throw new GraphException(
                    $"Property '{propertyName}' on {label} must have a maximum length of {maxLength}. Current length: {stringValue.Length}");
            }

            if (!string.IsNullOrEmpty(validation.Pattern) &&
                !System.Text.RegularExpressions.Regex.IsMatch(stringValue, validation.Pattern))
            {
                throw new GraphException(
                    $"Property '{propertyName}' on {label} must match the pattern '{validation.Pattern}'. Current value: {stringValue}");
            }
        }
    }

    private static void ValidateEnum(string propertyName, object value, Type propertyType, string label)
    {
        if (!propertyType.IsEnum)
        {
            return;
        }

        var isValid = value switch
        {
            string text => Enum.TryParse(propertyType, text, ignoreCase: true, out _),
            _ => Enum.IsDefined(propertyType, value),
        };

        if (!isValid)
        {
            var validValues = string.Join(", ", Enum.GetNames(propertyType));
            throw new GraphException(
                $"Property '{propertyName}' on {label} must be a valid enum value. Valid values are: {validValues}. Current value: {value}");
        }
    }
}
