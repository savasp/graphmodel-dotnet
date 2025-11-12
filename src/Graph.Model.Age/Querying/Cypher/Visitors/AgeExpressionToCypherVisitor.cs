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
using Cvoya.Graph.Model.Age.Core.Entities;
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
            _parametersDict[paramName] = AgeSerializationBridge.ToAgeValue(value);
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
                    nameof(IGraphPathSegment.StartNode) => _sourceAlias ?? "src0",
                    nameof(IGraphPathSegment.EndNode) => _targetAlias ?? "tgt0",
                    nameof(IGraphPathSegment.Relationship) => _relationshipAlias ?? "r0",
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
            // Special handling for IGrouping parameters (e.g., g.Key in GroupBy)
            if (param.Type.IsGenericType && param.Type.GetGenericTypeDefinition().Name.Contains("IGrouping"))
            {
                _logger.LogDebug("Processing IGrouping parameter property: {Property}", node.Member.Name);
                
                // g.Key should map to the GROUP BY expression stored in the context
                if (node.Member.Name == "Key")
                {
                    var groupByExpr = _queryBuilder?.Context.Scope.GroupByExpression;
                    if (string.IsNullOrEmpty(groupByExpr))
                    {
                        throw new InvalidOperationException("GroupBy expression not found in context for g.Key access");
                    }
                    _logger.LogDebug("Mapped g.Key to GROUP BY expression: {Expression}", groupByExpr);
                    return Expression.Constant(groupByExpr);
                }
                
                throw new NotSupportedException($"IGrouping property '{node.Member.Name}' is not supported. Use g.Key for the grouping key.");
            }
            
            // Special handling for path segment parameters
            if (typeof(IGraphPathSegment).IsAssignableFrom(param.Type))
            {
                _logger.LogDebug("Processing path segment parameter property: {Property}", node.Member.Name);

                var propertyMapping = node.Member.Name switch
                {
                    nameof(IGraphPathSegment.StartNode) => _sourceAlias ?? "src0",
                    nameof(IGraphPathSegment.EndNode) => _targetAlias ?? "tgt0", 
                    nameof(IGraphPathSegment.Relationship) => _relationshipAlias ?? "r0",
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
                nameof(IGraphPathSegment.StartNode) => _sourceAlias ?? "src0",
                nameof(IGraphPathSegment.EndNode) => _targetAlias ?? "tgt0",
                nameof(IGraphPathSegment.Relationship) => _relationshipAlias ?? "r0",
                _ => throw new NotSupportedException($"Path segment property '{segmentProperty}' is not supported")
            };

            _logger.LogDebug("Path segment alias mapping: {SegmentProperty} -> {Alias} (source={Source}, target={Target}, rel={Rel})", 
                segmentProperty, alias, _sourceAlias, _targetAlias, _relationshipAlias);
            
            // Add debug logging to identify the problem
            if (segmentProperty == nameof(IGraphPathSegment.StartNode) && _sourceAlias == null)
            {
                _logger.LogError("CRITICAL: _sourceAlias is null for StartNode access - falling back to 'src'");
            }
            if (segmentProperty == nameof(IGraphPathSegment.Relationship) && _relationshipAlias == null)
            {
                _logger.LogError("CRITICAL: _relationshipAlias is null for Relationship access - falling back to 'r'");
            }

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

        // Handle chained member access (e.g., p.FirstName.Length, p.Address.City)
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

            // Handle nested property access (e.g., p.Address.City, c.A.B.Property1)
            // Build the property path by walking up the member expression chain
            var propertyPath = new List<string>();
            var current = node;
            Expression? baseExpression = null;
            
            while (current != null)
            {
                propertyPath.Insert(0, MapPropertyName(current.Member.Name));
                
                if (current.Expression is MemberExpression nextMember)
                {
                    current = nextMember;
                }
                else if (current.Expression is ParameterExpression baseParam)
                {
                    baseExpression = baseParam;
                    break;
                }
                else if (current.Expression is UnaryExpression unaryExpr && 
                         (unaryExpr.NodeType == ExpressionType.Convert || unaryExpr.NodeType == ExpressionType.ConvertChecked) &&
                         unaryExpr.Operand is ParameterExpression convertedParam)
                {
                    baseExpression = convertedParam;
                    break;
                }
                else
                {
                    // Not a simple parameter chain, try evaluation
                    current = null;
                }
            }
            
            // If we found a parameter at the base, construct the nested property path
            if (baseExpression is ParameterExpression)
            {
                var fullPath = $"{_alias}.{string.Join(".", propertyPath)}";
                _logger.LogDebug("Mapped nested property access to {Path}", fullPath);
                return Expression.Constant(fullPath);
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
        // Handle implicit conversion operators (op_Implicit)
        // These are generated by C# compiler for implicit type conversions
        if (node.Method.Name == "op_Implicit" && node.Arguments.Count == 1)
        {
            // Just visit the argument being converted, ignore the conversion
            return Visit(node.Arguments[0]);
        }

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

        // Handle DateTime methods (both static and instance)
        if (node.Method.DeclaringType == typeof(DateTime))
        {
            return HandleDateTimeMethod(node);
        }

        // Handle LINQ Count method
        if (node.Method.Name == "Count")
        {
            return HandleCountMethod(node);
        }

        // Handle collection methods (Contains, Any, etc.) - but not string.Contains
        if (node.Method.Name == "Contains" && node.Arguments.Count >= 1 && node.Method.DeclaringType != typeof(string))
        {
            return HandleContainsMethod(node);
        }

        // For any other method call, try to evaluate it at compile time
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
        
        // Track which Cypher variables (aliases) are already being returned to avoid duplicates
        // Key: Cypher variable (e.g., "src0", "r0", "tgt0"), Value: column name it's returned as
        var returnedVariables = new Dictionary<string, string>();

        // First pass: scan for non-PathSegment arguments to track what's already being returned
        for (int i = 0; i < node.Arguments.Count; i++)
        {
            var argument = node.Arguments[i];
            
            // Skip PathSegment parameters in first pass
            if (argument is ParameterExpression paramExpr && IsPathSegmentType(paramExpr.Type))
                continue;
            
            // Visit the argument to get the Cypher expression
            var cypherExpression = VisitAndReturnCypher(argument);
            
            // Extract the base Cypher variable (e.g., "src0" from "src0.FirstName" or just "src0")
            var baseVariable = cypherExpression.Split('.')[0].Trim();
            var memberName = node.Members?[i]?.Name ?? $"Item{i + 1}";
            
            // Track this variable as being returned
            if (!string.IsNullOrEmpty(baseVariable))
            {
                returnedVariables[baseVariable] = $"c_{memberName}";
            }
        }

        // Second pass: process all arguments and build expressions
        for (int i = 0; i < node.Arguments.Count; i++)
        {
            var argument = node.Arguments[i];
            var memberName = node.Members?[i]?.Name ?? $"Item{i + 1}";
            
            // Special handling for PathSegment parameter projections
            if (argument is ParameterExpression paramExpr && IsPathSegmentType(paramExpr.Type))
            {
                // PathSegment projections need to return all three components (source, relationship, target)
                // so they can be reconstructed into a PathSegment object by the result processor.
                // However, if a component is already being returned (e.g., ps.StartNode), we should
                // skip that component to avoid duplicate returns.
                
                var pathSegmentParts = new List<string>();
                
                // Check source node - only add if not already returned
                if (_sourceAlias != null && !returnedVariables.ContainsKey(_sourceAlias))
                {
                    pathSegmentParts.Add($"{_sourceAlias} AS c_{memberName}_{_sourceAlias}");
                    returnedVariables[_sourceAlias] = $"c_{memberName}_{_sourceAlias}";
                }
                
                // Check relationship - only add if not already returned
                if (_relationshipAlias != null && !returnedVariables.ContainsKey(_relationshipAlias))
                {
                    pathSegmentParts.Add($"{_relationshipAlias} AS c_{memberName}_{_relationshipAlias}");
                    returnedVariables[_relationshipAlias] = $"c_{memberName}_{_relationshipAlias}";
                }
                
                // Check target node - only add if not already returned
                if (_targetAlias != null && !returnedVariables.ContainsKey(_targetAlias))
                {
                    pathSegmentParts.Add($"{_targetAlias} AS c_{memberName}_{_targetAlias}");
                    returnedVariables[_targetAlias] = $"c_{memberName}_{_targetAlias}";
                }
                
                if (pathSegmentParts.Count > 0)
                {
                    var pathSegmentProjection = string.Join(", ", pathSegmentParts);
                    memberNames.Add(memberName);
                    expressions.Add(pathSegmentProjection);
                    _logger.LogDebug("Added PathSegment projection: {Projection}", pathSegmentProjection);
                }
                else
                {
                    // All components already returned - still need to track this member for result processing
                    memberNames.Add(memberName);
                    // Add placeholder to maintain member count (will be handled by result processor)
                    _logger.LogDebug("PathSegment {MemberName} has no new columns; components already returned", memberName);
                }
            }
            else
            {
                // Visit the argument to get the Cypher expression
                var cypherExpression = VisitAndReturnCypher(argument);
                
                memberNames.Add(memberName);
                expressions.Add($"{cypherExpression} AS c_{memberName}");
            }
        }

        // Return the combined expression for the SELECT clause
        var selectExpression = string.Join(", ", expressions);
        _logger.LogDebug("VisitNew returning select expression: {Expression}", selectExpression);
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
            // Apache AGE doesn't support string concatenation in regex, so we build the pattern as a parameter
            "Contains" when node.Arguments.Count == 1 =>
                HandleStringContains(obj, node.Arguments[0]),

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

    private Expression HandleStringContains(string? obj, Expression substringExpression)
    {
        // For string.Contains(), we need to create a regex pattern
        // Try to evaluate the substring expression to get its actual value
        
        object? substringValue = null;
        
        // Try to extract the value from the expression
        if (substringExpression is ConstantExpression constantExpr)
        {
            substringValue = constantExpr.Value;
        }
        else if (substringExpression is MemberExpression memberExpr)
        {
            // Try to evaluate member expressions (e.g., variable captures)
            try
            {
                var lambda = Expression.Lambda<Func<object>>(Expression.Convert(memberExpr, typeof(object)));
                var compiled = lambda.Compile();
                substringValue = compiled();
            }
            catch
            {
                // If we can't evaluate it, fall back to parameter approach
            }
        }
        
        // If we got the value, create a regex pattern and add it as a parameter
        if (substringValue is string substring)
        {
            var regexPattern = $".*{System.Text.RegularExpressions.Regex.Escape(substring)}.*";
            var paramName = AddParameter(regexPattern);
            return Expression.Constant($"{obj} =~ {paramName}");
        }
        
        // Fallback: if we couldn't get the value, try visiting the expression
        // This will create a parameter for the substring, then we need to wrap it in a regex pattern
        // Unfortunately, AGE doesn't support string concatenation in expressions well,
        // so we'll use a workaround by creating the regex inline (may fail in some cases)
        _logger.LogWarning("Could not evaluate Contains argument, attempting fallback");
        var substringCypher = VisitAndReturnCypher(substringExpression);
        
        // Try one more approach: if the result is a parameter, replace it with a regex pattern
        if (substringCypher.StartsWith("$"))
        {
            // The value was parameterized. We can't easily modify it now.
            // Best we can do is use CONTAINS if AGE supports it, or fail gracefully
            _logger.LogError("Cannot handle parameterized Contains - this may fail");
        }
        
        // Last resort: try the concatenation approach (likely to fail)
        return Expression.Constant($"{obj} =~ ('.*' + {substringCypher} + '.*')");
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
        // Handle instance methods on DateTime objects (e.g., date.AddDays(7))
        if (node.Object != null)
        {
            try
            {
                // Try to evaluate the entire expression at compile time
                var objectMember = Expression.Convert(node, typeof(object));
                var getterLambda = Expression.Lambda<Func<object>>(objectMember);
                var getter = getterLambda.Compile();
                var value = getter();
                
                // Store as parameter and return reference
                var paramRef = AddParameter(value);
                return Expression.Constant(paramRef);
            }
            catch
            {
                // If evaluation fails, fall through to unsupported
                throw new NotSupportedException($"DateTime method {node.Method.Name} is not supported");
            }
        }
        
        // Handle static methods on DateTime type
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

    private Expression HandleCountMethod(MethodCallExpression node)
    {
        // Handle Count() method on collections
        // This could be:
        // 1. collection.Count() - instance method with no arguments
        // 2. Enumerable.Count(collection) - static method
        // 3. Enumerable.Count(collection, predicate) - static method with predicate
        
        if (node.Object != null && node.Arguments.Count == 0)
        {
            // Instance method: collection.Count()
            // Special case: if this is Count() on an IGrouping (g.Count()), translate to count(*)
            if (node.Object is ParameterExpression param && 
                param.Type.IsGenericType && 
                param.Type.GetGenericTypeDefinition().Name.Contains("IGrouping"))
            {
                _logger.LogDebug("Processing g.Count() in GroupBy context - translating to count(*)");
                return Expression.Constant("count(*)");
            }
            
            // In Cypher, use size() function for normal collections
            var collection = VisitAndReturnCypher(node.Object);
            return Expression.Constant($"size({collection})");
        }
        else if (node.Arguments.Count == 1)
        {
            // Static method without predicate: Enumerable.Count(collection)
            
            // Special case: if this is Count(g) where g is an IGrouping, translate to count(*)
            if (node.Arguments[0] is ParameterExpression param && 
                param.Type.IsGenericType && 
                param.Type.GetGenericTypeDefinition().Name.Contains("IGrouping"))
            {
                _logger.LogDebug("Processing Count(g) in GroupBy context - translating to count(*)");
                return Expression.Constant("count(*)");
            }
            
            var collection = VisitAndReturnCypher(node.Arguments[0]);
            return Expression.Constant($"size({collection})");
        }
        else if (node.Arguments.Count == 2)
        {
            // Static method with predicate: Enumerable.Count(collection, predicate)
            // This is more complex and may need pattern comprehension
            // For now, try to evaluate at compile time
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
                throw new NotSupportedException("Count with predicate requires compile-time evaluation");
            }
        }

        throw new NotSupportedException("Unsupported Count method signature");
    }

    /// <summary>
    /// Handles parameter expressions, particularly for PathSegment parameters.
    /// </summary>
    protected override Expression VisitParameter(ParameterExpression node)
    {
        _logger.LogDebug("VisitParameter called with parameter: {Name}, Type: {Type}", node.Name, node.Type.Name);
        
        // Check if this is the path segment parameter we're tracking
        if (_pathSegmentParameter != null && node == _pathSegmentParameter)
        {
            _logger.LogDebug("Parameter matches tracked PathSegment parameter, returning {Alias}", _alias);
            return Expression.Constant(_alias);
        }
        
        // For scalar projections (e.g., OrderBy(name => name) after Select(p => p.FirstName)),
        // the parameter represents the projected value, so return the current alias
        if (!typeof(INode).IsAssignableFrom(node.Type) && 
            !typeof(IRelationship).IsAssignableFrom(node.Type) &&
            !typeof(IGraphPathSegment).IsAssignableFrom(node.Type))
        {
            _logger.LogDebug("Parameter is scalar projection, returning alias: {Alias}", _alias);
            return Expression.Constant(_alias);
        }
        
        // For node/relationship parameters, return the alias
        _logger.LogDebug("Parameter is node/relationship, returning alias: {Alias}", _alias);
        return Expression.Constant(_alias);
    }

    /// <summary>
    /// Checks if a type is a PathSegment type
    /// </summary>
    private static bool IsPathSegmentType(Type type)
    {
        if (type == null) return false;
        
        // Check if it's the generic IGraphPathSegment interface
        if (type.IsGenericType)
        {
            var genericTypeDef = type.GetGenericTypeDefinition();
            return genericTypeDef.Name.StartsWith("IGraphPathSegment");
        }
        
        // Check if any implemented interfaces are PathSegment types
        foreach (var interfaceType in type.GetInterfaces())
        {
            if (interfaceType.IsGenericType)
            {
                var genericTypeDef = interfaceType.GetGenericTypeDefinition();
                if (genericTypeDef.Name.StartsWith("IGraphPathSegment"))
                {
                    return true;
                }
            }
        }
        
        return false;
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
