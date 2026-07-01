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
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using Cvoya.Graph.Model;
using Cvoya.Graph.Model.Cypher.Querying.Cypher.Visitors.Core;
using Microsoft.Extensions.Logging;

/// <summary>
/// Handles LINQ Join operations for AGE Cypher queries.
/// Supports both simple ParameterExpression result selectors (returning a single
/// relationship or node) and complex NewExpression result selectors (anonymous types
/// combining both relationship and node, e.g., new { Relationship = r, Person = p }).
/// </summary>
internal sealed class JoinHandler
{
    private readonly CypherQueryContext _context;
    private readonly ILogger _logger;
    private readonly Func<Expression, Expression> _visit;
    private readonly Func<Expression, LambdaExpression?> _extractLambda;
    private readonly Action<Type> _setupAdditionalMatch;

    public JoinHandler(
        CypherQueryContext context,
        ILogger logger,
        Func<Expression, Expression> visit,
        Func<Expression, LambdaExpression?> extractLambda,
        Action<Type> setupAdditionalMatch)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _visit = visit ?? throw new ArgumentNullException(nameof(visit));
        _extractLambda = extractLambda ?? throw new ArgumentNullException(nameof(extractLambda));
        _setupAdditionalMatch = setupAdditionalMatch ?? throw new ArgumentNullException(nameof(setupAdditionalMatch));
    }

    public Expression HandleJoin(MethodCallExpression node)
    {
        if (node.Arguments.Count != 5)
            throw new ArgumentException("Join method must have exactly 5 arguments");

        _logger.LogDebug("Processing JOIN operation");

        var outer = node.Arguments[0];
        var inner = node.Arguments[1];
        var outerKeySelector = _extractLambda(node.Arguments[2]);
        var innerKeySelector = _extractLambda(node.Arguments[3]);
        var resultSelector = _extractLambda(node.Arguments[4]);

        if (outerKeySelector == null || innerKeySelector == null || resultSelector == null)
            throw new ArgumentException("Join method requires lambda expressions for all selectors");

        // ---- Step 1: Visit outer (relationship) queryable ----
        _visit(outer);

        // Capture the outer alias (e.g., "r0" for relationship) before visiting inner
        var outerAlias = _context.Scope.CurrentAlias ?? "src0";
        _logger.LogDebug("JOIN: Outer alias after visit = '{OuterAlias}'", outerAlias);

        // ---- Step 2: Set up inner MATCH clause ----
        // Extract the element type from the inner queryable expression
        var innerElementType = ExtractElementType(inner);
        if (innerElementType == null)
            throw new ArgumentException("Cannot determine element type of inner queryable in Join");

        // Advance the hop so the inner MATCH gets a fresh, non-conflicting alias.
        // Use "tgt" as the base name since the inner MATCH represents the target side of the join.
        _context.Scope.AdvanceHop();
        var innerAlias = _context.Scope.GetNumberedAlias("tgt");

        // Set CurrentAlias so SetupAdditionalMatch uses this alias instead of generating
        // a context-based one (which might conflict with existing aliases like "src0").
        _context.Scope.CurrentAlias = innerAlias;

        // Call SetupAdditionalMatch to emit a MatchRootFragment for the inner type,
        // bypassing the "has match fragments" guard in SetupInitialMatch.
        _setupAdditionalMatch(innerElementType);

        _logger.LogDebug("JOIN: Inner alias after visit = '{InnerAlias}'", innerAlias);

        // ---- Step 3: Translate key selectors to Cypher expressions ----
        // Create a mapping from lambda parameter names to Cypher aliases.
        // outerKeySelector parameter (e.g., "r") -> outerAlias (e.g., "r0")
        // innerKeySelector parameter (e.g., "p") -> innerAlias (e.g., "tgt1")
        var outerParamName = outerKeySelector.Parameters[0].Name ?? "r";
        var innerParamName = innerKeySelector.Parameters[0].Name ?? "p";

        var outerKeyCypher = TranslateKeySelector(outerKeySelector.Body, outerAlias, outerParamName);
        var innerKeyCypher = TranslateKeySelector(innerKeySelector.Body, innerAlias, innerParamName);

        _logger.LogDebug("JOIN: Outer key Cypher = '{OuterKey}'", outerKeyCypher);
        _logger.LogDebug("JOIN: Inner key Cypher = '{InnerKey}'", innerKeyCypher);

        // ---- Step 4: Emit WHERE join condition ----
        var joinPredicate = $"{outerKeyCypher} = {innerKeyCypher}";
        var whereFragment = new WhereFragment(
            joinPredicate,
            ImmutableArray.Create(outerAlias, innerAlias),
            outerAlias);
        _context.AddFragment(whereFragment);
        _logger.LogDebug("JOIN: Emitted WhereFragment: {Predicate}", joinPredicate);

        // ---- Step 5: Handle result selector ----
        var resultBody = resultSelector.Body;

        if (resultBody is ParameterExpression paramExpr)
        {
            // Simple parameter projection (existing behavior)
            HandleParameterResult(paramExpr, resultSelector, outerAlias, innerAlias, outerParamName, innerParamName);
        }
        else if (resultBody is NewExpression newExpr)
        {
            // Complex anonymous type projection (new behavior)
            HandleNewExpressionResult(newExpr, resultSelector, outerAlias, innerAlias, outerParamName, innerParamName);
        }
        else if (resultBody is MemberExpression memberExpr)
        {
            // Single member projection like (r, p) => r.SomeProperty
            HandleMemberExpressionResult(memberExpr, resultSelector, outerAlias, innerAlias, outerParamName, innerParamName);
        }
        else
        {
            throw new NotSupportedException(
                $"Join result selector of type '{resultBody.GetType().Name}' is not yet supported. " +
                "Supported types: ParameterExpression (single entity), NewExpression (anonymous type), MemberExpression (property access).");
        }

        return outer;
    }

    /// <summary>
    /// Handles a simple ParameterExpression result selector (e.g., (r, p) => p or (r, p) => r).
    /// </summary>
    private void HandleParameterResult(
        ParameterExpression paramExpr,
        LambdaExpression resultSelector,
        string outerAlias,
        string innerAlias,
        string outerParamName,
        string innerParamName)
    {
        var parameterIndex = -1;
        for (int i = 0; i < resultSelector.Parameters.Count; i++)
        {
            if (resultSelector.Parameters[i].Name == paramExpr.Name)
            {
                parameterIndex = i;
                break;
            }
        }

        if (parameterIndex == 0)
        {
            // Return the outer (relationship) - use outerAlias
            _context.Scope.CurrentAlias = outerAlias;
            var returns = ImmutableArray.Create(outerAlias);
            var projectionFragment = new ProjectionFragment(returns, outerAlias);
            _context.AddFragment(projectionFragment);
            _logger.LogDebug("JOIN: Emitted ProjectionFragment for relationship alias {Alias}", outerAlias);
        }
        else if (parameterIndex == 1)
        {
            // Return the inner (node) - use innerAlias
            _context.Scope.CurrentAlias = innerAlias;
            var returns = ImmutableArray.Create(innerAlias);
            var projectionFragment = new ProjectionFragment(returns, innerAlias);
            _context.AddFragment(projectionFragment);
            _logger.LogDebug("JOIN: Emitted ProjectionFragment for node alias {Alias}", innerAlias);
        }
        else
        {
            throw new ArgumentException($"Invalid parameter reference in Join result selector: {paramExpr.Name}");
        }
    }

    /// <summary>
    /// Handles a NewExpression (anonymous type) result selector.
    /// e.g., (r, p) => new { Relationship = r, Person = p }
    /// Builds return expressions like "r0 AS c_Relationship, tgt1 AS c_Person".
    /// </summary>
    private void HandleNewExpressionResult(
        NewExpression newExpr,
        LambdaExpression resultSelector,
        string outerAlias,
        string innerAlias,
        string outerParamName,
        string innerParamName)
    {
        var returns = new List<string>();

        for (int i = 0; i < newExpr.Arguments.Count; i++)
        {
            var argExpr = newExpr.Arguments[i];
            var memberName = newExpr.Members?[i]?.Name ?? $"Prop{i}";
            var cypherAlias = $"c_{memberName}";

            if (argExpr is ParameterExpression paramArg)
            {
                // Argument is a direct reference to one of the Join parameters
                // e.g., (r, p) => new { R = r, P = p }
                if (paramArg.Name == outerParamName)
                {
                    returns.Add($"{outerAlias} AS {cypherAlias}");
                }
                else if (paramArg.Name == innerParamName)
                {
                    returns.Add($"{innerAlias} AS {cypherAlias}");
                }
                else
                {
                    // Try to find it by position in the result selector parameters
                    var pos = FindParameterPosition(resultSelector, paramArg.Name!);
                    var alias = pos == 0 ? outerAlias : innerAlias;
                    returns.Add($"{alias} AS {cypherAlias}");
                }
            }
            else if (argExpr is MemberExpression memberArg)
            {
                // Argument is a property access on one of the Join parameters
                // e.g., (r, p) => new { Id = r.EndNodeId, Name = p.Name }
                var memberExpr = TranslateMemberOnParameter(memberArg, resultSelector, outerAlias, innerAlias, outerParamName, innerParamName);
                returns.Add($"{memberExpr} AS {cypherAlias}");
            }
            else
            {
                // Complex expression - fall back to expression visitor
                var visitor = new AgeExpressionToCypherVisitor(_context, _logger, outerAlias);
                var cypherExpr = visitor.VisitAndReturnCypher(argExpr);
                returns.Add($"{cypherExpr} AS {cypherAlias}");
            }
        }

        var currentAlias = _context.Scope.CurrentAlias ?? outerAlias;
        _context.AddFragment(new ProjectionFragment(ImmutableArray.CreateRange(returns), currentAlias));
        _logger.LogDebug("JOIN: Emitted anonymous type projection: {Returns}", string.Join(", ", returns));
    }

    /// <summary>
    /// Handles a MemberExpression result selector.
    /// e.g., (r, p) => r.EndNodeId or (r, p) => p.Name
    /// </summary>
    private void HandleMemberExpressionResult(
        MemberExpression memberExpr,
        LambdaExpression resultSelector,
        string outerAlias,
        string innerAlias,
        string outerParamName,
        string innerParamName)
    {
        var cypherExpr = TranslateMemberOnParameter(memberExpr, resultSelector, outerAlias, innerAlias, outerParamName, innerParamName);
        var currentAlias = _context.Scope.CurrentAlias ?? outerAlias;
        _context.AddFragment(new ProjectionFragment(ImmutableArray.Create(cypherExpr), currentAlias));
        _logger.LogDebug("JOIN: Emitted member projection: {Expression}", cypherExpr);
    }

    /// <summary>
    /// Translates a key selector expression to a Cypher expression by mapping
    /// the lambda parameter name to the appropriate Cypher alias.
    /// Uses <see cref="ExpressionTranslationHelper.MapPropertyName"/> to correctly
    /// map C# property names to AGE property names (e.g., "Id" -> "user_id").
    /// </summary>
    private string TranslateKeySelector(Expression body, string cypherAlias, string paramName)
    {
        // The body is typically a MemberExpression like "r.EndNodeId" or "p.Id".
        // We need to replace the parameter reference "r" with the actual alias "r0"
        // and map property names correctly via MapPropertyName.
        if (body is MemberExpression member)
        {
            if (member.Expression is ParameterExpression param && param.Name == paramName)
            {
                var mappedProperty = ExpressionTranslationHelper.MapPropertyName(member.Member.Name);
                return $"{cypherAlias}.{mappedProperty}";
            }
        }

        // For more complex key selector expressions, use the AgeExpressionToCypherVisitor
        var visitor = new AgeExpressionToCypherVisitor(_context, _logger, cypherAlias);
        return visitor.VisitAndReturnCypher(body);
    }

    /// <summary>
    /// Translates a MemberExpression that is on one of the Join parameters.
    /// Maps the parameter name to the appropriate Cypher alias and uses
    /// <see cref="ExpressionTranslationHelper.MapPropertyName"/> for correct
    /// C#-to-AGE property name mapping (e.g., "Id" -> "user_id").
    /// </summary>
    private string TranslateMemberOnParameter(
        MemberExpression memberExpr,
        LambdaExpression resultSelector,
        string outerAlias,
        string innerAlias,
        string outerParamName,
        string innerParamName)
    {
        if (memberExpr.Expression is ParameterExpression param)
        {
            var alias = param.Name == outerParamName ? outerAlias
                      : param.Name == innerParamName ? innerAlias
                      : FindAliasByPosition(resultSelector, param.Name!, outerAlias, innerAlias);
            var mappedProperty = ExpressionTranslationHelper.MapPropertyName(memberExpr.Member.Name);
            return $"{alias}.{mappedProperty}";
        }

        // Fall back to full expression visitor
        var visitor = new AgeExpressionToCypherVisitor(_context, _logger, outerAlias);
        return visitor.VisitAndReturnCypher(memberExpr);
    }

    /// <summary>
    /// Extracts the element type from a queryable expression.
    /// Handles ConstantExpression (direct queryable) and MethodCallExpression (chained query).
    /// </summary>
    private static Type? ExtractElementType(Expression expression)
    {
        // Direct queryable constant: queryable.ElementType
        if (expression is ConstantExpression constExpr)
        {
            if (constExpr.Value is IQueryable queryable)
                return queryable.ElementType;
        }

        // Method call chain: the return type's generic argument
        if (expression is MethodCallExpression methodCall)
        {
            var type = methodCall.Type;
            if (type.IsGenericType)
            {
                var genericArg = type.GetGenericArguments().FirstOrDefault();
                if (genericArg != null && (typeof(INode).IsAssignableFrom(genericArg) || typeof(IRelationship).IsAssignableFrom(genericArg)))
                    return genericArg;
            }
        }

        // Try to find IQueryable<T> interface
        var queryableInterface = expression.Type.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQueryable<>));
        if (queryableInterface != null)
            return queryableInterface.GetGenericArguments()[0];

        // Fallback: check if the type itself is a generic IQueryable
        if (expression.Type.IsGenericType)
        {
            var genericDef = expression.Type.GetGenericTypeDefinition();
            if (genericDef.Name.Contains("Queryable") || genericDef.Name.Contains("GraphQueryable"))
            {
                return expression.Type.GetGenericArguments()[0];
            }
        }

        return null;
    }

    /// <summary>
    /// Finds the Cypher alias for a given lambda parameter name based on its position
    /// in the result selector's parameter list.
    /// </summary>
    private static string FindAliasByPosition(LambdaExpression resultSelector, string paramName, string outerAlias, string innerAlias)
    {
        var pos = FindParameterPosition(resultSelector, paramName);
        return pos switch
        {
            0 => outerAlias,
            1 => innerAlias,
            _ => outerAlias // fallback
        };
    }

    /// <summary>
    /// Finds the position (0-based) of a parameter by name in the result selector's parameter list.
    /// </summary>
    private static int FindParameterPosition(LambdaExpression resultSelector, string paramName)
    {
        for (int i = 0; i < resultSelector.Parameters.Count; i++)
        {
            if (resultSelector.Parameters[i].Name == paramName)
                return i;
        }
        return -1;
    }
}
