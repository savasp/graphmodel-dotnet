// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using Cvoya.Graph.Cypher.Ast;
using Cvoya.Graph.Cypher.Ast.Expressions;
using Cvoya.Graph.Querying;
using AstBinaryExpression = Cvoya.Graph.Cypher.Ast.Expressions.BinaryExpression;
using AstUnaryExpression = Cvoya.Graph.Cypher.Ast.Expressions.UnaryExpression;
using LinqBinaryExpression = System.Linq.Expressions.BinaryExpression;
using LinqUnaryExpression = System.Linq.Expressions.UnaryExpression;

namespace Cvoya.Graph.Cypher.Planning;

internal sealed class ExpressionToCypherAstLowerer(CypherParameterRegistry parameters)
{
    private const string NeutralDateTime = "temporal.datetime";
    private const string NeutralLocalDateTime = "temporal.localDateTime";
    private const string NeutralDate = "temporal.date";
    private const string NeutralTime = "temporal.time";

    private readonly List<MatchClause> _navigationMatches = [];
    private readonly HashSet<string> _navigationAliases = new(StringComparer.Ordinal);

    public IReadOnlyList<MatchClause> NavigationMatches => _navigationMatches;

    public CypherExpression LowerLambda(
        LambdaExpression lambda,
        string defaultAlias,
        IReadOnlyDictionary<ParameterExpression, string>? explicitAliases = null)
    {
        ArgumentNullException.ThrowIfNull(lambda);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultAlias);

        var aliases = explicitAliases is null
            ? new Dictionary<ParameterExpression, string>()
            : new Dictionary<ParameterExpression, string>(explicitAliases);

        foreach (var parameter in lambda.Parameters)
        {
            // Explicit aliases win; TryAdd leaves an already-bound parameter untouched.
            aliases.TryAdd(parameter, ResolveDefaultAlias(parameter.Type, defaultAlias));
        }

        return Lower(lambda.Body, aliases);
    }

    public CypherExpression Lower(
        Expression expression,
        IReadOnlyDictionary<ParameterExpression, string> aliases)
    {
        ArgumentNullException.ThrowIfNull(expression);

        return expression switch
        {
            ConstantExpression constant => LowerConstant(constant),
            ParameterExpression parameter => new VariableRef(ResolveAlias(parameter, aliases)),
            MemberExpression member => LowerMember(member, aliases),
            MethodCallExpression method => LowerMethodCall(method, aliases),
            LinqBinaryExpression binary => LowerBinary(binary, aliases),
            LinqUnaryExpression unary => LowerUnary(unary, aliases),
            ConditionalExpression conditional => new CaseExpression(
                Lower(conditional.Test, aliases),
                Lower(conditional.IfTrue, aliases),
                Lower(conditional.IfFalse, aliases)),
            NewArrayExpression array => new ListExpression(array.Expressions.Select(item => Lower(item, aliases)).ToArray()),
            _ => throw Unsupported(expression, $"Expression node '{expression.NodeType}' is not supported."),
        };
    }

    private CypherExpression LowerConstant(ConstantExpression node)
    {
        return node.Value is null ? new Literal(null) : parameters.Add(node.Value);
    }

    private CypherExpression LowerMember(
        MemberExpression node,
        IReadOnlyDictionary<ParameterExpression, string> aliases)
    {
        if (node.Expression is null)
        {
            return LowerStaticMember(node);
        }

        if (CanEvaluate(node))
        {
            return parameters.Add(Evaluate(node));
        }

        if (TryLowerComplexCollectionCount(node, aliases, out var collectionCount))
        {
            return collectionCount;
        }

        if (TryLowerComplexMember(node, aliases, out var complex))
        {
            return complex;
        }

        if (node.Expression is MemberExpression parentMember &&
            TryLowerTemporalMember(node.Member.DeclaringType, node.Member.Name, Lower(parentMember, aliases), out var temporal))
        {
            return temporal;
        }

        if (node.Member.DeclaringType == typeof(string) && node.Member.Name == nameof(string.Length))
        {
            return Function("size", Lower(node.Expression, aliases));
        }

        if (TryMapPathSegmentMember(node, aliases, out var pathMember))
        {
            return pathMember;
        }

        if (node.Expression is ParameterExpression relationshipParameter &&
            typeof(IRelationship).IsAssignableFrom(relationshipParameter.Type))
        {
            if (node.Member.Name == nameof(IRelationship.StartNodeId))
            {
                return Property("src", nameof(IEntity.Id));
            }

            if (node.Member.Name == nameof(IRelationship.EndNodeId))
            {
                return Property("tgt", nameof(IEntity.Id));
            }
        }

        if (TryLowerTemporalMember(node.Member.DeclaringType, node.Member.Name, Lower(node.Expression, aliases), out temporal))
        {
            return temporal;
        }

        var target = Lower(node.Expression, aliases);
        // The current storage contract keys simple values by CLR property name. PropertyAttribute.Label
        // remains schema metadata; changing physical keys belongs to a separately versioned migration.
        return new PropertyAccess(target, node.Member.Name);
    }

    private CypherExpression LowerStaticMember(MemberExpression node)
    {
        if (node.Member.DeclaringType == typeof(DateTime))
        {
            return node.Member.Name switch
            {
                nameof(DateTime.Now) => Function(NeutralLocalDateTime),
                nameof(DateTime.UtcNow) => Function(NeutralDateTime),
                nameof(DateTime.Today) => Function(NeutralDate),
                _ => throw Unsupported(node, $"Static DateTime member '{node.Member.Name}' is not supported."),
            };
        }

        if (CanEvaluate(node))
        {
            return parameters.Add(Evaluate(node));
        }

        throw Unsupported(node, $"Static member '{node.Member.Name}' is not supported.");
    }

    private CypherExpression LowerMethodCall(
        MethodCallExpression node,
        IReadOnlyDictionary<ParameterExpression, string> aliases)
    {
        if (TryLowerDynamicEntityMethod(node, aliases, out var dynamicExpression))
        {
            return dynamicExpression;
        }

        if (TryLowerComplexCollectionMethod(node, aliases, out var collectionExpression))
        {
            return collectionExpression;
        }

        if (node.Method.DeclaringType == typeof(string))
        {
            return LowerStringMethod(node, aliases);
        }

        if (node.Method.DeclaringType == typeof(Math))
        {
            return LowerMathMethod(node, aliases);
        }

        if (node.Method.DeclaringType == typeof(Convert))
        {
            return LowerConvertMethod(node, aliases);
        }

        if (IsTemporalType(node.Method.DeclaringType))
        {
            return LowerTemporalMethod(node, aliases);
        }

        if (TryLowerCollectionContains(node, aliases, out collectionExpression))
        {
            return collectionExpression;
        }

        if (TryLowerEnumerableAggregation(node, aliases, out var aggregateExpression))
        {
            return aggregateExpression;
        }

        if (node.Method.Name is "op_Implicit" or "op_Explicit" && node.Arguments.Count == 1)
        {
            return Lower(node.Arguments[0], aliases);
        }

        if (CanEvaluate(node))
        {
            return parameters.Add(Evaluate(node));
        }

        throw Unsupported(node, $"Method '{node.Method.DeclaringType?.Name}.{node.Method.Name}' is not supported.");
    }

    private CypherExpression LowerStringMethod(
        MethodCallExpression node,
        IReadOnlyDictionary<ParameterExpression, string> aliases)
    {
        if (node.Method.IsStatic)
        {
            var arguments = node.Arguments.Select(argument => Lower(argument, aliases)).ToArray();
            return node.Method.Name switch
            {
                nameof(string.IsNullOrEmpty) when arguments.Length == 1 =>
                    new AstBinaryExpression(
                        CypherBinaryOperator.Or,
                        new AstUnaryExpression(CypherUnaryOperator.IsNull, arguments[0]),
                        new AstBinaryExpression(CypherBinaryOperator.Equal, Function("size", arguments[0]), new Literal(0))),
                nameof(string.IsNullOrWhiteSpace) when arguments.Length == 1 =>
                    new AstBinaryExpression(
                        CypherBinaryOperator.Or,
                        new AstUnaryExpression(CypherUnaryOperator.IsNull, arguments[0]),
                        new AstBinaryExpression(
                            CypherBinaryOperator.Equal,
                            Function("size", Function("trim", arguments[0])),
                            new Literal(0))),
                nameof(string.Concat) when arguments.Length == 2 =>
                    new AstBinaryExpression(CypherBinaryOperator.Add, arguments[0], arguments[1]),
                nameof(string.Concat) => Function("string.join", new ListExpression(arguments), new Literal(string.Empty)),
                nameof(string.Join) when arguments.Length >= 2 =>
                    Function("string.join", new ListExpression(arguments.Skip(1).ToArray()), arguments[0]),
                _ => throw Unsupported(node, $"Static string method '{node.Method.Name}' is not supported."),
            };
        }

        var target = Lower(node.Object!, aliases);
        var args = node.Arguments.Select(argument => Lower(argument, aliases)).ToArray();

        return node.Method.Name switch
        {
            nameof(string.Contains) => new AstBinaryExpression(CypherBinaryOperator.Contains, target, args[0]),
            nameof(string.StartsWith) => new AstBinaryExpression(CypherBinaryOperator.StartsWith, target, args[0]),
            nameof(string.EndsWith) => new AstBinaryExpression(CypherBinaryOperator.EndsWith, target, args[0]),
            nameof(string.ToLower) => Function("toLower", target),
            nameof(string.ToUpper) => Function("toUpper", target),
            nameof(string.Trim) => Function("trim", target),
            nameof(string.TrimStart) => Function("ltrim", target),
            nameof(string.TrimEnd) => Function("rtrim", target),
            nameof(string.Replace) => Function("replace", target, args[0], args[1]),
            nameof(string.Substring) => Function("substring", [target, .. args]),
            nameof(string.IndexOf) when args.Length == 1 => Function("string.indexOf", target, args[0], new Literal(0)),
            nameof(string.LastIndexOf) when args.Length == 1 => Function("string.lastIndexOf", target, args[0]),
            nameof(string.PadLeft) when args.Length == 1 => Function("string.padLeft", target, args[0], new Literal(" ")),
            nameof(string.PadLeft) => Function("string.padLeft", target, args[0], args[1]),
            nameof(string.PadRight) when args.Length == 1 => Function("string.padRight", target, args[0], new Literal(" ")),
            nameof(string.PadRight) => Function("string.padRight", target, args[0], args[1]),
            nameof(string.CompareTo) => Function("string.compareTo", target, args[0]),
            nameof(string.ToString) => Function("toString", target),
            _ => throw Unsupported(node, $"String method '{node.Method.Name}' is not supported."),
        };
    }

    private CypherExpression LowerMathMethod(
        MethodCallExpression node,
        IReadOnlyDictionary<ParameterExpression, string> aliases)
    {
        var args = node.Arguments.Select(argument => Lower(argument, aliases)).ToArray();
        var name = node.Method.Name switch
        {
            nameof(Math.Abs) => "abs",
            nameof(Math.Floor) => "floor",
            nameof(Math.Ceiling) => "ceil",
            nameof(Math.Round) => "round",
            nameof(Math.Min) => "min",
            nameof(Math.Max) => "max",
            nameof(Math.Sqrt) => "sqrt",
            nameof(Math.Sign) => "sign",
            nameof(Math.Sin) => "sin",
            nameof(Math.Cos) => "cos",
            nameof(Math.Tan) => "tan",
            nameof(Math.Asin) => "asin",
            nameof(Math.Acos) => "acos",
            nameof(Math.Atan) => "atan",
            nameof(Math.Atan2) => "atan2",
            nameof(Math.Log) => "log",
            nameof(Math.Log10) => "log10",
            nameof(Math.Exp) => "exp",
            nameof(Math.Pow) => "math.power",
            _ => throw Unsupported(node, $"Math method '{node.Method.Name}' is not supported."),
        };

        return new FunctionCall(name, args);
    }

    private CypherExpression LowerConvertMethod(
        MethodCallExpression node,
        IReadOnlyDictionary<ParameterExpression, string> aliases)
    {
        var argument = Lower(node.Arguments[0], aliases);
        var name = node.Method.Name switch
        {
            nameof(Convert.ToInt16) or nameof(Convert.ToInt32) or nameof(Convert.ToInt64) => "toInteger",
            nameof(Convert.ToDouble) or nameof(Convert.ToSingle) or nameof(Convert.ToDecimal) => "toFloat",
            nameof(Convert.ToString) => "toString",
            nameof(Convert.ToBoolean) => "toBoolean",
            nameof(Convert.ToDateTime) => NeutralDateTime,
            _ => throw Unsupported(node, $"Convert method '{node.Method.Name}' is not supported."),
        };

        return Function(name, argument);
    }

    private CypherExpression LowerTemporalMethod(
        MethodCallExpression node,
        IReadOnlyDictionary<ParameterExpression, string> aliases)
    {
        if (node.Object is null)
        {
            return node.Method.Name switch
            {
                "get_Now" => Function(NeutralLocalDateTime),
                "get_UtcNow" => Function(NeutralDateTime),
                "get_Today" => Function(NeutralDate),
                _ when CanEvaluate(node) => parameters.Add(Evaluate(node)),
                _ => throw Unsupported(node, $"Static temporal method '{node.Method.Name}' is not supported."),
            };
        }

        var target = Lower(node.Object, aliases);
        var args = node.Arguments.Select(argument => Lower(argument, aliases)).ToArray();
        var wrapped = WrapTemporal(target, NeutralDateTime);

        return node.Method.Name switch
        {
            "AddYears" => AddDuration(wrapped, "years", args[0]),
            "AddMonths" => AddDuration(wrapped, "months", args[0]),
            "AddDays" => AddDuration(wrapped, "days", args[0]),
            "AddHours" => AddDuration(wrapped, "hours", args[0]),
            "AddMinutes" => AddDuration(wrapped, "minutes", args[0]),
            "AddSeconds" => AddDuration(wrapped, "seconds", args[0]),
            "AddMilliseconds" => AddDuration(wrapped, "milliseconds", args[0]),
            "ToUniversalTime" => Function(NeutralDateTime, target),
            "ToLocalTime" => Function(NeutralLocalDateTime, target),
            "ToString" => Function("toString", target),
            _ => throw Unsupported(node, $"Temporal method '{node.Method.Name}' is not supported."),
        };
    }

    private CypherExpression LowerBinary(
        LinqBinaryExpression node,
        IReadOnlyDictionary<ParameterExpression, string> aliases)
    {
        if (node.NodeType is ExpressionType.Equal or ExpressionType.NotEqual &&
            TryLowerComplexNullComparison(node, aliases, out var complexNull))
        {
            return complexNull;
        }

        var left = Lower(node.Left, aliases);
        var right = Lower(node.Right, aliases);

        if (node.NodeType is ExpressionType.Equal or ExpressionType.NotEqual)
        {
            if (left is Literal { Value: null })
            {
                return new AstUnaryExpression(
                    node.NodeType == ExpressionType.Equal ? CypherUnaryOperator.IsNull : CypherUnaryOperator.IsNotNull,
                    right);
            }

            if (right is Literal { Value: null })
            {
                return new AstUnaryExpression(
                    node.NodeType == ExpressionType.Equal ? CypherUnaryOperator.IsNull : CypherUnaryOperator.IsNotNull,
                    left);
            }
        }

        if (node.Left.Type.IsEnum)
        {
            left = Function("toInteger", left);
        }

        if (node.Right.Type.IsEnum)
        {
            right = Function("toInteger", right);
        }

        if (node.Left.Type == typeof(DateTime) && left is QueryParameter)
        {
            left = Function(NeutralDateTime, left);
        }

        if (node.Right.Type == typeof(DateTime) && right is QueryParameter)
        {
            right = Function(NeutralDateTime, right);
        }

        var op = node.NodeType switch
        {
            ExpressionType.AndAlso => CypherBinaryOperator.And,
            ExpressionType.OrElse => CypherBinaryOperator.Or,
            ExpressionType.Equal => CypherBinaryOperator.Equal,
            ExpressionType.NotEqual => CypherBinaryOperator.NotEqual,
            ExpressionType.LessThan => CypherBinaryOperator.LessThan,
            ExpressionType.LessThanOrEqual => CypherBinaryOperator.LessThanOrEqual,
            ExpressionType.GreaterThan => CypherBinaryOperator.GreaterThan,
            ExpressionType.GreaterThanOrEqual => CypherBinaryOperator.GreaterThanOrEqual,
            ExpressionType.Add => CypherBinaryOperator.Add,
            ExpressionType.Subtract => CypherBinaryOperator.Subtract,
            ExpressionType.Multiply => CypherBinaryOperator.Multiply,
            ExpressionType.Divide => CypherBinaryOperator.Divide,
            ExpressionType.Modulo => CypherBinaryOperator.Modulo,
            _ => throw Unsupported(node, $"Binary operator '{node.NodeType}' is not supported."),
        };

        return new AstBinaryExpression(op, left, right);
    }

    private CypherExpression LowerUnary(
        LinqUnaryExpression node,
        IReadOnlyDictionary<ParameterExpression, string> aliases)
    {
        var operand = Lower(node.Operand, aliases);
        if (node.NodeType == ExpressionType.Not)
        {
            return new AstUnaryExpression(CypherUnaryOperator.Not, operand);
        }

        if (node.NodeType is ExpressionType.Convert or ExpressionType.ConvertChecked)
        {
            var actualType = Nullable.GetUnderlyingType(node.Type) ?? node.Type;
            var function = actualType switch
            {
                var type when type == typeof(short) || type == typeof(int) || type == typeof(long) => "toInteger",
                var type when type == typeof(float) || type == typeof(double) || type == typeof(decimal) => "toFloat",
                var type when type == typeof(string) => "toString",
                var type when type == typeof(bool) => "toBoolean",
                var type when type == typeof(DateTime) => NeutralDateTime,
                _ => null,
            };

            return function is null ? operand : Function(function, operand);
        }

        if (node.NodeType == ExpressionType.Negate)
        {
            return new AstUnaryExpression(CypherUnaryOperator.Negate, operand);
        }

        throw Unsupported(node, $"Unary operator '{node.NodeType}' is not supported.");
    }

    private bool TryLowerDynamicEntityMethod(
        MethodCallExpression node,
        IReadOnlyDictionary<ParameterExpression, string> aliases,
        out CypherExpression expression)
    {
        expression = null!;
        if (node.Method.DeclaringType?.Name != "Neo4jDynamicEntityExtensions" || node.Arguments.Count < 1)
        {
            return false;
        }

        var target = Lower(node.Arguments[0], aliases);
        expression = node.Method.Name switch
        {
            "GetProperty" when node.Arguments.Count == 2 =>
                new EscapedPropertyAccess(target, RequireConstantIdentifier(node.Arguments[1], node.Method.Name)),
            "HasProperty" when node.Arguments.Count == 2 =>
                new AstUnaryExpression(
                    CypherUnaryOperator.IsNotNull,
                    new EscapedPropertyAccess(target, RequireConstantIdentifier(node.Arguments[1], node.Method.Name))),
            "HasLabel" when node.Arguments.Count == 2 =>
                new AstBinaryExpression(CypherBinaryOperator.In, Lower(node.Arguments[1], aliases), Function("labels", target)),
            "HasType" when node.Arguments.Count == 2 =>
                new AstBinaryExpression(CypherBinaryOperator.Equal, Function("type", target), Lower(node.Arguments[1], aliases)),
            _ => throw Unsupported(node, $"Dynamic entity method '{node.Method.Name}' is not supported."),
        };

        return true;
    }

    private bool TryLowerCollectionContains(
        MethodCallExpression node,
        IReadOnlyDictionary<ParameterExpression, string> aliases,
        out CypherExpression expression)
    {
        expression = null!;
        if (node.Method.Name != nameof(Enumerable.Contains))
        {
            return false;
        }

        Expression? source;
        Expression? item;
        if (node.Method.IsStatic && node.Arguments.Count >= 2)
        {
            source = node.Arguments[0];
            item = node.Arguments[1];
        }
        else if (node.Object is not null && node.Arguments.Count == 1)
        {
            source = node.Object;
            item = node.Arguments[0];
        }
        else
        {
            return false;
        }

        if (GraphDataModel.IsCollectionOfComplex(source.Type))
        {
            // Lowering would compare a parameter against the property node itself, which evaluates to
            // false/null in Cypher instead of matching by value.
            throw Unsupported(
                node,
                "Contains on a complex-property collection cannot be translated; use .Any(x => ...) with " +
                "explicit property comparisons instead.");
        }

        expression = new AstBinaryExpression(CypherBinaryOperator.In, Lower(item, aliases), Lower(source, aliases));
        return true;
    }

    private bool TryLowerComplexCollectionMethod(
        MethodCallExpression node,
        IReadOnlyDictionary<ParameterExpression, string> aliases,
        out CypherExpression expression)
    {
        expression = null!;
        if (node.Method.Name is not (nameof(Enumerable.Any) or nameof(Enumerable.All) or nameof(Enumerable.Count) or nameof(Enumerable.Select)))
        {
            return false;
        }

        var source = node.Method.IsStatic ? node.Arguments.FirstOrDefault() : node.Object;
        if (source is not MemberExpression member || !GraphDataModel.IsCollectionOfComplex(member.Type))
        {
            return false;
        }

        var patternInfo = BuildComplexPattern(member, aliases);
        var lambda = node.Arguments.Skip(1).Select(ExtractLambda).FirstOrDefault(item => item is not null);
        CypherExpression? predicate = null;
        CypherExpression projection = new VariableRef(patternInfo.TargetAlias);

        if (lambda is not null)
        {
            var nestedAliases = new Dictionary<ParameterExpression, string>(aliases);
            nestedAliases[lambda.Parameters[0]] = patternInfo.TargetAlias;
            var lowered = Lower(lambda.Body, nestedAliases);
            if (node.Method.Name == nameof(Enumerable.Select))
            {
                projection = lowered;
            }
            else
            {
                predicate = lowered;
            }
        }

        expression = node.Method.Name switch
        {
            nameof(Enumerable.Any) => new PatternSubqueryExpression(PatternSubqueryKind.Exists, patternInfo.Pattern, predicate),
            nameof(Enumerable.All) when predicate is not null => new AstUnaryExpression(
                CypherUnaryOperator.Not,
                new PatternSubqueryExpression(
                    PatternSubqueryKind.Exists,
                    patternInfo.Pattern,
                    new AstUnaryExpression(CypherUnaryOperator.Not, predicate))),
            nameof(Enumerable.Count) => new PatternSubqueryExpression(PatternSubqueryKind.Count, patternInfo.Pattern, predicate),
            nameof(Enumerable.Select) => new PatternComprehensionExpression(patternInfo.Pattern, projection),
            _ => throw Unsupported(node, $"Complex collection method '{node.Method.Name}' requires a predicate."),
        };

        return true;
    }

    private bool TryLowerComplexCollectionCount(
        MemberExpression node,
        IReadOnlyDictionary<ParameterExpression, string> aliases,
        out CypherExpression expression)
    {
        expression = null!;
        if (node.Member.Name != nameof(ICollection.Count) ||
            node.Expression is not MemberExpression collection ||
            !GraphDataModel.IsCollectionOfComplex(collection.Type))
        {
            return false;
        }

        var pattern = BuildComplexPattern(collection, aliases);
        expression = new PatternSubqueryExpression(PatternSubqueryKind.Count, pattern.Pattern);
        return true;
    }

    private bool TryLowerEnumerableAggregation(
        MethodCallExpression node,
        IReadOnlyDictionary<ParameterExpression, string> aliases,
        out CypherExpression expression)
    {
        expression = null!;
        if (node.Method.DeclaringType != typeof(Enumerable) ||
            node.Method.Name is not (nameof(Enumerable.Count) or nameof(Enumerable.Sum) or nameof(Enumerable.Average) or nameof(Enumerable.Min) or nameof(Enumerable.Max)))
        {
            return false;
        }

        var functionName = node.Method.Name switch
        {
            nameof(Enumerable.Count) => "count",
            nameof(Enumerable.Sum) => "sum",
            nameof(Enumerable.Average) => "avg",
            nameof(Enumerable.Min) => "min",
            nameof(Enumerable.Max) => "max",
            _ => throw new InvalidOperationException(),
        };

        var selector = node.Arguments.Skip(1).Select(ExtractLambda).FirstOrDefault(lambda => lambda is not null);
        if (selector is null)
        {
            expression = Function(functionName, Lower(node.Arguments[0], aliases));
            return true;
        }

        // The selector's parameter ranges over the aggregated source, which the aggregate function
        // evaluates against the current scope; default it deliberately rather than via ResolveAlias.
        var selectorAlias = aliases.TryGetValue(selector.Parameters[0], out var boundAlias)
            ? boundAlias
            : ResolveDefaultAlias(selector.Parameters[0].Type, "src");
        expression = Function(functionName, LowerLambda(selector, selectorAlias, aliases));
        return true;
    }

    private bool TryLowerComplexNullComparison(
        LinqBinaryExpression node,
        IReadOnlyDictionary<ParameterExpression, string> aliases,
        out CypherExpression expression)
    {
        expression = null!;
        MemberExpression? member = node.Left is MemberExpression leftMember && IsNull(node.Right)
            ? leftMember
            : node.Right is MemberExpression rightMember && IsNull(node.Left)
                ? rightMember
                : null;

        if (member is null || (!GraphDataModel.IsComplex(member.Type) && !GraphDataModel.IsCollectionOfComplex(member.Type)))
        {
            return false;
        }

        var pattern = BuildComplexPattern(member, aliases);
        var exists = new PatternSubqueryExpression(PatternSubqueryKind.Exists, pattern.Pattern);
        expression = node.NodeType == ExpressionType.Equal
            ? new AstUnaryExpression(CypherUnaryOperator.Not, exists)
            : exists;
        return true;
    }

    private bool TryLowerComplexMember(
        MemberExpression node,
        IReadOnlyDictionary<ParameterExpression, string> aliases,
        out CypherExpression expression)
    {
        expression = null!;
        if (!TryGetMemberChain(node, out var root, out var members))
        {
            return false;
        }

        var currentAlias = ResolveAlias(root, aliases);
        var index = 0;
        if (typeof(IGraphPathSegment).IsAssignableFrom(root.Type) && members.Count > 0)
        {
            currentAlias = members[0].Name switch
            {
                nameof(IGraphPathSegment.StartNode) => "src",
                nameof(IGraphPathSegment.Relationship) => "r",
                nameof(IGraphPathSegment.EndNode) => "tgt",
                _ => currentAlias,
            };
            index = currentAlias == ResolveAlias(root, aliases) ? 0 : 1;
        }

        var traversed = false;
        for (; index < members.Count; index++)
        {
            if (members[index] is not PropertyInfo property)
            {
                if (!traversed)
                {
                    return false;
                }

                expression = new PropertyAccess(new VariableRef(currentAlias), members[index].Name);
                continue;
            }

            var memberType = property.PropertyType;
            var targetType = GetComplexElementType(memberType);
            if (targetType is not null)
            {
                var targetAlias = $"{currentAlias}_{property.Name.ToLowerInvariant()}";
                AddNavigationMatch(currentAlias, targetAlias, property, targetType);
                currentAlias = targetAlias;
                traversed = true;
                expression = new VariableRef(currentAlias);
                continue;
            }

            if (!traversed)
            {
                return false;
            }

            expression = new PropertyAccess(new VariableRef(currentAlias), Labels.GetLabelFromProperty(property));
        }

        return traversed;
    }

    private ComplexPattern BuildComplexPattern(
        MemberExpression node,
        IReadOnlyDictionary<ParameterExpression, string> aliases)
    {
        if (!TryGetMemberChain(node, out var root, out var members))
        {
            throw Unsupported(node, "Complex-property navigation must start from a query parameter.");
        }

        var currentAlias = ResolveAlias(root, aliases);
        var elements = new List<PatternElement> { new NodePattern(currentAlias, []) };
        var index = 0;
        if (typeof(IGraphPathSegment).IsAssignableFrom(root.Type) && members.Count > 0)
        {
            currentAlias = members[0].Name switch
            {
                nameof(IGraphPathSegment.StartNode) => "src",
                nameof(IGraphPathSegment.Relationship) => "r",
                nameof(IGraphPathSegment.EndNode) => "tgt",
                _ => currentAlias,
            };
            elements[0] = new NodePattern(currentAlias, []);
            index = 1;
        }

        for (; index < members.Count; index++)
        {
            if (members[index] is not PropertyInfo property || GetComplexElementType(property.PropertyType) is not { } targetType)
            {
                break;
            }

            var targetAlias = $"{currentAlias}_{property.Name.ToLowerInvariant()}";
            elements.Add(new RelationshipPattern(
                alias: null,
                GraphDataModel.GetComplexPropertyRelationshipType(property),
                CypherDirection.Outgoing,
                depth: null));
            elements.Add(new NodePattern(targetAlias, [Labels.GetLabelFromType(targetType)]));
            currentAlias = targetAlias;
        }

        if (elements.Count == 1)
        {
            throw Unsupported(node, "The member is not a complex-property navigation.");
        }

        return new ComplexPattern(new PathPattern(elements), currentAlias);
    }

    private void AddNavigationMatch(string sourceAlias, string targetAlias, PropertyInfo property, Type targetType)
    {
        if (!_navigationAliases.Add(targetAlias))
        {
            return;
        }

        _navigationMatches.Add(new MatchClause(
        [
            new PathPattern(
            [
                new NodePattern(sourceAlias, []),
                new RelationshipPattern(
                    alias: null,
                    GraphDataModel.GetComplexPropertyRelationshipType(property),
                    CypherDirection.Outgoing,
                    depth: null),
                new NodePattern(targetAlias, [Labels.GetLabelFromType(targetType)])
            ])
            // Optional so that rows without the navigated property survive: a required MATCH would
            // silently drop them from OR-predicates, projections, and orderings that touch the path.
        ], optional: true));
    }

    private static bool TryMapPathSegmentMember(
        MemberExpression node,
        IReadOnlyDictionary<ParameterExpression, string> aliases,
        out CypherExpression expression)
    {
        expression = null!;
        if (node.Expression is not ParameterExpression parameter ||
            !typeof(IGraphPathSegment).IsAssignableFrom(parameter.Type))
        {
            return false;
        }

        expression = node.Member.Name switch
        {
            nameof(IGraphPathSegment.StartNode) => new VariableRef("src"),
            nameof(IGraphPathSegment.Relationship) => new VariableRef("r"),
            nameof(IGraphPathSegment.EndNode) => new VariableRef("tgt"),
            _ => new PropertyAccess(new VariableRef(ResolveAlias(parameter, aliases)), node.Member.Name),
        };
        return true;
    }

    private static bool TryLowerTemporalMember(
        Type? declaringType,
        string memberName,
        CypherExpression target,
        out CypherExpression expression)
    {
        expression = null!;
        if (declaringType == typeof(DateTime) || declaringType == typeof(DateTimeOffset))
        {
            var wrapped = WrapTemporal(target, NeutralDateTime);
            expression = memberName switch
            {
                "Year" => new PropertyAccess(wrapped, "year"),
                "Month" => new PropertyAccess(wrapped, "month"),
                "Day" => new PropertyAccess(wrapped, "day"),
                "Hour" => new PropertyAccess(wrapped, "hour"),
                "Minute" => new PropertyAccess(wrapped, "minute"),
                "Second" => new PropertyAccess(wrapped, "second"),
                "Millisecond" => new PropertyAccess(wrapped, "millisecond"),
                "DayOfWeek" => new PropertyAccess(wrapped, "dayOfWeek"),
                "DayOfYear" => new PropertyAccess(wrapped, "ordinalDay"),
                "Date" => Function(NeutralDate, wrapped),
                "TimeOfDay" => Function(NeutralTime, wrapped),
                _ => null!,
            };
            return expression is not null;
        }

        if (declaringType == typeof(DateOnly))
        {
            var wrapped = WrapTemporal(target, NeutralDate);
            expression = memberName switch
            {
                "Year" => new PropertyAccess(wrapped, "year"),
                "Month" => new PropertyAccess(wrapped, "month"),
                "Day" => new PropertyAccess(wrapped, "day"),
                "DayOfWeek" => new PropertyAccess(wrapped, "dayOfWeek"),
                "DayOfYear" => new PropertyAccess(wrapped, "ordinalDay"),
                _ => null!,
            };
            return expression is not null;
        }

        if (declaringType == typeof(TimeOnly))
        {
            expression = memberName switch
            {
                "Hour" => ToInteger(Function("floor", Divide(target, TimeSpan.TicksPerHour))),
                "Minute" => ToInteger(Function("floor", Divide(Modulo(target, TimeSpan.TicksPerHour), TimeSpan.TicksPerMinute))),
                "Second" => ToInteger(Function("floor", Divide(Modulo(target, TimeSpan.TicksPerMinute), TimeSpan.TicksPerSecond))),
                "Millisecond" => ToInteger(Function("floor", Divide(Modulo(target, TimeSpan.TicksPerSecond), TimeSpan.TicksPerMillisecond))),
                _ => null!,
            };
            return expression is not null;
        }

        if (declaringType == typeof(TimeSpan))
        {
            const long day = 86_400_000;
            const long hour = 3_600_000;
            const long minute = 60_000;
            const long second = 1_000;
            expression = memberName switch
            {
                "Days" => ToInteger(Function("floor", Divide(target, day))),
                "Hours" => ToInteger(Function("floor", Divide(Modulo(target, day), hour))),
                "Minutes" => ToInteger(Function("floor", Divide(Modulo(target, hour), minute))),
                "Seconds" => ToInteger(Function("floor", Divide(Modulo(target, minute), second))),
                "Milliseconds" => ToInteger(Modulo(target, second)),
                "TotalDays" => Divide(target, (double)day),
                "TotalHours" => Divide(target, (double)hour),
                "TotalMinutes" => Divide(target, (double)minute),
                "TotalSeconds" => Divide(target, (double)second),
                "TotalMilliseconds" => target,
                _ => null!,
            };
            return expression is not null;
        }

        return false;
    }

    private static CypherExpression AddDuration(CypherExpression target, string unit, CypherExpression value)
    {
        return new AstBinaryExpression(
            CypherBinaryOperator.Add,
            target,
            Function("temporal.duration", new MapExpression([new MapEntry(unit, value)])));
    }

    private static CypherExpression WrapTemporal(CypherExpression target, string function)
    {
        return target is FunctionCall call && call.Name.StartsWith("temporal.", StringComparison.Ordinal)
            ? target
            : Function(function, target);
    }

    private static Type? GetComplexElementType(Type type)
    {
        if (GraphDataModel.IsCollectionOfComplex(type))
        {
            return type.GetElementType() ?? type.GetGenericArguments().FirstOrDefault();
        }

        return GraphDataModel.IsComplex(type) && !typeof(IEntity).IsAssignableFrom(type) ? type : null;
    }

    private static bool TryGetMemberChain(
        MemberExpression leaf,
        out ParameterExpression root,
        out IReadOnlyList<MemberInfo> members)
    {
        var stack = new Stack<MemberInfo>();
        Expression? current = leaf;
        while (current is MemberExpression member)
        {
            stack.Push(member.Member);
            current = member.Expression;
        }

        while (current is LinqUnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
        {
            current = unary.Operand;
        }

        if (current is ParameterExpression parameter)
        {
            root = parameter;
            members = [.. stack];
            return true;
        }

        root = null!;
        members = [];
        return false;
    }

    private static string ResolveDefaultAlias(Type parameterType, string defaultAlias)
    {
        return typeof(IRelationship).IsAssignableFrom(parameterType) ? "r" : defaultAlias;
    }

    private static string ResolveAlias(
        ParameterExpression parameter,
        IReadOnlyDictionary<ParameterExpression, string> aliases)
    {
        if (aliases.TryGetValue(parameter, out var alias))
        {
            return alias;
        }

        // Guessing an alias here would render a syntactically valid query over the wrong variable;
        // Cypher would then return nulls instead of erroring, silently producing wrong results.
        throw new GraphQueryTranslationException(
            $"Lambda parameter '{parameter.Name}' of type '{parameter.Type.Name}' is not bound to a query scope.");
    }

    private static string RequireConstantIdentifier(Expression expression, string methodName)
    {
        if (expression is not ConstantExpression { Value: string value })
        {
            throw new NotSupportedException(
                $"{methodName} requires a compile-time constant property name; a computed or variable property name cannot be translated to Cypher.");
        }

        return value;
    }

    private static LambdaExpression? ExtractLambda(Expression expression)
    {
        return expression switch
        {
            LambdaExpression lambda => lambda,
            LinqUnaryExpression { NodeType: ExpressionType.Quote, Operand: LambdaExpression lambda } => lambda,
            _ => null,
        };
    }

    private static bool IsNull(Expression expression) => expression is ConstantExpression { Value: null };

    private static bool IsTemporalType(Type? type) =>
        type == typeof(DateTime) ||
        type == typeof(DateTimeOffset) ||
        type == typeof(DateOnly) ||
        type == typeof(TimeOnly);

    private static bool CanEvaluate(Expression expression)
    {
        var finder = new ParameterFinder();
        finder.Visit(expression);
        return !finder.HasParameter;
    }

    private static object? Evaluate(Expression expression)
    {
        try
        {
            return Expression.Lambda(expression).Compile().DynamicInvoke();
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw new GraphQueryTranslationException(
                $"Failed to evaluate parameter-free expression '{expression}': {exception.InnerException.Message}",
                exception.InnerException);
        }
    }

    private static FunctionCall Function(string name, params CypherExpression[] arguments) => new(name, arguments);

    private static PropertyAccess Property(string alias, string property) => new(new VariableRef(alias), property);

    private static CypherExpression Divide(CypherExpression left, object right) =>
        new AstBinaryExpression(CypherBinaryOperator.Divide, left, new Literal(right));

    private static CypherExpression Modulo(CypherExpression left, object right) =>
        new AstBinaryExpression(CypherBinaryOperator.Modulo, left, new Literal(right));

    private static CypherExpression ToInteger(CypherExpression value) => Function("toInteger", value);

    private static GraphQueryTranslationException Unsupported(Expression expression, string message) =>
        new($"Cannot lower expression '{expression}': {message}");

    private sealed class ParameterFinder : ExpressionVisitor
    {
        public bool HasParameter { get; private set; }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            HasParameter = true;
            return node;
        }
    }

    private sealed record ComplexPattern(PathPattern Pattern, string TargetAlias);
}
