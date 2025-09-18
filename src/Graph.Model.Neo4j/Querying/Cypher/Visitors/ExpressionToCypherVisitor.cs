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

using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Cvoya.Graph.Model.Neo4j.Linq;
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Builders;
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Core;
using Microsoft.Extensions.Logging;


/// <summary>
/// Unified expression visitor that translates .NET expressions to Cypher expressions.
/// This replaces the previous fragmented visitor/handler architecture with a single,
/// focused visitor that handles all expression types.
/// </summary>
internal class ExpressionToCypherVisitor : ExpressionVisitor
{
    private readonly CypherQueryBuilder _queryBuilder;
    private readonly CypherQueryScope _scope;
    private readonly ILogger _logger;
    private readonly string? _contextAlias;

    public ExpressionToCypherVisitor(
        CypherQueryBuilder queryBuilder,
        CypherQueryScope scope,
        ILogger logger,
        string? contextAlias = null)
    {
        _queryBuilder = queryBuilder;
        _scope = scope;
        _logger = logger;
        _contextAlias = contextAlias;
    }

    /// <summary>
    /// Visits an expression and returns the resulting Cypher string.
    /// This is the main entry point for translating expressions to Cypher.
    /// </summary>
    public string VisitAndReturnCypher(Expression expression)
    {
        var result = Visit(expression);
        return result switch
        {
            ConstantExpression { Value: string cypherString } => cypherString,
            _ => throw new InvalidOperationException($"Expected Cypher string result, got {result?.GetType()}")
        };
    }

    /// <summary>
    /// Checks if a string is a parameter reference (starts with $).
    /// </summary>
    private static bool IsParameterReference(string value)
    {
        return value.StartsWith("$");
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        _logger.LogDebug("Translating method call: {Method}", node.Method.Name);

        // Handle DateTime methods
        if (node.Method.DeclaringType == typeof(DateTime) ||
            node.Method.DeclaringType == typeof(DateTimeOffset) ||
            node.Method.DeclaringType == typeof(DateOnly) ||
            node.Method.DeclaringType == typeof(TimeOnly))
        {
            return VisitDateTimeMethod(node);
        }

        // Handle string methods
        if (node.Method.DeclaringType == typeof(string))
        {
            return VisitStringMethod(node);
        }

        // Handle Math methods
        if (node.Method.DeclaringType == typeof(Math))
        {
            return VisitMathMethod(node);
        }

        // Handle Convert methods
        if (node.Method.DeclaringType == typeof(Convert))
        {
            return VisitConvertMethod(node);
        }

        // Handle collection methods (Contains, Any, All, etc.)
        if (IsCollectionMethod(node))
        {
            return VisitCollectionMethod(node);
        }

        // Handle aggregation methods (Count, Sum, Average, Min, Max)
        if (IsAggregationMethod(node))
        {
            return VisitAggregationMethod(node);
        }

        // Handle dynamic entity extension methods
        if (node.Method.DeclaringType?.Name == "DynamicEntityExtensions")
        {
            return VisitDynamicEntityMethod(node);
        }

        // Handle conversion operators (op_Implicit, op_Explicit)
        if (node.Method.Name.StartsWith("op_") && node.Arguments.Count == 1)
        {
            _logger.LogDebug("Processing conversion operator: {Method}", node.Method.Name);
            return Visit(node.Arguments[0]);
        }

        // Check if this is a method call that should be evaluated as a closure variable
        if (IsEvaluableMethodCall(node))
        {
            _logger.LogDebug("Evaluating method call as closure variable: {Method}", node.Method.Name);
            try
            {
                var lambda = Expression.Lambda(node);
                var compiled = lambda.Compile();
                var result = compiled.DynamicInvoke();
                var paramRef = _queryBuilder.AddParameter(result);
                _logger.LogDebug("Evaluated method call {Method} to parameter reference: {ParamRef}", node.Method.Name, paramRef);
                return Expression.Constant(paramRef);
            }
            catch (Exception ex)
            {
                throw new GraphException($"Failed to evaluate method call '{node}': {ex.Message}", ex);
            }
        }

        throw new NotSupportedException($"Method {node.Method.DeclaringType?.Name}.{node.Method.Name} is not supported in Cypher expressions");
    }

    private Expression VisitDateTimeMethod(MethodCallExpression node)
    {
        _logger.LogDebug("Visiting DateTime method: {MethodName}", node.Method.Name);

        // Handle DateTime static property method calls (e.g., DateTime.Now.AddDays())
        if (node.Object is MemberExpression member &&
            member.Member.DeclaringType == typeof(DateTime) &&
            member.Expression == null) // static property
        {
            _logger.LogDebug("Processing method call on DateTime static property: {Property}.{Method}",
                member.Member.Name, node.Method.Name);

            var dateTimeExpr = member.Member.Name switch
            {
                "Now" => "datetime()",
                "UtcNow" => "datetime.realtime()",
                "Today" => "date()",
                _ => throw new NotSupportedException($"DateTime static property {member.Member.Name} is not supported")
            };

            var args = node.Arguments.Select(arg => VisitAndReturnCypher(arg)).ToList();

            var result = node.Method.Name switch
            {
                "AddYears" => $"{dateTimeExpr} + duration({{years: {args[0]}}})",
                "AddMonths" => $"{dateTimeExpr} + duration({{months: {args[0]}}})",
                "AddDays" => $"{dateTimeExpr} + duration({{days: {args[0]}}})",
                "AddHours" => $"{dateTimeExpr} + duration({{hours: {args[0]}}})",
                "AddMinutes" => $"{dateTimeExpr} + duration({{minutes: {args[0]}}})",
                "AddSeconds" => $"{dateTimeExpr} + duration({{seconds: {args[0]}}})",
                _ => throw new NotSupportedException($"DateTime method {node.Method.Name} is not supported")
            };

            _logger.LogDebug("Translated DateTime.{Property}.{Method} to {Result}",
                member.Member.Name, node.Method.Name, result);
            return Expression.Constant(result);
        }

        // Handle static methods/properties (get_ methods from properties)
        if (node.Object == null)
        {
            var expr = node.Method.Name switch
            {
                "get_Now" => "datetime()",
                "get_UtcNow" => "datetime.realtime()",
                "get_Today" => "date()",
                _ => throw new NotSupportedException($"Static DateTime method {node.Method.Name} is not supported")
            };

            _logger.LogDebug("Static DateTime method result: {Expression}", expr);
            return Expression.Constant(expr);
        }

        // Handle instance methods
        var target = VisitAndReturnCypher(node.Object!);
        var arguments = node.Arguments.Select(arg => VisitAndReturnCypher(arg)).ToList();

        var expression = node.Method.Name switch
        {
            "AddYears" when arguments.Count == 1 => $"datetime({target}) + duration({{years: {arguments[0]}}})",
            "AddMonths" when arguments.Count == 1 => $"datetime({target}) + duration({{months: {arguments[0]}}})",
            "AddDays" when arguments.Count == 1 => $"datetime({target}) + duration({{days: {arguments[0]}}})",
            "AddHours" when arguments.Count == 1 => $"datetime({target}) + duration({{hours: {arguments[0]}}})",
            "AddMinutes" when arguments.Count == 1 => $"datetime({target}) + duration({{minutes: {arguments[0]}}})",
            "AddSeconds" when arguments.Count == 1 => $"datetime({target}) + duration({{seconds: {arguments[0]}}})",
            "AddMilliseconds" when arguments.Count == 1 => $"datetime({target}) + duration({{milliseconds: {arguments[0]}}})",

            // Property accessors (get_ methods from properties)
            "get_Year" => $"datetime({target}).year",
            "get_Month" => $"datetime({target}).month",
            "get_Day" => $"datetime({target}).day",
            "get_Hour" => $"datetime({target}).hour",
            "get_Minute" => $"datetime({target}).minute",
            "get_Second" => $"datetime({target}).second",
            "get_Millisecond" => $"datetime({target}).millisecond",
            "get_DayOfWeek" => $"datetime({target}).dayOfWeek",
            "get_DayOfYear" => $"datetime({target}).ordinalDay",
            "get_Date" => $"date({target})",
            "get_TimeOfDay" => $"time({target})",

            // Conversion methods
            "ToUniversalTime" => $"datetime({target})",
            "ToLocalTime" => $"datetime({target})",
            "ToString" => arguments.Count == 0 ? $"toString({target})" : $"toString({target})",

            _ => throw new NotSupportedException($"DateTime method {node.Method.Name} is not supported")
        };

        _logger.LogDebug("DateTime method result: {Expression}", expression);
        return Expression.Constant(expression);
    }

    private Expression VisitStringMethod(MethodCallExpression node)
    {
        _logger.LogDebug("Visiting string method: {MethodName}", node.Method.Name);

        // Handle static methods
        if (node.Method.IsStatic)
        {
            var args = node.Arguments.Select(arg => VisitAndReturnCypher(arg)).ToList();

            var expr = node.Method.Name switch
            {
                "IsNullOrEmpty" when args.Count == 1 =>
                    $"({args[0]} IS NULL OR size({args[0]}) = 0)",
                "IsNullOrWhiteSpace" when args.Count == 1 =>
                    $"({args[0]} IS NULL OR size(trim({args[0]})) = 0)",
                "Concat" => args.Count switch
                {
                    2 => $"{args[0]} + {args[1]}",
                    _ => $"apoc.text.join([{string.Join(", ", args)}], '')"
                },
                "Join" when args.Count >= 2 =>
                    $"apoc.text.join([{string.Join(", ", args.Skip(1))}], {args[0]})",
                _ => throw new NotSupportedException($"Static string method {node.Method.Name} is not supported")
            };

            _logger.LogDebug("Static string method result: {Expression}", expr);
            return Expression.Constant(expr);
        }

        // Handle instance methods
        var target = VisitAndReturnCypher(node.Object!);
        var arguments = node.Arguments.Select(arg => VisitAndReturnCypher(arg)).ToList();

        var expression = node.Method.Name switch
        {
            "Contains" => $"{target} CONTAINS {arguments[0]}",
            "StartsWith" => $"{target} STARTS WITH {arguments[0]}",
            "EndsWith" => $"{target} ENDS WITH {arguments[0]}",
            "ToLower" => $"toLower({target})",
            "ToUpper" => $"toUpper({target})",
            "Trim" => $"trim({target})",
            "TrimStart" => $"ltrim({target})",
            "TrimEnd" => $"rtrim({target})",
            "Replace" => $"replace({target}, {arguments[0]}, {arguments[1]})",
            "Substring" => arguments.Count == 1
                ? $"substring({target}, {arguments[0]})"
                : $"substring({target}, {arguments[0]}, {arguments[1]})",
            "Length" => $"length({target})",
            "IndexOf" when arguments.Count == 1 => $"apoc.text.indexOf({target}, {arguments[0]}, 0)",
            "LastIndexOf" when arguments.Count == 1 => $"apoc.text.lastIndexOf({target}, {arguments[0]})",
            "PadLeft" when arguments.Count == 1 => $"apoc.text.lpad({target}, {arguments[0]}, ' ')",
            "PadLeft" when arguments.Count == 2 => $"apoc.text.lpad({target}, {arguments[0]}, {arguments[1]})",
            "PadRight" when arguments.Count == 1 => $"apoc.text.rpad({target}, {arguments[0]}, ' ')",
            "PadRight" when arguments.Count == 2 => $"apoc.text.rpad({target}, {arguments[0]}, {arguments[1]})",
            "CompareTo" when arguments.Count == 1 => $"apoc.text.compareTo({target}, {arguments[0]})",
            _ => throw new NotSupportedException($"String method {node.Method.Name} is not supported")
        };

        _logger.LogDebug("String method result: {Expression}", expression);
        return Expression.Constant(expression);
    }

    private Expression VisitMathMethod(MethodCallExpression node)
    {
        _logger.LogDebug("Visiting Math method: {MethodName}", node.Method.Name);

        var arguments = node.Arguments.Select(arg => VisitAndReturnCypher(arg)).ToList();

        var expression = node.Method.Name switch
        {
            "Abs" when arguments.Count == 1 => $"abs({arguments[0]})",
            "Floor" when arguments.Count == 1 => $"floor({arguments[0]})",
            "Ceiling" when arguments.Count == 1 => $"ceil({arguments[0]})",
            "Round" when arguments.Count == 1 => $"round({arguments[0]})",
            "Round" when arguments.Count == 2 => $"round({arguments[0]}, {arguments[1]})",
            "Min" when arguments.Count == 2 => $"min({arguments[0]}, {arguments[1]})",
            "Max" when arguments.Count == 2 => $"max({arguments[0]}, {arguments[1]})",
            "Pow" when arguments.Count == 2 => $"({arguments[0]} ^ {arguments[1]})",
            "Sqrt" when arguments.Count == 1 => $"sqrt({arguments[0]})",
            "Sign" when arguments.Count == 1 => $"sign({arguments[0]})",
            "Sin" when arguments.Count == 1 => $"sin({arguments[0]})",
            "Cos" when arguments.Count == 1 => $"cos({arguments[0]})",
            "Tan" when arguments.Count == 1 => $"tan({arguments[0]})",
            "Asin" when arguments.Count == 1 => $"asin({arguments[0]})",
            "Acos" when arguments.Count == 1 => $"acos({arguments[0]})",
            "Atan" when arguments.Count == 1 => $"atan({arguments[0]})",
            "Atan2" when arguments.Count == 2 => $"atan2({arguments[0]}, {arguments[1]})",
            "Log" when arguments.Count == 1 => $"log({arguments[0]})",
            "Log10" when arguments.Count == 1 => $"log10({arguments[0]})",
            "Exp" when arguments.Count == 1 => $"exp({arguments[0]})",
            _ => throw new NotSupportedException($"Math method {node.Method.Name} is not supported")
        };

        _logger.LogDebug("Math method result: {Expression}", expression);
        return Expression.Constant(expression);
    }

    private Expression VisitConvertMethod(MethodCallExpression node)
    {
        _logger.LogDebug("Visiting Convert method: {MethodName}", node.Method.Name);

        var argument = VisitAndReturnCypher(node.Arguments[0]);

        var expression = node.Method.Name switch
        {
            "ToInt32" or "ToInt16" or "ToInt64" => $"toInteger({argument})",
            "ToDouble" or "ToSingle" or "ToDecimal" => $"toFloat({argument})",
            "ToString" => $"toString({argument})",
            "ToBoolean" => $"toBoolean({argument})",
            "ToDateTime" => $"datetime({argument})",
            _ => throw new NotSupportedException($"Convert method {node.Method.Name} is not supported")
        };

        _logger.LogDebug("Convert method result: {Expression}", expression);
        return Expression.Constant(expression);
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        _logger.LogDebug("Visiting member: {Member}", node.Member.Name);

        // Handle static DateTime properties
        if (node.Expression == null && node.Member.DeclaringType == typeof(DateTime))
        {
            var expression = node.Member.Name switch
            {
                "Now" => "datetime()",
                "UtcNow" => "datetime.realtime()",
                "Today" => "date()",
                _ => throw new NotSupportedException($"DateTime static property {node.Member.Name} is not supported")
            };

            _logger.LogDebug("Translated DateTime.{Property} to {Expression}", node.Member.Name, expression);
            return Expression.Constant(expression);
        }

        // Handle nested member access - check if this is a closure variable chain
        if (node.Expression is MemberExpression nestedMember)
        {
            // Check if this is a closure variable chain (value(...).p1.Id)
            if (IsClosureVariableChain(node))
            {
                _logger.LogDebug("Processing closure variable chain for member: {Member}", node.Member.Name);

                var closureValue = EvaluateClosureExpression(node);
                var paramRef = _queryBuilder.AddParameter(closureValue);
                _logger.LogDebug("Resolved closure variable chain {Member} to parameter reference: {ParamRef}", node.Member.Name, paramRef);
                return Expression.Constant(paramRef);
            }

            // Check if this is path segment property access (e.g., k.Relationship.Since)
            if (nestedMember.Expression is ParameterExpression p &&
                typeof(IGraphPathSegment).IsAssignableFrom(p.Type))
            {
                _logger.LogDebug("Processing path segment property access: {Expression}", node);

                if (nestedMember.Member.Name == nameof(IGraphPathSegment.Relationship))
                {
                    _logger.LogDebug("Mapping path segment relationship property {Property} to r.{Property}", node.Member.Name, node.Member.Name);
                    return Expression.Constant($"r.{node.Member.Name}");
                }

                if (nestedMember.Member.Name == nameof(IGraphPathSegment.StartNode))
                {
                    _logger.LogDebug("Mapping path segment start node property {Property} to src.{Property}", node.Member.Name, node.Member.Name);
                    return Expression.Constant($"src.{node.Member.Name}");
                }

                if (nestedMember.Member.Name == nameof(IGraphPathSegment.EndNode))
                {
                    _logger.LogDebug("Mapping path segment end node property {Property} to tgt.{Property}", node.Member.Name, node.Member.Name);
                    return Expression.Constant($"tgt.{node.Member.Name}");
                }
            }

            // Check if this is IGrouping.Key property access (e.g., g.Key.FirstName)
            if (nestedMember.Expression is ParameterExpression groupParam &&
                groupParam.Type.IsGenericType &&
                groupParam.Type.GetGenericTypeDefinition() == typeof(IGrouping<,>) &&
                nestedMember.Member.Name == "Key")
            {
                _logger.LogDebug("Processing IGrouping.Key property access: {Expression}", node);

                // Get the GROUP BY expression (what g.Key represents)
                var groupByExpression = _scope.GroupByExpression;
                if (!string.IsNullOrEmpty(groupByExpression))
                {
                    _logger.LogDebug("Mapping IGrouping.Key.{Property} to {GroupByExpression}.{Property}", node.Member.Name, groupByExpression, node.Member.Name);
                    return Expression.Constant($"{groupByExpression}.{node.Member.Name}");
                }

                // Fallback if no GROUP BY expression was stored
                var alias = _contextAlias ?? _scope.CurrentAlias ?? "src";
                _logger.LogDebug("No GROUP BY expression stored, falling back to {Alias}.{Property}", alias, node.Member.Name);
                return Expression.Constant($"{alias}.{node.Member.Name}");
            }

            // Check if this is a complex property navigation (has nested member access)
            if (HasComplexPropertyNavigation(node))
            {
                var (alias, propertyName) = HandleComplexPropertyNavigation(node);
                return Expression.Constant($"{alias}.{propertyName}");
            }

            // Check if this is a string property access that needs special handling
            if (node.Member.DeclaringType == typeof(string))
            {
                var prnt = VisitAndReturnCypher(nestedMember);
                var cypherExpression = node.Member.Name switch
                {
                    "Length" => $"size({prnt})",
                    _ => $"{prnt}.{node.Member.Name}" // Default to property access for unsupported properties
                };
                _logger.LogDebug("Translated string property {Property} to {Expression}", node.Member.Name, cypherExpression);
                return Expression.Constant(cypherExpression);
            }

            // Regular nested member access
            var parent = VisitAndReturnCypher(nestedMember);
            return Expression.Constant($"{parent}.{node.Member.Name}");
        }

        // Handle parameter property access (including through conversions)
        var parameterExpression = GetParameterExpression(node.Expression);
        if (parameterExpression != null)
        {
            var param = parameterExpression;
            _logger.LogDebug("Processing parameter {ParamName} of type {ParamType}, RootType is {RootType}, CurrentAlias is {CurrentAlias}",
                param.Name, param.Type.Name, _scope.RootType?.Name, _scope.CurrentAlias);

            // Special handling for path segment parameters
            if (typeof(IGraphPathSegment).IsAssignableFrom(param.Type))
            {
                _logger.LogDebug("Processing path segment parameter property: {Property}", node.Member.Name);

                var propertyMapping = node.Member.Name switch
                {
                    nameof(IGraphPathSegment.StartNode) => "src",
                    nameof(IGraphPathSegment.EndNode) => "tgt",
                    nameof(IGraphPathSegment.Relationship) => "r",
                    _ => throw new NotSupportedException($"Path segment property '{node.Member.Name}' is not supported")
                };

                _logger.LogDebug("Mapped path segment property {Property} to alias {Alias}", node.Member.Name, propertyMapping);
                return Expression.Constant(propertyMapping);
            }

            // Special handling for IGrouping.Key in GROUP BY scenarios
            if (param.Type.IsGenericType && param.Type.GetGenericTypeDefinition() == typeof(IGrouping<,>))
            {
                if (node.Member.Name == "Key")
                {
                    // In GROUP BY scenarios, g.Key should reference the grouped field expression
                    var groupByExpression = _scope.GroupByExpression;
                    if (!string.IsNullOrEmpty(groupByExpression))
                    {
                        _logger.LogDebug("Mapping IGrouping.Key to stored GROUP BY expression: {Expression}", groupByExpression);
                        return Expression.Constant(groupByExpression);
                    }

                    // Fallback if no GROUP BY expression was stored
                    var a = _contextAlias ?? _scope.CurrentAlias ?? "src";
                    _logger.LogDebug("No GROUP BY expression stored, falling back to alias: {Alias}", a);
                    return Expression.Constant(a);
                }
            }

            // Special handling for relationship StartNodeId and EndNodeId properties
            if (typeof(Model.IRelationship).IsAssignableFrom(param.Type))
            {
                if (node.Member.Name == nameof(Model.IRelationship.StartNodeId))
                {
                    var idPropertyInfo = typeof(Model.IEntity).GetProperty(nameof(Model.IEntity.Id), BindingFlags.Public | BindingFlags.Instance);
                    if (idPropertyInfo == null)
                    {
                        throw new InvalidOperationException($"Property '{nameof(Model.IEntity.Id)}' not found on type '{typeof(Model.IEntity)}'.");
                    }

                    var idLabel = Labels.GetLabelFromProperty(idPropertyInfo);
                    _logger.LogDebug("Mapping relationship StartNodeId to src.{IdLabel}", idLabel);
                    return Expression.Constant($"src.{idLabel}");
                }

                if (node.Member.Name == nameof(Model.IRelationship.EndNodeId))
                {
                    var idPropertyInfo = typeof(Model.IEntity).GetProperty(nameof(Model.IEntity.Id), BindingFlags.Public | BindingFlags.Instance);
                    if (idPropertyInfo == null)
                    {
                        throw new InvalidOperationException($"Property '{nameof(Model.IEntity.Id)}' not found on type '{typeof(Model.IEntity)}'.");
                    }

                    var idLabel = Labels.GetLabelFromProperty(idPropertyInfo);
                    _logger.LogDebug("Mapping relationship EndNodeId to tgt.{IdLabel}", idLabel);
                    return Expression.Constant($"tgt.{idLabel}");
                }
            }

            // For all other cases, prefer contextAlias over parameter name
            var alias = _contextAlias ??
                (param.Type == _scope.RootType || _scope.CurrentAlias != null
                    ? (_scope.CurrentAlias ?? _scope.GetOrCreateAlias(param.Type, "src"))
                    : _scope.GetOrCreateAlias(param.Type));

            // Check if this is a string property access that needs special handling
            if (node.Member.DeclaringType == typeof(string))
            {
                // We need to get the property being accessed on the parameter, not the string property itself
                // For example, if we have p.Bio.Length, we need to get "Bio" not "Length"
                var baseProperty = node.Expression;
                if (baseProperty is MemberExpression baseMember)
                {
                    var cypherExpression = node.Member.Name switch
                    {
                        "Length" => $"size({alias}.{baseMember.Member.Name})",
                        _ => $"{alias}.{node.Member.Name}" // Default to property access for unsupported properties
                    };
                    _logger.LogDebug("Translated string property {Property} to {Expression}", node.Member.Name, cypherExpression);
                    return Expression.Constant(cypherExpression);
                }
            }

            _logger.LogDebug("Found parameter member access: {Alias}.{Member}", alias, node.Member.Name);
            return Expression.Constant($"{alias}.{node.Member.Name}");
        }

        // Handle closure variables (e.g., simple cases)
        if (node.Expression is ConstantExpression)
        {
            _logger.LogDebug("Processing closure variable for member: {Member}", node.Member.Name);

            var closureValue = EvaluateClosureExpression(node);
            if (closureValue == null)
            {
                throw new NotSupportedException($"Cannot resolve member {node.Member.Name} on a null closure variable.");
            }

            var paramRef = _queryBuilder.AddParameter(closureValue);
            _logger.LogDebug("Resolved closure variable member {Member} to parameter reference: {ParamRef}", node.Member.Name, paramRef);
            return Expression.Constant(paramRef);
        }

        throw new NotSupportedException($"Member expression {node.Member.Name} is not supported. Expression: {node}, Expression type: {node.Expression?.GetType().Name}, Current alias: {_scope.CurrentAlias}, Context alias: {_contextAlias}");
    }

    private static bool IsCollectionMethod(MethodCallExpression node)
    {
        var declaringType = node.Method.DeclaringType;
        if (declaringType == null) return false;

        // Handle extension methods from Enumerable, MemoryExtensions, etc.
        if (node.Method.IsStatic &&
            (declaringType == typeof(Enumerable) ||
             declaringType.Name == "MemoryExtensions" ||
             declaringType.Namespace?.StartsWith("System.Linq") == true))
        {
            return node.Method.Name is "Contains" or "Any" or "All";
        }

        // Handle instance methods on collections
        var methodName = node.Method.Name;
        if (methodName is "Contains" or "Any" or "All")
        {
            // Check if the declaring type implements IEnumerable
            return typeof(System.Collections.IEnumerable).IsAssignableFrom(declaringType) ||
                   declaringType.IsArray;
        }

        return false;
    }

    private Expression VisitCollectionMethod(MethodCallExpression node)
    {
        _logger.LogDebug("Visiting collection method: {Method}", node.Method.Name);

        var methodName = node.Method.Name;

        if (methodName == "Contains")
        {
            // Handle Contains method - two different patterns:
            // 1. collection.Contains(item) - instance method
            // 2. Enumerable.Contains(collection, item) - static method

            if (node.Method.IsStatic)
            {
                // Static method: Enumerable.Contains(collection, item) or MemoryExtensions.Contains(span, item)
                if (node.Arguments.Count >= 2)
                {
                    var collection = VisitAndReturnCypher(node.Arguments[0]);
                    var item = VisitAndReturnCypher(node.Arguments[1]);

                    _logger.LogDebug("Translated static Contains to: {Item} IN {Collection}", item, collection);
                    return Expression.Constant($"{item} IN {collection}");
                }
            }
            else
            {
                // Instance method: collection.Contains(item)
                if (node.Object != null && node.Arguments.Count == 1)
                {
                    var collection = VisitAndReturnCypher(node.Object);
                    var item = VisitAndReturnCypher(node.Arguments[0]);

                    _logger.LogDebug("Translated instance Contains to: {Item} IN {Collection}", item, collection);
                    return Expression.Constant($"{item} IN {collection}");
                }
            }
        }

        throw new NotSupportedException($"Collection method {node.Method.DeclaringType?.Name}.{node.Method.Name} is not supported in Cypher expressions");
    }

    private static bool IsAggregationMethod(MethodCallExpression node)
    {
        if (node.Method.DeclaringType == null)
            return false;

        // Check for LINQ aggregation methods from Enumerable
        if (node.Method.DeclaringType == typeof(Enumerable))
        {
            return node.Method.Name is "Count" or "Sum" or "Average" or "Min" or "Max";
        }

        return false;
    }

    private Expression VisitAggregationMethod(MethodCallExpression node)
    {
        _logger.LogDebug("Visiting aggregation method: {Method}", node.Method.Name);

        var alias = _contextAlias ?? _scope.CurrentAlias ?? "src";

        string cypherExpression;

        if (node.Method.Name == "Count" && node.Arguments.Count == 1)
        {
            cypherExpression = $"count({alias})";
        }
        else if (node.Method.Name == "Count" && node.Arguments.Count == 2)
        {
            var lambda = ExtractLambda(node.Arguments[1]);
            if (lambda == null)
                throw new GraphException("Count with predicate requires a lambda expression");
            var predicate = VisitAndReturnCypher(lambda.Body);
            cypherExpression = $"count(CASE WHEN {predicate} THEN 1 END)";
        }
        else if (node.Method.Name == "Sum" && node.Arguments.Count == 2)
        {
            var lambda = ExtractLambda(node.Arguments[1]);
            if (lambda == null)
                throw new GraphException("Sum requires a lambda expression");
            var sumExpr = VisitAndReturnCypher(lambda.Body);
            cypherExpression = $"sum({sumExpr})";
        }
        else if (node.Method.Name == "Average" && node.Arguments.Count == 2)
        {
            var lambda = ExtractLambda(node.Arguments[1]);
            if (lambda == null)
                throw new GraphException("Average requires a lambda expression");
            var avgExpr = VisitAndReturnCypher(lambda.Body);
            cypherExpression = $"avg({avgExpr})";
        }
        else if (node.Method.Name == "Min" && node.Arguments.Count == 2)
        {
            var lambda = ExtractLambda(node.Arguments[1]);
            if (lambda == null)
                throw new GraphException("Min requires a lambda expression");
            var minExpr = VisitAndReturnCypher(lambda.Body);
            cypherExpression = $"min({minExpr})";
        }
        else if (node.Method.Name == "Max" && node.Arguments.Count == 2)
        {
            var lambda = ExtractLambda(node.Arguments[1]);
            if (lambda == null)
                throw new GraphException("Max requires a lambda expression");
            var maxExpr = VisitAndReturnCypher(lambda.Body);
            cypherExpression = $"max({maxExpr})";
        }
        else
        {
            throw new NotSupportedException($"Aggregation method {node.Method.Name} with {node.Arguments.Count} arguments is not supported");
        }

        _logger.LogDebug("Translated aggregation method {Method} to {CypherExpression}", node.Method.Name, cypherExpression);
        return Expression.Constant(cypherExpression);
    }

    private Expression VisitDynamicEntityMethod(MethodCallExpression node)
    {
        _logger.LogDebug("Visiting dynamic entity method: {MethodName}", node.Method.Name);

        if (node.Arguments.Count < 1)
        {
            throw new NotSupportedException($"Dynamic entity method {node.Method.Name} requires at least one argument");
        }

        var target = VisitAndReturnCypher(node.Arguments[0]);
        var arguments = node.Arguments.Skip(1).ToList();

        string expression = node.Method.Name switch
        {
            "HasLabel" when arguments.Count == 1 =>
                $"{VisitAndReturnCypher(arguments[0])} IN labels({target})",
            "HasType" when arguments.Count == 1 =>
                $"type({target}) = {VisitAndReturnCypher(arguments[0])}",
            "HasProperty" when arguments.Count == 1 =>
                arguments[0] is ConstantExpression constExpr && constExpr.Value is string propName
                    ? $"{target}.{propName} IS NOT NULL"
                    : $"{target}.{{{VisitAndReturnCypher(arguments[0])}}} IS NOT NULL",
            "GetProperty" when arguments.Count == 1 =>
                arguments[0] is ConstantExpression constExpr && constExpr.Value is string propName
                    ? $"{target}.{propName}"
                    : $"{target}.{{{VisitAndReturnCypher(arguments[0])}}}",
            _ => throw new NotSupportedException($"Dynamic entity method {node.Method.Name} is not supported")
        };

        _logger.LogDebug("Dynamic entity method result: {Expression}", expression);
        return Expression.Constant(expression);
    }

    /// <summary>
    /// Extracts the underlying ParameterExpression from an expression, handling conversions.
    /// </summary>
    private static ParameterExpression? GetParameterExpression(Expression? expression)
    {
        return expression switch
        {
            ParameterExpression param => param,
            UnaryExpression { NodeType: ExpressionType.Convert } unary => GetParameterExpression(unary.Operand),
            _ => null
        };
    }

    protected override Expression VisitBinary(BinaryExpression node)
    {
        var left = VisitAndReturnCypher(node.Left);
        var right = VisitAndReturnCypher(node.Right);

        // Handle null comparisons specially
        if ((left == "null" || right == "null") &&
            (node.NodeType == ExpressionType.Equal || node.NodeType == ExpressionType.NotEqual))
        {
            var nonNullOperand = left == "null" ? right : left;
            var cypherOperator = node.NodeType == ExpressionType.Equal ? "IS NULL" : "IS NOT NULL";

            _logger.LogDebug("Translated null comparison: {Operand} {Operator}", nonNullOperand, cypherOperator);
            return Expression.Constant($"{nonNullOperand} {cypherOperator}");
        }

        // Handle DateTime comparisons specifically
        if (node.Left.Type == typeof(DateTime) || node.Right.Type == typeof(DateTime))
        {
            var operatorSymbol = node.NodeType switch
            {
                ExpressionType.LessThan => "<",
                ExpressionType.LessThanOrEqual => "<=",
                ExpressionType.GreaterThan => ">",
                ExpressionType.GreaterThanOrEqual => ">=",
                ExpressionType.Equal => "=",
                ExpressionType.NotEqual => "<>",
                _ => throw new NotSupportedException($"Binary operator {node.NodeType} is not supported for DateTime")
            };

            // Wrap DateTime parameters in datetime() function for proper type handling
            var leftExpression = node.Left.Type == typeof(DateTime) && IsParameterReference(left) ? $"datetime({left})" : left;
            var rightExpression = node.Right.Type == typeof(DateTime) && IsParameterReference(right) ? $"datetime({right})" : right;

            _logger.LogDebug("Translated DateTime binary expression: {Left} {Operator} {Right}", leftExpression, operatorSymbol, rightExpression);
            return Expression.Constant($"{leftExpression} {operatorSymbol} {rightExpression}");
        }

        // Handle logical operations
        if (node.NodeType == ExpressionType.OrElse || node.NodeType == ExpressionType.AndAlso)
        {
            var operatorSymbol = node.NodeType == ExpressionType.OrElse ? "OR" : "AND";
            _logger.LogDebug("Translated logical binary expression: ({Left} {Operator} {Right})", left, operatorSymbol, right);
            return Expression.Constant($"({left} {operatorSymbol} {right})");
        }

        // Handle general binary expressions
        var symbol = node.NodeType switch
        {
            ExpressionType.Equal => "=",
            ExpressionType.NotEqual => "<>",
            ExpressionType.LessThan => "<",
            ExpressionType.LessThanOrEqual => "<=",
            ExpressionType.GreaterThan => ">",
            ExpressionType.GreaterThanOrEqual => ">=",
            ExpressionType.Add => "+",
            ExpressionType.Subtract => "-",
            ExpressionType.Multiply => "*",
            ExpressionType.Divide => "/",
            ExpressionType.Modulo => "%",
            _ => throw new NotSupportedException($"Binary operator {node.NodeType} is not supported")
        };

        return Expression.Constant($"{left} {symbol} {right}");
    }

    protected override Expression VisitUnary(UnaryExpression node)
    {
        // Handle cast operations
        if (node.NodeType == ExpressionType.Convert || node.NodeType == ExpressionType.ConvertChecked)
        {
            var operand = VisitAndReturnCypher(node.Operand);
            var targetType = node.Type;

            // Handle nullable types
            var actualType = Nullable.GetUnderlyingType(targetType) ?? targetType;

            var expression = actualType.Name switch
            {
                "Int32" or "Int16" or "Int64" => $"toInteger({operand})",
                "Double" or "Single" or "Decimal" => $"toFloat({operand})",
                "String" => $"toString({operand})",
                "Boolean" => $"toBoolean({operand})",
                "DateTime" => $"datetime({operand})",
                _ => operand // For complex types or unsupported casts, just return the operand
            };

            _logger.LogDebug("Cast operation result: {Expression}", expression);
            return Expression.Constant(expression);
        }

        // Handle NOT operations
        if (node.NodeType == ExpressionType.Not)
        {
            var operand = VisitAndReturnCypher(node.Operand);
            return Expression.Constant($"NOT ({operand})");
        }

        return base.VisitUnary(node);
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        _logger.LogDebug("Visiting constant: {Value}", node.Value);

        if (node.Value == null)
        {
            return Expression.Constant("null");
        }

        var paramRef = _queryBuilder.AddParameter(node.Value);
        _logger.LogDebug("Added parameter: {Value} -> {ParamRef}", node.Value, paramRef);
        return Expression.Constant(paramRef);
    }

    protected override Expression VisitParameter(ParameterExpression node)
    {
        // If this parameter is a relationship, always use "r"
        if (typeof(Model.IRelationship).IsAssignableFrom(node.Type))
        {
            _logger.LogDebug("Parameter expression for relationship: using alias 'r'");
            return Expression.Constant("r");
        }

        var result = _scope.CurrentAlias ?? throw new InvalidOperationException("No current alias set");
        _logger.LogDebug("Parameter expression result: {Result}", result);
        return Expression.Constant(result);
    }

    protected override Expression VisitConditional(ConditionalExpression node)
    {
        _logger.LogDebug("Visiting conditional expression (ternary operator)");

        var test = VisitAndReturnCypher(node.Test);
        var ifTrue = VisitAndReturnCypher(node.IfTrue);
        var ifFalse = VisitAndReturnCypher(node.IfFalse);

        var cypherExpression = $"CASE WHEN {test} THEN {ifTrue} ELSE {ifFalse} END";

        _logger.LogDebug("Translated conditional expression to: {Expression}", cypherExpression);
        return Expression.Constant(cypherExpression);
    }

    // Helper methods moved from BaseExpressionVisitor
    private static bool IsEvaluableMethodCall(MethodCallExpression node)
    {
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

        if (evaluableMethods.Contains(node.Method.Name))
        {
            return true;
        }

        if (node.Method.DeclaringType == typeof(DateTime) &&
            (node.Method.Name == "get_UtcNow" || node.Method.Name == "get_Now"))
        {
            return true;
        }

        var parameterFinder = new ParameterExpressionFinder();
        parameterFinder.Visit(node);
        return !parameterFinder.HasParameters;
    }

    private bool IsClosureVariableChain(MemberExpression node)
    {
        // Check if this is a chain of member accesses starting from a closure variable
        var current = node;
        while (current.Expression is MemberExpression parentMember)
        {
            current = parentMember;
        }

        // If we end up at a ConstantExpression, this is a closure variable chain
        return current.Expression is ConstantExpression;
    }

    private object? EvaluateClosureExpression(MemberExpression node)
    {
        try
        {
            var lambda = Expression.Lambda(node);
            var compiled = lambda.Compile();
            return compiled.DynamicInvoke();
        }
        catch (Exception ex)
        {
            throw new GraphException($"Failed to evaluate closure expression '{node}': {ex.Message}", ex);
        }
    }

    private (string alias, string? propertyName) HandleComplexPropertyNavigation(MemberExpression node)
    {
        _logger.LogDebug("Processing complex property navigation: {Expression}", node);

        // Find the root parameter
        var current = node;
        var propertyChain = new List<string> { current.Member.Name };

        while (current.Expression is MemberExpression parentMember)
        {
            propertyChain.Insert(0, parentMember.Member.Name);
            current = parentMember;
        }

        if (current.Expression is not ParameterExpression rootParam)
        {
            throw new NotSupportedException($"Complex property navigation must start from a parameter, but found {current.Expression?.GetType()}");
        }

        // For now, handle simple one-level complex properties
        if (propertyChain.Count == 2)
        {
            var parentProperty = propertyChain[0];
            var childProperty = propertyChain[1];

            var parentAlias = _scope.CurrentAlias ?? _scope.GetOrCreateAlias(rootParam.Type, "src");
            var propertyAlias = $"{parentAlias}_{parentProperty.ToLowerInvariant()}";

            _logger.LogDebug("Adding MATCH clause for complex property: {Property} -> {Alias}", parentProperty, propertyAlias);
            _queryBuilder.AddMatchClause($"({parentAlias})-[:{GraphDataModel.PropertyNameToRelationshipTypeName(parentProperty)}]->({propertyAlias})");

            return (propertyAlias, childProperty);
        }

        throw new NotSupportedException($"Multi-level complex property navigation is not yet supported: {string.Join(".", propertyChain)}");
    }

    private bool HasComplexPropertyNavigation(MemberExpression node)
    {
        return IsComplexPropertyNavigation(node);
    }

    private bool IsComplexPropertyNavigation(MemberExpression node)
    {
        if (node.Expression is not MemberExpression parentMember)
            return false;

        if (parentMember.Expression is not ParameterExpression)
            return false;

        // Check if the parent property is a complex property using the helper
        if (parentMember.Member is PropertyInfo parentProperty)
        {
            return ComplexPropertyHelper.IsComplexProperty(parentProperty);
        }

        return false;
    }

    private static LambdaExpression? ExtractLambda(Expression expression)
    {
        return expression switch
        {
            LambdaExpression lambda => lambda,
            UnaryExpression { NodeType: ExpressionType.Quote, Operand: LambdaExpression quotedLambda } => quotedLambda,
            _ => null
        };
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
}