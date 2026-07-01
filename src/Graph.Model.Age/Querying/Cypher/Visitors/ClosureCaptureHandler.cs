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

namespace Cvoya.Graph.Model.Age.Querying.Cypher.Visitors;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Cvoya.Graph.Model;
using Microsoft.Extensions.Logging;

/// <summary>
/// Detects and handles closure-captured <c>IEnumerable&lt;IRelationship&gt;.Count(lambda)</c> expressions,
/// translating them to Cypher size() patterns.
/// </summary>
internal sealed class ClosureCaptureHandler
{
    /// <summary>
    /// Allowlist of known-safe types whose members may be compiled at query-translation time.
    /// </summary>
    private static readonly HashSet<string> SafeTypes = new(StringComparer.Ordinal)
    {
        "System.Boolean",
        "System.Byte",
        "System.Char",
        "System.DateTime",
        "System.DateTimeOffset",
        "System.Decimal",
        "System.Double",
        "System.Guid",
        "System.Int16",
        "System.Int32",
        "System.Int64",
        "System.SByte",
        "System.Single",
        "System.String",
        "System.TimeSpan",
        "System.UInt16",
        "System.UInt32",
        "System.UInt64",
    };

    /// <summary>
    /// Cache for compiled expressions to avoid recompilation of identical expression trees.
    /// Uses ConditionalWeakTable which holds weak references to keys (expressions),
    /// allowing GC to reclaim cached entries when expressions are no longer referenced.
    /// </summary>
    private static readonly ConditionalWeakTable<Expression, object?> ExpressionCache = new();

    /// <summary>
    /// Checks whether the given type is considered safe for query-time expression compilation.
    /// Safe types include primitive types, common value types, Enum values, and collections/arrays of safe types.
    /// </summary>
    private static bool IsTypeSafe(Type type)
    {
        if (type == null)
            return false;

        // Check if the type itself is in the safe list
        if (type.FullName != null && SafeTypes.Contains(type.FullName))
            return true;

        // Handle Nullable<T> where T is safe
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            return IsTypeSafe(type.GetGenericArguments()[0]);

        // Handle arrays of safe types
        if (type.IsArray && type.HasElementType)
            return IsTypeSafe(type.GetElementType()!);

        // Handle generic IEnumerable<T>, IList<T>, List<T>, etc. where T is safe
        if (type.IsGenericType)
        {
            var genDef = type.GetGenericTypeDefinition();
            if (genDef == typeof(List<>) || genDef == typeof(IList<>) || genDef == typeof(IEnumerable<>) ||
                genDef == typeof(ICollection<>) || genDef == typeof(IReadOnlyList<>) || genDef == typeof(IReadOnlyCollection<>))
            {
                return IsTypeSafe(type.GetGenericArguments()[0]);
            }
        }

        // Check interfaces for IEnumerable<T> with safe element type
        foreach (var iface in type.GetInterfaces())
        {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                return IsTypeSafe(iface.GetGenericArguments()[0]);
        }

        // Allow Enum types (all enums derive from System.Enum)
        if (type.IsEnum)
            return true;

        return false;
    }

    /// <summary>
    /// Attempts to extract a constant value from a MemberExpression by walking down
    /// to any nested ConstantExpression, avoiding the need for Compile().
    /// </summary>
    private static bool TryExtractConstantValue(MemberExpression node, out object? value)
    {
        try
        {
            // Walk the expression tree to find the inner constant
            var inner = node.Expression;
            while (inner is MemberExpression innerMember)
            {
                inner = innerMember.Expression;
            }

            // If we found a ConstantExpression, we can use reflection to get the value
            if (inner is ConstantExpression constExpr)
            {
                var obj = constExpr.Value;
                if (obj == null)
                {
                    value = null;
                    return true;
                }

                // Walk back up the member chain using reflection
                var member = node.Member;
                object? result;
                if (member is FieldInfo field)
                {
                    result = field.GetValue(obj);
                }
                else if (member is PropertyInfo prop)
                {
                    result = prop.GetValue(obj);
                }
                else
                {
                    value = null;
                    return false;
                }

                value = result;
                return true;
            }
        }
        catch
        {
            // Fall through to return false
        }

        value = null;
        return false;
    }

    private readonly ILogger _logger;
    private readonly string _sourceAlias;

    public ClosureCaptureHandler(ILogger logger, string sourceAlias)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _sourceAlias = sourceAlias;
    }

    /// <summary>
    /// Tries to detect a closure-captured <c>IEnumerable&lt;IRelationship&gt;.Count(lambda)</c> expression
    /// and determines if it can be translated to a Cypher size() pattern.
    /// Returns true if the expression matches the expected pattern.
    /// </summary>
    public bool TryHandleClosureCountOnRelationship(MethodCallExpression node)
    {
        Expression? sourceExpr;
        Expression? predicateExpr;

        if (node.Object != null && node.Arguments.Count == 1)
        {
            sourceExpr = node.Object;
            predicateExpr = node.Arguments[0];
        }
        else if (node.Object == null && node.Arguments.Count == 2)
        {
            sourceExpr = node.Arguments[0];
            predicateExpr = node.Arguments[1];
        }
        else
        {
            return false;
        }

        if (sourceExpr is not MemberExpression memberExpr)
            return false;

        object? capturedValue;

        // Security check: verify the member's type is on the safe-type allowlist
        // before compiling the expression at query-translation time
        if (!IsTypeSafe(memberExpr.Type))
        {
            _logger.LogWarning(
                "closure-captured member of type '{MemberType}' is not in the safe-type allowlist. " +
                "Falling back to constant extraction if possible.",
                memberExpr.Type.FullName);

            // Try to extract a ConstantExpression directly instead of compiling
            if (TryExtractConstantValue(memberExpr, out var constantValue))
            {
                capturedValue = constantValue;
            }
            else
            {
                throw new NotSupportedException(
                    $"closure-captured member of type '{memberExpr.Type.FullName}' is not in the safe-type allowlist. " +
                    $"Only primitive types, common value types, and collections of safe types are permitted. " +
                    $"Add the type to the allowlist if you are sure it is safe.");
            }
        }
        else
        {
            _logger.LogWarning(
                "Compiling closure-captured member expression of type '{MemberType}' at query-translation time. " +
                "This is allowed because the type is on the safe-type allowlist.",
                memberExpr.Type.FullName);

            try
            {
                capturedValue = ExpressionCache.GetValue(memberExpr, static key =>
                {
                    var capturedLambda = Expression.Lambda<Func<object>>(Expression.Convert(key, typeof(object)));
                    return capturedLambda.Compile()();
                });
            }
            catch
            {
                return false;
            }
        }

        if (capturedValue is not IEnumerable enumerable)
            return false;

        var elementType = GetEnumerableElementType(capturedValue.GetType());
        if (elementType == null || !typeof(IRelationship).IsAssignableFrom(elementType))
            return false;

        var relationshipLabel = GetRelationshipLabel(enumerable, elementType);
        if (relationshipLabel == null)
            return false;

        var lambdaArg = predicateExpr;
        if (lambdaArg is UnaryExpression { NodeType: ExpressionType.Quote } quote)
            lambdaArg = quote.Operand;
        if (lambdaArg is not LambdaExpression lambda)
            return false;

        return IsCountByNodeIdPredicate(lambda.Body);
    }

    /// <summary>
    /// AGE does not support size() with pattern expressions. Degree queries
    /// (.Where(p => p.NavigationProperty.Count(...) > N)) are now handled by
    /// <see cref="Cvoya.Graph.Model.Age.Querying.Cypher.Visitors.Core.Modular.DegreeQueryHandler"/> at the query-visitor level.
    /// For other contexts (Select projections, etc.), this remains unsupported.
    /// </summary>
    public string HandleClosureCountOnRelationship(MethodCallExpression node)
    {
        _logger.LogWarning(
            "closure-captured Count(lambda) on relationships is not supported outside of Where predicates. " +
            "Use .Where(p => p.NavigationProperty.Count(...) > N) for degree queries.");

        throw new NotSupportedException(
            "closure-captured Count(lambda) on relationships is not supported in this context. " +
            "Use .Where(p => p.NavigationProperty.Count(...) > N) to filter by relationship degree. " +
            "AGE does not support size() with pattern expressions.");
    }

    private enum RelationshipDirection { Outgoing, Incoming, Both }

    private static RelationshipDirection DetectRelationshipDirection(Expression predicateBody)
    {
        if (predicateBody is BinaryExpression binary && binary.NodeType == ExpressionType.OrElse)
            return RelationshipDirection.Both;

        if (predicateBody is not BinaryExpression eq || eq.NodeType != ExpressionType.Equal)
            return RelationshipDirection.Both;

        var isStartNodeId = IsMemberAccess(eq.Left, "StartNodeId") || IsMemberAccess(eq.Right, "StartNodeId");
        var isEndNodeId = IsMemberAccess(eq.Left, "EndNodeId") || IsMemberAccess(eq.Right, "EndNodeId");

        if (isStartNodeId) return RelationshipDirection.Outgoing;
        if (isEndNodeId) return RelationshipDirection.Incoming;
        return RelationshipDirection.Both;
    }

    private static bool IsMemberAccess(Expression expr, string memberName)
    {
        if (expr is MemberExpression mem)
            return mem.Member.Name == memberName;
        if (expr is UnaryExpression unary && (unary.NodeType == ExpressionType.Convert || unary.NodeType == ExpressionType.ConvertChecked))
            return IsMemberAccess(unary.Operand, memberName);
        return false;
    }

    private static bool IsCountByNodeIdPredicate(Expression predicateBody)
    {
        if (predicateBody is BinaryExpression binary)
        {
            if (binary.NodeType == ExpressionType.Equal)
                return IsMemberAccess(binary.Left, "StartNodeId") || IsMemberAccess(binary.Left, "EndNodeId")
                    || IsMemberAccess(binary.Right, "StartNodeId") || IsMemberAccess(binary.Right, "EndNodeId");
            if (binary.NodeType == ExpressionType.OrElse)
                return IsCountByNodeIdPredicate(binary.Left) && IsCountByNodeIdPredicate(binary.Right);
        }
        return false;
    }

    private static Type? GetEnumerableElementType(Type type)
    {
        if (type.IsGenericType)
        {
            var genDef = type.GetGenericTypeDefinition();
            if (genDef == typeof(List<>) || genDef == typeof(IList<>) || genDef == typeof(IEnumerable<>))
                return type.GetGenericArguments()[0];
        }

        foreach (var iface in type.GetInterfaces())
        {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                return iface.GetGenericArguments()[0];
        }

        return null;
    }

    private static string? GetRelationshipLabel(IEnumerable enumerable, Type elementType)
    {
        foreach (var item in enumerable)
        {
            if (item == null) continue;

            try
            {
                return Labels.GetLabelFromType(elementType);
            }
            catch { }

            var name = elementType.Name;
            if (name.EndsWith("Relationship", StringComparison.Ordinal))
                name = name.Substring(0, name.Length - "Relationship".Length);
            return name;
        }

        var typeName = elementType.Name;
        if (typeName.EndsWith("Relationship", StringComparison.Ordinal))
            typeName = typeName.Substring(0, typeName.Length - "Relationship".Length);
        return typeName;
    }
}


