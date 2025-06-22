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
    CypherQueryContext context,
    string? contextAlias = null,
    ICypherExpressionVisitor? nextVisitor = null)
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

    // ...existing code...
    public override string VisitMember(MemberExpression node)
    {
        Logger.LogDebug("Visiting member: {Member}", node.Member.Name);

        // Handle nested member access - check if this is a closure variable chain
        if (node.Expression is MemberExpression nestedMember)
        {
            // Check if this is a closure variable chain (value(...).p1.Id)
            if (IsClosureVariableChain(node))
            {
                Logger.LogDebug("Processing closure variable chain for member: {Member}", node.Member.Name);

                // Evaluate the entire expression to get the final value
                var closureValue = EvaluateClosureExpression(node);
                var paramRef = Builder.AddParameter(closureValue);
                Logger.LogDebug("Resolved closure variable chain {Member} to parameter reference: {ParamRef}", node.Member.Name, paramRef);
                return paramRef;
            }

            // Check if this is path segment property access (e.g., k.Relationship.Since)
            if (nestedMember.Expression is ParameterExpression p &&
                typeof(IGraphPathSegment).IsAssignableFrom(p.Type))
            {
                Logger.LogDebug("Processing path segment property access: {Expression}", node);

                // Handle k.Relationship.PropertyName
                if (nestedMember.Member.Name == nameof(IGraphPathSegment.Relationship))
                {
                    Logger.LogDebug("Mapping path segment relationship property {Property} to r.{Property}", node.Member.Name, node.Member.Name);
                    return $"r.{node.Member.Name}";
                }

                // Handle k.StartNode.PropertyName
                if (nestedMember.Member.Name == nameof(IGraphPathSegment.StartNode))
                {
                    Logger.LogDebug("Mapping path segment start node property {Property} to src.{Property}", node.Member.Name, node.Member.Name);
                    return $"src.{node.Member.Name}";
                }

                // Handle k.EndNode.PropertyName
                if (nestedMember.Member.Name == nameof(IGraphPathSegment.EndNode))
                {
                    Logger.LogDebug("Mapping path segment end node property {Property} to tgt.{Property}", node.Member.Name, node.Member.Name);
                    return $"tgt.{node.Member.Name}";
                }
            }

            // Regular nested member access
            var parent = VisitMember(nestedMember);
            return $"{parent}.{node.Member.Name}";
        }

        // Handle parameter property access
        if (node.Expression is ParameterExpression param)
        {
            Logger.LogDebug("Processing parameter {ParamName} of type {ParamType}, RootType is {RootType}, CurrentAlias is {CurrentAlias}",
                param.Name, param.Type.Name, Scope.RootType?.Name, Scope.CurrentAlias);

            // Special handling for path segment parameters
            if (typeof(IGraphPathSegment).IsAssignableFrom(param.Type))
            {
                Logger.LogDebug("Processing path segment parameter property: {Property}", node.Member.Name);

                // Map path segment properties to the correct aliases
                var propertyMapping = node.Member.Name switch
                {
                    nameof(IGraphPathSegment.StartNode) => "src",
                    nameof(IGraphPathSegment.EndNode) => "tgt",
                    nameof(IGraphPathSegment.Relationship) => "r",
                    _ => throw new NotSupportedException($"Path segment property '{node.Member.Name}' is not supported")
                };

                Logger.LogDebug("Mapped path segment property {Property} to alias {Alias}", node.Member.Name, propertyMapping);
                return propertyMapping;
            }

            // Special handling for relationship StartNodeId and EndNodeId properties
            if (typeof(Model.IRelationship).IsAssignableFrom(param.Type))
            {
                if (node.Member.Name == nameof(Model.IRelationship.StartNodeId))
                {
                    // StartNodeId maps to src.Id (or whatever the Id property is called)
                    // Since INode inherits from IEntity, we get the Id property from IEntity
                    var idPropertyInfo = typeof(Model.IEntity).GetProperty(nameof(Model.IEntity.Id), BindingFlags.Public | BindingFlags.Instance);
                    if (idPropertyInfo == null)
                    {
                        throw new InvalidOperationException($"Property '{nameof(Model.IEntity.Id)}' not found on type '{typeof(Model.IEntity)}'.");
                    }

                    var idLabel = Labels.GetLabelFromProperty(idPropertyInfo);
                    Logger.LogDebug("Mapping relationship StartNodeId to src.{IdLabel}", idLabel);
                    return $"src.{idLabel}";
                }

                if (node.Member.Name == nameof(Model.IRelationship.EndNodeId))
                {
                    // EndNodeId maps to tgt.Id (or whatever the Id property is called)
                    // Since INode inherits from IEntity, we get the Id property from IEntity
                    var idPropertyInfo = typeof(Model.IEntity).GetProperty(nameof(Model.IEntity.Id), BindingFlags.Public | BindingFlags.Instance);
                    if (idPropertyInfo == null)
                    {
                        throw new InvalidOperationException($"Property '{nameof(Model.IEntity.Id)}' not found on type '{typeof(Model.IEntity)}'.");
                    }

                    var idLabel = Labels.GetLabelFromProperty(idPropertyInfo);
                    Logger.LogDebug("Mapping relationship EndNodeId to tgt.{IdLabel}", idLabel);
                    return $"tgt.{idLabel}";
                }
            }

            // For all other cases, prefer contextAlias over parameter name
            // Only use parameter name when there's no contextAlias and it's the root type
            var alias = contextAlias ??
                (param.Type == Scope.RootType || Scope.CurrentAlias != null
                    ? (Scope.CurrentAlias ?? Scope.GetOrCreateAlias(param.Type, "src"))
                    : Scope.GetOrCreateAlias(param.Type));

            Logger.LogDebug("Found parameter member access: {Alias}.{Member}", alias, node.Member.Name);
            return $"{alias}.{node.Member.Name}";
        }

        // Handle closure variables (e.g., simple cases)
        if (node.Expression is ConstantExpression constant)
        {
            Logger.LogDebug("Processing closure variable for member: {Member}", node.Member.Name);

            // Extract the value of the closure variable
            var closureValue = EvaluateClosureExpression(node);
            if (closureValue == null)
            {
                throw new NotSupportedException($"Cannot resolve member {node.Member.Name} on a null closure variable.");
            }

            var paramRef = Builder.AddParameter(closureValue);
            Logger.LogDebug("Resolved closure variable member {Member} to parameter reference: {ParamRef}", node.Member.Name, paramRef);
            return paramRef;
        }

        // Handle convert expressions (Convert(n, IEntity).Id)
        if (node.Expression is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
        {
            Logger.LogDebug("Processing member access on convert expression: {Expression}.{Member}", unary, node.Member.Name);
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

        // Check if this is a method call that should be evaluated as a closure variable
        // This includes DateTime methods, string methods, etc. that don't reference graph data
        if (IsEvaluableMethodCall(node))
        {
            Logger.LogDebug("Evaluating method call as closure variable: {Method}", node.Method.Name);
            try
            {
                var lambda = Expression.Lambda(node);
                var compiled = lambda.Compile();
                var result = compiled.DynamicInvoke();
                var paramRef = Builder.AddParameter(result);
                Logger.LogDebug("Evaluated method call {Method} to parameter reference: {ParamRef}", node.Method.Name, paramRef);
                return paramRef;
            }
            catch (Exception ex)
            {
                throw new GraphException($"Failed to evaluate method call '{node}': {ex.Message}", ex);
            }
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
        // If this parameter is a relationship, always use "r"
        if (typeof(Model.IRelationship).IsAssignableFrom(node.Type))
        {
            Logger.LogDebug("Parameter expression for relationship: using alias 'r'");
            return "r";
        }

        var result = Scope.CurrentAlias ?? throw new InvalidOperationException("No current alias set");
        Logger.LogDebug("Parameter expression result: {Result}", result);
        return result;
    }

    private static bool IsEvaluableMethodCall(MethodCallExpression node)
    {
        // Methods that should be evaluated at compile time rather than translated to Cypher
        var evaluableMethods = new[]
        {
        // DateTime methods
        "AddYears", "AddMonths", "AddDays", "AddHours", "AddMinutes", "AddSeconds", "AddMilliseconds",
        "Date", "TimeOfDay", "Year", "Month", "Day", "Hour", "Minute", "Second", "Millisecond",
        
        // String methods (that don't need Cypher translation)
        "Concat", "Join", "Format",
        
        // Math methods
        "Abs", "Max", "Min", "Round", "Floor", "Ceiling",
        
        // Guid methods
        "NewGuid"
    };

        // Check if this is a method we should evaluate
        if (evaluableMethods.Contains(node.Method.Name))
        {
            return true;
        }

        // Also check for static DateTime properties like UtcNow, Now
        if (node.Method.DeclaringType == typeof(DateTime) &&
            (node.Method.Name == "get_UtcNow" || node.Method.Name == "get_Now"))
        {
            return true;
        }

        // Check if this method call doesn't reference any parameters (making it evaluable)
        var parameterFinder = new ParameterExpressionFinder();
        parameterFinder.Visit(node);
        return !parameterFinder.HasParameters;
    }

    private sealed class ParameterExpressionFinder : ExpressionVisitor
    {
        public bool HasParameters { get; private set; }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            HasParameters = true;
            return base.VisitParameter(node);
        }
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