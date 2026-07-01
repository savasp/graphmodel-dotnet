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

namespace Cvoya.Graph.Model.Age.Querying.Cypher.Visitors.Core.Modular;

using System.Collections.Immutable;
using System.Linq.Expressions;
using Cvoya.Graph.Model.Cypher.Querying.Cypher.Visitors.Core;
using Microsoft.Extensions.Logging;

/// <summary>
/// Handles nested collect() detection and translation for group projections.
/// Detects patterns like group.Select(p => new { ... }).ToList() inside projection expressions.
/// </summary>
internal sealed class NestedCollectHandler
{
    private readonly CypherQueryContext _context;
    private readonly ILogger _logger;

    public NestedCollectHandler(CypherQueryContext context, ILogger logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Detects nested .Select().ToList() chains on IGrouping parameters inside projection expressions.
    /// Pattern: group.Select(p => new { ... }).ToList()
    /// Emits a CollectFragment that generates collect({...}) in Cypher.
    /// </summary>
    public bool TryHandleNestedCollect(Expression propertyExpr, string propertyName, string cypherAlias, List<string> returns)
    {
        // Unwrap Convert expressions (e.g., boxing List<T> -> object)
        if (propertyExpr is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } convertExpr)
            propertyExpr = convertExpr.Operand;

        // Check for .ToList() call
        if (propertyExpr is not MethodCallExpression toListCall ||
            toListCall.Method.Name != "ToList" ||
            toListCall.Arguments.Count == 0)
            return false;

        var toListSource = toListCall.Arguments[0];

        // Unwrap Convert on the source too
        if (toListSource is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } srcConvert)
            toListSource = srcConvert.Operand;

        // Walk through Where/OrderBy chains to find the Select
        MethodCallExpression? selectCall = null;
        var wherePredicates = new List<LambdaExpression>();
        var orderByClauses = new List<(LambdaExpression Lambda, bool Descending)>();
        var current = toListSource;
        while (current is MethodCallExpression chainMc)
        {
            if (chainMc.Method.Name == "Select" && chainMc.Arguments.Count >= 2)
            {
                selectCall = chainMc;
                current = chainMc.Arguments.Count > 0 ? chainMc.Arguments[0] : null;
                if (current is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } sc)
                    current = sc.Operand;
                continue;
            }

            if (chainMc.Method.Name == "Where" && chainMc.Arguments.Count >= 2)
            {
                var whereArg = chainMc.Arguments[1];
                if (whereArg is UnaryExpression { NodeType: ExpressionType.Quote } wq)
                    whereArg = wq.Operand;
                if (whereArg is LambdaExpression whereLambda)
                    wherePredicates.Add(whereLambda);
            }
            else if ((chainMc.Method.Name == "OrderBy" || chainMc.Method.Name == "ThenBy") && chainMc.Arguments.Count >= 2)
            {
                var orderArg = chainMc.Arguments[1];
                if (orderArg is UnaryExpression { NodeType: ExpressionType.Quote } oq)
                    orderArg = oq.Operand;
                if (orderArg is LambdaExpression orderLambda)
                    orderByClauses.Add((orderLambda, false));
            }
            else if ((chainMc.Method.Name == "OrderByDescending" || chainMc.Method.Name == "ThenByDescending") && chainMc.Arguments.Count >= 2)
            {
                var orderArg = chainMc.Arguments[1];
                if (orderArg is UnaryExpression { NodeType: ExpressionType.Quote } oq)
                    orderArg = oq.Operand;
                if (orderArg is LambdaExpression orderLambda)
                    orderByClauses.Add((orderLambda, true));
            }

            current = chainMc.Arguments.Count > 0 ? chainMc.Arguments[0] : null;
            if (current is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } chainConv)
                current = chainConv.Operand;
        }

        if (selectCall == null)
            return false;

        // Check if the chain's starting source is an IGrouping parameter
        var chainStart = toListSource;
        while (chainStart is MethodCallExpression chainMc)
        {
            chainStart = chainMc.Arguments.Count > 0 ? chainMc.Arguments[0] : chainStart;
            if (chainStart is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } chainConv)
                chainStart = chainConv.Operand;
        }

        var isGroupingParam = chainStart is ParameterExpression paramExpr &&
                              paramExpr.Type.IsGenericType &&
                              paramExpr.Type.GetGenericTypeDefinition().Name.Contains("IGrouping");

        if (!isGroupingParam)
            return false;

        // Extract the inner lambda
        var lambdaArg = selectCall.Arguments[1];
        if (lambdaArg is UnaryExpression { NodeType: ExpressionType.Quote } quote)
            lambdaArg = quote.Operand;
        if (lambdaArg is not LambdaExpression innerLambda)
            return false;

        // Guard: reject nested GroupBy inside collect (requires CALL subqueries, not available in AGE)
        if (ContainsNestedGroupBy(innerLambda.Body))
        {
            _logger.LogWarning("Nested GroupBy inside collect is not supported in AGE. Property: {Property}", propertyName);
            return false;
        }

        // Determine traversal context for correct alias resolution
        bool isPathContext = _context.FragmentSequence.OfType<MatchSegmentFragment>().Any();

        // Resolve aliases based on actual traversal context
        string srcAlias, relAlias, tgtAlias;

        if (_context.Scope.LastPathSegmentHop >= 0)
        {
            // Use the stored aliases from the path segment hop
            var hop = _context.Scope.LastPathSegmentHop;
            var aliases = _context.Scope.GetHopAliases(hop);
            srcAlias = aliases?.SourceAlias ?? "src0";
            relAlias = aliases?.RelationshipAlias ?? "r0";
            tgtAlias = aliases?.TargetAlias ?? "tgt0";
        }
        else if (isPathContext)
        {
            // Path segment context but no hop stored - use numbered aliases
            var hop = _context.Scope.CurrentHop > 0 ? _context.Scope.CurrentHop - 1 : 0;
            srcAlias = _context.Scope.GetNumberedAliasForHop("src", hop);
            relAlias = _context.Scope.GetNumberedAliasForHop("r", hop);
            tgtAlias = _context.Scope.GetNumberedAliasForHop("tgt", hop);
        }
        else
        {
            // Simple node query - the group-by source is the current alias
            srcAlias = _context.Scope.CurrentAlias ?? "src0";
            relAlias = "r0";
            tgtAlias = srcAlias;
        }

        // Fix for Traverse()+GroupBy: when the identity GroupBy rewrites tgt0 -> src0,
        // the collect expression must use tgt0 (traversal target), not src0.
        // Force isPathContext=true and use the traversal target alias for correct resolution.
        if (_context.Scope.IdentityGroupByRewritten)
        {
            isPathContext = true;
            if (_context.Scope.LastPathSegmentHop >= 0)
            {
                var hop = _context.Scope.LastPathSegmentHop;
                var aliases = _context.Scope.GetHopAliases(hop);
                tgtAlias = aliases?.TargetAlias ?? "tgt0";
            }
            else
            {
                tgtAlias = "tgt0";
            }
            _logger.LogDebug("Traverse+GroupBy collect: forcing path context with tgtAlias={TgtAlias}", tgtAlias);
        }

        // Build collect expression with path-context flag
        var innerParam = innerLambda.Parameters[0];
        string collectExpr;
        try
        {
            collectExpr = CollectExpressionTranslator.TranslateInnerSelectBody(innerLambda.Body, innerParam, srcAlias, relAlias, tgtAlias, isPathContext);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to translate nested collect for property {Property}", propertyName);
            return false;
        }

        // Translate OrderBy clauses to Cypher ORDER BY expressions
        string? orderByExpression = null;
        if (orderByClauses.Count > 0)
        {
            var orderByParts = new List<string>();
            foreach (var (orderLambda, descending) in orderByClauses)
            {
                try
                {
                    var orderExpr = CollectExpressionTranslator.TranslateInnerExpression(
                        orderLambda.Body, orderLambda.Parameters[0], srcAlias, relAlias, tgtAlias, isPathContext);
                    orderByParts.Add(descending ? $"{orderExpr} DESC" : orderExpr);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to translate OrderBy expression for {Property}", propertyName);
                }
            }

            if (orderByParts.Count > 0)
            {
                orderByExpression = string.Join(", ", orderByParts);
                _logger.LogDebug("Translated OrderBy for collect: {OrderBy}", orderByExpression);
            }
        }

        // Inject collected Where predicates as additional WHERE clauses
        var alias = _context.Scope.CurrentAlias ?? "src0";
        foreach (var wherePred in wherePredicates)
        {
            try
            {
                var whereCypher = CollectExpressionTranslator.TranslateInnerExpression(
                    wherePred.Body, wherePred.Parameters[0], srcAlias, relAlias, tgtAlias, isPathContext);
                _context.AddFragment(new WhereFragment(
                    whereCypher, ImmutableArray<string>.Empty, alias));
                _logger.LogDebug("Injected inner Where filter for collect: {Where}", whereCypher);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to translate inner Where predicate for {Property}", propertyName);
            }
        }

        // Find the last GroupByFragment to get the group-by expression for WITH clause generation
        var groupByFragment = _context.FragmentSequence.OfType<GroupByFragment>().LastOrDefault();
        var groupByExpression = groupByFragment?.Expression;

        // Emit a CollectFragment with the group-by expression for renderer use
        _context.AddFragment(new CollectFragment(collectExpr, alias, cypherAlias, groupByExpression, orderByExpression));

        // Add to the RETURN clause (with optional ORDER BY inside collect())
        var orderBySuffix = !string.IsNullOrEmpty(orderByExpression) ? $" ORDER BY {orderByExpression}" : "";
        returns.Add($"collect({collectExpr}{orderBySuffix}) AS {cypherAlias}");

        _logger.LogDebug("Emitted CollectFragment for {Property}: collect({Expr}{OrderBy}) AS {Alias}",
            propertyName, collectExpr, orderBySuffix, cypherAlias);
        return true;
    }

    /// <summary>
    /// Checks whether an expression tree contains a nested GroupBy call.
    /// Inner GroupBy requires CALL subqueries which AGE does not support.
    /// </summary>
    private static bool ContainsNestedGroupBy(Expression expr)
    {
        if (expr is MethodCallExpression mc && mc.Method.Name == "GroupBy")
            return true;

        // Recursively check child expressions
        if (expr is BinaryExpression bin)
            return ContainsNestedGroupBy(bin.Left) || ContainsNestedGroupBy(bin.Right);
        if (expr is UnaryExpression unary)
            return ContainsNestedGroupBy(unary.Operand);
        if (expr is ConditionalExpression cond)
            return ContainsNestedGroupBy(cond.Test) || ContainsNestedGroupBy(cond.IfTrue) || ContainsNestedGroupBy(cond.IfFalse);
        if (expr is MethodCallExpression methodCall)
        {
            foreach (var arg in methodCall.Arguments)
            {
                if (ContainsNestedGroupBy(arg))
                    return true;
            }
            if (methodCall.Object != null && ContainsNestedGroupBy(methodCall.Object))
                return true;
        }
        if (expr is LambdaExpression lambda)
            return ContainsNestedGroupBy(lambda.Body);
        if (expr is MemberExpression member && member.Expression != null)
            return ContainsNestedGroupBy(member.Expression);
        if (expr is NewExpression newExpr)
        {
            foreach (var arg in newExpr.Arguments)
            {
                if (ContainsNestedGroupBy(arg))
                    return true;
            }
        }

        return false;
    }
}
