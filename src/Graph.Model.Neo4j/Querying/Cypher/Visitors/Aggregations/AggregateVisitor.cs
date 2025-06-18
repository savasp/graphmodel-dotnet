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

namespace Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors;

using System.Linq.Expressions;
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

internal sealed class AggregateVisitor : ExpressionVisitor
{
    private readonly ILogger<AggregateVisitor> _logger;
    private readonly CypherQueryContext _context;

    public AggregateVisitor(CypherQueryContext context, ILoggerFactory? loggerFactory = null)
    {
        _context = context;
        _logger = loggerFactory?.CreateLogger<AggregateVisitor>() ?? NullLogger<AggregateVisitor>.Instance;
    }

    public void VisitAggregate(string aggregateFunction, Expression? selector = null)
    {
        var targetExpression = selector != null
            ? ExpressionToCypher(selector)
            : _context.Scope.CurrentAlias ?? "n";

        var cypherFunction = aggregateFunction switch
        {
            "Count" or "LongCount" => $"COUNT({targetExpression})",
            "Sum" => $"SUM({targetExpression})",
            "Average" => $"AVG({targetExpression})",
            "Min" => $"MIN({targetExpression})",
            "Max" => $"MAX({targetExpression})",
            "Any" => $"COUNT({targetExpression}) > 0",
            "All" => BuildAllAggregate(selector!),
            _ => throw new NotSupportedException($"Aggregate function {aggregateFunction} not supported")
        };

        _context.Builder.AddReturn(cypherFunction);
    }

    private string BuildAllAggregate(Expression predicate)
    {
        // For All(), we need to check that all elements match the predicate
        // In Cypher, this is: NONE(x IN collection WHERE NOT predicate)
        var alias = _context.Scope.CurrentAlias ?? "n";
        var predicateString = ExpressionToCypher(predicate);

        return $"NONE(x IN COLLECT({alias}) WHERE NOT ({predicateString}))";
    }

    private string ExpressionToCypher(Expression expression)
    {
        return expression switch
        {
            LambdaExpression lambda => ExpressionToCypher(lambda.Body),
            MemberExpression member => BuildMemberAccess(member),
            BinaryExpression binary => BuildBinaryExpression(binary),
            ConstantExpression constant => constant.Value is null ? "null" : _context.Builder.AddParameter(constant.Value),
            ParameterExpression param => _context.Scope.GetAliasForType(param.Type) ?? param.Name ?? throw new InvalidOperationException($"No alias found for parameter {param.Type.Name}"),
            _ => throw new NotSupportedException($"Expression type {expression.NodeType} not supported in aggregates")
        };
    }

    private string BuildMemberAccess(MemberExpression member)
    {
        var obj = member.Expression switch
        {
            ParameterExpression param => _context.Scope.GetAliasForType(param.Type)
                ?? param.Name
                ?? throw new InvalidOperationException($"No alias found for parameter of type {param.Type.Name}"),
            _ => ExpressionToCypher(member.Expression!)
        };

        return $"{obj}.{member.Member.Name}";
    }

    private string BuildBinaryExpression(BinaryExpression binary)
    {
        var left = ExpressionToCypher(binary.Left);
        var right = ExpressionToCypher(binary.Right);

        var op = binary.NodeType switch
        {
            ExpressionType.Equal => "=",
            ExpressionType.NotEqual => "<>",
            ExpressionType.GreaterThan => ">",
            ExpressionType.GreaterThanOrEqual => ">=",
            ExpressionType.LessThan => "<",
            ExpressionType.LessThanOrEqual => "<=",
            ExpressionType.AndAlso => "AND",
            ExpressionType.OrElse => "OR",
            _ => throw new NotSupportedException($"Binary operator {binary.NodeType} not supported")
        };

        return $"({left} {op} {right})";
    }
}