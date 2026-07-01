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
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Cvoya.Graph.Model.Age.Querying.Cypher.Visitors.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Translates .NET LINQ expressions to Cypher expressions for AGE.
/// Simplified version focused on common query operations.
/// </summary>
internal sealed class AgeExpressionToCypherVisitor : ExpressionVisitor
{
    /// <summary>
    /// Allowlist of known-safe method full names that may be compiled at query-translation time.
    /// Only methods on this list are permitted in the fallback compilation path.
    /// </summary>
    private static readonly HashSet<string> SafeMethods = new(StringComparer.Ordinal)
    {
        // System.Math methods
        "System.Math.Abs",
        "System.Math.Acos",
        "System.Math.Asin",
        "System.Math.Atan",
        "System.Math.Atan2",
        "System.Math.BigMul",
        "System.Math.Ceiling",
        "System.Math.Clamp",
        "System.Math.Cos",
        "System.Math.Cosh",
        "System.Math.DivRem",
        "System.Math.Exp",
        "System.Math.Floor",
        "System.Math.IEEERemainder",
        "System.Math.Log",
        "System.Math.Log10",
        "System.Math.Max",
        "System.Math.Min",
        "System.Math.Pow",
        "System.Math.Round",
        "System.Math.Sign",
        "System.Math.Sin",
        "System.Math.Sinh",
        "System.Math.Sqrt",
        "System.Math.Tan",
        "System.Math.Tanh",
        "System.Math.Truncate",

        // System.String methods
        "System.String.Concat",
        "System.String.Contains",
        "System.String.Copy",
        "System.String.EndsWith",
        "System.String.Equals",
        "System.String.Format",
        "System.String.IsNullOrEmpty",
        "System.String.IsNullOrWhiteSpace",
        "System.String.Join",
        "System.String.StartsWith",
        "System.String.ToLower",
        "System.String.ToLowerInvariant",
        "System.String.ToString",
        "System.String.ToUpper",
        "System.String.ToUpperInvariant",
        "System.String.Trim",
        "System.String.TrimEnd",
        "System.String.TrimStart",

        // System.Convert methods
        "System.Convert.ChangeType",
        "System.Convert.FromBase64String",
        "System.Convert.ToBase64String",
        "System.Convert.ToBoolean",
        "System.Convert.ToByte",
        "System.Convert.ToChar",
        "System.Convert.ToDateTime",
        "System.Convert.ToDecimal",
        "System.Convert.ToDouble",
        "System.Convert.ToInt16",
        "System.Convert.ToInt32",
        "System.Convert.ToInt64",
        "System.Convert.ToSByte",
        "System.Convert.ToSingle",
        "System.Convert.ToString",
        "System.Convert.ToUInt16",
        "System.Convert.ToUInt32",
        "System.Convert.ToUInt64",

        // System.Guid methods
        "System.Guid.NewGuid",
        "System.Guid.Parse",
        "System.Guid.ParseExact",
        "System.Guid.ToString",
        "System.Guid.TryParse",
        "System.Guid.TryParseExact",

        // System.DateTime methods (static)
        "System.DateTime.Compare",
        "System.DateTime.DaysInMonth",
        "System.DateTime.FromBinary",
        "System.DateTime.FromFileTime",
        "System.DateTime.FromFileTimeUtc",
        "System.DateTime.FromOADate",
        "System.DateTime.IsLeapYear",
        "System.DateTime.Now",
        "System.DateTime.Parse",
        "System.DateTime.ParseExact",
        "System.DateTime.SpecifyKind",
        "System.DateTime.Today",
        "System.DateTime.TryParse",
        "System.DateTime.TryParseExact",
        "System.DateTime.UtcNow",

        // System.DateTime instance methods (checked via declaring type)
        "System.DateTime.AddDays",
        "System.DateTime.AddHours",
        "System.DateTime.AddMilliseconds",
        "System.DateTime.AddMinutes",
        "System.DateTime.AddMonths",
        "System.DateTime.AddSeconds",
        "System.DateTime.AddTicks",
        "System.DateTime.AddYears",
        "System.DateTime.ToLocalTime",
        "System.DateTime.ToLongDateString",
        "System.DateTime.ToLongTimeString",
        "System.DateTime.ToOADate",
        "System.DateTime.ToShortDateString",
        "System.DateTime.ToShortTimeString",
        "System.DateTime.ToString",
        "System.DateTime.ToUniversalTime",

        // System.DateTimeOffset methods
        "System.DateTimeOffset.Now",
        "System.DateTimeOffset.UtcNow",
        "System.DateTimeOffset.Parse",
        "System.DateTimeOffset.TryParse",
        "System.DateTimeOffset.ToLocalTime",
        "System.DateTimeOffset.ToUniversalTime",
        "System.DateTimeOffset.ToString",

        // System.TimeSpan methods
        "System.TimeSpan.Compare",
        "System.TimeSpan.FromDays",
        "System.TimeSpan.FromHours",
        "System.TimeSpan.FromMilliseconds",
        "System.TimeSpan.FromMinutes",
        "System.TimeSpan.FromSeconds",
        "System.TimeSpan.FromTicks",
        "System.TimeSpan.Parse",
        "System.TimeSpan.TryParse",
        "System.TimeSpan.ToString",

        // System.Enum methods
        "System.Enum.Format",
        "System.Enum.GetName",
        "System.Enum.GetNames",
        "System.Enum.GetUnderlyingType",
        "System.Enum.GetValues",
        "System.Enum.IsDefined",
        "System.Enum.Parse",
        "System.Enum.TryParse",
        "System.Enum.ToString",

        // System.Array methods
        "System.Array.BinarySearch",
        "System.Array.ConvertAll",
        "System.Array.GetValue",
        "System.Array.IndexOf",
        "System.Array.LastIndexOf",
        "System.Array.Sort",

        // System.Collections.Generic.List<T> indexer access (e.g., list[0])
        "System.Collections.Generic.List`1.get_Item",
    };

    /// <summary>
    /// Checks whether the given type is considered safe for query-time expression compilation.
    /// Safe types include primitive types, common value types, Enum values, and collections/arrays of safe types.
    /// </summary>
    private static bool IsTypeSafe(Type type)
    {
        if (type == null)
            return false;

        // Base set of safe type full names
        if (type.FullName != null)
        {
            switch (type.FullName)
            {
                case "System.Boolean":
                case "System.Byte":
                case "System.Char":
                case "System.DateTime":
                case "System.DateTimeOffset":
                case "System.Decimal":
                case "System.Double":
                case "System.Guid":
                case "System.Int16":
                case "System.Int32":
                case "System.Int64":
                case "System.SByte":
                case "System.Single":
                case "System.String":
                case "System.TimeSpan":
                case "System.UInt16":
                case "System.UInt32":
                case "System.UInt64":
                    return true;
            }
        }

        // Handle Nullable<T> where T is safe
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            return IsTypeSafe(type.GetGenericArguments()[0]);

        // Handle arrays of safe types
        if (type.IsArray && type.HasElementType)
            return IsTypeSafe(type.GetElementType()!);

        // Handle generic collection types with safe element type
        if (type.IsGenericType)
        {
            var genDef = type.GetGenericTypeDefinition();
            if (genDef == typeof(List<>) || genDef == typeof(IList<>) || genDef == typeof(IEnumerable<>) ||
                genDef == typeof(ICollection<>) || genDef == typeof(IReadOnlyList<>) || genDef == typeof(IReadOnlyCollection<>))
            {
                return IsTypeSafe(type.GetGenericArguments()[0]);
            }
        }

        // Check interfaces for IEnumerable<T> with safe element type
        foreach (var iface in type.GetInterfaces())
        {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                return IsTypeSafe(iface.GetGenericArguments()[0]);
        }

        // Allow Enum types
        if (type.IsEnum)
            return true;

        return false;
    }

    /// <summary>
    /// Cache for compiled expressions to avoid recompilation of identical expression trees.
    /// Uses ConditionalWeakTable which holds weak references to keys (expressions),
    /// allowing GC to reclaim cached entries when expressions are no longer referenced.
    /// </summary>
    private static readonly ConditionalWeakTable<Expression, object?> ExpressionCache = new();

    private readonly CypherQueryContext _context;
    private readonly QueryParameterStore _parameterStore;
    private readonly ILogger _logger;
    private readonly string _alias = null!;
    private readonly ParameterExpression? _pathSegmentParameter;
    private readonly string? _sourceAlias;
    private readonly string? _relationshipAlias;
    private readonly string? _targetAlias;
    private readonly StringMethodHandler _stringHandler;
    private readonly MathMethodHandler _mathHandler;
    private readonly DateTimeMethodHandler _dateTimeHandler;
    private readonly CollectionExpressionHandler _collectionHandler;
    private readonly ClosureCaptureHandler _closureCaptureHandler;
    private readonly MemberExpressionHandler _memberHandler;

    // Security counters to prevent DoS via deeply nested or extremely large expression trees
    private int _visitedNodeCount;
    private int _currentDepth;
    private const int MaxNodeCount = 10_000;
    private const int MaxRecursionDepth = 100;

    public AgeExpressionToCypherVisitor(
        CypherQueryContext context,
        ILogger? logger = null,
        string alias = "n",
        ParameterExpression? pathSegmentParameter = null,
        string? sourceAlias = null,
        string? relationshipAlias = null,
        string? targetAlias = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _parameterStore = context.ParameterStore;
        _logger = logger ?? NullLogger.Instance;
        _alias = alias;
        _pathSegmentParameter = pathSegmentParameter;
        _sourceAlias = sourceAlias;
        _relationshipAlias = relationshipAlias;
        _targetAlias = targetAlias;

        _stringHandler = new StringMethodHandler(
            visitAndReturnCypher: VisitAndReturnCypher,
            addParameter: AddParameter,
            logger: _logger);
        _mathHandler = new MathMethodHandler(visitAndReturnCypher: VisitAndReturnCypher);
        _dateTimeHandler = new DateTimeMethodHandler(
            visitAndReturnCypher: VisitAndReturnCypher,
            addParameter: AddParameter);
        _collectionHandler = new CollectionExpressionHandler(
            visitAndReturnCypher: VisitAndReturnCypher,
            visit: (Expression e) => Visit(e)!,
            addParameter: AddParameter,
            logger: _logger);
        _closureCaptureHandler = new ClosureCaptureHandler(_logger, _sourceAlias ?? _alias ?? "src0");
        _memberHandler = new MemberExpressionHandler(
            _context, _logger, _alias ?? "n",
            _sourceAlias, _relationshipAlias, _targetAlias,
            isPathSegmentContext: _pathSegmentParameter != null,
            tryEvaluateStatic: (MemberExpression m, bool _) =>
            {
                var ok = TryEvaluateStaticMember(m, out var v);
                return (ok, v);
            },
            visitAndReturnCypher: VisitAndReturnCypher,
            addParameter: AddParameter);
    }

    private string AddParameter(object? value)
    {
        return _parameterStore.Add(value);
    }

    // ---- Security guards for expression tree depth / node-count limits ----

    /// <summary>
    /// Increments the node-visit counter and throws if the limit is exceeded.
    /// Call once per expression node visited.
    /// </summary>
    private void OnNodeVisited()
    {
        if (++_visitedNodeCount > MaxNodeCount)
            throw new InvalidOperationException(
                $"Expression tree exceeds maximum node count of {MaxNodeCount}. Simplify the query expression.");
    }

    /// <summary>
    /// Increments the recursion depth counter and throws if the limit is exceeded.
    /// Call before recursing into child expressions.
    /// </summary>
    private void OnEnterNode()
    {
        if (++_currentDepth > MaxRecursionDepth)
            throw new InvalidOperationException(
                $"Expression tree exceeds maximum recursion depth of {MaxRecursionDepth}. Simplify the query expression.");
    }

    /// <summary>
    /// Decrements the recursion depth counter after returning from a child expression.
    /// </summary>
    private void OnExitNode()
    {
        _currentDepth--;
    }

    public override Expression? Visit(Expression? node)
    {
        if (node == null)
            return null;

        OnNodeVisited();
        OnEnterNode();
        try
        {
            return base.Visit(node);
        }
        finally
        {
            OnExitNode();
        }
    }

    /// <summary>
    /// Tries to evaluate a static member expression at query-translation time.
    /// Only allows compilation if the member's type is on the safe-type allowlist.
    /// </summary>
    private static bool TryEvaluateStaticMember(MemberExpression node, out object? value)
    {
        // Security check: only allow compilation for safe types
        if (!IsTypeSafe(node.Type))
        {
            value = null;
            return false;
        }

        try
        {
            value = ExpressionCache.GetValue(node, static key =>
            {
                var objectMember = Expression.Convert(key, typeof(object));
                var getterLambda = Expression.Lambda<Func<object>>(objectMember);
                return getterLambda.Compile()();
            });
            return true;
        }
        catch
        {
            value = null;
            return false;
        }
    }

    /// <summary>
    /// Visits an expression and returns the resulting Cypher string.
    /// </summary>
    public string VisitAndReturnCypher(Expression expression)
    {
        // Reset security counters for each top-level query translation
        _visitedNodeCount = 0;
        _currentDepth = 0;

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
        // Handle null comparisons: x = null → x IS NULL, x <> null → x IS NOT NULL
        // Using IS NULL/IS NOT NULL is required in Cypher/AGE for correct null handling
        var isRightNull = node.Right is ConstantExpression cr && cr.Value is null;
        var isLeftNull = node.Left is ConstantExpression cl && cl.Value is null;

        if (isRightNull || isLeftNull)
        {
            var nonNullSide = isRightNull ? VisitAndReturnCypher(node.Left) : VisitAndReturnCypher(node.Right);
            var cypher = node.NodeType switch
            {
                ExpressionType.Equal => $"{nonNullSide} IS NULL",
                ExpressionType.NotEqual => $"{nonNullSide} IS NOT NULL",
                _ => throw new NotSupportedException($"Null comparison with operator {node.NodeType} is not supported")
            };
            return Expression.Constant(cypher);
        }

        var left = VisitAndReturnCypher(node.Left);
        var right = VisitAndReturnCypher(node.Right);

        var cypherResult = node.NodeType switch
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

        return Expression.Constant(cypherResult);
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        return _memberHandler.VisitMember(node);
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
            return Visit(node.Arguments[0])!;
        }

        // Handle Math methods
        if (node.Method.DeclaringType == typeof(Math))
        {
            return _mathHandler.HandleMathMethod(node);
        }

        // Handle string methods
        if (node.Method.DeclaringType == typeof(string))
        {
            return _stringHandler.HandleStringMethod(node);
        }

        // Handle DateTime methods (both static and instance)
        if (node.Method.DeclaringType == typeof(DateTime))
        {
            return _dateTimeHandler.HandleDateTimeMethod(node);
        }

        // Handle closure-captured IEnumerable<IRelationship>.Count(lambda) patterns FIRST
        // Must be before the generic Count handler which would intercept all Count calls
        if (node.Method.Name == "Count")
        {
            if (_closureCaptureHandler.TryHandleClosureCountOnRelationship(node))
                return Expression.Constant(_closureCaptureHandler.HandleClosureCountOnRelationship(node));
            return _collectionHandler.HandleCountMethod(node);
        }

        // Handle IGrouping aggregation: group.Average/Max/Min/Sum(lambda)
        if ((node.Method.Name == "Average" || node.Method.Name == "Max" ||
             node.Method.Name == "Min" || node.Method.Name == "Sum")
            && node.Arguments.Count >= 1)
        {
            return _collectionHandler.HandleGroupingAggregation(node, node.Method.Name.ToLowerInvariant());
        }

        // Handle collection methods (Contains, Any, etc.) - but not string.Contains
        if (node.Method.Name == "Contains" && node.Arguments.Count >= 1 && node.Method.DeclaringType != typeof(string))
        {
            return _collectionHandler.HandleContainsMethod(node);
        }

        // For any other method call, check the allowlist first before compiling
        var methodFullName = node.Method.DeclaringType != null
            ? $"{node.Method.DeclaringType.FullName}.{node.Method.Name}"
            : node.Method.Name;

        var isSafe = SafeMethods.Contains(methodFullName);

        // For constructed generic types (e.g., List<string>), FullName includes the type arguments,
        // so also check against the open generic type definition (e.g., List<T>).
        if (!isSafe && node.Method.DeclaringType is { IsGenericType: true } declType)
        {
            var genericDefFullName = $"{declType.GetGenericTypeDefinition().FullName}.{node.Method.Name}";
            isSafe = SafeMethods.Contains(genericDefFullName);
        }

        if (!isSafe)
        {
            throw new NotSupportedException(
                $"Method '{methodFullName}' is not in the allowlist of safe methods for query-time compilation. " +
                $"Add it to the allowlist if you are sure it is safe.");
        }

        // Method is on the allowlist - log a warning and proceed with compilation
        _logger.LogWarning(
            "Compiling method call '{MethodFullName}' at query-translation time. " +
            "This is allowed because the method is on the safe-method allowlist.",
            methodFullName);

        try
        {
            var value = ExpressionCache.GetValue(node, static key =>
            {
                var objectMember = Expression.Convert(key, typeof(object));
                var getterLambda = Expression.Lambda<Func<object>>(objectMember);
                return getterLambda.Compile()();
            });

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

            // For conversions to integer types, use toInteger() in Cypher
            if (node.Type == typeof(int) || node.Type == typeof(long) || node.Type == typeof(short) || node.Type == typeof(byte) ||
                node.Type == typeof(sbyte) || node.Type == typeof(ushort) || node.Type == typeof(uint) || node.Type == typeof(ulong) ||
                node.Type == typeof(int?) || node.Type == typeof(long?) || node.Type == typeof(short?) || node.Type == typeof(byte?) ||
                node.Type == typeof(sbyte?) || node.Type == typeof(ushort?) || node.Type == typeof(uint?) || node.Type == typeof(ulong?))
            {
                return Expression.Constant($"toInteger({operandCypher})");
            }

            // For other conversions, just pass through
            return Visit(node.Operand)!;
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

    // String methods moved to StringMethodHandler.
    // Math methods moved to MathMethodHandler.
    // DateTime methods moved to DateTimeMethodHandler.
    // Collection methods (Contains, Count, GroupingAggregation) moved to CollectionExpressionHandler.

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

    // MapPropertyName and TryCompileEval are now imported via
    // `using static ExpressionTranslationHelper` at the top of the file.
}
