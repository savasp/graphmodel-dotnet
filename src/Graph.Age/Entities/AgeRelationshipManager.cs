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


/// <summary>
/// Manages CRUD operations for relationships in Age.
/// All methods assume that there is already a transaction in progress.
/// </summary>
internal sealed class AgeRelationshipManager(AgeGraphContext context)
{
    private readonly EntityFactory _serializer = new();

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
                transaction.Runner,
                cancellationToken).ConfigureAwait(false))
        {
            throw new GraphException("The selected relationship endpoints disappeared before creation could be applied.");
        }
    }

    private static async Task<bool> CreateRelationshipInGraphAsync(
        EntityInfo entity,
        long sourceGraphId,
        long targetGraphId,
        AgeQueryRunner transaction,
        CancellationToken cancellationToken)
    {
        var properties = BuildRelationshipProperties(entity);
        var storageType = SerializationBridge.GetRootStorageName(entity.Label, relationship: true);

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

    internal static Dictionary<string, object?> BuildRelationshipProperties(EntityInfo entity)
    {
        var properties = SerializationHelpers.SerializeSimpleProperties(entity)
            .Where(kv => kv.Key != nameof(Graph.IRelationship.Type))
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
        var storageType = SerializationBridge.GetRootStorageName(entity.Label, relationship: true);
        if (SerializationBridge.IsEncodedRootStorageName(storageType, relationship: true))
        {
            properties[AgeElementMatcher.InheritanceLabelsProperty] = new[] { entity.Label };
        }

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
