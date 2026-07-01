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

using System.Linq.Expressions;
using System.Reflection;
using Cvoya.Graph.Model;

/// <summary>
/// Analyzes expression trees to detect projections and extract element types.
/// Uses <see cref="MethodInfo"/> comparison for robust operation under
/// obfuscation and AOT compilation.
/// </summary>
internal static class QueryExpressionAnalyzer
{
    /// <summary>
    /// Pre-computed set of <see cref="MethodInfo"/> for the <c>Queryable.Select</c>
    /// overloads. Used for O(1) lookup instead of magic string comparison.
    /// </summary>
    private static readonly HashSet<MethodInfo> SelectMethods = new(
        typeof(Queryable).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name == nameof(Queryable.Select))
            .Concat(typeof(GraphQueryableExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == nameof(GraphQueryableExtensions.Select)))
            .Concat(typeof(GraphNodeQueryableExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == nameof(GraphNodeQueryableExtensions.Select)))
            .Concat(typeof(GraphRelationshipQueryableExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == nameof(GraphRelationshipQueryableExtensions.Select))));

    /// <summary>
    /// Determines whether the given <paramref name="method"/> is one of the
    /// <c>Queryable.Select</c> overloads by comparing against the
    /// pre-computed set (handling generic methods correctly).
    /// </summary>
    private static bool IsSelectMethod(MethodInfo method)
    {
        var key = method.IsGenericMethod ? method.GetGenericMethodDefinition() : method;
        return SelectMethods.Contains(key);
    }

    public static (bool hasProjection, LambdaExpression? projectionExpression, Type sourceElementType) DetectProjection(Expression expression)
    {
        Expression current = expression;
        while (current is MethodCallExpression methodCall)
        {
            if (IsSelectMethod(methodCall.Method) && methodCall.Arguments.Count >= 2)
            {
                var lambdaArg = methodCall.Arguments[1];

                if (lambdaArg is UnaryExpression { NodeType: ExpressionType.Quote } quote)
                    lambdaArg = quote.Operand;

                if (lambdaArg is LambdaExpression lambda)
                {
                    var sourceType = methodCall.Arguments[0].Type;
                    if (sourceType.IsGenericType)
                    {
                        var genericArgs = sourceType.GetGenericArguments();
                        if (genericArgs.Length > 0)
                            return (true, lambda, genericArgs[0]);
                    }
                }
            }

            if (methodCall.Arguments.Count > 0)
                current = methodCall.Arguments[0];
            else
                break;
        }

        return (false, null, typeof(object));
    }

    public static Type ExtractElementType(Type resultType, Expression expression)
    {
        // If the result type is a collection, extract the element type
        if (resultType.IsGenericType)
        {
            var genericArgs = resultType.GetGenericArguments();
            if (genericArgs.Length == 1)
                return genericArgs[0];
        }

        // Try to extract from the expression tree
        return ExtractElementTypeFromExpression(expression) ?? resultType;
    }

    public static Type? ExtractElementTypeFromExpression(Expression expression)
    {
        if (expression is ConstantExpression constant)
        {
            var queryableType = constant.Type;
            if (queryableType.IsGenericType)
            {
                var interfaces = queryableType.GetInterfaces();
                foreach (var iface in interfaces)
                {
                    if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IQueryable<>))
                        return iface.GetGenericArguments()[0];
                }
            }
        }

        if (expression is MethodCallExpression methodCall && methodCall.Arguments.Count > 0)
            return ExtractElementTypeFromExpression(methodCall.Arguments[0]);

        return null;
    }

    public static Type ExtractProjectedResultType(Type resultType, LambdaExpression? projection)
    {
        if (projection?.Body is MemberExpression member)
            return member.Type;

        // For List<T> result types, T is the element type
        if (resultType.IsGenericType)
        {
            var args = resultType.GetGenericArguments();
            if (args.Length == 1) return args[0];
        }

        return resultType;
    }
}
