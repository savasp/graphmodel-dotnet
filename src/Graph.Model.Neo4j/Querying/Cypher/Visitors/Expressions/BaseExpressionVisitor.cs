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
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Core;
using Microsoft.Extensions.Logging;

internal class BaseExpressionVisitor(
    CypherQueryContext context, ICypherExpressionVisitor? nextVisitor = null)
    : CypherExpressionVisitorBase<BaseExpressionVisitor>(context, nextVisitor)
{
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
        Logger.LogDebug("Visiting member: {MemberName}", node.Member.Name);

        if (node.Expression is ParameterExpression param)
        {
            var propertyName = node.Member.Name;
            var result = propertyName switch
            {
                "StartNodeId" => $"{Scope.GetAliasForType(typeof(IEntity))}.Id",
                "EndNodeId" => $"{Scope.GetAliasForType(typeof(INode))}.Id",
                "Direction" => $"{Scope.CurrentAlias}.Direction",
                "Id" => $"{Scope.CurrentAlias}.Id",  // Handle relationship's own Id property
                _ => $"{Scope.CurrentAlias}.{propertyName}"
            };
            Logger.LogDebug("Member expression result: {Result}", result);
            return result;
        }
        else if (node.Expression is MemberExpression memberExpr)
        {
            // Handle nested member access (e.g., r.relationshipId)
            var baseExpr = Visit(memberExpr);
            var result = $"{baseExpr}.{node.Member.Name}";
            Logger.LogDebug("Nested member expression result: {Result}", result);
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
                Logger.LogDebug("Closure value is null, returning NULL");
                return "NULL";
            }

            var paramRef = Builder.AddParameter(value);
            Logger.LogDebug("Closure value result: {Result}", paramRef);
            return paramRef;
        }

        var defaultResult = $"{Scope.CurrentAlias}.{node.Member.Name}";
        Logger.LogDebug("Default member expression result: {Result}", defaultResult);
        return defaultResult;
    }

    public override string VisitMethodCall(MethodCallExpression node)
    {
        throw new NotSupportedException($"Method {node.Method.Name} is not supported in base visitor");
    }

    public override string VisitConstant(ConstantExpression node)
    {
        Logger.LogDebug("Visiting constant: {Value}", node.Value);

        if (node.Value == null)
        {
            return "null";
        }

        // Add the parameter and return its reference
        var paramRef = Builder.AddParameter(node.Value);
        Logger.LogDebug("Added parameter: {Value} -> {ParamRef}", node.Value, paramRef);
        return paramRef;
    }

    public override string VisitParameter(ParameterExpression node)
    {
        var result = Scope.CurrentAlias ?? throw new InvalidOperationException("No current alias set");
        Logger.LogDebug("Parameter expression result: {Result}", result);
        return result;
    }
}