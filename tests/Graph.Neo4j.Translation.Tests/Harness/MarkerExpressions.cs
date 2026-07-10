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

namespace Cvoya.Graph.Neo4j.Translation.Tests.Harness;

using System.Linq.Expressions;
using System.Reflection;

/// <summary>
/// Builds <see cref="MethodCallExpression"/> nodes for the internal
/// <see cref="QueryTerminals"/> methods that <c>CypherQueryVisitor.HandleLinqMethod</c> (via
/// the shared query-model builder dispatches on (e.g. <c>FirstAsyncMarker</c>,
/// <c>CountAsyncMarker</c>). The real async extension methods
/// (<see cref="GraphQueryableExtensions"/>, <c>QueryableAsyncExtensions</c>) build these same
/// marker calls internally but then immediately execute them via the provider - since this
/// harness never executes, the marker calls are built directly via reflection instead so the
/// resulting expression tree can be fed straight into <c>CypherQueryVisitor</c>.
/// </summary>
internal static class MarkerExpressions
{
    private static readonly Type MarkersType = typeof(QueryTerminals);

    /// <summary>
    /// Builds a call to a marker method by name, generic element type, and argument list. The
    /// marker overload is resolved by matching the number of extra (non-source) arguments.
    /// </summary>
    public static MethodCallExpression Call<TSource>(
        string markerName,
        Expression source,
        params Expression[] extraArgs)
    {
        var candidates = MarkersType
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Where(m => m.Name == markerName)
            .ToList();

        MethodInfo? resolved = null;
        foreach (var candidate in candidates)
        {
            if (!candidate.IsGenericMethodDefinition)
            {
                if (MatchesShape(candidate, extraArgs, source.Type))
                {
                    resolved = candidate;
                    break;
                }

                continue;
            }

            var genericArgCount = candidate.GetGenericArguments().Length;

            // Single generic parameter: the source element type (e.g. FirstAsyncMarker<T>).
            if (genericArgCount == 1)
            {
                var genericCandidate = TryMakeGeneric(candidate, [typeof(TSource)]);
                if (genericCandidate is not null && MatchesShape(genericCandidate, extraArgs, source.Type))
                {
                    resolved = genericCandidate;
                    break;
                }

                continue;
            }

            // Two generic parameters: source element type + a result type inferred from the
            // selector's delegate return type (e.g. MinAsyncMarker<T, TResult>).
            if (genericArgCount == 2 && extraArgs.Length == 1 && TryGetSelectorResultType(extraArgs[0], out var resultType))
            {
                var genericCandidate = TryMakeGeneric(candidate, [typeof(TSource), resultType]);
                if (genericCandidate is not null && MatchesShape(genericCandidate, extraArgs, source.Type))
                {
                    resolved = genericCandidate;
                    break;
                }
            }
        }

        if (resolved is null)
        {
            throw new InvalidOperationException(
                $"Could not resolve marker method '{markerName}' for source type {typeof(TSource)} with {extraArgs.Length} extra argument(s).");
        }

        var args = new Expression[extraArgs.Length + 1];
        args[0] = source;
        Array.Copy(extraArgs, 0, args, 1, extraArgs.Length);

        return Expression.Call(null, resolved, args);
    }

    private static bool MatchesShape(MethodInfo method, Expression[] extraArgs, Type sourceType)
    {
        var parameters = method.GetParameters();
        if (parameters.Length != extraArgs.Length + 1) return false;
        return IsAssignableSource(parameters[0].ParameterType, sourceType);
    }

    private static bool TryGetSelectorResultType(Expression selectorExpression, out Type resultType)
    {
        // For an Expression<Func<TSource, TResult>> value, Expression.Type returns the
        // *delegate* type Func<TSource, TResult> (not Expression<Func<...>> itself) - unwrap
        // that to get TResult.
        var funcType = selectorExpression.Type;
        if (funcType.IsGenericType && funcType.GetGenericArguments() is { Length: 2 } funcArgs)
        {
            resultType = funcArgs[1];
            return true;
        }

        resultType = typeof(object);
        return false;
    }

    private static MethodInfo? TryMakeGeneric(MethodInfo definition, Type[] typeArguments)
    {
        try
        {
            return definition.MakeGenericMethod(typeArguments);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static bool IsAssignableSource(Type parameterType, Type sourceType) =>
        parameterType.IsAssignableFrom(sourceType) || parameterType == sourceType;
}
