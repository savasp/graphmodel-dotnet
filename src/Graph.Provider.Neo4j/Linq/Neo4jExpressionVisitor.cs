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

using System.Linq.Expressions;
using System.Text;
using Cvoya.Graph.Model;
using Neo4j.Driver;

namespace Cvoya.Graph.Provider.Neo4j.Linq;

public class Neo4jExpressionVisitor(
    Neo4jGraphProvider provider,
    Type rootType,
    Type elementType,
    IGraphTransaction? transaction = null)
{
    private readonly Neo4jGraphProvider _provider = provider;
    private readonly Type _rootType = rootType;
    private readonly Type _elementType = elementType;
    private readonly IGraphTransaction? _transaction = transaction;
    private readonly StringBuilder _matchClause = new();
    private readonly StringBuilder _whereClause = new();
    private readonly StringBuilder _returnClause = new();
    private readonly StringBuilder _orderByClause = new();
    private readonly Dictionary<string, object> _parameters = new();
    private int _parameterIndex = 0;
    private string _alias = "n";
    private int? _skip;
    private int? _take;
    private bool _isCountQuery = false;
    private bool _isAnyQuery = false;
    private bool _isAllQuery = false;
    private bool _isFirstQuery = false;
    private bool _isLastQuery = false;
    private bool _isSingleQuery = false;
    private bool _isSingleOrDefaultQuery = false;
    private bool _isDistinct = false;
    private bool _isGrouping = false;
    private string _groupingKeyAlias = "";
    private string _groupingItemsAlias = "";
    private Expression? _projectionExpression;
    private LambdaExpression? _anyPredicate;
    private LambdaExpression? _allPredicate;
    private string _groupingKeyProperty = "";

    public bool IsGroupingQuery => _isGrouping;

    public string Translate(Expression expression)
    {
        Visit(expression);
        return BuildQuery();
    }

    private void Visit(Expression? expression)
    {
        if (expression == null) return;

        switch (expression)
        {
            case MethodCallExpression methodCall:
                VisitMethodCall(methodCall);
                break;
            case ConstantExpression constant:
                VisitConstant(constant);
                break;
            default:
                break;
        }
    }

    private void VisitMethodCall(MethodCallExpression node)
    {
        // Visit the source first
        if (node.Arguments.Count > 0)
        {
            Visit(node.Arguments[0]);
        }

        switch (node.Method.Name)
        {
            case "Where":
                VisitWhere(node);
                break;
            case "Select":
                if (_isGrouping)
                    VisitSelectAfterGroupBy(node);
                else
                    VisitSelect(node);
                break;
            case "OrderBy":
            case "OrderByDescending":
                VisitOrderBy(node, ascending: node.Method.Name == "OrderBy");
                break;
            case "ThenBy":
            case "ThenByDescending":
                VisitThenBy(node, ascending: node.Method.Name == "ThenBy");
                break;
            case "Skip":
                VisitSkip(node);
                break;
            case "Take":
                VisitTake(node);
                break;
            case "Count":
                VisitCount(node);
                break;
            case "GroupBy":
                VisitGroupBy(node);
                break;
            case "First":
            case "FirstOrDefault":
                VisitFirst(node);
                break;
            case "Last":
            case "LastOrDefault":
                VisitLast(node);
                break;
            case "Single":
            case "SingleOrDefault":
                VisitSingle(node);
                break;
            case "Any":
                VisitAny(node);
                break;
            case "All":
                VisitAll(node);
                break;
            case "Distinct":
                VisitDistinct(node);
                break;
            case "ToList":
            case "ToArray":
                // Terminal operations - no special handling needed
                break;
        }
    }

    private void VisitConstant(ConstantExpression node)
    {
        if (node.Value is IQueryable)
        {
            // Determine if we're querying nodes or relationships
            var queryableType = node.Value.GetType();
            if (queryableType.IsGenericType)
            {
                var genericDef = queryableType.GetGenericTypeDefinition();
                if (genericDef == typeof(Neo4jQueryable<>))
                {
                    var elementType = queryableType.GetGenericArguments()[0];
                    var label = Neo4jGraphProvider.GetLabel(elementType);
                    if (typeof(Model.IRelationship).IsAssignableFrom(elementType))
                    {
                        // For relationships, include source and target nodes to get their IDs
                        _matchClause.Append($"MATCH (s)-[{_alias}:{label}]->(t)");
                        // Update the default return clause for relationships
                        _alias = "n"; // Keep using 'n' as the alias for the relationship
                    }
                    else
                    {
                        _matchClause.Append($"MATCH ({_alias}:{label})");
                    }
                }
            }
        }
    }

    private void VisitWhere(MethodCallExpression node)
    {
        if (node.Arguments.Count < 2) return;

        var lambda = ExtractLambda(node.Arguments[1]);
        if (lambda == null) return;

        var condition = TranslateExpression(lambda.Body, lambda.Parameters[0]);
        if (!string.IsNullOrEmpty(condition))
        {
            if (_whereClause.Length > 0)
                _whereClause.Append(" AND ");
            _whereClause.Append($"({condition})");
        }
    }

    private void VisitSelect(MethodCallExpression node)
    {
        if (node.Arguments.Count < 2) return;

        var lambda = ExtractLambda(node.Arguments[1]);
        if (lambda == null) return;

        _projectionExpression = lambda.Body;
        _returnClause.Clear();
        _returnClause.Append("RETURN ");

        // Add DISTINCT if needed
        if (_isDistinct)
        {
            _returnClause.Append("DISTINCT ");
        }

        if (lambda.Body is NewExpression newExpr && newExpr.Members != null)
        {
            // Anonymous type projection
            var projectionParts = new List<string>();
            for (int i = 0; i < newExpr.Members.Count; i++)
            {
                var member = newExpr.Members[i];
                var arg = newExpr.Arguments[i];
                var translatedArg = TranslateExpression(arg, lambda.Parameters[0]);
                projectionParts.Add($"{translatedArg} AS {member.Name}");
            }
            _returnClause.Append(string.Join(", ", projectionParts));
        }
        else if (lambda.Body is MemberExpression memberExpr)
        {
            // Single property projection
            var translated = TranslateExpression(memberExpr, lambda.Parameters[0]);
            _returnClause.Append(translated);
        }
        else
        {
            // Default to returning the full node
            _returnClause.Append(_alias);
        }
    }

    private void VisitGroupBy(MethodCallExpression node)
    {
        if (node.Arguments.Count < 2)
            throw new NotSupportedException("GroupBy requires at least 2 arguments");

        var keySelectorLambda = ExtractLambda(node.Arguments[1]);
        if (keySelectorLambda == null)
            throw new NotSupportedException("GroupBy key selector must be a lambda expression");

        var keyExpression = keySelectorLambda.Body;

        // Generate the grouping key
        var keyProperty = ExtractPropertyName(keyExpression);
        if (keyProperty != null)
        {
            _groupingKeyAlias = "groupKey";
            _groupingItemsAlias = "groupedItems";
            _groupingKeyProperty = keyProperty; // Store the property name
            _isGrouping = true;

            // Build WITH clause for grouping
            _returnClause.Clear();
            _returnClause.Append($"WITH {_alias}.{keyProperty} AS {_groupingKeyAlias}, collect({_alias}) AS {_groupingItemsAlias}");
        }
        else
        {
            throw new NotSupportedException("Complex grouping keys are not yet supported");
        }
    }

    private void VisitSelectAfterGroupBy(MethodCallExpression node)
    {
        if (!_isGrouping || node.Arguments.Count < 2) return;

        var selectorLambda = ExtractLambda(node.Arguments[1]);
        if (selectorLambda == null) return;

        _projectionExpression = selectorLambda.Body;

        // Clear any existing return clause from GroupBy
        _returnClause.Clear();
        _returnClause.Append("RETURN ");

        if (selectorLambda.Body is NewExpression newExpr && newExpr.Members != null)
        {
            var projectionParts = new List<string>();

            for (int i = 0; i < newExpr.Members.Count; i++)
            {
                var member = newExpr.Members[i];
                var arg = newExpr.Arguments[i];
                var translatedValue = "";

                // Handle different types of expressions in the projection
                if (arg is MemberExpression memberExpr && memberExpr.Member.Name == "Key")
                {
                    // Projecting the grouping key
                    translatedValue = _groupingKeyAlias;
                }
                else if (arg is MethodCallExpression methodCall)
                {
                    // Handle aggregate functions
                    translatedValue = TranslateAggregateFunction(methodCall);
                }
                else
                {
                    // Handle other expressions
                    translatedValue = TranslateExpression(arg, selectorLambda.Parameters[0]);
                }

                projectionParts.Add($"{translatedValue} AS {member.Name}");
            }

            _returnClause.Append(string.Join(", ", projectionParts));
        }
    }

    private string TranslateAggregateFunction(MethodCallExpression methodCall)
    {
        return methodCall.Method.Name switch
        {
            "Count" => $"size({_groupingItemsAlias})",
            "Sum" => TranslateAggregateWithProperty(methodCall, "sum"),
            "Average" => TranslateAggregateWithProperty(methodCall, "avg"),
            "Min" => TranslateAggregateWithProperty(methodCall, "min"),
            "Max" => TranslateAggregateWithProperty(methodCall, "max"),
            _ => throw new NotSupportedException($"Aggregate function {methodCall.Method.Name} is not supported")
        };
    }

    private string TranslateAggregateWithProperty(MethodCallExpression methodCall, string cypherFunction)
    {
        // If there's a property selector, extract it
        if (methodCall.Arguments.Count > 1)
        {
            var propSelectorLambda = ExtractLambda(methodCall.Arguments[1]);
            if (propSelectorLambda?.Body is MemberExpression memberExpr)
            {
                var propName = memberExpr.Member.Name;
                return $"{cypherFunction}([item IN {_groupingItemsAlias} | item.{propName}])";
            }
        }

        // Simple aggregate without property
        return $"{cypherFunction}({_groupingItemsAlias})";
    }

    private void VisitOrderBy(MethodCallExpression node, bool ascending)
    {
        if (node.Arguments.Count < 2) return;

        var lambda = ExtractLambda(node.Arguments[1]);
        if (lambda == null) return;

        var orderExpression = TranslateExpression(lambda.Body, lambda.Parameters[0]);
        _orderByClause.Clear();
        _orderByClause.Append(orderExpression);
        if (!ascending) _orderByClause.Append(" DESC");
    }

    private void VisitThenBy(MethodCallExpression node, bool ascending)
    {
        if (node.Arguments.Count < 2) return;

        var lambda = ExtractLambda(node.Arguments[1]);
        if (lambda == null) return;

        var orderExpression = TranslateExpression(lambda.Body, lambda.Parameters[0]);
        _orderByClause.Append(", ");
        _orderByClause.Append(orderExpression);
        if (!ascending) _orderByClause.Append(" DESC");
    }

    private void VisitSkip(MethodCallExpression node)
    {
        if (node.Arguments.Count < 2) return;

        if (node.Arguments[1] is ConstantExpression constant && constant.Value is int skip)
        {
            _skip = skip;
        }
    }

    private void VisitTake(MethodCallExpression node)
    {
        if (node.Arguments.Count < 2) return;

        if (node.Arguments[1] is ConstantExpression constant && constant.Value is int take)
        {
            _take = take;
        }
    }

    private void VisitCount(MethodCallExpression node)
    {
        _isCountQuery = true;
        _returnClause.Clear();
        _returnClause.Append($"RETURN count({_alias})");
    }
    private void VisitAny(MethodCallExpression node)
    {
        _isAnyQuery = true;
        if (node.Arguments.Count >= 2)
        {
            _anyPredicate = ExtractLambda(node.Arguments[1]);
        }
    }

    private void VisitAll(MethodCallExpression node)
    {
        _isAllQuery = true;

        if (node.Arguments.Count >= 2)
        {
            var lambda = ExtractLambda(node.Arguments[1]);
            if (lambda != null)
            {
                _allPredicate = lambda;
            }
        }
    }

    private void VisitFirst(MethodCallExpression node)
    {
        _isFirstQuery = true;
        _take = 1;
    }

    private void VisitLast(MethodCallExpression node)
    {
        _isLastQuery = true;
    }

    private void VisitSingle(MethodCallExpression node)
    {
        if (node.Method.Name == "SingleOrDefault")
        {
            _isSingleOrDefaultQuery = true;
        }
        else
        {
            _isSingleQuery = true;
        }

        // Check if Single/SingleOrDefault has a predicate
        if (node.Arguments.Count >= 2)
        {
            // Single/SingleOrDefault with predicate - treat it like Where + Single
            var lambda = ExtractLambda(node.Arguments[1]);
            if (lambda != null)
            {
                var condition = TranslateExpression(lambda.Body, lambda.Parameters[0]);
                if (!string.IsNullOrEmpty(condition))
                {
                    if (_whereClause.Length > 0)
                        _whereClause.Append(" AND ");
                    _whereClause.Append($"({condition})");
                }
            }
        }

        _take = 2; // Take 2 to verify there's only one
    }

    private void VisitDistinct(MethodCallExpression node)
    {
        _isDistinct = true;
    }

    private string TranslateExpression(Expression expression, ParameterExpression parameter)
    {
        return expression switch
        {
            MemberExpression member => TranslateMemberExpression(member, parameter),
            BinaryExpression binary => TranslateBinaryExpression(binary, parameter),
            ConstantExpression constant => AddParameter(constant.Value),
            MethodCallExpression methodCall => TranslateMethodCallExpression(methodCall, parameter),
            UnaryExpression unary => TranslateUnaryExpression(unary, parameter),
            NewExpression newExpr => TranslateNewExpression(newExpr, parameter),
            ConditionalExpression conditional => TranslateConditionalExpression(conditional, parameter),
            _ => throw new NotSupportedException($"Expression type {expression.NodeType} is not supported")
        };
    }

    private string TranslateMemberExpression(MemberExpression member, ParameterExpression parameter)
    {
        if (member.Expression == parameter)
        {
            return $"{_alias}.{member.Member.Name}";
        }
        else if (member.Expression is ConstantExpression)
        {
            // Evaluate the member access
            var value = Expression.Lambda(member).Compile().DynamicInvoke();
            return AddParameter(value);
        }
        else if (member.Expression is MemberExpression parentMember)
        {
            // Handle nested member access - check if it contains the parameter
            if (ContainsParameter(member, parameter))
            {
                // Check if this is a DateTime property access
                if (member.Member.DeclaringType == typeof(DateTime))
                {
                    var parentTranslated = TranslateMemberExpression(parentMember, parameter);
                    return member.Member.Name switch
                    {
                        "Year" => $"{parentTranslated}.year",
                        "Month" => $"{parentTranslated}.month",
                        "Day" => $"{parentTranslated}.day",
                        "Hour" => $"{parentTranslated}.hour",
                        "Minute" => $"{parentTranslated}.minute",
                        "Second" => $"{parentTranslated}.second",
                        "Date" => $"date({parentTranslated})",
                        _ => throw new NotSupportedException($"DateTime property {member.Member.Name} is not supported")
                    };
                }
                else if (member.Member.Name == "Length" && parentMember.Type == typeof(string))
                {
                    // Handle string Length property
                    var parentTranslated = TranslateMemberExpression(parentMember, parameter);
                    return $"size({parentTranslated})";
                }
                else
                {
                    // This is a chained property access on the parameter (like p.Bio.Length)
                    var parentTranslated = TranslateMemberExpression(parentMember, parameter);
                    return $"{parentTranslated}.{member.Member.Name}";
                }
            }
            else
            {
                // This is a nested member access on a captured variable - evaluate it
                var value = Expression.Lambda(member).Compile().DynamicInvoke();
                return AddParameter(value);
            }
        }
        else if (member.Expression == null)
        {
            // Handle static member access (like DateTime.Now)
            if (member.Member.DeclaringType == typeof(DateTime))
            {
                return member.Member.Name switch
                {
                    "Now" => "datetime()",
                    "UtcNow" => "datetime()",
                    "Today" => "date()",
                    _ => throw new NotSupportedException($"DateTime static member {member.Member.Name} is not supported")
                };
            }

            // For other static members, evaluate them
            var value = Expression.Lambda(member).Compile().DynamicInvoke();
            return AddParameter(value);
        }
        else
        {
            // Check if this member expression contains the parameter
            if (ContainsParameter(member, parameter))
            {
                // This contains a parameter reference, we need to translate it properly
                var baseTranslated = TranslateExpression(member.Expression, parameter);
                return $"{baseTranslated}.{member.Member.Name}";
            }
            else
            {
                // For other member access (like accessing a property of a captured variable),
                // evaluate the entire member expression
                var value = Expression.Lambda(member).Compile().DynamicInvoke();
                return AddParameter(value);
            }
        }
    }

    private bool ContainsParameter(Expression? expression, ParameterExpression parameter)
    {
        if (expression == null)
            return false;

        if (expression == parameter)
            return true;

        if (expression is MemberExpression member)
            return ContainsParameter(member.Expression, parameter);

        if (expression is MethodCallExpression method)
        {
            if (method.Object != null && ContainsParameter(method.Object, parameter))
                return true;
            return method.Arguments.Any(arg => ContainsParameter(arg, parameter));
        }

        if (expression is BinaryExpression binary)
            return ContainsParameter(binary.Left, parameter) || ContainsParameter(binary.Right, parameter);

        return false;
    }

    private string TranslateBinaryExpression(BinaryExpression binary, ParameterExpression parameter)
    {
        var left = TranslateExpression(binary.Left, parameter);
        var right = TranslateExpression(binary.Right, parameter);

        return binary.NodeType switch
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
            _ => throw new NotSupportedException($"Binary operator {binary.NodeType} is not supported")
        };
    }

    private string TranslateMethodCallExpression(MethodCallExpression methodCall, ParameterExpression parameter)
    {
        // Handle LINQ collection methods
        if (methodCall.Method.DeclaringType == typeof(Enumerable) ||
            methodCall.Method.DeclaringType == typeof(Queryable))
        {
            return methodCall.Method.Name switch
            {
                "Select" => TranslateSelectInExpression(methodCall, parameter),
                "Where" => TranslateWhereInExpression(methodCall, parameter),
                "Count" => $"size({TranslateExpression(methodCall.Arguments[0], parameter)})",
                "Any" => $"size({TranslateExpression(methodCall.Arguments[0], parameter)}) > 0",
                _ => throw new NotSupportedException($"LINQ method {methodCall.Method.Name} in expression is not supported")
            };
        }

        // Handle string methods
        if (methodCall.Method.DeclaringType == typeof(string))
        {
            var obj = methodCall.Object != null ? TranslateExpression(methodCall.Object, parameter) : "";

            return methodCall.Method.Name switch
            {
                "Contains" => $"{obj} CONTAINS {TranslateExpression(methodCall.Arguments[0], parameter)}",
                "StartsWith" => $"{obj} STARTS WITH {TranslateExpression(methodCall.Arguments[0], parameter)}",
                "EndsWith" => $"{obj} ENDS WITH {TranslateExpression(methodCall.Arguments[0], parameter)}",
                "ToUpper" => $"toUpper({obj})",
                "ToLower" => $"toLower({obj})",
                "Trim" => $"trim({obj})",
                "Substring" when methodCall.Arguments.Count == 1 => $"substring({obj}, {TranslateExpression(methodCall.Arguments[0], parameter)})",
                "Substring" when methodCall.Arguments.Count == 2 => $"substring({obj}, {TranslateExpression(methodCall.Arguments[0], parameter)}, {TranslateExpression(methodCall.Arguments[1], parameter)})",
                "Replace" => $"replace({obj}, {TranslateExpression(methodCall.Arguments[0], parameter)}, {TranslateExpression(methodCall.Arguments[1], parameter)})",
                "get_Length" => $"length({obj})",  // Changed from size() to length()
                _ => throw new NotSupportedException($"String method {methodCall.Method.Name} is not supported")
            };
        }

        // Handle Math methods
        if (methodCall.Method.DeclaringType == typeof(Math))
        {
            return methodCall.Method.Name switch
            {
                "Abs" => $"abs({TranslateExpression(methodCall.Arguments[0], parameter)})",
                "Sqrt" => $"sqrt({TranslateExpression(methodCall.Arguments[0], parameter)})",
                "Floor" => $"floor({TranslateExpression(methodCall.Arguments[0], parameter)})",
                "Ceiling" => $"ceil({TranslateExpression(methodCall.Arguments[0], parameter)})",
                "Round" => $"round({TranslateExpression(methodCall.Arguments[0], parameter)})",
                _ => throw new NotSupportedException($"Math method {methodCall.Method.Name} is not supported")
            };
        }

        // Handle DateTime properties and methods
        if (methodCall.Method.DeclaringType == typeof(DateTime))
        {
            var obj = methodCall.Object != null ? TranslateExpression(methodCall.Object, parameter) : "";

            return methodCall.Method.Name switch
            {
                "get_Now" => "datetime()",
                "get_UtcNow" => "datetime()",
                "get_Today" => "date()",
                "get_Year" => $"{obj}.year",
                "get_Month" => $"{obj}.month",
                "get_Day" => $"{obj}.day",
                "get_Hour" => $"{obj}.hour",
                "get_Minute" => $"{obj}.minute",
                "get_Second" => $"{obj}.second",
                "AddDays" => $"{obj} + duration({{days: {TranslateExpression(methodCall.Arguments[0], parameter)})",
                "AddHours" => $"{obj} + duration({{hours: {TranslateExpression(methodCall.Arguments[0], parameter)})",
                "AddMinutes" => $"{obj} + duration({{minutes: {TranslateExpression(methodCall.Arguments[0], parameter)})",
                "AddSeconds" => $"{obj} + duration({{seconds: {TranslateExpression(methodCall.Arguments[0], parameter)})",
                _ => throw new NotSupportedException($"DateTime method {methodCall.Method.Name} is not supported")
            };
        }

        throw new NotSupportedException($"Method {methodCall.Method.Name} is not supported");
    }

    private string TranslateSelectInExpression(MethodCallExpression methodCall, ParameterExpression parameter)
    {
        // For Select within an expression (like in a projection), we need to handle it as a list comprehension
        if (methodCall.Arguments.Count >= 2)
        {
            var source = TranslateExpression(methodCall.Arguments[0], parameter);
            var lambda = ExtractLambda(methodCall.Arguments[1]);
            if (lambda != null)
            {
                // In Cypher, this would be a list comprehension
                var itemParam = lambda.Parameters[0];
                var body = TranslateExpression(lambda.Body, itemParam);
                return $"[item IN {source} | {body}]";
            }
        }
        throw new NotSupportedException("Select in expression requires a lambda");
    }

    private string TranslateWhereInExpression(MethodCallExpression methodCall, ParameterExpression parameter)
    {
        // For Where within an expression
        if (methodCall.Arguments.Count >= 2)
        {
            var source = TranslateExpression(methodCall.Arguments[0], parameter);
            var lambda = ExtractLambda(methodCall.Arguments[1]);
            if (lambda != null)
            {
                // In Cypher, this would be a list comprehension with a filter
                var itemParam = lambda.Parameters[0];
                var condition = TranslateExpression(lambda.Body, itemParam);
                return $"[item IN {source} WHERE {condition}]";
            }
        }
        throw new NotSupportedException("Where in expression requires a lambda");
    }

    private string TranslateUnaryExpression(UnaryExpression unary, ParameterExpression parameter)
    {
        return unary.NodeType switch
        {
            ExpressionType.Not => $"NOT ({TranslateExpression(unary.Operand, parameter)})",
            ExpressionType.Convert => TranslateExpression(unary.Operand, parameter),
            ExpressionType.ConvertChecked => TranslateExpression(unary.Operand, parameter),
            _ => throw new NotSupportedException($"Unary operator {unary.NodeType} is not supported")
        };
    }

    private string TranslateNewExpression(NewExpression newExpr, ParameterExpression parameter)
    {
        // Handle string concatenation
        if (newExpr.Type == typeof(string) && newExpr.Arguments.Count > 0)
        {
            var parts = newExpr.Arguments.Select(arg => TranslateExpression(arg, parameter));
            return string.Join(" + ", parts);
        }

        throw new NotSupportedException($"New expression {newExpr} is not supported");
    }

    private string TranslateConditionalExpression(ConditionalExpression conditional, ParameterExpression parameter)
    {
        var test = TranslateExpression(conditional.Test, parameter);
        var ifTrue = TranslateExpression(conditional.IfTrue, parameter);
        var ifFalse = TranslateExpression(conditional.IfFalse, parameter);
        return $"CASE WHEN {test} THEN {ifTrue} ELSE {ifFalse} END";
    }

    private string BuildQuery()
    {
        var query = new StringBuilder();

        // For grouping queries, handle differently
        if (_isGrouping)
        {
            query.Append(_matchClause);
            if (_whereClause.Length > 0)
            {
                query.AppendLine();
                query.Append($"WHERE {_whereClause}");
            }
            query.AppendLine();

            // Check if we have a WITH clause from GroupBy
            if (_returnClause.ToString().StartsWith("WITH"))
            {
                query.Append(_returnClause);
                query.AppendLine();

                // The actual RETURN clause should be set by VisitSelectAfterGroupBy
                // which replaces the _returnClause with a RETURN statement
            }

            // If VisitSelectAfterGroupBy has been called, _returnClause will start with RETURN
            if (_returnClause.ToString().StartsWith("RETURN"))
            {
                // Get the WITH clause that was stored during GroupBy
                var matchAndWhere = query.ToString();
                query.Clear();
                query.Append(matchAndWhere);

                // Use the stored grouping key property
                if (!string.IsNullOrEmpty(_groupingKeyProperty))
                {
                    query.Append($"WITH {_alias}.{_groupingKeyProperty} AS {_groupingKeyAlias}, collect({_alias}) AS {_groupingItemsAlias}");
                }
                else
                {
                    // Fallback if somehow the property wasn't stored
                    query.Append($"WITH {_alias} AS {_groupingKeyAlias}, collect({_alias}) AS {_groupingItemsAlias}");
                }

                query.AppendLine();
                query.Append(_returnClause);
            }
        }
        else
        {
            // Normal query
            query.Append(_matchClause);

            if (_whereClause.Length > 0)
            {
                query.AppendLine();
                query.Append($"WHERE {_whereClause}");
            }

            if (_anyPredicate != null)
            {
                var condition = TranslateExpression(_anyPredicate.Body, _anyPredicate.Parameters[0]);
                if (_whereClause.Length > 0)
                {
                    query.Append($" AND {condition}");
                }
                else
                {
                    query.AppendLine();
                    query.Append($"WHERE {condition}");
                }
            }

            if (_allPredicate != null)
            {
                var label = _elementType.Name;
                var condition = TranslateExpression(_allPredicate.Body, _allPredicate.Parameters[0]);

                // Replace the alias used in the condition with 'temp' for the subquery
                var subqueryCondition = condition.Replace($"{_alias}.", "temp.");

                // Clear the query and build a simple EXISTS check
                query.Clear();
                query.Append($"RETURN NOT EXISTS {{ MATCH (temp:{label}) WHERE NOT ({subqueryCondition}) }}");
                return query.ToString();
            }

            // Return clause
            if (_returnClause.Length > 0)
            {
                query.AppendLine();
                // Check if we need to add DISTINCT to an existing return clause
                var returnStr = _returnClause.ToString();
                if (_isDistinct && !returnStr.Contains("DISTINCT"))
                {
                    // Insert DISTINCT after RETURN
                    if (returnStr.StartsWith("RETURN "))
                    {
                        query.Append("RETURN DISTINCT ");
                        query.Append(returnStr.Substring(7)); // Skip "RETURN "
                    }
                    else
                    {
                        query.Append(returnStr);
                    }
                }
                else
                {
                    query.Append(returnStr);
                }
            }
            else if (_isAnyQuery)
            {
                query.AppendLine();
                query.Append($"RETURN count({_alias}) > 0");
            }
            else if (_isAllQuery)
            {
                query.AppendLine();
                query.Append("RETURN allMatch");
            }
            else
            {
                query.AppendLine();
                query.Append($"RETURN ");
                if (_isDistinct) query.Append("DISTINCT ");

                // Check if we're querying relationships
                if (_matchClause.ToString().Contains($"(s)-[{_alias}:"))
                {
                    // For relationships, also return source and target IDs
                    query.Append($"{_alias}, s.Id as sourceId, t.Id as targetId");
                }
                else
                {
                    query.Append(_alias);
                }
            }

            // Order by
            if (_orderByClause.Length > 0)
            {
                query.AppendLine();
                query.Append($"ORDER BY {_orderByClause}");
            }

            // Skip/Take
            if (_skip.HasValue)
            {
                query.AppendLine();
                query.Append($"SKIP {_skip.Value}");
            }

            if (_take.HasValue)
            {
                query.AppendLine();
                query.Append($"LIMIT {_take.Value}");
            }
        }

        return query.ToString();
    }

    private string AddParameter(object? value)
    {
        var paramName = $"p{_parameterIndex++}";
        _parameters[paramName] = value ?? "";
        return $"${paramName}";
    }

    private LambdaExpression? ExtractLambda(Expression expression)
    {
        return expression switch
        {
            UnaryExpression { Operand: LambdaExpression lambda } => lambda,
            LambdaExpression lambda => lambda,
            _ => null
        };
    }

    private string? ExtractPropertyName(Expression expression)
    {
        return expression switch
        {
            MemberExpression memberExpr => memberExpr.Member.Name,
            _ => null
        };
    }

    public async Task<object?> ExecuteQueryAsync(string cypher, Type elementType)
    {
        var (_, tx) = await _provider.GetOrCreateTransaction(_transaction);
        Console.WriteLine($"DEBUG: ExecuteQueryAsync cypher: {cypher}");
        var cursor = await tx.RunAsync(cypher, _parameters);

        if (_isCountQuery)
        {
            var record = await cursor.SingleAsync();
            return record[0].As<long>();
        }

        if (_isAnyQuery)
        {
            var record = await cursor.SingleAsync();
            return record[0].As<bool>();
        }

        if (_isAllQuery)
        {
            var record = await cursor.SingleAsync();
            return record[0].As<bool>();
        }

        if (_isGrouping)
        {
            return await ParseGroupingResultsAsync(cursor);
        }

        if (_isSingleQuery || _isSingleOrDefaultQuery)
        {
            var results = new List<IRecord>();
            await foreach (var record in cursor)
            {
                results.Add(record);
                // For Single(), we set _take = 2, so we only need to check the first 2 records
                if (results.Count > 1)
                {
                    throw new InvalidOperationException("Sequence contains more than one element");
                }
            }

            if (results.Count == 0)
            {
                if (_isSingleQuery)
                {
                    throw new InvalidOperationException("Sequence contains no elements");
                }
                else // SingleOrDefault
                {
                    return null;
                }
            }

            return ParseRecord(results[0], elementType);
        }

        if (_isFirstQuery)
        {
            var record = await cursor.SingleAsync();
            return ParseRecord(record, elementType);
        }

        if (_isLastQuery)
        {
            var results = await cursor.ToListAsync();
            if (results.Count == 0) return null;
            return ParseRecord(results[results.Count - 1], elementType);
        }

        // Regular query - check if this is a projection query
        if (_projectionExpression != null)
        {
            // This is a projection query - handle it differently
            var listType = typeof(List<>).MakeGenericType(elementType);
            var items = (System.Collections.IList)Activator.CreateInstance(listType)!;

            await foreach (var record in cursor)
            {
                var item = ParseProjectionRecord(record, elementType);
                if (item != null) items.Add(item);
            }

            return items;
        }
        else
        {
            // Regular entity query
            var listType = typeof(List<>).MakeGenericType(elementType);
            var items = (System.Collections.IList)Activator.CreateInstance(listType)!;

            await foreach (var record in cursor)
            {
                var item = ParseEntityRecord(record, elementType);
                if (item != null) items.Add(item);
            }

            return items;
        }
    }

    public object? ExecuteQuery(string cypher, Type elementType)
    {
        return ExecuteQueryAsync(cypher, elementType).GetAwaiter().GetResult();
    }

    private async Task<object> ParseGroupingResultsAsync(IResultCursor cursor)
    {
        if (_projectionExpression is NewExpression newExpr && newExpr.Members != null)
        {
            // Get the type of the anonymous type from the NewExpression
            var anonymousType = newExpr.Type;
            var listType = typeof(List<>).MakeGenericType(anonymousType);
            var results = (System.Collections.IList)Activator.CreateInstance(listType)!;

            await foreach (var record in cursor)
            {
                // Create anonymous type instance
                var constructor = newExpr.Constructor;
                var args = new object?[newExpr.Arguments.Count];

                for (int i = 0; i < newExpr.Members.Count; i++)
                {
                    var memberName = newExpr.Members[i].Name;
                    if (record.TryGetValue(memberName, out var value))
                    {
                        args[i] = ConvertValue(value, constructor!.GetParameters()[i].ParameterType);
                    }
                }

                var instance = constructor!.Invoke(args);
                results.Add(instance);
            }

            return results;
        }

        // Fallback to List<object> if no projection expression
        var objectResults = new List<object>();
        await foreach (var record in cursor)
        {
            objectResults.Add(record);
        }
        return objectResults;
    }

    private object? ParseProjectionRecord(IRecord record, Type elementType)
    {
        if (_projectionExpression is NewExpression newExpr && newExpr.Members != null)
        {
            // Anonymous type projection
            var constructor = newExpr.Constructor;
            var args = new object?[newExpr.Arguments.Count];

            for (int i = 0; i < newExpr.Members.Count; i++)
            {
                var memberName = newExpr.Members[i].Name;
                if (record.TryGetValue(memberName, out var value))
                {
                    args[i] = ConvertValue(value, constructor!.GetParameters()[i].ParameterType);
                }
            }

            return constructor!.Invoke(args);
        }
        else if (_projectionExpression is MemberExpression)
        {
            // Simple member projection
            if (record.Values.Count > 0)
            {
                return ConvertValue(record.Values.First().Value, elementType);
            }
        }

        return null;
    }

    private object? ParseEntityRecord(IRecord record, Type elementType)
    {
        // Regular entity handling
        if (record.TryGetValue(_alias, out var nodeValue) && nodeValue is global::Neo4j.Driver.INode node)
        {
            // Use reflection to call the generic method
            var convertMethod = typeof(SerializationExtensions).GetMethod(nameof(SerializationExtensions.ConvertToGraphEntity))!;
            var genericMethod = convertMethod.MakeGenericMethod(elementType);
            return genericMethod.Invoke(null, new object[] { node });
        }

        if (record.TryGetValue(_alias, out var relValue) && relValue is global::Neo4j.Driver.IRelationship rel)
        {
            // Use reflection to call the generic method
            var convertMethod = typeof(SerializationExtensions).GetMethod(nameof(SerializationExtensions.ConvertToGraphEntity))!;
            var genericMethod = convertMethod.MakeGenericMethod(elementType);
            var entity = genericMethod.Invoke(null, new object[] { rel });

            // For relationships, we need to handle source and target IDs properly
            if (entity is Model.IRelationship relationship)
            {
                // Check if the query included source and target node IDs
                if (record.TryGetValue("sourceId", out var sourceId) && sourceId != null)
                {
                    relationship.SourceId = sourceId.As<string>();
                }
                if (record.TryGetValue("targetId", out var targetId) && targetId != null)
                {
                    relationship.TargetId = targetId.As<string>();
                }
            }

            return entity;
        }

        // Scalar values
        if (record.Values.Count > 0)
        {
            return ConvertValue(record.Values.First().Value, elementType);
        }

        return null;
    }

    private object? ParseRecord(IRecord record, Type elementType)
    {
        if (_projectionExpression != null)
        {
            return ParseProjectionRecord(record, elementType);
        }
        else
        {
            return ParseEntityRecord(record, elementType);
        }
    }

    private object? ConvertValue(object? value, Type targetType)
    {
        if (value == null) return null;
        if (targetType == value.GetType()) return value;

        // Handle nullable types
        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (underlyingType == typeof(int))
            return Convert.ToInt32(value);
        if (underlyingType == typeof(long))
            return Convert.ToInt64(value);
        if (underlyingType == typeof(double))
            return Convert.ToDouble(value);
        if (underlyingType == typeof(float))
            return Convert.ToSingle(value);
        if (underlyingType == typeof(decimal))
            return Convert.ToDecimal(value);
        if (underlyingType == typeof(bool))
            return Convert.ToBoolean(value);
        if (underlyingType == typeof(string))
            return value.ToString();
        if (underlyingType == typeof(DateTime))
        {
            if (value is ZonedDateTime zdt)
                return zdt.ToDateTimeOffset().DateTime;
            if (value is LocalDateTime ldt)
                return ldt.ToDateTime();
            return Convert.ToDateTime(value);
        }
        if (underlyingType == typeof(DateTimeOffset))
        {
            if (value is ZonedDateTime zdt)
                return zdt.ToDateTimeOffset();
            if (value is LocalDateTime ldt)
                return new DateTimeOffset(ldt.ToDateTime());
            return new DateTimeOffset(Convert.ToDateTime(value));
        }

        return value;
    }
}