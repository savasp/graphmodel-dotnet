// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Entities;

using System.Reflection;
using Cvoya.Graph.Neo4j.Core;
using Cvoya.Graph.Neo4j.Querying.Cypher;
using Cvoya.Graph.Neo4j.Serialization;
using Cvoya.Graph.Serialization;
using global::Neo4j.Driver;


/// <summary>
/// Manages CRUD operations for relationships in Neo4j.
/// All methods assume that there is already a transaction in progress.
/// </summary>
internal sealed class Neo4jRelationshipManager(GraphContext context)
{
    private const string RelationshipIdentityChangeMessage =
        "Relationship type or concrete CLR type cannot be changed on update; delete and recreate the relationship.";

    private readonly EntityFactory _serializer = new();

    internal async Task<bool> UpdateByElementIdAsync(
        Graph.IRelationship relationship,
        string elementId,
        GraphTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(relationship);
        ArgumentException.ThrowIfNullOrWhiteSpace(elementId);
        ArgumentNullException.ThrowIfNull(transaction);
        cancellationToken.ThrowIfCancellationRequested();

        GraphDataModel.EnsureNoReferenceCycle(relationship);
        GraphDataModel.EnsureComplexPropertyDepth(relationship);
        ValidateRelationshipProperties(relationship);
        var entity = _serializer.Serialize(relationship);
        if (entity.ComplexProperties.Count > 0)
        {
            throw new GraphException(
                $"Relationships cannot have complex properties. Found {entity.ComplexProperties.Count} complex properties on {relationship.GetType().Name}");
        }

        var properties = BuildElementBoundRelationshipProperties(entity);

        var requiresClrMetadata = entity.ActualType.IsConstructedGenericType;
        var cypher = $"""
            OPTIONAL MATCH ()-[r]->()
            WHERE elementId(r) = $elementId
            WITH r,
                 type(r) AS storedPhysicalType,
                 r.{SerializationBridge.MetadataPropertyName} AS storedClrMetadata
            WITH r, storedPhysicalType, storedClrMetadata,
                 coalesce(storedPhysicalType = $incomingStorageType, false) AS physicalTypeMatches,
                 coalesce($requiresClrMetadata = false OR storedClrMetadata = $incomingClrType, false) AS clrTypeMatches
            FOREACH (_ IN CASE WHEN r IS NOT NULL AND physicalTypeMatches AND clrTypeMatches THEN [1] ELSE [] END |
                SET r = $props)
            RETURN r IS NOT NULL AS exists,
                   physicalTypeMatches AS physicalTypeMatches,
                   clrTypeMatches AS clrTypeMatches,
                   storedPhysicalType AS storedPhysicalType,
                   storedClrMetadata AS storedClrMetadata
            """;
        var incomingClrType = requiresClrMetadata
            ? SerializationBridge.GetAssemblyQualifiedTypeName(entity.ActualType)
            : null;
        var result = await transaction.Transaction.RunAsync(
            cypher,
            new
            {
                elementId,
                incomingStorageType = entity.Label,
                incomingClrType,
                requiresClrMetadata,
                props = properties
            }).WaitAsync(cancellationToken).ConfigureAwait(false);
        var record = await result.SingleAsync(cancellationToken).ConfigureAwait(false);
        if (!record["exists"].As<bool>())
        {
            return false;
        }

        if (!record["physicalTypeMatches"].As<bool>() || !record["clrTypeMatches"].As<bool>())
        {
            var storedClrIdentity = record["storedClrMetadata"] as string ?? "<missing>";
            throw new GraphException(
                $"{RelationshipIdentityChangeMessage} " +
                $"Stored physical type is '{record["storedPhysicalType"].As<string>()}' and CLR identity is '{storedClrIdentity}'; " +
                $"incoming relationship type is '{entity.Label}' and CLR type is '{incomingClrType}'.");
        }

        return true;
    }

    internal static async Task<int> DeleteByElementIdsAsync(
        IReadOnlyList<string> elementIds,
        GraphTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(elementIds);
        ArgumentNullException.ThrowIfNull(transaction);
        cancellationToken.ThrowIfCancellationRequested();
        if (elementIds.Count == 0)
        {
            return 0;
        }

        const string cypher = """
            MATCH ()-[relationship]->()
            WHERE elementId(relationship) IN $targetIds
            DELETE relationship
            RETURN count(*) AS affectedCount
            """;
        var result = await transaction.Transaction.RunAsync(
            cypher,
            new { targetIds = elementIds }).WaitAsync(cancellationToken).ConfigureAwait(false);
        return (await result.SingleAsync(cancellationToken).ConfigureAwait(false))["affectedCount"].As<int>();
    }

    internal static Dictionary<string, object?> BuildElementBoundRelationshipProperties(EntityInfo entity)
    {
        var properties = SerializationHelpers.SerializeSimpleProperties(entity)
            .Where(pair => pair.Key != nameof(Graph.IRelationship.Type))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        if (entity.ActualType.IsConstructedGenericType)
        {
            properties[SerializationBridge.MetadataPropertyName] =
                SerializationBridge.GetAssemblyQualifiedTypeName(entity.ActualType);
        }

        return properties;
    }

    internal void ValidateRelationshipProperties<TRelationship>(TRelationship relationship) where TRelationship : class, Graph.IRelationship
    {
        // For DynamicRelationship, validate against existing schemas if any
        if (relationship is DynamicRelationship dynamicRelationship)
        {
            // The relationship type is a Cypher identifier, not a value: reject it here, before any
            // Cypher is built, rather than relying solely on escaping at the point of interpolation.
            CypherIdentifier.Validate(dynamicRelationship.Type, "relationship type");
            ValidateDynamicRelationshipProperties(dynamicRelationship);
            return;
        }

        var type = Labels.GetLabelFromType(relationship.GetType());
        var schema = context.SchemaManager.GetSchemaRegistry().GetRelationshipSchema(type);

        if (schema == null) return;

        foreach (var (propertyName, propertySchema) in schema.Properties)
        {
            var property = relationship.GetType().GetProperty(propertyName);
            if (property == null) continue;

            var value = property.GetValue(relationship);

            // Validate required fields
            if (propertySchema.IsRequired)
            {
                if (value == null || (value is string stringValue && string.IsNullOrWhiteSpace(stringValue)))
                {
                    throw new GraphException($"Property '{propertyName}' on {type} is required and cannot be null or empty.");
                }
            }

            // Validate custom validation rules
            if (propertySchema.Validation is { } validation && value is not null)
            {
                ValidatePropertyValue(propertyName, value, validation, type);
            }
        }
    }

    private void ValidateDynamicRelationshipProperties(DynamicRelationship relationship)
    {
        var schema = context.SchemaManager.GetSchemaRegistry().GetRelationshipSchema(relationship.Type);
        if (schema == null) return;

        // Found a schema for this relationship type, validate the dynamic relationship against it
        var validatedProperties = new HashSet<string>();

        foreach (var (propertyName, propertySchema) in schema.Properties)
        {
            // Use the mapped property name from the schema (from PropertyAttribute.Label)
            var mappedPropertyName = propertySchema.Name;

            // Check if the property exists in the dynamic relationship's properties
            if (!relationship.Properties.TryGetValue(mappedPropertyName, out var value))
            {
                // Property doesn't exist in dynamic relationship
                if (propertySchema.IsRequired)
                {
                    throw new GraphException($"Property '{mappedPropertyName}' on {relationship.Type} is required but not provided in DynamicRelationship.");
                }
                continue;
            }

            // Mark this property as validated
            validatedProperties.Add(mappedPropertyName);

            // Validate required fields
            if (propertySchema.IsRequired)
            {
                if (value == null || (value is string stringValue && string.IsNullOrWhiteSpace(stringValue)))
                {
                    throw new GraphException($"Property '{mappedPropertyName}' on {relationship.Type} is required and cannot be null or empty.");
                }
            }

            // Validate custom validation rules
            if (propertySchema.Validation is { } validation && value is not null)
            {
                ValidatePropertyValue(mappedPropertyName, value, validation, relationship.Type);
            }

            // Validate enum values
            if (value is not null)
            {
                ValidateEnumValue(mappedPropertyName, value, propertySchema.PropertyInfo.PropertyType, relationship.Type);
            }
        }

        // Check for extra properties that don't exist in the schema
        foreach (var propertyName in relationship.Properties.Keys)
        {
            if (!validatedProperties.Contains(propertyName))
            {
                throw new GraphException($"Property '{propertyName}' on {relationship.Type} is not defined in the schema and cannot be used.");
            }
        }
    }

    private static void ValidatePropertyValue(string propertyName, object value, PropertyValidation validation, string entityLabel)
    {
        // MinValue validation
        if (validation.MinValue is not null)
        {
            if (value is IComparable comparable)
            {
                if (comparable.CompareTo(validation.MinValue) < 0)
                {
                    throw new GraphException($"Property '{propertyName}' on {entityLabel} must be greater than or equal to {validation.MinValue}. Current value: {value}");
                }
            }
        }

        // MaxValue validation
        if (validation.MaxValue is not null)
        {
            if (value is IComparable comparable)
            {
                if (comparable.CompareTo(validation.MaxValue) > 0)
                {
                    throw new GraphException($"Property '{propertyName}' on {entityLabel} must be less than or equal to {validation.MaxValue}. Current value: {value}");
                }
            }
        }

        // MinLength validation
        if (validation.MinLength is not null)
        {
            if (value is string stringValue)
            {
                if (stringValue.Length < validation.MinLength)
                {
                    throw new GraphException($"Property '{propertyName}' on {entityLabel} must have a minimum length of {validation.MinLength.Value}. Current length: {stringValue.Length}");
                }
            }
        }

        // MaxLength validation
        if (validation.MaxLength is not null)
        {
            if (value is string stringValue)
            {
                if (stringValue.Length > validation.MaxLength)
                {
                    throw new GraphException($"Property '{propertyName}' on {entityLabel} must have a maximum length of {validation.MaxLength.Value}. Current length: {stringValue.Length}");
                }
            }
        }

        // Pattern validation
        if (!string.IsNullOrEmpty(validation.Pattern))
        {
            if (value is string stringValue)
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(stringValue, validation.Pattern))
                {
                    throw new GraphException($"Property '{propertyName}' on {entityLabel} must match the pattern '{validation.Pattern}'. Current value: {stringValue}");
                }
            }
        }
    }

    private static void ValidateEnumValue(string propertyName, object value, Type propertyType, string entityLabel)
    {
        // Check if the property type is an enum
        if (propertyType.IsEnum)
        {
            // If the value is a string, try to parse it as the enum
            if (value is string stringValue)
            {
                if (!Enum.TryParse(propertyType, stringValue, ignoreCase: true, out _))
                {
                    var validValues = string.Join(", ", Enum.GetNames(propertyType));
                    throw new GraphException($"Property '{propertyName}' on {entityLabel} must be a valid enum value. Valid values are: {validValues}. Current value: {stringValue}");
                }
            }
            // If the value is not a string, check if it can be converted to the enum
            else if (!Enum.IsDefined(propertyType, value))
            {
                var validValues = string.Join(", ", Enum.GetNames(propertyType));
                throw new GraphException($"Property '{propertyName}' on {entityLabel} must be a valid enum value. Valid values are: {validValues}. Current value: {value}");
            }
        }
    }

    private static async Task<IRecord> GetSingleRecordAsync(IResultCursor result, CancellationToken cancellationToken)
    {
        return await result.SingleAsync(cancellationToken).ConfigureAwait(false);
    }
}
