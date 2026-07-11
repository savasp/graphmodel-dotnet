// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Entities;

using System.Reflection;
using Cvoya.Graph.Age.Core;
using Cvoya.Graph.Age.Querying.Cypher;
using Cvoya.Graph.Age.Querying.Cypher.Execution;
using Cvoya.Graph.Age.Serialization;
using Cvoya.Graph.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;


/// <summary>
/// Manages CRUD operations for relationships in Age.
/// All methods assume that there is already a transaction in progress.
/// </summary>
internal sealed class AgeRelationshipManager(AgeGraphContext context)
{
    private readonly ILogger<AgeRelationshipManager> _logger = context.LoggerFactory?.CreateLogger<AgeRelationshipManager>()
        ?? NullLogger<AgeRelationshipManager>.Instance;
    private readonly EntityFactory _serializer = new();

    private static readonly string[] _ignoredProperties =
    [
        nameof(Graph.IRelationship.StartNodeId),
        nameof(Graph.IRelationship.EndNodeId)
    ];

    public async Task<TRelationship> CreateRelationshipAsync<TRelationship>(
        TRelationship relationship,
        AgeGraphTransaction transaction,
        CancellationToken cancellationToken = default)
        where TRelationship : class, Graph.IRelationship
    {
        ArgumentNullException.ThrowIfNull(relationship);

        _logger.LogDebug("Creating relationship of type {RelationshipType} from {StartNodeId} to {EndNodeId}",
            typeof(TRelationship).Name, relationship.StartNodeId, relationship.EndNodeId);

        try
        {
            // Validate no reference cycles
            GraphDataModel.EnsureNoReferenceCycle(relationship);
            GraphDataModel.EnsureComplexPropertyDepth(relationship);

            // Validate property constraints at application level
            ValidateRelationshipProperties(relationship);

            // Serialize the relationship
            var entity = _serializer.Serialize(relationship);

            await ValidateRelationshipUniquenessAsync(
                relationship, transaction.Runner, excludeId: null, cancellationToken).ConfigureAwait(false);

            if (await RelationshipExistsAsync(relationship.Id, transaction.Runner, cancellationToken).ConfigureAwait(false))
            {
                throw new GraphException($"Relationship with ID '{relationship.Id}' already exists.");
            }

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
                relationship.Direction,
                transaction.Runner,
                cancellationToken).ConfigureAwait(false);

            if (!created)
            {
                throw new GraphException(
                    $"Failed to create relationship of type {typeof(TRelationship).Name} from {relationship.StartNodeId} to {relationship.EndNodeId}. " +
                    "One or both nodes may not exist.");
            }

            _logger.LogInformation("Created relationship of type {RelationshipType} with ID {RelationshipId}",
                typeof(TRelationship).Name, relationship.Id);

            return relationship;
        }
        catch (Exception ex) when (ex is not GraphException and not OperationCanceledException)
        {
            _logger.LogError(ex, "Error creating relationship of type {RelationshipType}", typeof(TRelationship).Name);
            throw new GraphException("Failed to create relationship.", ex);
        }
    }

    public async Task<bool> UpdateRelationshipAsync<TRelationship>(
        TRelationship relationship,
        AgeGraphTransaction transaction,
        CancellationToken cancellationToken = default)
        where TRelationship : class, Graph.IRelationship
    {
        ArgumentNullException.ThrowIfNull(relationship);

        _logger.LogDebug("Updating relationship of type {RelationshipType} with ID {RelationshipId}",
            typeof(TRelationship).Name, relationship.Id);

        try
        {
            // Validate no reference cycles
            GraphDataModel.EnsureNoReferenceCycle(relationship);
            GraphDataModel.EnsureComplexPropertyDepth(relationship);

            // Serialize the relationship
            var entity = _serializer.Serialize(relationship);

            await ValidateRelationshipUniquenessAsync(
                relationship, transaction.Runner, relationship.Id, cancellationToken).ConfigureAwait(false);

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
                relationship.Direction,
                transaction.Runner,
                cancellationToken).ConfigureAwait(false);

            if (!updated.Exists)
            {
                _logger.LogWarning("Relationship with ID {RelationshipId} not found for update", relationship.Id);
                throw new EntityNotFoundException($"Relationship with ID {relationship.Id} not found for update");
            }

            if (!updated.DirectionMatches)
            {
                throw new GraphException(
                    "Direction cannot be changed on update; delete and recreate the relationship. " +
                    $"Stored direction is {updated.StoredDirection}; incoming direction is {relationship.Direction}.");
            }

            // Validate property constraints after confirming the target row exists. If validation
            // fails, the transaction rolls back the guarded update statement above.
            ValidateRelationshipProperties(relationship);

            _logger.LogInformation("Updated relationship of type {RelationshipType} with ID {RelationshipId}",
                typeof(TRelationship).Name, relationship.Id);

            return true;
        }
        catch (Exception ex) when (ex is not GraphException and not OperationCanceledException)
        {
            _logger.LogError(ex, "Error updating relationship {RelationshipId} of type {RelationshipType}",
                relationship.Id, typeof(TRelationship).Name);
            throw new GraphException("Failed to update relationship.", ex);
        }
    }

    public async Task<bool> DeleteRelationshipAsync(
        string relationshipId,
        AgeGraphTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(relationshipId);

        _logger.LogDebug("Deleting relationship with ID {RelationshipId}", relationshipId);

        try
        {
            var cypher = "MATCH ()-[r {Id: $relId}]-() DELETE r RETURN COUNT(r) AS deletedCount";

            var result = await transaction.Runner.RunAsync(
                cypher,
                new { relId = relationshipId }, cancellationToken).ConfigureAwait(false);

            var record = await GetSingleRecordAsync(result, cancellationToken).ConfigureAwait(false);
            var deletedCount = record["deletedCount"].As<int>();

            if (deletedCount == 0)
            {
                _logger.LogWarning("Relationship with ID {RelationshipId} not found for deletion", relationshipId);
                throw new EntityNotFoundException($"Relationship with ID {relationshipId} not found for deletion");
            }

            _logger.LogInformation("Deleted relationship with ID {RelationshipId}", relationshipId);
            return true;
        }
        catch (Exception ex) when (ex is not GraphException and not OperationCanceledException)
        {
            _logger.LogError(ex, "Error deleting relationship with ID {RelationshipId}", relationshipId);
            throw new GraphException("Failed to delete relationship.", ex);
        }
    }

    private async Task<bool> CreateRelationshipInGraphAsync(
        EntityInfo entity,
        string startNodeId,
        string endNodeId,
        RelationshipDirection direction,
        AgeQueryRunner transaction,
        CancellationToken cancellationToken)
    {
        var (sourceNodeId, targetNodeId) = direction switch
        {
            RelationshipDirection.Outgoing => (startNodeId, endNodeId),
            RelationshipDirection.Incoming => (endNodeId, startNodeId),
            _ => throw new GraphException($"Unsupported relationship direction '{direction}'.")
        };

        var properties = SerializationHelpers.SerializeSimpleProperties(entity)
            .Where(kv => !_ignoredProperties.Contains(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        properties[nameof(Graph.IRelationship.Type)] = entity.Label;
        properties["inheritance_labels"] = GetInheritanceLabels(entity.ActualType);
        var parameters = new Dictionary<string, object?>
        {
            ["sourceNodeId"] = sourceNodeId,
            ["targetNodeId"] = targetNodeId,
        };
        var setClause = AgeCypherProperties.BuildSetClause("r", properties, parameters, "relationshipProperty");
        var cypher = $@"
            MATCH (source {{Id: $sourceNodeId}})
            MATCH (target {{Id: $targetNodeId}})
            CREATE (source)-[r:{SerializationBridge.PhysicalRelationshipType}]->(target)
            {setClause}
            RETURN r IS NOT NULL AS created";

        var result = await transaction.RunAsync(cypher, parameters, cancellationToken).ConfigureAwait(false);

        var record = await GetSingleRecordAsync(result, cancellationToken).ConfigureAwait(false);
        return record["created"].As<bool>();
    }

    private static async Task<bool> RelationshipExistsAsync(
        string id,
        AgeQueryRunner transaction,
        CancellationToken cancellationToken)
    {
        var result = await transaction.RunAsync(
            "MATCH ()-[r {Id: $relationshipId}]-() RETURN count(r) AS existingCount",
            new { relationshipId = id }, cancellationToken).ConfigureAwait(false);
        return (await result.SingleAsync(cancellationToken).ConfigureAwait(false))["existingCount"].As<long>() > 0;
    }


    private async Task<(bool Exists, bool DirectionMatches, RelationshipDirection StoredDirection)> UpdateRelationshipPropertiesAsync(
        string relationshipId,
        EntityInfo entity,
        RelationshipDirection incomingDirection,
        AgeQueryRunner transaction,
        CancellationToken cancellationToken)
    {
        var properties = SerializationHelpers.SerializeSimpleProperties(entity)
            .Where(kv => !_ignoredProperties.Contains(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value);
        properties[nameof(Graph.IRelationship.Type)] = entity.Label;
        properties["inheritance_labels"] = GetInheritanceLabels(entity.ActualType);
        var lookup = await transaction.RunAsync(
            "MATCH ()-[r {Id: $relId}]->() RETURN r.Direction AS storedDirection",
            new { relId = relationshipId }, cancellationToken).ConfigureAwait(false);
        var lookupRecords = await lookup.ToListAsync(cancellationToken).ConfigureAwait(false);
        if (lookupRecords.Count == 0)
        {
            return (false, false, RelationshipDirection.Outgoing);
        }

        var lookupRecord = lookupRecords[0];
        var storedDirection = ToRelationshipDirection(lookupRecord["storedDirection"]);
        var directionMatches = storedDirection == incomingDirection;
        if (!directionMatches)
        {
            return (true, false, storedDirection);
        }

        var parameters = new Dictionary<string, object?>
        {
            ["relId"] = relationshipId,
        };
        var setClause = AgeCypherProperties.BuildSetClause("r", properties, parameters, "relationshipProperty");
        var cypher = $@"
            MATCH ()-[r {{Id: $relId}}]->()
            {setClause}
            RETURN true AS updated";

        var result = await transaction.RunAsync(cypher, parameters, cancellationToken).ConfigureAwait(false);
        _ = await GetSingleRecordAsync(result, cancellationToken).ConfigureAwait(false);
        return (true, true, storedDirection);
    }

    private static RelationshipDirection ToRelationshipDirection(object? value)
    {
        if (value is RelationshipDirection direction && Enum.IsDefined(direction))
        {
            return direction;
        }

        var text = value.As<string>();
        return Enum.TryParse<RelationshipDirection>(text, ignoreCase: true, out var parsedDirection) &&
            Enum.IsDefined(parsedDirection)
                ? parsedDirection
                : RelationshipDirection.Outgoing;
    }

    private static IReadOnlyList<string> GetInheritanceLabels(Type actualType)
    {
        var labels = new List<string>();
        for (var type = actualType; type is not null && typeof(Graph.IRelationship).IsAssignableFrom(type); type = type.BaseType)
        {
            if (type == typeof(Graph.Relationship) || type == typeof(object))
            {
                break;
            }

            labels.Add(Labels.GetLabelFromType(type));
        }

        return labels.Distinct(StringComparer.Ordinal).ToArray();
    }

    private void ValidateRelationshipProperties<TRelationship>(TRelationship relationship) where TRelationship : class, Graph.IRelationship
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
            if (propertySchema.IsRequired &&
                (value == null || (value is string stringValue && string.IsNullOrWhiteSpace(stringValue))))
            {
                throw new GraphException($"Property '{propertyName}' on {type} is required and cannot be null or empty.");
            }

            // Validate custom validation rules
            if (propertySchema.Validation is { } validation && value is not null)
            {
                ValidatePropertyValue(propertyName, value, validation, type);
            }
        }
    }

    private async Task ValidateRelationshipUniquenessAsync<TRelationship>(
        TRelationship relationship,
        AgeQueryRunner transaction,
        string? excludeId,
        CancellationToken cancellationToken)
        where TRelationship : class, Graph.IRelationship
    {
        if (relationship is DynamicRelationship)
        {
            return;
        }

        var type = Labels.GetLabelFromType(relationship.GetType());
        var schema = context.SchemaManager.GetSchemaRegistry().GetRelationshipSchema(type);
        if (schema is null)
        {
            return;
        }

        var keyProperties = schema.GetKeyProperties().ToArray();
        if (keyProperties.Length > 0)
        {
            await AssertNoMatchingRelationshipAsync(keyProperties, "composite key", type).ConfigureAwait(false);
        }

        foreach (var property in schema.Properties.Values.Where(property => property.IsUnique && !property.IsKey))
        {
            await AssertNoMatchingRelationshipAsync([property], $"unique property '{property.Name}'", type)
                .ConfigureAwait(false);
        }

        async Task AssertNoMatchingRelationshipAsync(
            IReadOnlyList<PropertySchemaInfo> properties,
            string constraint,
            string relationshipType)
        {
            var parameters = new Dictionary<string, object?>
            {
                ["relationshipType"] = relationshipType,
                ["excludeId"] = excludeId,
            };
            var predicates = new List<string>
            {
                "$relationshipType IN coalesce(r.inheritance_labels, [])",
            };
            if (excludeId is not null)
            {
                predicates.Add("r.Id <> $excludeId");
            }

            for (var index = 0; index < properties.Count; index++)
            {
                var property = properties[index];
                var parameterName = $"uniqueValue{index}";
                parameters[parameterName] = SerializationBridge.ToAgeValue(property.PropertyInfo.GetValue(relationship));
                predicates.Add($"r.{CypherIdentifier.Escape(property.Name, "property name")} = ${parameterName}");
            }

            var result = await transaction.RunAsync(
                $"MATCH ()-[r]->() WHERE {string.Join(" AND ", predicates)} RETURN count(r) AS duplicateCount",
                parameters,
                cancellationToken).ConfigureAwait(false);
            var count = (await result.SingleAsync(cancellationToken).ConfigureAwait(false))["duplicateCount"].As<long>();
            if (count > 0)
            {
                throw new GraphException($"Relationship '{relationshipType}' violates {constraint} uniqueness.");
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
            if (propertySchema.IsRequired &&
                (value == null || (value is string stringValue && string.IsNullOrWhiteSpace(stringValue))))
            {
                throw new GraphException($"Property '{mappedPropertyName}' on {relationship.Type} is required and cannot be null or empty.");
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
        var extraProperty = relationship.Properties.Keys.FirstOrDefault(propertyName => !validatedProperties.Contains(propertyName));
        if (extraProperty is not null)
        {
            throw new GraphException($"Property '{extraProperty}' on {relationship.Type} is not defined in the schema and cannot be used.");
        }
    }

    private void ValidatePropertyValue(string propertyName, object value, PropertyValidation validation, string entityLabel)
    {
        // MinValue validation
        if (validation.MinValue is not null &&
            value is IComparable minComparable &&
            minComparable.CompareTo(validation.MinValue) < 0)
        {
            throw new GraphException($"Property '{propertyName}' on {entityLabel} must be greater than or equal to {validation.MinValue}. Current value: {value}");
        }

        // MaxValue validation
        if (validation.MaxValue is not null &&
            value is IComparable maxComparable &&
            maxComparable.CompareTo(validation.MaxValue) > 0)
        {
            throw new GraphException($"Property '{propertyName}' on {entityLabel} must be less than or equal to {validation.MaxValue}. Current value: {value}");
        }

        // MinLength validation
        if (validation.MinLength is not null &&
            value is string minLengthValue &&
            minLengthValue.Length < validation.MinLength)
        {
            throw new GraphException($"Property '{propertyName}' on {entityLabel} must have a minimum length of {validation.MinLength.Value}. Current length: {minLengthValue.Length}");
        }

        // MaxLength validation
        if (validation.MaxLength is not null &&
            value is string maxLengthValue &&
            maxLengthValue.Length > validation.MaxLength)
        {
            throw new GraphException($"Property '{propertyName}' on {entityLabel} must have a maximum length of {validation.MaxLength.Value}. Current length: {maxLengthValue.Length}");
        }

        // Pattern validation
        if (!string.IsNullOrEmpty(validation.Pattern) &&
            value is string patternValue &&
            !System.Text.RegularExpressions.Regex.IsMatch(patternValue, validation.Pattern))
        {
            throw new GraphException($"Property '{propertyName}' on {entityLabel} must match the pattern '{validation.Pattern}'. Current value: {patternValue}");
        }
    }

    private void ValidateEnumValue(string propertyName, object value, Type propertyType, string entityLabel)
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

    private static async Task<AgeRecord> GetSingleRecordAsync(AgeResultCursor result, CancellationToken cancellationToken)
    {
        return await result.SingleAsync(cancellationToken).ConfigureAwait(false);
    }
}
