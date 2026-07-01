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

using System.Globalization;
using Cvoya.Graph.Model;
using Microsoft.Extensions.Logging;

/// <summary>
/// Validates unique constraints (composite keys and individual unique properties)
/// for entities before persisting changes.
/// </summary>
internal static class UniqueConstraintValidator
{
    public static async Task ValidateUniqueConstraintsAsync(
        string entityId,
        object entity,
        EntitySchemaInfo schema,
        AgeGraphContext context,
        AgeGraphTransaction transaction,
        string alias,
        string labelForMatch,
        string entityDisplayName,
        bool isRelationship,
        bool isUpdate,
        CancellationToken cancellationToken)
    {
        await ValidateCompositeKeyAsync(entityId, entity, schema, context, transaction, alias, labelForMatch, entityDisplayName, isRelationship, isUpdate, cancellationToken).ConfigureAwait(false);
        await ValidateUniquePropertiesAsync(entityId, entity, schema, context, transaction, alias, labelForMatch, entityDisplayName, isRelationship, isUpdate, cancellationToken).ConfigureAwait(false);
    }

    private static async Task ValidateCompositeKeyAsync(
        string entityId,
        object entity,
        EntitySchemaInfo schema,
        AgeGraphContext context,
        AgeGraphTransaction transaction,
        string alias,
        string labelForMatch,
        string entityDisplayName,
        bool isRelationship,
        bool isUpdate,
        CancellationToken cancellationToken)
    {
        if (!schema.HasCompositeKey())
        {
            return;
        }

        var keyProperties = schema.GetKeyProperties().Where(p => !p.Ignore).ToList();
        if (keyProperties.Count == 0)
        {
            return;
        }

        var parameters = new Dictionary<string, object?>();
        var whereParts = new List<string>();
        var parameterIndex = 0;

        foreach (var keyProperty in keyProperties)
        {
            var value = keyProperty.PropertyInfo.GetValue(entity);
            if (value is null || (value is string stringValue && string.IsNullOrWhiteSpace(stringValue)))
            {
                throw new GraphException($"Composite key property '{keyProperty.PropertyInfo.Name}' on {entityDisplayName} is required and cannot be null or empty.");
            }

            var parameterName = $"p{parameterIndex++}";
            whereParts.Add($"{CypherQueryHelper.GetQualifiedProperty(alias, keyProperty.Name)} = ${parameterName}");
            parameters[parameterName] = AgeSerializationBridge.ToAgeValue(value);
        }

        if (isUpdate)
        {
            parameters["currentId"] = entityId;
            whereParts.Add($"{CypherQueryHelper.GetQualifiedIdProperty(alias)} <> $currentId");
        }

        var whereClause = string.Join(" AND ", whereParts);
        var logger = context.LoggerFactory.CreateLogger(nameof(AgeEntityAttributeValidator));
        logger.LogDebug(
            "Validating composite key for {EntityDisplayName} with WHERE {WhereClause} and parameters {Parameters}",
            entityDisplayName,
            whereClause,
            string.Join(", ", parameters.Select(kvp => $"{kvp.Key}={kvp.Value}")));

        var conflict = await CypherQueryHelper.HasConflictAsync(context, transaction, alias, labelForMatch, isRelationship, whereClause, parameters, cancellationToken).ConfigureAwait(false);
        if (conflict)
        {
            await ConflictLogger.LogConflictDetailsAsync(context, alias, labelForMatch, isRelationship, whereClause, parameters, cancellationToken).ConfigureAwait(false);
            var keyList = string.Join(", ", keyProperties.Select(p => p.PropertyInfo.Name));
            throw new GraphException($"Composite key ({keyList}) on {entityDisplayName} must be unique.");
        }
    }

    private static async Task ValidateUniquePropertiesAsync(
        string entityId,
        object entity,
        EntitySchemaInfo schema,
        AgeGraphContext context,
        AgeGraphTransaction transaction,
        string alias,
        string labelForMatch,
        string entityDisplayName,
        bool isRelationship,
        bool isUpdate,
        CancellationToken cancellationToken)
    {
        foreach (var propertySchema in schema.Properties.Values)
        {
            if (propertySchema.Ignore)
            {
                continue;
            }

            if (!propertySchema.IsUnique)
            {
                continue;
            }

            if (schema.HasCompositeKey() && propertySchema.IsKey)
            {
                // Composite keys are validated as a group, skip individual checks.
                continue;
            }

            var value = propertySchema.PropertyInfo.GetValue(entity);
            if (value is null)
            {
                continue;
            }

            var parameters = new Dictionary<string, object?>
            {
                ["value"] = AgeSerializationBridge.ToAgeValue(value)
            };

            var whereClause = $"{CypherQueryHelper.GetQualifiedProperty(alias, propertySchema.Name)} = $value";

            if (isUpdate)
            {
                parameters["currentId"] = entityId;
                whereClause = $"{whereClause} AND {CypherQueryHelper.GetQualifiedIdProperty(alias)} <> $currentId";
            }

            var conflict = await CypherQueryHelper.HasConflictAsync(context, transaction, alias, labelForMatch, isRelationship, whereClause, parameters, cancellationToken).ConfigureAwait(false);
            if (conflict)
            {
                var valueString = value switch
                {
                    string stringValue => stringValue,
                    IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                    _ => value.ToString() ?? string.Empty
                };

                throw new GraphException($"Property '{propertySchema.PropertyInfo.Name}' on {entityDisplayName} must be unique. The value '{valueString}' already exists.");
            }
        }
    }
}
