// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Entities;

using System.Reflection;
using Cvoya.Graph.Age.Core;
using Cvoya.Graph.Age.Querying;
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
        nameof(Graph.IRelationship.EndNodeId),
        nameof(Graph.IRelationship.Type),
        nameof(Graph.DynamicRelationship.Direction),
        nameof(Graph.INode.Labels),
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
                relationship, transaction.Runner, excludeGraphId: null, cancellationToken).ConfigureAwait(false);

            // Validate that relationships don't have complex properties
            if (entity.ComplexProperties.Count > 0)
            {
                throw new GraphException(
                    $"Relationships cannot have complex properties. Found {entity.ComplexProperties.Count} complex properties on {typeof(TRelationship).Name}");
            }

            var direction = LegacyRelationshipEndpoints.LegacyDirection(relationship);
            var (sourceNodeId, targetNodeId) = direction switch
            {
                RelationshipDirection.Outgoing => (relationship.StartNodeId, relationship.EndNodeId),
                RelationshipDirection.Incoming => (relationship.EndNodeId, relationship.StartNodeId),
                _ => throw new GraphException($"Unsupported relationship direction '{direction}'.")
            };
            var sourceGraphId = await ResolveEndpointGraphIdAsync(
                sourceNodeId, "source", transaction.Runner, cancellationToken).ConfigureAwait(false);
            var targetGraphId = await ResolveEndpointGraphIdAsync(
                targetNodeId, "target", transaction.Runner, cancellationToken).ConfigureAwait(false);

            // Create the relationship against the exact endpoints selected above. Domain Id is an
            // ordinary property, so it must not remain the mutation identity after preflight.
            var created = await CreateRelationshipInGraphAsync(
                entity,
                sourceGraphId,
                targetGraphId,
                direction,
                true,
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

    /// <summary>Creates a relationship between exact command-selected endpoint graphids.</summary>
    internal async Task CreateRelationshipForCommandAsync(
        Graph.IRelationship relationship,
        long sourceGraphId,
        long targetGraphId,
        RelationshipDirection direction,
        AgeGraphTransaction transaction,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(relationship);
        GraphDataModel.EnsureNoReferenceCycle(relationship);
        GraphDataModel.EnsureComplexPropertyDepth(relationship);
        ValidateRelationshipProperties(relationship);

        var entity = _serializer.Serialize(relationship);
        if (entity.ComplexProperties.Count > 0)
        {
            throw new GraphException(
                $"Relationships cannot have complex properties. Found {entity.ComplexProperties.Count} complex properties on {relationship.GetType().Name}");
        }

        await ValidateRelationshipUniquenessAsync(
            relationship, transaction.Runner, excludeGraphId: null, cancellationToken).ConfigureAwait(false);
        var (physicalSource, physicalTarget) = direction switch
        {
            RelationshipDirection.Outgoing => (sourceGraphId, targetGraphId),
            RelationshipDirection.Incoming => (targetGraphId, sourceGraphId),
            _ => throw new GraphException($"Unsupported relationship direction '{direction}'."),
        };
        if (!await CreateRelationshipInGraphAsync(
                entity,
                physicalSource,
                physicalTarget,
                direction,
                false,
                transaction.Runner,
                cancellationToken).ConfigureAwait(false))
        {
            throw new GraphException("The selected relationship endpoints disappeared before creation could be applied.");
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

            var target = await ResolveRelationshipAsync(
                relationship.Id,
                entity,
                relationship.StartNodeId,
                relationship.EndNodeId,
                direction,
                transaction.Runner,
                cancellationToken).ConfigureAwait(false);
            if (target is null)
            {
                _logger.LogWarningAgeRelationshipManager138(relationship.Id);
                throw new EntityNotFoundException($"Relationship with ID {relationship.Id} not found for update");
            }

            await ValidateRelationshipUniquenessAsync(
                relationship, transaction.Runner, target.GraphId, cancellationToken).ConfigureAwait(false);

            await UpdateRelationshipPropertiesAsync(
                target,
                entity,
                transaction.Runner,
                cancellationToken).ConfigureAwait(false);

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
            var lookup = await transaction.Runner.RunAsync(
                $"MATCH ()-[r {{Id: $relId}}]->() WHERE {AgeElementMatcher.UserRelationshipPredicate("r")} RETURN id(r) AS graphId",
                new { relId = relationshipId }, cancellationToken).ConfigureAwait(false);
            var targets = await lookup.ToListAsync(cancellationToken).ConfigureAwait(false);
            if (targets.Count == 0)
            {
                _logger.LogWarningAgeRelationshipManager188(relationshipId);
                throw new EntityNotFoundException($"Relationship with ID {relationshipId} not found for deletion");
            }

            if (targets.Count > 1)
            {
                throw new GraphException(
                    $"Cannot delete relationship {relationshipId} because the ID matches {targets.Count} graph relationships. " +
                    "DeleteRelationshipAsync requires the ID to identify exactly one relationship.");
            }

            var graphId = targets[0]["graphId"].As<long>();
            var result = await transaction.Runner.RunAsync(
                "MATCH ()-[r]->() WHERE id(r) = $graphId DELETE r RETURN true AS deleted",
                new { graphId }, cancellationToken).ConfigureAwait(false);
            _ = await GetSingleRecordAsync(result, cancellationToken).ConfigureAwait(false);

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
        long sourceGraphId,
        long targetGraphId,
        RelationshipDirection direction,
        bool persistLegacyDirection,
        AgeQueryRunner transaction,
        CancellationToken cancellationToken)
    {
        var properties = BuildRelationshipProperties(entity);
        if (persistLegacyDirection)
        {
            properties[nameof(Graph.DynamicRelationship.Direction)] = direction.ToString();
        }

        var nativeStorage = CypherIdentifier.IsNativeLabelName(entity.Label);
        var storageType = nativeStorage ? entity.Label : SerializationBridge.PhysicalRelationshipType;
        if (!nativeStorage)
        {
            properties[nameof(Graph.IRelationship.Type)] = entity.Label;
            properties[AgeElementMatcher.InheritanceLabelsProperty] =
                new[] { Labels.GetLabelFromType(entity.ActualType) };
            properties[SerializationBridge.MetadataPropertyName] =
                SerializationBridge.CreateScalarMetadata(entity.ActualType);
        }

        SerializationBridge.ValidateRootStorageName(entity.Label, "relationship type");
        await transaction.EnsureLabelAsync(storageType, relationship: true, cancellationToken).ConfigureAwait(false);
        var physicalType = CypherIdentifier.Escape(storageType, "relationship type");
        var parameters = new Dictionary<string, object?>
        {
            ["sourceGraphId"] = sourceGraphId,
            ["targetGraphId"] = targetGraphId,
        };
        var setClause = AgeCypherProperties.BuildSetClause("r", properties, parameters, "relationshipProperty");
        var cypher = $@"
            MATCH (source)
            WHERE id(source) = $sourceGraphId
            MATCH (target)
            WHERE id(target) = $targetGraphId
            CREATE (source)-[r:{physicalType}]->(target)
            {setClause}
            RETURN r IS NOT NULL AS created";

        var result = await transaction.RunAsync(cypher, parameters, cancellationToken).ConfigureAwait(false);

        var record = await GetSingleRecordAsync(result, cancellationToken).ConfigureAwait(false);
        return record["created"].As<bool>();
    }

    private static async Task<long> ResolveEndpointGraphIdAsync(
        string nodeId,
        string role,
        AgeQueryRunner transaction,
        CancellationToken cancellationToken)
    {
        var result = await transaction.RunAsync(
            $"{AgeElementMatcher.UserRootMatch("n", " {Id: $nodeId}")} RETURN id(n) AS graphId",
            new { nodeId }, cancellationToken).ConfigureAwait(false);
        var records = await result.ToListAsync(cancellationToken).ConfigureAwait(false);
        if (records.Count == 0)
        {
            throw new GraphException($"Relationship {role} node with ID {nodeId} was not found.");
        }

        if (records.Count > 1)
        {
            throw new GraphException(
                $"Relationship {role} node ID {nodeId} matches {records.Count} graph nodes; an endpoint must resolve to exactly one node.");
        }

        return records[0]["graphId"].As<long>();
    }

    private static async Task UpdateRelationshipPropertiesAsync(
        RelationshipTarget target,
        EntityInfo entity,
        AgeQueryRunner transaction,
        CancellationToken cancellationToken)
    {
        var properties = BuildRelationshipProperties(entity);
        if (target.IsLegacy && target.StoredMetadata is null)
        {
            // Preserve the former lazy metadata migration for legacy rows. Native non-generic rows
            // intentionally require no CLR metadata.
            properties[SerializationBridge.MetadataPropertyName] =
                SerializationBridge.CreateScalarMetadata(entity.ActualType);
        }

        var parameters = new Dictionary<string, object?> { ["graphId"] = target.GraphId };
        var setClause = AgeCypherProperties.BuildSetClause("r", properties, parameters, "relationshipProperty");
        var result = await transaction.RunAsync(
            $"MATCH ()-[r]->() WHERE id(r) = $graphId {setClause} RETURN true AS updated",
            parameters,
            cancellationToken).ConfigureAwait(false);
        var records = await result.ToListAsync(cancellationToken).ConfigureAwait(false);
        if (records.Count != 1)
        {
            throw new GraphException("The selected relationship disappeared before the update could be applied.");
        }
    }

    private async Task<RelationshipTarget?> ResolveRelationshipAsync(
        string relationshipId,
        EntityInfo entity,
        string incomingStartNodeId,
        string incomingEndNodeId,
        RelationshipDirection incomingDirection,
        AgeQueryRunner transaction,
        CancellationToken cancellationToken)
    {
        var incomingMetadata = SerializationBridge.CreateScalarMetadata(entity.ActualType);
        var incomingCanonicalType = Labels.GetLabelFromType(entity.ActualType);
        var lookup = await transaction.RunAsync(
            $@"
            MATCH (source)-[r {{Id: $relId}}]->(target)
            WHERE {AgeElementMatcher.UserRelationshipPredicate("r")}
            RETURN id(r) AS graphId,
                   source.Id AS sourceId,
                   target.Id AS targetId,
                   type(r) AS physicalType,
                   r.Type AS storedType,
                   r.{SerializationBridge.MetadataPropertyName} AS storedMetadata,
                   head(r.inheritance_labels) AS storedCanonicalType",
            new { relId = relationshipId }, cancellationToken).ConfigureAwait(false);
        var lookupRecords = await lookup.ToListAsync(cancellationToken).ConfigureAwait(false);
        if (lookupRecords.Count == 0)
        {
            return null;
        }

        if (lookupRecords.Count > 1)
        {
            throw new GraphException(
                $"Cannot update relationship {relationshipId} because the ID matches {lookupRecords.Count} graph relationships. " +
                "UpdateRelationshipAsync requires the ID to identify exactly one relationship.");
        }

        var lookupRecord = lookupRecords[0];
        var expectedSourceId = incomingDirection == RelationshipDirection.Outgoing
            ? incomingStartNodeId
            : incomingEndNodeId;
        var expectedTargetId = incomingDirection == RelationshipDirection.Outgoing
            ? incomingEndNodeId
            : incomingStartNodeId;
        var storedSourceId = lookupRecord["sourceId"].As<string>();
        var storedTargetId = lookupRecord["targetId"].As<string>();
        if (!string.Equals(storedSourceId, expectedSourceId, StringComparison.Ordinal) ||
            !string.Equals(storedTargetId, expectedTargetId, StringComparison.Ordinal))
        {
            throw new GraphException(
                "Direction cannot be changed on update; delete and recreate the relationship.");
        }

        var physicalType = lookupRecord["physicalType"].As<string>();
        var isLegacy = string.Equals(
            physicalType, SerializationBridge.PhysicalRelationshipType, StringComparison.Ordinal);
        var storedType = isLegacy ? lookupRecord["storedType"].As<string>() : physicalType;
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

        var allowLegacyClrLabel = isLegacy && !entity.ActualType.IsConstructedGenericType;
        if (entity.ActualType.IsConstructedGenericType &&
            !string.Equals(storedMetadata, incomingMetadata, StringComparison.Ordinal))
        {
            throw RelationshipIdentityChanged(
                storedType,
                storedMetadata,
                storedCanonicalType,
                entity.Label,
                incomingMetadata);
        }
        else if (isLegacy && storedMetadata is null &&
            (!allowLegacyClrLabel ||
             !string.Equals(storedCanonicalType, incomingCanonicalType, StringComparison.Ordinal)))
        {
            throw RelationshipIdentityChanged(
                storedType,
                storedMetadata,
                storedCanonicalType,
                entity.Label,
                incomingMetadata);
        }

        else if (!isLegacy &&
            context.SchemaManager.GetSchemaRegistry().GetRelationshipSchema(entity.Label) is { } schema &&
            schema.Type != entity.ActualType)
        {
            throw RelationshipIdentityChanged(
                storedType,
                storedMetadata,
                storedCanonicalType,
                entity.Label,
                incomingMetadata);
        }

        return new RelationshipTarget(
            lookupRecord["graphId"].As<long>(),
            isLegacy,
            storedMetadata);
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

    private sealed record RelationshipTarget(long GraphId, bool IsLegacy, string? StoredMetadata);

    internal static Dictionary<string, object?> BuildRelationshipProperties(EntityInfo entity)
    {
        var properties = SerializationHelpers.SerializeSimpleProperties(entity)
            .Where(kv => !_ignoredProperties.Contains(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
        if (entity.ActualType.IsConstructedGenericType)
        {
            properties[SerializationBridge.MetadataPropertyName] =
                SerializationBridge.CreateScalarMetadata(entity.ActualType);
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
            SerializationBridge.ValidateRootStorageName(dynamicRelationship.Type, "relationship type");
            ValidateDynamicRelationshipProperties(dynamicRelationship);
            return;
        }

        var type = Labels.GetLabelFromType(relationship.GetType());
        SerializationBridge.ValidateRootStorageName(type, "relationship type");
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
        long? excludeGraphId,
        CancellationToken cancellationToken)
        where TRelationship : class, Graph.IRelationship
    {
        var checks = BuildRelationshipUniquenessChecks(relationship, excludeGraphId);

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
        long? excludeGraphId)
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
                ["excludeGraphId"] = excludeGraphId,
            };
            var predicates = new List<string>
            {
                AgeElementMatcher.UserRelationshipPredicate("r"),
                AgeElementMatcher.RelationshipPredicate("r", "$relationshipType"),
            };
            if (excludeGraphId is not null)
            {
                predicates.Add("id(r) <> $excludeGraphId");
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
