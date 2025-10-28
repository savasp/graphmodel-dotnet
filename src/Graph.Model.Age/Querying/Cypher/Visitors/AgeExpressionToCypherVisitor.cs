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

using System.Linq.Expressions;
using Cvoya.Graph.Model.Age.Querying.Cypher.Builders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Translates .NET LINQ expressions to Cypher expressions for AGE.
/// Simplified version focused on common query operations.
/// </summary>
internal sealed class AgeExpressionToCypherVisitor : ExpressionVisitor
{
    private readonly AgeCypherQueryBuilder? _queryBuilder;
    private readonly Dictionary<string, object?>? _parametersDict;
    private readonly ILogger _logger;
    private readonly string _alias;
    private readonly ParameterExpression? _pathSegmentParameter;
    private readonly string? _sourceAlias;
    private readonly string? _relationshipAlias;
    private readonly string? _targetAlias;

    // Constructor for new approach using builder
    public AgeExpressionToCypherVisitor(
        AgeCypherQueryBuilder queryBuilder,
        ILogger? logger = null,
        string alias = "n")
    {
        _queryBuilder = queryBuilder ?? throw new ArgumentNullException(nameof(queryBuilder));
        _parametersDict = null;
        _logger = logger ?? NullLogger.Instance;
        _alias = alias;
        _pathSegmentParameter = null;
        _sourceAlias = null;
        _relationshipAlias = null;
        _targetAlias = null;
    }

    // Constructor for path segment context
    public AgeExpressionToCypherVisitor(
        AgeCypherQueryBuilder queryBuilder,
        ILogger? logger,
        string alias,
        ParameterExpression pathSegmentParameter)
    {
        _queryBuilder = queryBuilder ?? throw new ArgumentNullException(nameof(queryBuilder));
        _parametersDict = null;
        _logger = logger ?? NullLogger.Instance;
        _alias = alias;
        _pathSegmentParameter = pathSegmentParameter;
        _sourceAlias = null;
        _relationshipAlias = null;
        _targetAlias = null;
    }

    // Constructor for path segment context with hop-specific aliases
    public AgeExpressionToCypherVisitor(
        AgeCypherQueryBuilder queryBuilder,
        ILogger? logger,
        string alias,
        ParameterExpression pathSegmentParameter,
        string sourceAlias,
        string relationshipAlias,
        string targetAlias)
    {
        _queryBuilder = queryBuilder ?? throw new ArgumentNullException(nameof(queryBuilder));
        _parametersDict = null;
        _logger = logger ?? NullLogger.Instance;
        _alias = alias;
        _pathSegmentParameter = pathSegmentParameter;
        _sourceAlias = sourceAlias;
        _relationshipAlias = relationshipAlias;
        _targetAlias = targetAlias;
    }

    // Constructor for legacy approach using dictionary
    public AgeExpressionToCypherVisitor(
        Dictionary<string, object?> parameters,
        ILogger? logger = null,
        string alias = "n")
    {
        _parametersDict = parameters ?? throw new ArgumentNullException(nameof(parameters));
        _queryBuilder = null;
        _logger = logger ?? NullLogger.Instance;
        _alias = alias;
        _pathSegmentParameter = null;
        _sourceAlias = null;
        _relationshipAlias = null;
        _targetAlias = null;
    }

    private string AddParameter(object? value)
    {
        if (_queryBuilder != null)
        {
            // New approach: use the builder
            return _queryBuilder.AddParameter(value);
        }
        else if (_parametersDict != null)
        {
            // Legacy approach: use the dictionary
            var paramName = $"param_{_parametersDict.Count}";
            _parametersDict[paramName] = value;
            return $"${paramName}";
        }
        else
        {
            throw new InvalidOperationException("Neither query builder nor parameters dictionary is available");
        }
    }

    /// <summary>
    /// Visits an expression and returns the resulting Cypher string.
    /// </summary>
    public string VisitAndReturnCypher(Expression expression)
    {
        _logger.LogDebug("Starting VisitAndReturnCypher with expression type: {ExpressionType}", expression.GetType().Name);
        var result = Visit(expression);
        _logger.LogDebug("VisitAndReturnCypher result type: {ResultType}, value: {Result}", result?.GetType().Name, result);
        return result switch
        {
            ConstantExpression { Value: string cypherString } => cypherString,
            _ => throw new InvalidOperationException($"Expected Cypher string result, got {result?.GetType()}")
        };
    }

    protected override Expression VisitBinary(BinaryExpression node)
    {
        var left = VisitAndReturnCypher(node.Left);
        var right = VisitAndReturnCypher(node.Right);

        var cypher = node.NodeType switch
        {
            ExpressionType.Equal => $"{left} = {right}",
            ExpressionType.NotEqual => $"{left} <> {right}",
            ExpressionType.GreaterThan => $"{left} > {right}",
            ExpressionType.GreaterThanOrEqual => $"{left} >= {right}",
            ExpressionType.LessThan => $"{left} < {right}",
            ExpressionType.LessThanOrEqual => $"{left} <= {right}",
            ExpressionType.AndAlso => $"({left} AND {right})",
            ExpressionType.OrElse => $"({left} OR {right})",
            ExpressionType.Add => $"({left} + {right})",
            ExpressionType.Subtract => $"({left} - {right})",
            ExpressionType.Multiply => $"({left} * {right})",
            ExpressionType.Divide => $"({left} / {right})",
            ExpressionType.Modulo => $"({left} % {right})",
            _ => throw new NotSupportedException($"Binary operator {node.NodeType} is not supported")
        };

        return Expression.Constant(cypher);
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        // Special handling when we have a path segment parameter context
        if (_pathSegmentParameter != null)
        {
            // Handle path segment property access (e.g., ps.EndNode.Age)
            if (node.Expression is MemberExpression pathSegmentMember &&
                pathSegmentMember.Expression is ParameterExpression pathParam &&
                pathParam == _pathSegmentParameter)
            {
                _logger.LogDebug("Processing path segment property access: {Parameter}.{SegmentProperty}.{NodeProperty}",
                    pathParam.Name, pathSegmentMember.Member.Name, node.Member.Name);

                var segmentProperty = pathSegmentMember.Member.Name;
                var nodeProperty = node.Member.Name;

                var alias = segmentProperty switch
                {
                    nameof(IGraphPathSegment.StartNode) => _sourceAlias ?? "src",
                    nameof(IGraphPathSegment.EndNode) => _targetAlias ?? "tgt",
                    nameof(IGraphPathSegment.Relationship) => _relationshipAlias ?? "r",
                    _ => throw new NotSupportedException($"Path segment property '{segmentProperty}' is not supported")
                };

                // Map C# property names to AGE property names
                var propertyName = MapPropertyName(nodeProperty);
                var result = $"{alias}.{propertyName}";
                _logger.LogDebug("Mapped path segment property {SegmentProperty}.{NodeProperty} to {Result}",
                    segmentProperty, nodeProperty, result);
                return Expression.Constant(result);
            }
        }

        // If accessing a member of the parameter (e.g., p.FirstName)
        if (node.Expression is ParameterExpression param)
        {
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

            // Map C# property names to AGE property names
            var propertyName = MapPropertyName(node.Member.Name);
            return Expression.Constant($"{_alias}.{propertyName}");
        }

        // Handle path segment property access through nested member expressions (e.g., ps.EndNode.FirstName)
        if (node.Expression is MemberExpression nestedMember &&
            nestedMember.Expression is ParameterExpression nestedParam &&
            typeof(IGraphPathSegment).IsAssignableFrom(nestedParam.Type))
        {
            _logger.LogDebug("Processing nested path segment property access: {Expression}", node);

            var segmentProperty = nestedMember.Member.Name;
            var nodeProperty = node.Member.Name;

            var alias = segmentProperty switch
            {
                nameof(IGraphPathSegment.StartNode) => _sourceAlias ?? "src",
                nameof(IGraphPathSegment.EndNode) => _targetAlias ?? "tgt",
                nameof(IGraphPathSegment.Relationship) => _relationshipAlias ?? "r",
                _ => throw new NotSupportedException($"Path segment property '{segmentProperty}' is not supported")
            };

            _logger.LogDebug("Path segment alias mapping: {SegmentProperty} -> {Alias} (source={Source}, target={Target}, rel={Rel})", 
                segmentProperty, alias, _sourceAlias, _targetAlias, _relationshipAlias);

            // Map C# property names to AGE property names
            var propertyName = MapPropertyName(nodeProperty);
            var result = $"{alias}.{propertyName}";
            _logger.LogDebug("Mapped path segment nested property {SegmentProperty}.{NodeProperty} to {Result}", 
                segmentProperty, nodeProperty, result);
            return Expression.Constant(result);
        }

        // Handle member access on a converted parameter (e.g., ((IEntity)p).Id)
        if (node.Expression is UnaryExpression unary && 
            (unary.NodeType == ExpressionType.Convert || unary.NodeType == ExpressionType.ConvertChecked) &&
            unary.Operand is ParameterExpression)
        {
            // Map C# property names to AGE property names
            var propertyName = MapPropertyName(node.Member.Name);
            return Expression.Constant($"{_alias}.{propertyName}");
        }

        // Handle chained member access (e.g., p.FirstName.Length)
        if (node.Expression is MemberExpression innerMember)
        {
            // Special case: string.Length property
            if (node.Member.Name == "Length" && node.Member.DeclaringType == typeof(string))
            {
                var innerValue = VisitAndReturnCypher(innerMember);
                return Expression.Constant($"size({innerValue})");
            }

            // Special case: DateTime properties (Year, Month, Day, etc.)
            if (node.Member.DeclaringType == typeof(DateTime))
            {
                var innerValue = VisitAndReturnCypher(innerMember);
                return node.Member.Name switch
                {
                    "Year" => Expression.Constant($"toInteger(substring({innerValue}, 0, 4))"),
                    "Month" => Expression.Constant($"toInteger(substring({innerValue}, 5, 2))"),
                    "Day" => Expression.Constant($"toInteger(substring({innerValue}, 8, 2))"),
                    "Hour" => Expression.Constant($"toInteger(substring({innerValue}, 11, 2))"),
                    "Minute" => Expression.Constant($"toInteger(substring({innerValue}, 14, 2))"),
                    "Second" => Expression.Constant($"toInteger(substring({innerValue}, 17, 2))"),
                    "DayOfWeek" => Expression.Constant($"toInteger(substring({innerValue}, 0, 4)) % 7"), // Approximate
                    _ => throw new NotSupportedException($"DateTime property {node.Member.Name} is not supported")
                };
            }
        }

        // Otherwise, evaluate the member access (e.g., local variable)
        try
        {
            var objectMember = Expression.Convert(node, typeof(object));
            var getterLambda = Expression.Lambda<Func<object>>(objectMember);
            var getter = getterLambda.Compile();
            var value = getter();

            var paramRef = AddParameter(value);
            return Expression.Constant(paramRef);
        }
        catch
        {
            throw new NotSupportedException($"Cannot evaluate member expression: {node}");
        }
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        _logger.LogDebug("Visiting constant: {Value} (Type: {Type})", node.Value, node.Type);
        // Add constant value as a parameter
        var paramRef = AddParameter(node.Value);
        _logger.LogDebug("Created parameter reference: {ParamRef}", paramRef);
        return Expression.Constant(paramRef);
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        // Handle Math methods
        if (node.Method.DeclaringType == typeof(Math))
        {
            return HandleMathMethod(node);
        }

        // Handle string methods
        if (node.Method.DeclaringType == typeof(string))
        {
            return HandleStringMethod(node);
        }

        // Handle DateTime static methods
        if (node.Method.DeclaringType == typeof(DateTime))
        {
            return HandleDateTimeMethod(node);
        }

        // Handle collection methods (Contains, Any, etc.) - but not string.Contains
        if (node.Method.Name == "Contains" && node.Arguments.Count >= 1 && node.Method.DeclaringType != typeof(string))
        {
            return HandleContainsMethod(node);
        }

        // For any other method call, try to evaluate it
        try
        {
            var objectMember = Expression.Convert(node, typeof(object));
            var getterLambda = Expression.Lambda<Func<object>>(objectMember);
            var getter = getterLambda.Compile();
            var value = getter();

            var paramRef = AddParameter(value);
            return Expression.Constant(paramRef);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to evaluate method call: {Method}", node.Method.Name);
            throw new NotSupportedException($"Method {node.Method.Name} is not supported");
        }
    }

    protected override Expression VisitUnary(UnaryExpression node)
    {
        // Handle NOT operator
        if (node.NodeType == ExpressionType.Not)
        {
            var operand = VisitAndReturnCypher(node.Operand);
            return Expression.Constant($"NOT ({operand})");
        }

        // Handle type conversions
        if (node.NodeType == ExpressionType.Convert || node.NodeType == ExpressionType.ConvertChecked)
        {
            var operandCypher = VisitAndReturnCypher(node.Operand);
            
            // For conversions to double or float, use toFloat() in Cypher
            if (node.Type == typeof(double) || node.Type == typeof(float) || 
                node.Type == typeof(double?) || node.Type == typeof(float?))
            {
                return Expression.Constant($"toFloat({operandCypher})");
            }
            
            // For other conversions, just pass through
            return Visit(node.Operand);
        }

        return base.VisitUnary(node);
    }

    protected override Expression VisitConditional(ConditionalExpression node)
    {
        // Translate C# ternary operator (condition ? trueValue : falseValue) to Cypher CASE expression
        // Example: p.Age >= 30 ? "Adult" : "Young" => CASE WHEN p.Age >= 30 THEN "Adult" ELSE "Young" END
        var condition = VisitAndReturnCypher(node.Test);
        var ifTrue = VisitAndReturnCypher(node.IfTrue);
        var ifFalse = VisitAndReturnCypher(node.IfFalse);

        var caseExpression = $"CASE WHEN {condition} THEN {ifTrue} ELSE {ifFalse} END";
        return Expression.Constant(caseExpression);
    }

    protected override Expression VisitNew(NewExpression node)
    {
        // Handle anonymous type creation: new { Start = ps.StartNode.FirstName, End = ps.EndNode.FirstName }
        // Convert to comma-separated list of property expressions
        var memberNames = new List<string>();
        var expressions = new List<string>();

        for (int i = 0; i < node.Arguments.Count; i++)
        {
            var argument = node.Arguments[i];
            var memberName = node.Members?[i]?.Name ?? $"Item{i + 1}";
            
            // Visit the argument to get the Cypher expression
            var cypherExpression = VisitAndReturnCypher(argument);
            
            memberNames.Add(memberName);
            expressions.Add($"{cypherExpression} AS c_{memberName}");
        }

        // Return the combined expression for the SELECT clause
        var selectExpression = string.Join(", ", expressions);
        return Expression.Constant(selectExpression);
    }

    private Expression HandleStringMethod(MethodCallExpression node)
    {
        var obj = node.Object != null ? VisitAndReturnCypher(node.Object) : null;

        return node.Method.Name switch
        {
            "StartsWith" when node.Arguments.Count == 1 =>
                Expression.Constant($"{obj} STARTS WITH {VisitAndReturnCypher(node.Arguments[0])}"),

            "EndsWith" when node.Arguments.Count == 1 =>
                Expression.Constant($"{obj} ENDS WITH {VisitAndReturnCypher(node.Arguments[0])}"),

            // Contains - check if string contains substring
            // Apache AGE doesn't support CONTAINS operator, use regex =~ instead
            // We need to build a regex pattern: '.*' + substring + '.*'
            "Contains" when node.Arguments.Count == 1 =>
                Expression.Constant($"{obj} =~ ('.*' + {VisitAndReturnCypher(node.Arguments[0])} + '.*')"),

            "ToLower" when node.Arguments.Count == 0 =>
                Expression.Constant($"toLower({obj})"),

            "ToUpper" when node.Arguments.Count == 0 =>
                Expression.Constant($"toUpper({obj})"),

            "Trim" when node.Arguments.Count == 0 =>
                Expression.Constant($"trim({obj})"),

            // Substring(start) - get substring from start to end
            "Substring" when node.Arguments.Count == 1 =>
                Expression.Constant($"substring({obj}, {VisitAndReturnCypher(node.Arguments[0])})"),

            // Substring(start, length) - get substring with specific length
            "Substring" when node.Arguments.Count == 2 =>
                Expression.Constant($"substring({obj}, {VisitAndReturnCypher(node.Arguments[0])}, {VisitAndReturnCypher(node.Arguments[1])})"),

            // Replace(oldValue, newValue)
            "Replace" when node.Arguments.Count == 2 =>
                Expression.Constant($"replace({obj}, {VisitAndReturnCypher(node.Arguments[0])}, {VisitAndReturnCypher(node.Arguments[1])})"),

            // Length property (accessed as method in expression tree)
            "get_Length" when node.Arguments.Count == 0 =>
                Expression.Constant($"size({obj})"),

            _ => throw new NotSupportedException($"String method {node.Method.Name} is not supported")
        };
    }

    private Expression HandleMathMethod(MethodCallExpression node)
    {
        // Math methods in Cypher
        return node.Method.Name switch
        {
            // abs(expression)
            "Abs" when node.Arguments.Count == 1 =>
                Expression.Constant($"abs({VisitAndReturnCypher(node.Arguments[0])})"),

            // ceil(expression)
            "Ceiling" when node.Arguments.Count == 1 =>
                Expression.Constant($"ceil({VisitAndReturnCypher(node.Arguments[0])})"),

            // floor(expression)
            "Floor" when node.Arguments.Count == 1 =>
                Expression.Constant($"floor({VisitAndReturnCypher(node.Arguments[0])})"),

            // round(expression)
            "Round" when node.Arguments.Count == 1 =>
                Expression.Constant($"round({VisitAndReturnCypher(node.Arguments[0])})"),

            // round(expression, precision)
            "Round" when node.Arguments.Count == 2 =>
                Expression.Constant($"round({VisitAndReturnCypher(node.Arguments[0])}, {VisitAndReturnCypher(node.Arguments[1])})"),

            // sqrt(expression)
            "Sqrt" when node.Arguments.Count == 1 =>
                Expression.Constant($"sqrt({VisitAndReturnCypher(node.Arguments[0])})"),

            // exp(expression)
            "Exp" when node.Arguments.Count == 1 =>
                Expression.Constant($"exp({VisitAndReturnCypher(node.Arguments[0])})"),

            // log(expression)
            "Log" when node.Arguments.Count == 1 =>
                Expression.Constant($"log({VisitAndReturnCypher(node.Arguments[0])})"),

            // log10(expression)
            "Log10" when node.Arguments.Count == 1 =>
                Expression.Constant($"log10({VisitAndReturnCypher(node.Arguments[0])})"),

            // sign(expression) - returns -1, 0, or 1
            "Sign" when node.Arguments.Count == 1 =>
                Expression.Constant($"sign({VisitAndReturnCypher(node.Arguments[0])})"),

            // sin(expression)
            "Sin" when node.Arguments.Count == 1 =>
                Expression.Constant($"sin({VisitAndReturnCypher(node.Arguments[0])})"),

            // cos(expression)
            "Cos" when node.Arguments.Count == 1 =>
                Expression.Constant($"cos({VisitAndReturnCypher(node.Arguments[0])})"),

            // tan(expression)
            "Tan" when node.Arguments.Count == 1 =>
                Expression.Constant($"tan({VisitAndReturnCypher(node.Arguments[0])})"),

            // asin(expression)
            "Asin" when node.Arguments.Count == 1 =>
                Expression.Constant($"asin({VisitAndReturnCypher(node.Arguments[0])})"),

            // acos(expression)
            "Acos" when node.Arguments.Count == 1 =>
                Expression.Constant($"acos({VisitAndReturnCypher(node.Arguments[0])})"),

            // atan(expression)
            "Atan" when node.Arguments.Count == 1 =>
                Expression.Constant($"atan({VisitAndReturnCypher(node.Arguments[0])})"),

            // Max(a, b)
            "Max" when node.Arguments.Count == 2 =>
                Expression.Constant($"CASE WHEN {VisitAndReturnCypher(node.Arguments[0])} > {VisitAndReturnCypher(node.Arguments[1])} THEN {VisitAndReturnCypher(node.Arguments[0])} ELSE {VisitAndReturnCypher(node.Arguments[1])} END"),

            // Min(a, b)
            "Min" when node.Arguments.Count == 2 =>
                Expression.Constant($"CASE WHEN {VisitAndReturnCypher(node.Arguments[0])} < {VisitAndReturnCypher(node.Arguments[1])} THEN {VisitAndReturnCypher(node.Arguments[0])} ELSE {VisitAndReturnCypher(node.Arguments[1])} END"),

            // Pow(base, exponent)
            "Pow" when node.Arguments.Count == 2 =>
                Expression.Constant($"({VisitAndReturnCypher(node.Arguments[0])}) ^ ({VisitAndReturnCypher(node.Arguments[1])})"),

            _ => throw new NotSupportedException($"Math method {node.Method.Name} is not supported")
        };
    }

    private Expression HandleDateTimeMethod(MethodCallExpression node)
    {
        return node.Method.Name switch
        {
            // DateTime.Now - returns current local datetime
            "get_Now" when node.Arguments.Count == 0 =>
                Expression.Constant("localdatetime()"),

            // DateTime.Today - returns current date at midnight (local)
            "get_Today" when node.Arguments.Count == 0 =>
                Expression.Constant("date()"),

            // DateTime.UtcNow - returns current UTC datetime
            "get_UtcNow" when node.Arguments.Count == 0 =>
                Expression.Constant("datetime()"),

            _ => throw new NotSupportedException($"DateTime method {node.Method.Name} is not supported")
        };
    }

    private Expression HandleContainsMethod(MethodCallExpression node)
    {
        // Collection.Contains(item) => item IN collection
        if (node.Object != null)
        {
            // Instance method: collection.Contains(item)
            var collection = VisitAndReturnCypher(node.Object);
            var item = VisitAndReturnCypher(node.Arguments[0]);
            return Expression.Constant($"{item} IN {collection}");
        }
        else if (node.Arguments.Count == 2)
        {
            // Static method: Enumerable.Contains(collection, item)
            var collection = VisitAndReturnCypher(node.Arguments[0]);
            var item = VisitAndReturnCypher(node.Arguments[1]);
            return Expression.Constant($"{item} IN {collection}");
        }

        throw new NotSupportedException("Unsupported Contains method signature");
    }

    /// <summary>
    /// Maps C# property names to AGE property names.
    /// </summary>
    private static string MapPropertyName(string csharpPropertyName)
    {
        return csharpPropertyName switch
        {
            // Map C# "Id" property to our prefixed "user_id" field to avoid conflict with PostgreSQL internal "Id"
            // This ensures we always use our application-controlled IDs, not PostgreSQL internal IDs
            "Id" => "user_id",
            
            // For all other properties, keep the same name
            _ => csharpPropertyName
        };
    }
}
