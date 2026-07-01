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
using System.Linq.Expressions;
using Microsoft.Extensions.Logging;

/// <summary>
/// Handles translation of .NET collection operations (Contains, Count, GroupBy aggregations)
/// to Cypher expressions for AGE.
/// </summary>
internal sealed class CollectionExpressionHandler
{
    private readonly Func<Expression, string> _visitAndReturnCypher;
    private readonly Func<Expression, Expression> _visit;
    private readonly Func<object?, string> _addParameter;
    private readonly ILogger _logger;

    public CollectionExpressionHandler(
        Func<Expression, string> visitAndReturnCypher,
        Func<Expression, Expression> visit,
        Func<object?, string> addParameter,
        ILogger logger)
    {
        _visitAndReturnCypher = visitAndReturnCypher;
        _visit = visit;
        _addParameter = addParameter;
        _logger = logger;
    }

    public Expression HandleContainsMethod(MethodCallExpression node)
    {
        if (node.Object != null)
        {
            var collection = _visitAndReturnCypher(node.Object);
            var item = _visitAndReturnCypher(node.Arguments[0]);
            return Expression.Constant($"{item} IN {collection}");
        }
        else if (node.Arguments.Count == 2)
        {
            var collection = _visitAndReturnCypher(node.Arguments[0]);
            var item = _visitAndReturnCypher(node.Arguments[1]);
            return Expression.Constant($"{item} IN {collection}");
        }

        throw new NotSupportedException("Unsupported Contains method signature");
    }

    public Expression HandleCountMethod(MethodCallExpression node)
    {
        if (node.Object != null && node.Arguments.Count == 0)
        {
            if (node.Object is ParameterExpression param &&
                param.Type.IsGenericType &&
                param.Type.GetGenericTypeDefinition().Name.Contains("IGrouping"))
            {
                _logger.LogDebug("Processing g.Count() in GroupBy context - translating to count(*)");
                return Expression.Constant("count(*)");
            }

            var collection = _visitAndReturnCypher(node.Object);
            return Expression.Constant($"size({collection})");
        }
        else if (node.Arguments.Count == 1)
        {
            if (node.Arguments[0] is ParameterExpression param &&
                param.Type.IsGenericType &&
                param.Type.GetGenericTypeDefinition().Name.Contains("IGrouping"))
            {
                _logger.LogDebug("Processing Count(g) in GroupBy context - translating to count(*)");
                return Expression.Constant("count(*)");
            }

            var collection = _visitAndReturnCypher(node.Arguments[0]);
            return Expression.Constant($"size({collection})");
        }
        else if (node.Arguments.Count == 2)
        {
            try
            {
                var objectMember = Expression.Convert(node, typeof(object));
                var getterLambda = Expression.Lambda<Func<object>>(objectMember);
                var getter = getterLambda.Compile();
                var value = getter();

                var paramRef = _addParameter(value);
                return Expression.Constant(paramRef);
            }
            catch
            {
                throw new NotSupportedException("Count with predicate requires compile-time evaluation");
            }
        }

        throw new NotSupportedException("Unsupported Count method signature");
    }

    public Expression HandleGroupingAggregation(MethodCallExpression node, string aggFn)
    {
        var source = node.Object ?? (node.Arguments.Count >= 2 ? node.Arguments[0] : null);
        if (source is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } conv)
            source = conv.Operand;
        var isGrouping = source is ParameterExpression param &&
            param.Type.IsGenericType &&
            param.Type.GetGenericTypeDefinition().Name.Contains("IGrouping");
        if (!isGrouping) return Expression.Constant($"{aggFn}(*)");
        var lambdaIdx = node.Arguments.Count >= 2 ? 1 : 0;
        if (lambdaIdx >= node.Arguments.Count) return Expression.Constant($"{aggFn}(*)");
        var lambdaArg = node.Arguments[lambdaIdx];
        if (lambdaArg is UnaryExpression { NodeType: ExpressionType.Quote } quote)
            lambdaArg = quote.Operand;
        if (lambdaArg is not LambdaExpression lambda)
            return Expression.Constant($"{aggFn}(*)");
        var ageFn = aggFn switch
        {
            "average" => "avg",
            _ => aggFn
        };
        var typedExpr = _visit(lambda.Body);
        string cypherExpr = (typedExpr is ConstantExpression constExpr)
            ? (constExpr.Value?.ToString() ?? "*")
            : "*";
        _logger.LogDebug("IGrouping.{Agg}(lambda) -> {Fn}({Expr})", node.Method.Name, aggFn, cypherExpr);
        return Expression.Constant($"{ageFn}({cypherExpr})");
    }
}
