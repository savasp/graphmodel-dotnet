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

namespace Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Expressions;

using System.Linq.Expressions;
using System.Reflection;
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Builders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

internal class BaseExpressionVisitor : CypherExpressionVisitorBase
{
    private readonly CypherQueryScope _scope;
    private readonly CypherQueryBuilder _builder;
    private readonly ILogger<BaseExpressionVisitor> _logger;

    public BaseExpressionVisitor(CypherQueryScope scope, CypherQueryBuilder builder, ILoggerFactory? loggerFactory = null)
    {
        _scope = scope;
        _builder = builder;
        _logger = loggerFactory?.CreateLogger<BaseExpressionVisitor>() ?? NullLogger<BaseExpressionVisitor>.Instance;
    }

    public override string VisitBinary(BinaryExpression node)
    {
        var left = Visit(node.Left);
        var right = Visit(node.Right);
        return $"{left} = {right}";
    }

    public override string VisitUnary(UnaryExpression node)
    {
        var operand = Visit(node.Operand);
        return $"NOT ({operand})";
    }

    public override string VisitMember(MemberExpression node)
    {
        _logger.LogDebug("Visiting member: {MemberName}", node.Member.Name);

        if (node.Expression is ParameterExpression param)
        {
            var propertyName = node.Member.Name;
            var result = propertyName switch
            {
                "StartNodeId" => $"{_scope.GetAliasForType(typeof(IEntity))}.Id",
                "EndNodeId" => $"{_scope.GetAliasForType(typeof(INode))}.Id",
                "Direction" => $"{_scope.CurrentAlias}.Direction",
                "Id" => $"{_scope.CurrentAlias}.Id",  // Handle relationship's own Id property
                _ => $"{_scope.CurrentAlias}.{propertyName}"
            };
            _logger.LogDebug("Member expression result: {Result}", result);
            return result;
        }
        else if (node.Expression is MemberExpression memberExpr)
        {
            // Handle nested member access (e.g., r.relationshipId)
            var baseExpr = Visit(memberExpr);
            var result = $"{baseExpr}.{node.Member.Name}";
            _logger.LogDebug("Nested member expression result: {Result}", result);
            return result;
        }
        else if (node.Expression is ConstantExpression constantExpr)
        {
            // Handle closure values (e.g., relationshipId from the outer scope)
            var value = node.Member switch
            {
                FieldInfo field => field.GetValue(constantExpr.Value),
                PropertyInfo prop => prop.GetValue(constantExpr.Value),
                _ => throw new NotSupportedException($"Member type {node.Member.MemberType} is not supported")
            };

            if (value == null)
            {
                _logger.LogDebug("Closure value is null, returning NULL");
                return "NULL";
            }

            var paramRef = _builder.AddParameter(value);
            _logger.LogDebug("Closure value result: {Result}", paramRef);
            return paramRef;
        }

        var defaultResult = $"{_scope.CurrentAlias}.{node.Member.Name}";
        _logger.LogDebug("Default member expression result: {Result}", defaultResult);
        return defaultResult;
    }

    public override string VisitMethodCall(MethodCallExpression node)
    {
        throw new NotSupportedException($"Method {node.Method.Name} is not supported in base visitor");
    }

    public override string VisitConstant(ConstantExpression node)
    {
        _logger.LogDebug("Visiting constant: {Value}", node.Value);

        if (node.Value == null)
        {
            return "null";
        }

        // Add the parameter and return its reference
        var paramRef = _builder.AddParameter(node.Value);
        _logger.LogDebug("Added parameter: {Value} -> {ParamRef}", node.Value, paramRef);
        return paramRef;
    }

    public override string VisitParameter(ParameterExpression node)
    {
        var result = _scope.CurrentAlias ?? throw new InvalidOperationException("No current alias set");
        _logger.LogDebug("Parameter expression result: {Result}", result);
        return result;
    }
}