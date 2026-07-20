// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Entities;

using System.Reflection;
using Cvoya.Graph.Neo4j.Core;
using Cvoya.Graph.Neo4j.Querying.Cypher;
using Cvoya.Graph.Neo4j.Serialization;
using Cvoya.Graph.Serialization;
using global::Neo4j.Driver;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;


/// <summary>
/// Manages CRUD operations for relationships in Neo4j.
/// All methods assume that there is already a transaction in progress.
/// </summary>
internal sealed class Neo4jRelationshipManager(GraphContext context)
{
    private const string RelationshipIdentityChangeMessage =
        "Relationship type or concrete CLR type cannot be changed on update; delete and recreate the relationship.";

    private readonly ILogger<Neo4jRelationshipManager> _logger = context.LoggerFactory?.CreateLogger<Neo4jRelationshipManager>()
        ?? NullLogger<Neo4jRelationshipManager>.Instance;
    private readonly EntityFactory _serializer = new();

    private static readonly string[] _ignoredProperties =
    [
        nameof(Graph.IRelationship.StartNodeId),
        nameof(Graph.IRelationship.EndNodeId)
    ];

    public async Task<TRelationship> CreateRelationshipAsync<TRelationship>(
        TRelationship relationship,
        GraphTransaction transaction,
        CancellationToken cancellationToken = default)
        where TRelationship : class, Graph.IRelationship
    {
        ArgumentNullException.ThrowIfNull(relationship);

        _logger.LogDebugNeo4jRelationshipManager39(typeof(TRelationship).Name, relationship.StartNodeId, relationship.EndNodeId);

        try
        {
            // Validate no reference cycles
            GraphDataModel.EnsureNoReferenceCycle(relationship);
            GraphDataModel.EnsureComplexPropertyDepth(relationship);

            // Validate property constraints at application level
            ValidateRelationshipProperties(relationship);

            // Serialize the relationship
            var entity = _serializer.Serialize(relationship);

            // Validate that relationships don't have complex properties
            if (entity.ComplexProperties.Count > 0)
            {
                throw new GraphException(
                    $"Relationships cannot have complex properties. Found {entity.ComplexProperties.Count} complex properties on {typeof(TRelationship).Name}");
            }

            // Create the relationship
            var created = await CreateRelationshipInGraphAsync(
                entity,
                relationship.StartNodeId,
                relationship.EndNodeId,
                LegacyRelationshipEndpoints.LegacyDirection(relationship),
                transaction.Transaction,
                cancellationToken).ConfigureAwait(false);

            if (!created)
            {
                throw new GraphException(
                    $"Failed to create relationship of type {typeof(TRelationship).Name} from {relationship.StartNodeId} to {relationship.EndNodeId}. " +
                    "One or both nodes may not exist.");
            }

            _logger.LogInformationNeo4jRelationshipManager77(typeof(TRelationship).Name, relationship.Id);

            return relationship;
        }
        catch (Exception ex) when (ex is not GraphException and not OperationCanceledException)
        {
            _logger.LogErrorNeo4jRelationshipManager84(ex, typeof(TRelationship).Name);
            throw new GraphException($"Failed to create relationship: {ex.Message}", ex);
        }
    }

    public async Task<bool> UpdateRelationshipAsync<TRelationship>(
        TRelationship relationship,
        GraphTransaction transaction,
        CancellationToken cancellationToken = default)
        where TRelationship : class, Graph.IRelationship
    {
        ArgumentNullException.ThrowIfNull(relationship);

        _logger.LogDebugNeo4jRelationshipManager97(typeof(TRelationship).Name, relationship.Id);

        try
        {
            var direction = LegacyRelationshipEndpoints.LegacyDirection(relationship);

            // Validate no reference cycles
            GraphDataModel.EnsureNoReferenceCycle(relationship);
            GraphDataModel.EnsureComplexPropertyDepth(relationship);

            // Serialize the relationship
            var entity = _serializer.Serialize(relationship);

            // Validate that relationships don't have complex properties
            if (entity.ComplexProperties.Count > 0)
            {
                throw new GraphException(
                    $"Relationships cannot have complex properties. Found {entity.ComplexProperties.Count} complex properties on {typeof(TRelationship).Name}");
            }

            // Update the relationship properties
            var updated = await UpdateRelationshipPropertiesAsync(
                relationship.Id,
                entity,
                direction,
                transaction.Transaction,
                cancellationToken).ConfigureAwait(false);

            if (!updated.Exists)
            {
                _logger.LogWarningNeo4jRelationshipManager126(relationship.Id);
                throw new EntityNotFoundException($"Relationship with ID {relationship.Id} not found for update");
            }

            if (!updated.DirectionMatches)
            {
                throw new GraphException(
                    "Direction cannot be changed on update; delete and recreate the relationship. " +
                    $"Stored direction is {updated.StoredDirection}; incoming direction is {direction}.");
            }

            if (!updated.PhysicalTypeMatches || !updated.LogicalTypeMatches || !updated.ClrTypeMatches)
            {
                var incomingClrType = SerializationBridge.GetAssemblyQualifiedTypeName(entity.ActualType);
                var storedClrIdentity = updated.StoredClrMetadata ?? updated.StoredLegacyClrLabel ?? "<missing>";
                throw new GraphException(
                    $"{RelationshipIdentityChangeMessage} " +
                    $"Stored physical type is '{updated.StoredPhysicalType}', logical type is '{updated.StoredLogicalType}', " +
                    $"and CLR identity is '{storedClrIdentity}'; incoming relationship type is '{entity.Label}' " +
                    $"and CLR type is '{incomingClrType}'.");
            }

            // Validate property constraints after confirming the target row exists. If validation
            // fails, the transaction rolls back the guarded update statement above.
            ValidateRelationshipProperties(relationship);

            _logger.LogInformationNeo4jRelationshipManager141(typeof(TRelationship).Name, relationship.Id);

            return true;
        }
        catch (Exception ex) when (ex is not GraphException and not OperationCanceledException)
        {
            _logger.LogErrorNeo4jRelationshipManager148(ex, relationship.Id, typeof(TRelationship).Name);
            throw new GraphException($"Failed to update relationship: {ex.Message}", ex);
        }
    }

    public async Task<bool> DeleteRelationshipAsync(
        string relationshipId,
        GraphTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(relationshipId);

        _logger.LogDebugNeo4jRelationshipManager161(relationshipId);

        try
        {
            var cypher = "MATCH ()-[r {Id: $relId}]-() DELETE r RETURN COUNT(r) AS deletedCount";

            var result = await transaction.Transaction.RunAsync(
                cypher,
                new { relId = relationshipId }).ConfigureAwait(false);

            var record = await GetSingleRecordAsync(result, cancellationToken).ConfigureAwait(false);
            var deletedCount = record["deletedCount"].As<int>();

            if (deletedCount == 0)
            {
                _logger.LogWarningNeo4jRelationshipManager176(relationshipId);
                throw new EntityNotFoundException($"Relationship with ID {relationshipId} not found for deletion");
            }

            _logger.LogInformationNeo4jRelationshipManager180(relationshipId);
            return true;
        }
        catch (Exception ex) when (ex is not GraphException and not OperationCanceledException)
        {
            _logger.LogErrorNeo4jRelationshipManager185(ex, relationshipId);
            throw new GraphException($"Failed to delete relationship: {ex.Message}", ex);
        }
    }

    private static async Task<bool> CreateRelationshipInGraphAsync(
        EntityInfo entity,
        string startNodeId,
        string endNodeId,
        RelationshipDirection direction,
        IAsyncTransaction transaction,
        CancellationToken cancellationToken)
    {
        // entity.Label is a relationship type, not a Cypher parameter value: it must be validated
        // and escaped before interpolation, since it can originate from caller-supplied input
        // (DynamicRelationship.Type is set at runtime and cannot be parameterized by the driver).
        var relationshipType = CypherIdentifier.Escape(entity.Label, "relationship type");

        var (sourceNodeId, targetNodeId) = direction switch
        {
            RelationshipDirection.Outgoing => (startNodeId, endNodeId),
            RelationshipDirection.Incoming => (endNodeId, startNodeId),
            _ => throw new GraphException($"Unsupported relationship direction '{direction}'.")
        };

        var cypher = $@"
            MATCH (source {{Id: $sourceNodeId}})
            MATCH (target {{Id: $targetNodeId}})
            CREATE (source)-[r:{relationshipType} $props]->(target)
            RETURN r IS NOT NULL AS created";

        var properties = BuildRelationshipProperties(entity);

        var result = await transaction.RunAsync(cypher, new
        {
            sourceNodeId,
            targetNodeId,
            props = properties
        }).ConfigureAwait(false);

        var record = await GetSingleRecordAsync(result, cancellationToken).ConfigureAwait(false);
        return record["created"].As<bool>();
    }


    private static async Task<(
        bool Exists,
        bool DirectionMatches,
        bool PhysicalTypeMatches,
        bool LogicalTypeMatches,
        bool ClrTypeMatches,
        RelationshipDirection StoredDirection,
        string? StoredPhysicalType,
        string? StoredLogicalType,
        string? StoredClrMetadata,
        string? StoredLegacyClrLabel)> UpdateRelationshipPropertiesAsync(
        string relationshipId,
        EntityInfo entity,
        RelationshipDirection incomingDirection,
        IAsyncTransaction transaction,
        CancellationToken cancellationToken)
    {
        var cypher = @"
            OPTIONAL MATCH ()-[r {Id: $relId}]->()
            WITH r,
                 r.Direction AS storedDirection,
                 type(r) AS storedPhysicalType,
                 r.Type AS storedLogicalType,
                 r.__metadata__ AS storedClrMetadata,
                 head(r.Labels) AS storedLegacyClrLabel
            WITH r,
                 storedDirection,
                 storedPhysicalType,
                 storedLogicalType,
                 storedClrMetadata,
                 storedLegacyClrLabel,
                 CASE
                     WHEN r IS NULL THEN false
                     WHEN r.Direction IS NULL THEN $incomingDirection = $defaultDirection
                     ELSE toString(r.Direction) = $incomingDirection
                 END AS directionMatches,
                 CASE
                     WHEN r IS NULL THEN false
                     ELSE storedPhysicalType = $incomingStorageType
                 END AS physicalTypeMatches,
                 CASE
                     WHEN r IS NULL THEN false
                     WHEN storedLogicalType IS NULL OR storedLogicalType = '' THEN true
                     ELSE storedLogicalType = $incomingStorageType
                 END AS logicalTypeMatches,
                 CASE
                     WHEN r IS NULL THEN false
                     WHEN storedClrMetadata IS NULL THEN $allowLegacyClrLabel AND storedLegacyClrLabel = $incomingClrLabel
                     ELSE storedClrMetadata = $incomingClrType
                 END AS clrTypeMatches
            FOREACH (_ IN CASE WHEN r IS NOT NULL
                                      AND directionMatches
                                      AND physicalTypeMatches
                                      AND logicalTypeMatches
                                      AND clrTypeMatches THEN [1] ELSE [] END |
                SET r = $props, r.Direction = storedDirection)
            RETURN r IS NOT NULL AS exists,
                   directionMatches AS directionMatches,
                   physicalTypeMatches AS physicalTypeMatches,
                   logicalTypeMatches AS logicalTypeMatches,
                   clrTypeMatches AS clrTypeMatches,
                   storedDirection AS storedDirection,
                   storedPhysicalType AS storedPhysicalType,
                   storedLogicalType AS storedLogicalType,
                   storedClrMetadata AS storedClrMetadata,
                   storedLegacyClrLabel AS storedLegacyClrLabel";

        var properties = BuildRelationshipProperties(entity);
        var incomingClrType = SerializationBridge.GetAssemblyQualifiedTypeName(entity.ActualType);
        var incomingClrLabel = Labels.GetLabelFromType(entity.ActualType);

        var result = await transaction.RunAsync(cypher, new
        {
            relId = relationshipId,
            props = properties,
            incomingDirection = incomingDirection.ToString(),
            defaultDirection = RelationshipDirection.Outgoing.ToString(),
            incomingStorageType = entity.Label,
            incomingClrType,
            incomingClrLabel,
            allowLegacyClrLabel = !entity.ActualType.IsConstructedGenericType
        }).ConfigureAwait(false);

        var record = await GetSingleRecordAsync(result, cancellationToken).ConfigureAwait(false);
        var exists = record["exists"].As<bool>();
        var directionMatches = record["directionMatches"].As<bool>();
        var physicalTypeMatches = record["physicalTypeMatches"].As<bool>();
        var logicalTypeMatches = record["logicalTypeMatches"].As<bool>();
        var clrTypeMatches = record["clrTypeMatches"].As<bool>();
        var storedDirectionValue = record["storedDirection"];
        return (
            exists,
            directionMatches,
            physicalTypeMatches,
            logicalTypeMatches,
            clrTypeMatches,
            ToRelationshipDirection(storedDirectionValue),
            record["storedPhysicalType"] as string,
            record["storedLogicalType"] as string,
            record["storedClrMetadata"] as string,
            record["storedLegacyClrLabel"] as string);
    }

    internal static Dictionary<string, object?> BuildRelationshipProperties(EntityInfo entity)
    {
        var properties = SerializationHelpers.SerializeSimpleProperties(entity)
            .Where(kv => !_ignoredProperties.Contains(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        properties[nameof(Graph.IRelationship.Type)] = entity.Label;
        properties[SerializationBridge.MetadataPropertyName] =
            SerializationBridge.GetAssemblyQualifiedTypeName(entity.ActualType);
        return properties;
    }

    private static RelationshipDirection ToRelationshipDirection(object? value)
    {
        return value switch
        {
            RelationshipDirection direction when Enum.IsDefined(direction) => direction,
            string directionString when Enum.TryParse<RelationshipDirection>(directionString, out var parsedDirection) &&
                Enum.IsDefined(parsedDirection) => parsedDirection,
            not null when Enum.TryParse<RelationshipDirection>(value.ToString(), out var parsedDirection) &&
                Enum.IsDefined(parsedDirection) => parsedDirection,
            _ => RelationshipDirection.Outgoing
        };
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
