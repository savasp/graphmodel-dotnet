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
        Logger.LogDebug("Visiting member: {Member}", node.Member.Name);

        // Handle nested member access - evaluate the entire chain for closure variables
        if (node.Expression is MemberExpression nestedMember)
        {
            // Check if this is a closure variable chain (e.g., p1.Id)
            if (IsClosureVariableChain(node))
            {
                // Evaluate the entire expression chain and extract the final value
                var finalValue = EvaluateClosureExpression(node);
                Logger.LogDebug("Evaluated closure expression chain {Expression}: {Value} (Type: {Type})",
                    node, finalValue, finalValue?.GetType().FullName ?? "null");

                var paramRef = Builder.AddParameter(finalValue);
                return paramRef;
            }
            else
            {
                // Regular nested access (e.g., entity property chaining)
                var parent = VisitMember(nestedMember);
                return $"{parent}.{node.Member.Name}";
            }
        }

        // Check if this is accessing a closure variable (captured variable)
        if (node.Expression is ConstantExpression constantExpr)
        {
            try
            {
                // Get the value from the closure
                var container = constantExpr.Value;
                if (container is null)
                {
                    throw new GraphException($"Cannot access member '{node.Member.Name}' on null container");
                }

                var member = node.Member;

                object? value = member switch
                {
                    FieldInfo field => field.GetValue(container),
                    PropertyInfo property => property.GetValue(container),
                    _ => throw new NotSupportedException($"Member type {member.MemberType} is not supported")
                };

                Logger.LogDebug("Extracted closure value for {Member}: {Value} (Type: {Type})",
                    member.Name, value, value?.GetType().FullName ?? "null");

                // Add as parameter and return reference
                // AddParameter should handle null values appropriately
                var paramRef = Builder.AddParameter(value);
                return paramRef;
            }
            catch (Exception ex) when (ex is not GraphException)
            {
                Logger.LogError(ex, "Failed to extract value from closure for member {Member}", node.Member.Name);
                throw new GraphException($"Failed to access closure variable '{node.Member.Name}'", ex);
            }
        }

        // Check if we're accessing a parameter property
        if (node.Expression is ParameterExpression param)
        {
            Logger.LogDebug("Processing parameter {ParamName} of type {ParamType}, RootType is {RootType}, CurrentAlias is {CurrentAlias}",
                param.Name, param.Type.Name, Scope.RootType?.Name, Scope.CurrentAlias);

            // If this parameter is for the root type, use the current alias (set by VisitConstant)
            // For aggregation queries, the root type might be the return type (e.g., Boolean for Any())
            // so we also check if there's a current alias set and use it for the main entity being queried
            var alias = (param.Type == Scope.RootType || Scope.CurrentAlias != null)
                ? (Scope.CurrentAlias ?? Scope.GetOrCreateAlias(param.Type, "n"))
                : Scope.GetOrCreateAlias(param.Type);

            // Special handling for relationship properties
            if (alias == "r" && typeof(Model.IRelationship).IsAssignableFrom(param.Type))
            {
                var propertyMapping = node.Member.Name switch
                {
                    nameof(Model.IRelationship.StartNodeId) => "src.Id",
                    nameof(Model.IRelationship.EndNodeId) => "tgt.Id",
                    _ => $"{alias}.{node.Member.Name}"
                };
                Logger.LogDebug("Mapped relationship property {Property} to {Mapping}", node.Member.Name, propertyMapping);
                return propertyMapping;
            }

            Logger.LogDebug("Found parameter member access: {Alias}.{Member}", alias, node.Member.Name);
            return $"{alias}.{node.Member.Name}";
        }

        // Handle nested member access
        if (node.Expression is MemberExpression nested)
        {
            var parent = VisitMember(nested);
            return $"{parent}.{node.Member.Name}";
        }

        // Handle convert expressions (Convert(n, IEntity).Id)
        if (node.Expression is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
        {
            Logger.LogDebug("Processing member access on convert expression: {Expression}.{Member}", unary, node.Member.Name);
            // For conversions, just visit the operand and ignore the conversion
            var operand = Visit(unary.Operand);
            return $"{operand}.{node.Member.Name}";
        }

        throw new NotSupportedException($"Member expression type {node.Expression?.GetType().Name} is not supported");
    }

    public override string VisitMethodCall(MethodCallExpression node)
    {
        // Handle implicit/explicit conversion operators (op_Implicit, op_Explicit)
        if (node.Method.Name.StartsWith("op_") && node.Arguments.Count == 1)
        {
            Logger.LogDebug("Processing conversion operator: {Method}", node.Method.Name);
            // For conversion operators, just visit the operand and ignore the conversion
            return Visit(node.Arguments[0]);
        }

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

    private bool IsClosureVariableChain(MemberExpression node)
    {
        // Walk up the expression tree to see if this eventually leads to a ConstantExpression (closure)
        var current = node;
        while (current != null)
        {
            if (current.Expression is ConstantExpression)
            {
                return true;
            }
            if (current.Expression is MemberExpression parent)
            {
                current = parent;
                continue;
            }
            break;
        }
        return false;
    }

    private object? EvaluateClosureExpression(MemberExpression node)
    {
        try
        {
            // Use Expression.Lambda to compile and evaluate the expression
            var lambda = Expression.Lambda(node);
            var compiled = lambda.Compile();
            return compiled.DynamicInvoke();
        }
        catch (Exception ex)
        {
            throw new GraphException($"Failed to evaluate closure expression '{node}': {ex.Message}", ex);
        }
    }
}