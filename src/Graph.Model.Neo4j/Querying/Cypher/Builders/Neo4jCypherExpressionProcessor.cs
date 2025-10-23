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

namespace Cvoya.Graph.Model.Neo4j.Querying.Cypher.Builders;

using System.Linq.Expressions;
using Cvoya.Graph.Model;
using Cvoya.Graph.Model.Cypher.Querying.Cypher.Builders;
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors;
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Neo4j-specific implementation of ICypherExpressionProcessor that supports APOC functions.
/// This implementation wraps the existing ExpressionToCypherVisitor to provide 
/// Neo4j-specific expression processing capabilities.
/// </summary>
internal class Neo4jCypherExpressionProcessor : ICypherExpressionProcessor
{
    private readonly ICypherQueryScope _scope;
    private readonly ILogger<ExpressionToCypherVisitor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="Neo4jCypherExpressionProcessor"/> class.
    /// </summary>
    /// <param name="scope">The query scope for variable tracking.</param>
    /// <param name="loggerFactory">Optional logger factory for diagnostic output.</param>
    public Neo4jCypherExpressionProcessor(
        ICypherQueryScope scope, 
        ILoggerFactory? loggerFactory = null)
    {
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        _logger = loggerFactory?.CreateLogger<ExpressionToCypherVisitor>() 
            ?? NullLogger<ExpressionToCypherVisitor>.Instance;
    }

    /// <summary>
    /// Processes a LINQ expression and converts it to Cypher with Neo4j-specific extensions.
    /// </summary>
    /// <param name="expression">The expression to convert.</param>
    /// <param name="alias">The alias to use for the entity in the expression.</param>
    /// <returns>The Cypher representation of the expression with Neo4j-specific extensions.</returns>
    public string ProcessExpression(Expression expression, string alias)
    {
        // For complex expressions that need parameter handling, use the full visitor
        // For now, use a simplified approach that handles most cases
        var processor = new SimpleExpressionProcessor(_scope, alias);
        return processor.ProcessExpression(expression);
    }

    /// <summary>
    /// Simplified expression processor for basic Cypher generation.
    /// This handles common expression patterns without the complexity of the full visitor.
    /// </summary>
    private class SimpleExpressionProcessor
    {
        private readonly ICypherQueryScope _scope;
        private readonly string _alias;

        public SimpleExpressionProcessor(ICypherQueryScope scope, string alias)
        {
            _scope = scope;
            _alias = alias;
        }

        public string ProcessExpression(Expression expression)
        {
            return expression switch
            {
                BinaryExpression binary => ProcessBinaryExpression(binary),
                MemberExpression member => ProcessMemberExpression(member),
                ConstantExpression constant => ProcessConstantExpression(constant),
                ParameterExpression parameter => ProcessParameterExpression(parameter),
                MethodCallExpression method => ProcessMethodCallExpression(method),
                UnaryExpression unary => ProcessUnaryExpression(unary),
                _ => throw new NotSupportedException($"Expression type {expression.GetType().Name} is not supported in this simplified processor")
            };
        }

        private string ProcessBinaryExpression(BinaryExpression binary)
        {
            var left = ProcessExpression(binary.Left);
            var right = ProcessExpression(binary.Right);

            var operatorString = binary.NodeType switch
            {
                ExpressionType.Equal => "=",
                ExpressionType.NotEqual => "<>",
                ExpressionType.GreaterThan => ">",
                ExpressionType.GreaterThanOrEqual => ">=",
                ExpressionType.LessThan => "<",
                ExpressionType.LessThanOrEqual => "<=",
                ExpressionType.AndAlso => " AND ",
                ExpressionType.OrElse => " OR ",
                ExpressionType.Add => "+",
                ExpressionType.Subtract => "-",
                ExpressionType.Multiply => "*",
                ExpressionType.Divide => "/",
                _ => throw new NotSupportedException($"Binary operator {binary.NodeType} is not supported")
            };

            return $"({left} {operatorString} {right})";
        }

        private string ProcessMemberExpression(MemberExpression member)
        {
            // Handle path segment property access (e.g., k.Relationship.Since)
            if (member.Expression is MemberExpression nestedMember &&
                nestedMember.Expression is ParameterExpression parameter &&
                typeof(Model.IGraphPathSegment).IsAssignableFrom(parameter.Type))
            {
                var propertyName = nestedMember.Member.Name;
                var finalPropertyName = member.Member.Name;

                // Map path segment properties to appropriate aliases
                var alias = propertyName switch
                {
                    nameof(Model.IGraphPathSegment.StartNode) => "src",
                    nameof(Model.IGraphPathSegment.EndNode) => "tgt", 
                    nameof(Model.IGraphPathSegment.Relationship) => "r",
                    _ => throw new NotSupportedException($"Path segment property '{propertyName}' is not supported")
                };

                return $"{alias}.{finalPropertyName}";
            }

            // Handle simple parameter property access
            if (member.Expression is ParameterExpression param)
            {
                // Special handling for relationship StartNodeId and EndNodeId
                if (typeof(Model.IRelationship).IsAssignableFrom(param.Type))
                {
                    if (member.Member.Name == nameof(Model.IRelationship.StartNodeId))
                    {
                        return "src.Id";
                    }

                    if (member.Member.Name == nameof(Model.IRelationship.EndNodeId))
                    {
                        return "tgt.Id"; 
                    }
                }

                return $"{_alias}.{member.Member.Name}";
            }

            // Handle closure variable access - this represents a variable captured from outer scope
            if (member.Expression is ConstantExpression constantExpr)
            {
                // This is a closure variable - we need to evaluate it and return as a literal value
                try 
                {
                    var value = Expression.Lambda(member).Compile().DynamicInvoke();
                    return ProcessConstantExpression(Expression.Constant(value));
                }
                catch
                {
                    // If evaluation fails, fall back to the original behavior
                    var fallbackExpression = ProcessExpression(member.Expression!);
                    return $"{fallbackExpression}.{member.Member.Name}";
                }
            }

            // Handle member access on other expressions (like method calls)
            // First try to evaluate the entire member expression
            try
            {
                var value = Expression.Lambda(member).Compile().DynamicInvoke();
                return ProcessConstantExpression(Expression.Constant(value));
            }
            catch
            {
                // If we can't evaluate it, fall back to property access syntax
                var objectExpression = ProcessExpression(member.Expression!);
                return $"{objectExpression}.{member.Member.Name}";
            }
        }

        private string ProcessConstantExpression(ConstantExpression constant)
        {
            return constant.Value switch
            {
                null => "null",
                string str => $"'{str.Replace("'", "\\'")}'",
                bool b => b.ToString().ToLower(),
                DateTime dt => $"datetime('{dt:yyyy-MM-ddTHH:mm:ss.fffZ}')",
                DateTimeOffset dto => $"datetime('{dto:yyyy-MM-ddTHH:mm:ss.fffZ}')",
                int or long or double or decimal or float => constant.Value.ToString()!,
                _ => $"'{constant.Value}'"
            };
        }

        private string ProcessParameterExpression(ParameterExpression parameter)
        {
            return _alias;
        }

        private string ProcessMethodCallExpression(MethodCallExpression method)
        {
            // Handle string methods
            if (method.Method.DeclaringType == typeof(string))
            {
                var instance = ProcessExpression(method.Object!);
                return method.Method.Name switch
                {
                    "Contains" when method.Arguments.Count == 1 =>
                        $"{instance} CONTAINS {ProcessExpression(method.Arguments[0])}",
                    "StartsWith" when method.Arguments.Count == 1 =>
                        $"{instance} STARTS WITH {ProcessExpression(method.Arguments[0])}",
                    "EndsWith" when method.Arguments.Count == 1 =>
                        $"{instance} ENDS WITH {ProcessExpression(method.Arguments[0])}",
                    _ => throw new NotSupportedException($"String method {method.Method.Name} is not supported in this simplified processor")
                };
            }

            // Handle DateTime methods by translating to Cypher
            if (method.Method.DeclaringType == typeof(DateTime) || 
                method.Method.DeclaringType == typeof(DateTimeOffset) ||
                (method.Object?.Type == typeof(DateTime)) ||
                (method.Object?.Type == typeof(DateTimeOffset)))
            {
                // Handle DateTime static property method calls (e.g., DateTime.UtcNow.AddDays())
                if (method.Object is MemberExpression member &&
                    member.Member.DeclaringType == typeof(DateTime) &&
                    member.Expression == null) // static property
                {
                    var dateTimeExpr = member.Member.Name switch
                    {
                        "Now" => "localdatetime()",
                        "UtcNow" => "datetime()",
                        "Today" => "date()",
                        _ => throw new NotSupportedException($"DateTime static property {member.Member.Name} is not supported")
                    };

                    var args = method.Arguments.Select(arg => ProcessExpression(arg)).ToList();

                    return method.Method.Name switch
                    {
                        "AddYears" => $"{dateTimeExpr} + duration({{years: {args[0]}}})",
                        "AddMonths" => $"{dateTimeExpr} + duration({{months: {args[0]}}})",
                        "AddDays" => $"{dateTimeExpr} + duration({{days: {args[0]}}})",
                        "AddHours" => $"{dateTimeExpr} + duration({{hours: {args[0]}}})",
                        "AddMinutes" => $"{dateTimeExpr} + duration({{minutes: {args[0]}}})",
                        "AddSeconds" => $"{dateTimeExpr} + duration({{seconds: {args[0]}}})",
                        _ => throw new NotSupportedException($"DateTime method {method.Method.Name} is not supported")
                    };
                }

                // For other DateTime operations, try to evaluate if they don't involve parameters
                try
                {
                    var value = Expression.Lambda(method).Compile().DynamicInvoke();
                    return ProcessConstantExpression(Expression.Constant(value));
                }
                catch
                {
                    throw new NotSupportedException($"DateTime method {method.Method.Name} could not be evaluated in this simplified processor");
                }
            }

            // Handle DynamicEntityExtensions methods
            if (method.Method.DeclaringType?.Name == "DynamicEntityExtensions")
            {
                if (method.Arguments.Count < 1)
                {
                    throw new NotSupportedException($"Dynamic entity method {method.Method.Name} requires at least one argument");
                }

                var target = ProcessExpression(method.Arguments[0]);
                var arguments = method.Arguments.Skip(1).ToList();

                return method.Method.Name switch
                {
                    "HasLabel" when arguments.Count == 1 =>
                        $"{ProcessExpression(arguments[0])} IN labels({target})",
                    "HasType" when arguments.Count == 1 =>
                        $"type({target}) = {ProcessExpression(arguments[0])}",
                    "HasProperty" when arguments.Count == 1 =>
                        arguments[0] is ConstantExpression constExpr && constExpr.Value is string propName
                            ? $"{target}.{propName} IS NOT NULL"
                            : $"{target}.{{{ProcessExpression(arguments[0])}}} IS NOT NULL",
                    "GetProperty" when arguments.Count == 1 =>
                        arguments[0] is ConstantExpression constExpr2 && constExpr2.Value is string propName2
                            ? $"{target}.{propName2}"
                            : $"{target}.{{{ProcessExpression(arguments[0])}}}",
                    _ => throw new NotSupportedException($"Dynamic entity method {method.Method.Name} is not supported in this simplified processor")
                };
            }

            // For other method calls, try to evaluate them if possible
            try
            {
                var value = Expression.Lambda(method).Compile().DynamicInvoke();
                return ProcessConstantExpression(Expression.Constant(value));
            }
            catch
            {
                throw new NotSupportedException($"Method call {method.Method.Name} is not supported in this simplified processor");
            }
        }

        private string ProcessUnaryExpression(UnaryExpression unary)
        {
            var operand = ProcessExpression(unary.Operand);
            return unary.NodeType switch
            {
                ExpressionType.Not => $"NOT ({operand})",
                ExpressionType.Convert => operand, // Ignore type conversions
                _ => throw new NotSupportedException($"Unary operator {unary.NodeType} is not supported")
            };
        }
    }
}