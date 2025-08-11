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

namespace Cvoya.Graph.Model.Neo4j.Entities;

using Cvoya.Graph.Model.Neo4j.Core;
using Cvoya.Graph.Model.Serialization;
using global::Neo4j.Driver;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;


/// <summary>
/// Manages CRUD operations for relationships in Neo4j.
/// All methods assume that there is already a transaction in progress.
/// </summary>
internal sealed class Neo4jRelationshipManager(GraphContext context)
{
    private readonly ILogger<Neo4jRelationshipManager> _logger = context.LoggerFactory?.CreateLogger<Neo4jRelationshipManager>()
        ?? NullLogger<Neo4jRelationshipManager>.Instance;
    private readonly EntityFactory _serializer = new();

    private static readonly string[] _ignoredProperties =
    [
        nameof(Model.IRelationship.StartNodeId),
        nameof(Model.IRelationship.EndNodeId),
        nameof(Model.IRelationship.Direction)
    ];

    public async Task<TRelationship> CreateRelationshipAsync<TRelationship>(
        TRelationship relationship,
        GraphTransaction transaction,
        CancellationToken cancellationToken = default)
        where TRelationship : Model.IRelationship
    {
        ArgumentNullException.ThrowIfNull(relationship);

        _logger.LogDebug("Creating relationship of type {RelationshipType} from {StartNodeId} to {EndNodeId}",
            typeof(TRelationship).Name, relationship.StartNodeId, relationship.EndNodeId);

        try
        {
            // Validate no reference cycles
            GraphDataModel.EnsureNoReferenceCycle(relationship);

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
                transaction.Transaction,
                cancellationToken);

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
        catch (Exception ex) when (ex is not GraphException)
        {
            _logger.LogError(ex, "Error creating relationship of type {RelationshipType}", typeof(TRelationship).Name);
            throw new GraphException($"Failed to create relationship: {ex.Message}", ex);
        }
    }

    public async Task<bool> UpdateRelationshipAsync<TRelationship>(
        TRelationship relationship,
        GraphTransaction transaction,
        CancellationToken cancellationToken = default)
        where TRelationship : Model.IRelationship
    {
        ArgumentNullException.ThrowIfNull(relationship);

        _logger.LogDebug("Updating relationship of type {RelationshipType} with ID {RelationshipId}",
            typeof(TRelationship).Name, relationship.Id);

        try
        {
            // Validate no reference cycles
            GraphDataModel.EnsureNoReferenceCycle(relationship);

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

            // Update the relationship properties
            var updated = await UpdateRelationshipPropertiesAsync(
                relationship.Id,
                entity,
                transaction.Transaction,
                cancellationToken);

            if (!updated)
            {
                _logger.LogWarning("Relationship with ID {RelationshipId} not found for update", relationship.Id);
                throw new KeyNotFoundException($"Relationship with ID {relationship.Id} not found for update");
            }

            _logger.LogInformation("Updated relationship of type {RelationshipType} with ID {RelationshipId}",
                typeof(TRelationship).Name, relationship.Id);

            return true;
        }
        catch (Exception ex) when (ex is not GraphException and not KeyNotFoundException)
        {
            _logger.LogError(ex, "Error updating relationship {RelationshipId} of type {RelationshipType}",
                relationship.Id, typeof(TRelationship).Name);
            throw new GraphException($"Failed to update relationship: {ex.Message}", ex);
        }
    }

    public async Task<bool> DeleteRelationshipAsync(
        string relationshipId,
        GraphTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(relationshipId);

        _logger.LogDebug("Deleting relationship with ID {RelationshipId}", relationshipId);

        try
        {
            var cypher = "MATCH ()-[r {Id: $relId}]-() DELETE r RETURN COUNT(r) AS deletedCount";

            var result = await transaction.Transaction.RunAsync(
                cypher,
                new { relId = relationshipId });

            var record = await result.SingleAsync(cancellationToken);
            var deletedCount = record["deletedCount"].As<int>();

            if (deletedCount == 0)
            {
                _logger.LogWarning("Relationship with ID {RelationshipId} not found for deletion", relationshipId);
                throw new KeyNotFoundException($"Relationship with ID {relationshipId} not found for deletion");
            }

            _logger.LogInformation("Deleted relationship with ID {RelationshipId}", relationshipId);
            return true;
        }
        catch (Exception ex) when (ex is not KeyNotFoundException)
        {
            _logger.LogError(ex, "Error deleting relationship with ID {RelationshipId}", relationshipId);
            throw new GraphException($"Failed to delete relationship: {ex.Message}", ex);
        }
    }

    private async Task<bool> CreateRelationshipInGraphAsync(
        EntityInfo entity,
        string startNodeId,
        string endNodeId,
        IAsyncTransaction transaction,
        CancellationToken cancellationToken)
    {
        var cypher = $@"
            MATCH (source {{Id: $startNodeId}})
            MATCH (target {{Id: $endNodeId}})
            CREATE (source)-[r:{entity.Label} $props]->(target)
            RETURN r IS NOT NULL AS created";

        var properties = SerializationHelpers.SerializeSimpleProperties(entity)
            .Where(kv => !_ignoredProperties.Contains(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        var result = await transaction.RunAsync(cypher, new
        {
            startNodeId,
            endNodeId,
            props = properties
        });

        var record = await result.SingleAsync(cancellationToken);
        return record["created"].As<bool>();
    }

    private async Task<bool> UpdateRelationshipPropertiesAsync(
        string relationshipId,
        EntityInfo entity,
        IAsyncTransaction transaction,
        CancellationToken cancellationToken)
    {
        var cypher = "MATCH ()-[r {Id: $relId}]->() SET r = $props RETURN r IS NOT NULL AS updated";

        var properties = SerializationHelpers.SerializeSimpleProperties(entity)
            .Where(kv => !_ignoredProperties.Contains(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        var result = await transaction.RunAsync(cypher, new
        {
            relId = relationshipId,
            props = properties
        });

        var record = await result.SingleAsync(cancellationToken);
        return record["updated"].As<bool>();
    }

    private void ValidateRelationshipProperties<TRelationship>(TRelationship relationship) where TRelationship : Model.IRelationship
    {
        // For DynamicRelationship, validate against existing schemas if any
        if (relationship is DynamicRelationship dynamicRelationship)
        {
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

    private void ValidatePropertyValue(string propertyName, object value, PropertyValidation validation, string entityLabel)
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
}