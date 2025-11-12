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

using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Cvoya.Graph.Model;
using Npgsql.Age;
using Microsoft.Extensions.Logging;
using Npgsql.Age.Types;

/// <summary>
/// Validates attribute-driven constraints for AGE entities before persisting changes.
/// </summary>
internal static class AgeEntityAttributeValidator
{
    public static async Task ValidateNodeAsync<TNode>(
        TNode node,
        AgeGraphContext context,
        AgeGraphTransaction transaction,
        bool isUpdate,
        CancellationToken cancellationToken)
        where TNode : INode
    {
        ArgumentNullException.ThrowIfNull(node);
        await EnsureSchemaInitializedAsync(context.SchemaRegistry, cancellationToken).ConfigureAwait(false);

        var nodeType = node.GetType();
        var schemaLabel = Labels.GetLabelFromType(nodeType);
        if (context.SchemaRegistry.GetNodeSchema(schemaLabel) is not { } schema)
        {
            return;
        }

        ValidatePropertyRules(node, schema, schemaLabel);

        await ValidateUniqueConstraintsAsync(
                entityId: node.Id,
                entity: node,
                schema: schema,
                context: context,
                transaction: transaction,
                alias: "n",
                labelForMatch: Labels.GetBaseTypeLabel(nodeType),
                entityDisplayName: schema.Label,
                isRelationship: false,
                isUpdate: isUpdate,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public static async Task ValidateRelationshipAsync<TRelationship>(
        TRelationship relationship,
        AgeGraphContext context,
        AgeGraphTransaction transaction,
        bool isUpdate,
        CancellationToken cancellationToken)
        where TRelationship : IRelationship
    {
        ArgumentNullException.ThrowIfNull(relationship);
        await EnsureSchemaInitializedAsync(context.SchemaRegistry, cancellationToken).ConfigureAwait(false);

        var relationshipType = relationship.GetType();
        var schemaLabel = Labels.GetLabelFromType(relationshipType);
        if (context.SchemaRegistry.GetRelationshipSchema(schemaLabel) is not { } schema)
        {
            return;
        }

        ValidatePropertyRules(relationship, schema, schemaLabel);

        await ValidateUniqueConstraintsAsync(
                entityId: relationship.Id,
                entity: relationship,
                schema: schema,
                context: context,
                transaction: transaction,
                alias: "r",
                labelForMatch: Labels.GetBaseTypeLabel(relationshipType),
                entityDisplayName: schema.Label,
                isRelationship: true,
                isUpdate: isUpdate,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task EnsureSchemaInitializedAsync(SchemaRegistry registry, CancellationToken cancellationToken)
    {
        if (!registry.IsInitialized)
        {
            await registry.InitializeAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static void ValidatePropertyRules(object entity, EntitySchemaInfo schema, string entityDisplayName)
    {
        foreach (var propertySchema in schema.Properties.Values)
        {
            if (propertySchema.Ignore)
            {
                continue;
            }

            var propertyInfo = propertySchema.PropertyInfo;
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

            if (NormalizeValidation(propertySchema.Validation) is not { } validation)
            {
                continue;
            }

            ValidatePropertyValue(propertyDisplayName, value, validation, entityDisplayName);
        }
    }

    private static PropertyValidation? NormalizeValidation(PropertyValidation validation)
    {
    int? minLength = validation.MinLength is int min && min != int.MinValue ? min : null;
    int? maxLength = validation.MaxLength is int max && max != int.MaxValue ? max : null;
        var pattern = string.IsNullOrWhiteSpace(validation.Pattern) ? null : validation.Pattern;

        // Currently PropertyAttribute does not expose MinValue/MaxValue, but keep placeholders for future parity.
        var minValue = validation.MinValue;
        var maxValue = validation.MaxValue;

        if (minLength is null && maxLength is null && pattern is null && minValue is null && maxValue is null)
        {
            return null;
        }

        return new PropertyValidation(
            minLength: minLength,
            maxLength: maxLength,
            minValue: minValue,
            maxValue: maxValue,
            pattern: pattern);
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

    private static async Task ValidateUniqueConstraintsAsync(
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
            whereParts.Add($"{GetQualifiedProperty(alias, keyProperty.Name)} = ${parameterName}");
            parameters[parameterName] = AgeSerializationBridge.ToAgeValue(value);
        }

        if (isUpdate)
        {
            parameters["currentId"] = entityId;
            whereParts.Add($"{GetQualifiedIdProperty(alias)} <> $currentId");
        }

        var whereClause = string.Join(" AND ", whereParts);
        var logger = context.LoggerFactory.CreateLogger(nameof(AgeEntityAttributeValidator));
        logger.LogDebug(
            "Validating composite key for {EntityDisplayName} with WHERE {WhereClause} and parameters {Parameters}",
            entityDisplayName,
            whereClause,
            string.Join(", ", parameters.Select(kvp => $"{kvp.Key}={kvp.Value}")));

        var conflict = await HasConflictAsync(context, transaction, alias, labelForMatch, isRelationship, whereClause, parameters, cancellationToken).ConfigureAwait(false);
        if (conflict)
        {
            await LogConflictDetailsAsync(context, alias, labelForMatch, isRelationship, whereClause, parameters, cancellationToken).ConfigureAwait(false);
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

            var whereClause = $"{GetQualifiedProperty(alias, propertySchema.Name)} = $value";

            if (isUpdate)
            {
                parameters["currentId"] = entityId;
                whereClause = $"{whereClause} AND {GetQualifiedIdProperty(alias)} <> $currentId";
            }

            var conflict = await HasConflictAsync(context, transaction, alias, labelForMatch, isRelationship, whereClause, parameters, cancellationToken).ConfigureAwait(false);
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

    private static async Task<bool> HasConflictAsync(
        AgeGraphContext context,
        AgeGraphTransaction transaction,
        string alias,
        string labelForMatch,
        bool isRelationship,
        string whereClause,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(whereClause))
        {
            return false;
        }

        var escapedLabel = EscapeLabel(labelForMatch);
        var matchPattern = isRelationship
            ? $"()-[{alias}:{escapedLabel}]-()"
            : $"({alias}:{escapedLabel})";

        var cypher = $"MATCH {matchPattern} WHERE {whereClause} RETURN 1 LIMIT 1";
        await using var command = context.Connection.CreateCypherCommand(context.GraphName, cypher, new Dictionary<string, object?>(parameters));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        return !reader.IsDBNull(0);
    }

    private static async Task LogConflictDetailsAsync(
        AgeGraphContext context,
        string alias,
        string labelForMatch,
        bool isRelationship,
        string whereClause,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        var logger = context.LoggerFactory.CreateLogger(nameof(AgeEntityAttributeValidator));

        try
        {
            var escapedLabel = EscapeLabel(labelForMatch);
            var matchPattern = isRelationship
                ? $"()-[{alias}:{escapedLabel}]-()"
                : $"({alias}:{escapedLabel})";

            var cypher = $"MATCH {matchPattern} WHERE {whereClause} RETURN {alias} LIMIT 3";
            await using var command = context.Connection.CreateCypherCommand(context.GraphName, cypher, new Dictionary<string, object?>(parameters));
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            var conflicts = new List<string>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (!reader.IsDBNull(0))
                {
                    var agtype = reader.GetFieldValue<Agtype>(0);
                    conflicts.Add(FormatAgtype(agtype));
                }
            }

            if (conflicts.Count == 0)
            {
                logger.LogWarning("Composite key conflict reported but no matching entities were found for logging.");
            }
            else
            {
                logger.LogWarning("Composite key conflict details: {Conflicts}", string.Join(" | ", conflicts));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to log composite key conflict details.");
        }
    }

    private static string FormatAgtype(Agtype agtype)
    {
        if (agtype.IsEdge)
        {
            var edge = agtype.GetEdge();
            var builder = new StringBuilder();
            builder.Append("Edge(");
            builder.Append("Id=").Append(edge.Id);
            builder.Append(", Label=").Append(edge.Label);
            builder.Append(", Start=").Append(edge.StartId);
            builder.Append(", End=").Append(edge.EndId);
            builder.Append(", Properties={");

            var first = true;
            foreach (var (key, value) in edge.Properties)
            {
                if (!first)
                {
                    builder.Append(", ");
                }
                first = false;
                builder.Append(key).Append('=').Append(FormatValue(value));
            }

            builder.Append("})");
            return builder.ToString();
        }

        if (agtype.IsVertex)
        {
            var vertex = agtype.GetVertex();
            var builder = new StringBuilder();
            builder.Append("Vertex(");
            builder.Append("Id=").Append(vertex.Id);
            builder.Append(", Label=").Append(vertex.Label);
            builder.Append(", Properties={");

            var first = true;
            foreach (var (key, value) in vertex.Properties)
            {
                if (!first)
                {
                    builder.Append(", ");
                }
                first = false;
                builder.Append(key).Append('=').Append(FormatValue(value));
            }

            builder.Append("})");
            return builder.ToString();
        }

        return agtype.ToString() ?? "<null>";
    }

    private static string FormatValue(object? value)
    {
        return value switch
        {
            null => "<null>",
            string s => s,
            Agtype nested => FormatAgtype(nested),
            IEnumerable<object?> list => $"[{string.Join(", ", list.Select(FormatValue))}]",
            IEnumerable enumerable => $"[{string.Join(", ", enumerable.Cast<object?>().Select(FormatValue))}]",
            _ => value.ToString() ?? string.Empty
        };
    }

    private static string GetQualifiedProperty(string alias, string propertyGraphName)
    {
        var mapped = MapPropertyName(propertyGraphName);
        return $"{alias}.`{EscapePropertyName(mapped)}`";
    }

    private static string GetQualifiedIdProperty(string alias)
    {
        var mapped = MapPropertyName(nameof(IEntity.Id));
        return $"{alias}.`{EscapePropertyName(mapped)}`";
    }

    private static string MapPropertyName(string propertyName) => propertyName switch
    {
        nameof(IEntity.Id) => "user_id",
        _ => propertyName
    };

    private static string EscapeLabel(string label) => $"`{label.Replace("`", "``")}`";

    private static string EscapePropertyName(string propertyName) => propertyName.Replace("`", "``");
}
