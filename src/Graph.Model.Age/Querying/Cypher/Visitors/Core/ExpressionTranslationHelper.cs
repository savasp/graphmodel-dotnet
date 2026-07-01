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

namespace Cvoya.Graph.Model.Age.Querying.Cypher.Visitors.Core;

using System;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

/// <summary>
/// Shared helper for expression-to-Cypher translation used across multiple visitors.
/// </summary>
internal static class ExpressionTranslationHelper
{
    /// <summary>
    /// Cache for compiled expressions to avoid recompilation of identical expression trees.
    /// Uses ConditionalWeakTable which holds weak references to keys (expressions),
    /// allowing GC to reclaim cached entries when expressions are no longer referenced.
    /// </summary>
    private static readonly ConditionalWeakTable<Expression, object?> ExpressionCache = new();

    /// <summary>
    /// Validates and backtick-quotes a property name for safe Cypher identifier interpolation.
    /// Escapes any backticks within the name by doubling them, then wraps in backticks.
    /// Also rejects names containing characters that could break Cypher syntax (line breaks,
    /// null chars, or backslashes).
    /// </summary>
    /// <param name="propertyName">The property name to quote.</param>
    /// <returns>The backtick-quoted property name, safe for Cypher interpolation.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="propertyName"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="propertyName"/> is empty or contains invalid characters.</exception>
    public static string QuotePropertyName(string propertyName)
    {
        ArgumentNullException.ThrowIfNull(propertyName);

        if (propertyName.Length == 0)
            throw new ArgumentException("Property name cannot be empty.", nameof(propertyName));

        // Reject property names containing characters that could break Cypher syntax
        if (propertyName.Any(c => c == '\n' || c == '\r' || c == '\0' || c == '\\'))
            throw new ArgumentException($"Property name '{propertyName}' contains invalid characters (line breaks, null chars, or backslashes).", nameof(propertyName));

        // Escape backticks by doubling them, then wrap in backticks
        var escaped = propertyName.Replace("`", "``");
        return $"`{escaped}`";
    }

    /// <summary>
    /// Maps C# property names to AGE storage names.
    /// </summary>
    public static string MapPropertyName(string csharpPropertyName)
    {
        return csharpPropertyName switch
        {
            // Map C# "Id" property to our prefixed "user_id" field to avoid conflict with PostgreSQL internal "Id"
            // This ensures we always use our application-controlled IDs, not PostgreSQL internal IDs
            "Id" => "user_id",

            // For all other properties, keep the same name
            _ => csharpPropertyName
        };
    }

    /// <summary>
    /// Attempts to evaluate an expression at compile time and return its string value.
    /// Falls back to the expression's string representation on failure.
    /// </summary>
    public static string TryCompileEval(Expression expr)
    {
        try
        {
            var result = ExpressionCache.GetValue(expr, static key =>
            {
                var lambda = Expression.Lambda<Func<object>>(Expression.Convert(key, typeof(object)));
                var val = lambda.Compile()();
                return val?.ToString() ?? "null";
            }) as string;
            return result ?? "unknown";
        }
        catch
        {
            return expr.ToString() ?? "unknown";
        }
    }

    /// <summary>
    /// Checks whether the given type is a path segment type (IGraphPathSegment or GraphPathSegment).
    /// </summary>
    public static bool IsPathSegmentType(Type type)
    {
        if (!type.IsGenericType)
        {
            return false;
        }

        if (type.GetGenericTypeDefinition().Name.Contains("GraphPathSegment", StringComparison.Ordinal))
        {
            return true;
        }

        // Also check for the interface form (IGraphPathSegment) used as result types
        if (type.GetGenericTypeDefinition().Name.Contains("IGraphPathSegment", StringComparison.Ordinal))
        {
            return true;
        }

        return type.GetInterfaces()
            .Any(i => i.IsGenericType && i.GetGenericTypeDefinition().Name.Contains("IGraphPathSegment", StringComparison.Ordinal));
    }
}
