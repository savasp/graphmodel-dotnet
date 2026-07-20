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
    private const string RelationshipIdentityChangeMessage =
        "Relationship type or concrete CLR type cannot be changed on update; delete and recreate the relationship.";

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

        _logger.LogDebugAgeRelationshipManager40(typeof(TRelationship).Name, relationship.StartNodeId, relationship.EndNodeId);

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

            _logger.LogInformationAgeRelationshipManager86(typeof(TRelationship).Name, relationship.Id);

            return relationship;
        }
        catch (Exception ex) when (ex is not GraphException and not OperationCanceledException)
        {
            _logger.LogErrorAgeRelationshipManager93(ex, typeof(TRelationship).Name);
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

        _logger.LogDebugAgeRelationshipManager106(typeof(TRelationship).Name, relationship.Id);

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
                _logger.LogWarningAgeRelationshipManager138(relationship.Id);
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

            _logger.LogInformationAgeRelationshipManager153(typeof(TRelationship).Name, relationship.Id);

            return true;
        }
        catch (Exception ex) when (ex is not GraphException and not OperationCanceledException)
        {
            _logger.LogErrorAgeRelationshipManager160(ex, relationship.Id, typeof(TRelationship).Name);
            throw new GraphException("Failed to update relationship.", ex);
        }
    }

    public async Task<bool> DeleteRelationshipAsync(
        string relationshipId,
        AgeGraphTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(relationshipId);

        _logger.LogDebugAgeRelationshipManager173(relationshipId);

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
                _logger.LogWarningAgeRelationshipManager188(relationshipId);
                throw new EntityNotFoundException($"Relationship with ID {relationshipId} not found for deletion");
            }

            _logger.LogInformationAgeRelationshipManager192(relationshipId);
            return true;
        }
        catch (Exception ex) when (ex is not GraphException and not OperationCanceledException)
        {
            _logger.LogErrorAgeRelationshipManager197(ex, relationshipId);
            throw new GraphException("Failed to delete relationship.", ex);
        }
    }

    private static async Task<bool> CreateRelationshipInGraphAsync(
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

        var properties = BuildRelationshipProperties(entity);
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


    private static async Task<(bool Exists, bool DirectionMatches, RelationshipDirection StoredDirection)> UpdateRelationshipPropertiesAsync(
        string relationshipId,
        EntityInfo entity,
        RelationshipDirection incomingDirection,
        AgeQueryRunner transaction,
        CancellationToken cancellationToken)
    {
        var properties = BuildRelationshipProperties(entity);
        var incomingMetadata = SerializationBridge.CreateScalarMetadata(entity.ActualType);
        var incomingCanonicalType = Labels.GetLabelFromType(entity.ActualType);
        var lookup = await transaction.RunAsync(
            $@"
            MATCH ()-[r:{SerializationBridge.PhysicalRelationshipType} {{Id: $relId}}]->()
            RETURN r.Type AS storedType,
                   r.Direction AS storedDirection,
                   r.{SerializationBridge.MetadataPropertyName} AS storedMetadata,
                   head(r.inheritance_labels) AS storedCanonicalType",
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

        var storedType = lookupRecord["storedType"].As<string>();
        var storedCanonicalType = lookupRecord["storedCanonicalType"].As<string>();
        string? storedMetadata;
        try
        {
            storedMetadata = lookupRecord["storedMetadata"].As<string>();
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException)
        {
            throw RelationshipIdentityChanged(
                storedType,
                storedMetadata: "<invalid>",
                storedCanonicalType,
                entity.Label,
                incomingMetadata);
        }

        if (!string.Equals(storedType, entity.Label, StringComparison.Ordinal))
        {
            throw RelationshipIdentityChanged(
                storedType,
                storedMetadata,
                storedCanonicalType,
                entity.Label,
                incomingMetadata);
        }

        var allowLegacyClrLabel = !entity.ActualType.IsConstructedGenericType;
        if (storedMetadata is null)
        {
            if (!allowLegacyClrLabel ||
                !string.Equals(storedCanonicalType, incomingCanonicalType, StringComparison.Ordinal))
            {
                throw RelationshipIdentityChanged(
                    storedType,
                    storedMetadata,
                    storedCanonicalType,
                    entity.Label,
                    incomingMetadata);
            }
        }
        else if (!string.Equals(storedMetadata, incomingMetadata, StringComparison.Ordinal))
        {
            throw RelationshipIdentityChanged(
                storedType,
                storedMetadata,
                storedCanonicalType,
                entity.Label,
                incomingMetadata);
        }

        var parameters = new Dictionary<string, object?>
        {
            ["relId"] = relationshipId,
            ["incomingType"] = entity.Label,
            ["incomingDirection"] = incomingDirection.ToString(),
            ["defaultDirection"] = RelationshipDirection.Outgoing.ToString(),
            ["incomingMetadata"] = incomingMetadata,
            ["incomingCanonicalType"] = incomingCanonicalType,
            ["allowLegacyClrLabel"] = allowLegacyClrLabel,
        };
        var setClause = AgeCypherProperties.BuildSetClause("r", properties, parameters, "relationshipProperty");
        var cypher = $@"
            MATCH ()-[r:{SerializationBridge.PhysicalRelationshipType} {{Id: $relId}}]->()
            WHERE r.Type = $incomingType
              AND (r.Direction = $incomingDirection OR (r.Direction IS NULL AND $incomingDirection = $defaultDirection))
              AND (r.{SerializationBridge.MetadataPropertyName} = $incomingMetadata
                   OR (r.{SerializationBridge.MetadataPropertyName} IS NULL
                       AND $allowLegacyClrLabel
                       AND head(r.inheritance_labels) = $incomingCanonicalType))
            {setClause}
            RETURN true AS updated";

        var result = await transaction.RunAsync(cypher, parameters, cancellationToken).ConfigureAwait(false);
        var updateRecords = await result.ToListAsync(cancellationToken).ConfigureAwait(false);
        if (updateRecords.Count == 0)
        {
            throw new GraphException($"{RelationshipIdentityChangeMessage} The stored relationship identity changed before the update could be applied.");
        }

        return (true, true, storedDirection);
    }

    private static GraphException RelationshipIdentityChanged(
        string? storedType,
        string? storedMetadata,
        string? storedCanonicalType,
        string incomingType,
        string incomingMetadata) => new(
            $"{RelationshipIdentityChangeMessage} " +
            $"Stored logical type is '{storedType ?? "<null>"}', CLR metadata is '{storedMetadata ?? "<missing>"}', " +
            $"and legacy CLR label is '{storedCanonicalType ?? "<missing>"}'; " +
            $"incoming logical type is '{incomingType}' and CLR metadata is '{incomingMetadata}'.");

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

    internal static Dictionary<string, object?> BuildRelationshipProperties(EntityInfo entity)
    {
        var properties = SerializationHelpers.SerializeSimpleProperties(entity)
            .Where(kv => !_ignoredProperties.Contains(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value);
        properties[nameof(Graph.IRelationship.Type)] = entity.Label;
        properties[SerializationBridge.MetadataPropertyName] = SerializationBridge.CreateScalarMetadata(entity.ActualType);
        properties["inheritance_labels"] = GetInheritanceLabels(entity.ActualType);
        return properties;
    }

    private static string[] GetInheritanceLabels(Type actualType)
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
        var checks = BuildRelationshipUniquenessChecks(relationship, excludeId);

        // Take every lock before the first probe: holding them through the write that follows makes
        // probe-then-write atomic against a competing transaction claiming the same values.
        await transaction.AcquireUniquenessLocksAsync(
            checks.Select(check => check.LockKey).ToArray(),
            cancellationToken).ConfigureAwait(false);

        foreach (var check in checks)
        {
            var result = await transaction.RunAsync(
                check.Cypher,
                check.Parameters,
                cancellationToken).ConfigureAwait(false);
            var count = (await result.SingleAsync(cancellationToken).ConfigureAwait(false))["duplicateCount"].As<long>();
            if (count > 0)
            {
                throw new GraphException(check.ErrorMessage);
            }
        }
    }

    /// <summary>
    /// Builds the uniqueness probes for <paramref name="relationship"/>. A
    /// <see cref="DynamicRelationship"/> is checked against the registered schema of its type,
    /// reading values from its property bag by mapped storage name - the same resolution
    /// <see cref="ValidateDynamicRelationshipProperties"/> uses for required/validation rules, so a
    /// dynamic write cannot bypass constraints a typed write of the same type would honour.
    /// </summary>
    internal IReadOnlyList<AgeUniquenessCheck> BuildRelationshipUniquenessChecks<TRelationship>(
        TRelationship relationship,
        string? excludeId)
        where TRelationship : class, Graph.IRelationship
    {
        var dynamicRelationship = relationship as DynamicRelationship;
        var requestedType = dynamicRelationship?.Type ?? Labels.GetLabelFromType(relationship.GetType());
        var readValue = dynamicRelationship is null
            ? (Func<PropertySchemaInfo, object?>)(property => property.PropertyInfo.GetValue(relationship))
            : property => dynamicRelationship.Properties.GetValueOrDefault(property.Name);

        var schema = context.SchemaManager.GetSchemaRegistry().GetRelationshipSchema(requestedType);
        if (schema is null)
        {
            return [];
        }

        var type = schema.Label;
        var checks = new List<AgeUniquenessCheck>();
        var keyProperties = schema.GetKeyProperties().ToArray();
        if (keyProperties.Length > 0)
        {
            checks.Add(BuildCheck(keyProperties, "composite key"));
        }

        foreach (var property in schema.Properties.Values.Where(
                     property => property.IsUnique && (!property.IsKey || schema.HasCompositeKey())))
        {
            checks.Add(BuildCheck([property], $"unique property '{property.Name}'"));
        }

        return checks;

        AgeUniquenessCheck BuildCheck(
            IReadOnlyList<PropertySchemaInfo> properties,
            string constraint)
        {
            var parameters = new Dictionary<string, object?>
            {
                ["relationshipType"] = type,
                ["excludeId"] = excludeId,
            };
            var predicates = new List<string>
            {
                "size([age_type IN coalesce(r.inheritance_labels, []) " +
                    "WHERE toLower(age_type) = toLower($relationshipType)]) > 0",
            };
            if (excludeId is not null)
            {
                predicates.Add("r.Id <> $excludeId");
            }

            var values = new List<object?>(properties.Count);
            for (var index = 0; index < properties.Count; index++)
            {
                var property = properties[index];
                var parameterName = $"uniqueValue{index}";
                var value = SerializationBridge.ToAgeValue(readValue(property));
                parameters[parameterName] = value;
                values.Add(value);
                predicates.Add($"r.{CypherIdentifier.Escape(property.Name, "property name")} = ${parameterName}");
            }

            var constraintKey = AgeUniquenessCheck.BuildConstraintKey(type, constraint, values);
            return new AgeUniquenessCheck(
                $"MATCH ()-[r]->() WHERE {string.Join(" AND ", predicates)} RETURN count(r) AS duplicateCount",
                parameters,
                $"Relationship '{type}' violates {constraint} uniqueness.",
                constraintKey,
                AgeUniquenessLockKey.Compute(
                    context.GraphName,
                    AgeUniquenessLockKey.RelationshipEntityKind,
                    type,
                    constraint,
                    values));
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

    private static void ValidatePropertyValue(string propertyName, object value, PropertyValidation validation, string entityLabel)
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

    private static async Task<AgeRecord> GetSingleRecordAsync(AgeResultCursor result, CancellationToken cancellationToken)
    {
        return await result.SingleAsync(cancellationToken).ConfigureAwait(false);
    }
}
