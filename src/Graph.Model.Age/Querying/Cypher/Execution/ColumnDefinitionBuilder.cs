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

namespace Cvoya.Graph.Model.Age.Querying.Cypher.Execution;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using Cvoya.Graph.Model.Age.Querying.Cypher.Visitors.Core;
using Npgsql;
using Npgsql.Age.Types;

/// <summary>
/// Builds SQL column definitions and Cypher commands for AGE projection queries.
/// </summary>
internal static class ColumnDefinitionBuilder
{
    /// <summary>
    /// Builds SQL column definitions from a projection expression.
    /// Handles simple property projections and anonymous type projections.
    /// </summary>
    public static string BuildColumnDefinitions(LambdaExpression projectionExpression)
    {
        var body = projectionExpression.Body;

        // Handle simple property projection: Select(p => p.FirstName)
        if (body is MemberExpression memberExpr)
        {
            var columnName = memberExpr.Member.Name;
            var quotedColumnName = ValidateAndQuoteIdentifier(columnName);
            return $"({quotedColumnName} agtype)";
        }

        // Handle anonymous type projection: Select(p => new { p.FirstName, p.LastName })
        if (body is NewExpression newExpr)
        {
            var columns = new List<string>();

            for (int i = 0; i < newExpr.Arguments.Count; i++)
            {
                var member = newExpr.Members?[i];
                var columnName = member?.Name ?? GetNewExpressionParameterName(newExpr, i) ?? $"field{i}";
                var memberType = member switch
                {
                    PropertyInfo pi => pi.PropertyType,
                    FieldInfo fi => fi.FieldType,
                    _ => newExpr.Arguments[i].Type
                };

                var rawAlias = $"c_{columnName}";
                var quotedAlias = ValidateAndQuoteIdentifier(rawAlias);

                if (ExpressionTranslationHelper.IsPathSegmentType(memberType))
                {
                    columns.Add($"{ValidateAndQuoteIdentifier($"{rawAlias}_src")} agtype");
                    columns.Add($"{ValidateAndQuoteIdentifier($"{rawAlias}_r")} agtype");
                    columns.Add($"{ValidateAndQuoteIdentifier($"{rawAlias}_tgt")} agtype");
                }
                else
                {
                    columns.Add($"{quotedAlias} agtype");
                }
            }

            return $"({string.Join(", ", columns)})";
        }

        return "(result agtype)";
    }

    /// <summary>
    /// Builds default column definitions for path segment queries with no projection.
    /// </summary>
    public static string BuildPathSegmentColumnDefinitions()
    {
        return "(src0 agtype, r0 agtype, tgt0 agtype)";
    }

    /// <summary>
    /// Creates an NpgsqlCommand with explicit column definitions for the SQL wrapper.
    /// </summary>
    public static NpgsqlCommand CreateCypherCommandWithColumns(
        NpgsqlConnection connection,
        string graphName,
        string cypher,
        Dictionary<string, object?> parameters,
        string columnDefinitions)
    {
        // Validate graphName to prevent SQL injection (defense-in-depth alongside parameterization)
        if (!s_validIdentifierRegex.IsMatch(graphName))
        {
            throw new ArgumentException(
                $"The graph name '{graphName}' is not a valid identifier. Graph names must match the pattern '{s_validIdentifierPattern}'.",
                nameof(graphName));
        }

        var parametersJson = System.Text.Json.JsonSerializer.Serialize(parameters);
        var agtypeParams = new Agtype(parametersJson);

        var escapedCypher = cypher.Replace("$$", "\\$\\$");
        var sql = $"SELECT * FROM ag_catalog.cypher('{graphName}', $$ {escapedCypher} $$, @agtypeParams) as {columnDefinitions};";

        var command = new NpgsqlCommand(sql, connection);
        command.Parameters.Add(new NpgsqlParameter { ParameterName = "agtypeParams", Value = agtypeParams, DataTypeName = "ag_catalog.agtype" });

        return command;
    }

    /// <summary>
    /// Validates that the given identifier matches the SQL identifier pattern and wraps it in double quotes.
    /// Throws <see cref="ArgumentException"/> if the identifier is invalid.
    /// </summary>
    private static string ValidateAndQuoteIdentifier(string name)
    {
        if (name is null)
        {
            throw new ArgumentNullException(nameof(name), "Column identifier cannot be null.");
        }

        if (!s_validIdentifierRegex.IsMatch(name))
        {
            throw new ArgumentException(
                $"The column name '{name}' is not a valid SQL identifier. Identifiers must match the pattern '{s_validIdentifierPattern}'.",
                nameof(name));
        }

        return $"\"{name}\"";
    }

    private const string s_validIdentifierPattern = @"^[a-zA-Z_][a-zA-Z0-9_]*$";
    private static readonly Regex s_validIdentifierRegex = new Regex(s_validIdentifierPattern, RegexOptions.Compiled);

    /// <summary>
    /// When a NewExpression's Members array is null (happens for named record types),
    /// extracts the constructor parameter name at the given index.
    /// </summary>
    private static string? GetNewExpressionParameterName(NewExpression newExpr, int parameterIndex)
    {
        var constructor = newExpr.Type.GetConstructors().FirstOrDefault();
        if (constructor == null)
            return null;
        var parameters = constructor.GetParameters();
        if (parameterIndex < 0 || parameterIndex >= parameters.Length)
            return null;
        return parameters[parameterIndex].Name;
    }
}
