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

using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using Cvoya.Graph.Model;

namespace Cvoya.Graph.Provider.Neo4j.Linq;

internal static class CypherExpressionBuilder
{
    public static string BuildCypherConcat(Expression expr, string varName)
    {
        // Recursively build Cypher string concatenation using + operator
        if (expr is BinaryExpression bin && bin.NodeType == ExpressionType.Add)
        {
            var left = BuildCypherConcat(bin.Left, varName);
            var right = BuildCypherConcat(bin.Right, varName);
            return $"{left} + {right}";
        }
        else if (expr is MemberExpression me)
        {
            return $"{varName}.{me.Member.Name}";
        }
        else if (expr is ConstantExpression ce)
        {
            // String literal
            return $"'{ce.Value}'";
        }
        else if (expr is MethodCallExpression mcex)
        {
            var mapped = TryMapMethodCallToCypher(mcex, varName);
            if (mapped != null) return mapped;
        }
        // Fallback
        return "''";
    }

    public static string? TryMapMethodCallToCypher(MethodCallExpression mcex, string varName)
    {
        // Only support simple cases for now
        if (mcex.Method.Name == "ToUpper" && mcex.Object is MemberExpression me)
            return $"toUpper({varName}.{me.Member.Name})";
        if (mcex.Method.Name == "ToLower" && mcex.Object is MemberExpression me2)
            return $"toLower({varName}.{me2.Member.Name})";
        if (mcex.Method.Name == "Trim" && mcex.Object is MemberExpression me3)
            return $"trim({varName}.{me3.Member.Name})";
        // Add more mappings as needed
        return null;
    }

    public static string BuildCypherExpression(Expression expr, string varName)
    {
        switch (expr)
        {
            case MemberExpression me:
                // Handle DateTime static properties
                if (me.Type == typeof(DateTime) && me.Member is PropertyInfo propertyInfo && propertyInfo.DeclaringType == typeof(DateTime))
                {
                    switch (propertyInfo.Name)
                    {
                        case "Now":
                            return "datetime()";
                        case "UtcNow":
                            return "datetime()";
                        case "Today":
                            return "date()";
                    }
                }
                // Handle DateTime instance properties
                else if (me.Expression != null && me.Expression.Type == typeof(DateTime))
                {
                    var dateTimeExpr = BuildCypherExpression(me.Expression, varName);
                    switch (me.Member.Name)
                    {
                        case "Year":
                            return $"{dateTimeExpr}.year";
                        case "Month":
                            return $"{dateTimeExpr}.month";
                        case "Day":
                            return $"{dateTimeExpr}.day";
                        case "Hour":
                            return $"{dateTimeExpr}.hour";
                        case "Minute":
                            return $"{dateTimeExpr}.minute";
                        case "Second":
                            return $"{dateTimeExpr}.second";
                        case "DayOfWeek":
                            return $"{dateTimeExpr}.dayOfWeek";
                        case "DayOfYear":
                            return $"{dateTimeExpr}.ordinalDay";
                    }
                }
                else if (me.Expression != null && me.Expression.Type == typeof(string) && me.Member.Name == "Length")
                {
                    // String.Length -> size(n.Property)
                    var innerExpr = BuildCypherExpression(me.Expression, varName);
                    return $"size({innerExpr})";
                }
                else if (me.Member.Name == "Count" && me.Expression is MemberExpression collectionMe)
                {
                    // Handle collection.Count where collection is a navigation property
                    var collectionProp = collectionMe.Member as PropertyInfo;
                    if (collectionProp != null && typeof(IEnumerable).IsAssignableFrom(collectionProp.PropertyType) && collectionProp.PropertyType != typeof(string))
                    {
                        // This is a collection navigation property (e.g., p.Knows.Count)
                        // Generate: COUNT { (n)-[:KNOWS]->() }
                        var relType = collectionProp.Name.ToUpperInvariant();

                        // Handle generic collection types to get the relationship type
                        var elementType = collectionProp.PropertyType.IsArray
                            ? collectionProp.PropertyType.GetElementType()
                            : collectionProp.PropertyType.GetGenericArguments().FirstOrDefault();

                        if (elementType != null && typeof(IRelationship).IsAssignableFrom(elementType))
                        {
                            // Try to get the label from the relationship type
                            var labelMethod = typeof(Neo4jGraphProvider).GetMethod(
                                "GetLabel",
                                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                            if (labelMethod != null)
                            {
                                relType = (string)labelMethod.Invoke(null, new[] { elementType })!;
                            }
                        }

                        // Use COUNT {} syntax instead of size()
                        return $"COUNT {{ ({varName})-[:{relType}]->() }}";
                    }
                }
                else if (me.Expression is ParameterExpression)
                {
                    // Simple property access
                    return $"{varName}.{me.Member.Name}";
                }
                else if (me.Expression is MemberExpression parentMe)
                {
                    // Nested property access (e.g., p.Address.City)
                    var parent = BuildCypherExpression(parentMe, varName);
                    return $"{parent}.{me.Member.Name}";
                }
                return $"{varName}.{me.Member.Name}";

            case BinaryExpression be:
                var left = BuildCypherExpression(be.Left, varName);
                var right = BuildCypherExpression(be.Right, varName);
                var op = be.NodeType switch
                {
                    ExpressionType.Add => "+",
                    ExpressionType.Subtract => "-",
                    ExpressionType.Multiply => "*",
                    ExpressionType.Divide => "/",
                    ExpressionType.Modulo => "%",
                    ExpressionType.Equal => "=",
                    ExpressionType.NotEqual => "<>",
                    ExpressionType.LessThan => "<",
                    ExpressionType.LessThanOrEqual => "<=",
                    ExpressionType.GreaterThan => ">",
                    ExpressionType.GreaterThanOrEqual => ">=",
                    ExpressionType.AndAlso => "AND",
                    ExpressionType.OrElse => "OR",
                    _ => throw new NotSupportedException($"Binary operator {be.NodeType} is not supported in projections")
                };
                return $"({left} {op} {right})";

            case ConstantExpression ce:
                return FormatValueForCypher(ce.Value);

            case MethodCallExpression mce:
                // Handle collection methods
                if (mce.Method.Name == "Count" && mce.Object != null)
                {
                    // collection.Count -> size(collection)
                    var collection = BuildCypherExpression(mce.Object, varName);
                    return $"size({collection})";
                }
                else if (mce.Method.Name == "Select")
                {
                    // Handle different Select patterns
                    Expression? collectionExpr = null;
                    LambdaExpression? lambdaExpr = null;

                    // Pattern 1: collection.Select(lambda) - instance method
                    if (mce.Object != null && mce.Arguments.Count == 1)
                    {
                        collectionExpr = mce.Object;
                        if (mce.Arguments[0] is UnaryExpression ue && ue.Operand is LambdaExpression lambda)
                        {
                            lambdaExpr = lambda;
                        }
                        else if (mce.Arguments[0] is LambdaExpression lambda2)
                        {
                            lambdaExpr = lambda2;
                        }
                    }
                    // Pattern 2: Enumerable.Select(collection, lambda) - static method
                    else if (mce.Object == null && mce.Arguments.Count == 2)
                    {
                        collectionExpr = mce.Arguments[0];
                        if (mce.Arguments[1] is UnaryExpression ue && ue.Operand is LambdaExpression lambda)
                        {
                            lambdaExpr = lambda;
                        }
                        else if (mce.Arguments[1] is LambdaExpression lambda2)
                        {
                            lambdaExpr = lambda2;
                        }
                    }

                    if (collectionExpr != null && lambdaExpr != null)
                    {
                        // Check if the collection is a navigation property
                        if (collectionExpr is MemberExpression me && me.Member is PropertyInfo prop)
                        {
                            var propType = prop.PropertyType;
                            if (typeof(IEnumerable).IsAssignableFrom(propType) && propType != typeof(string))
                            {
                                // This is a collection navigation property (e.g., p.Knows)
                                // We need to use a pattern comprehension instead
                                var relType = prop.Name.ToUpperInvariant();

                                // Handle generic collection types to get the relationship type
                                var elementType = propType.IsArray
                                    ? propType.GetElementType()
                                    : propType.GetGenericArguments().FirstOrDefault();

                                if (elementType != null && typeof(IRelationship).IsAssignableFrom(elementType))
                                {
                                    // Try to get the label from the relationship type
                                    var labelMethod = typeof(Neo4jGraphProvider).GetMethod(
                                        "GetLabel",
                                        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                                    if (labelMethod != null)
                                    {
                                        relType = (string)labelMethod.Invoke(null, new[] { elementType })!;
                                    }
                                }

                                var itemVar = lambdaExpr.Parameters[0].Name ?? "r";
                                var targetVar = $"{itemVar}_target";

                                // Build the projection expression, replacing relationship references with target node references
                                var projection = BuildCypherExpressionForRelationshipProjection(lambdaExpr.Body, itemVar, targetVar);

                                // Generate: [(n)-[r:KNOWS]->(target) | projection]
                                return $"[({varName})-[{itemVar}:{relType}]->({targetVar}) | {projection}]";
                            }
                        }

                        // Default case: regular collection
                        var collection = BuildCypherExpression(collectionExpr, varName);
                        var itemVar2 = lambdaExpr.Parameters[0].Name ?? "item";
                        var projection2 = BuildCypherExpression(lambdaExpr.Body, itemVar2);
                        return $"[{itemVar2} IN {collection} | {projection2}]";
                    }
                }
                else if (mce.Method.DeclaringType == typeof(Enumerable) && mce.Method.Name == "Count")
                {
                    // Enumerable.Count(collection) -> size(collection)
                    if (mce.Arguments.Count >= 1)
                    {
                        var collection = BuildCypherExpression(mce.Arguments[0], varName);
                        return $"size({collection})";
                    }
                }
                else if (mce.Method.DeclaringType == typeof(string))
                {
                    var target = BuildCypherExpression(mce.Object!, varName);
                    switch (mce.Method.Name)
                    {
                        case "ToUpper":
                            return $"toUpper({target})";
                        case "ToLower":
                            return $"toLower({target})";
                        case "Trim":
                            return $"trim({target})";
                        case "Substring" when mce.Arguments.Count == 1:
                            var start = BuildCypherExpression(mce.Arguments[0], varName);
                            return $"substring({target}, {start})";
                        case "Substring" when mce.Arguments.Count == 2:
                            var start2 = BuildCypherExpression(mce.Arguments[0], varName);
                            var length = BuildCypherExpression(mce.Arguments[1], varName);
                            return $"substring({target}, {start2}, {length})";
                        case "Replace" when mce.Arguments.Count == 2:
                            var search = BuildCypherExpression(mce.Arguments[0], varName);
                            var replace = BuildCypherExpression(mce.Arguments[1], varName);
                            return $"replace({target}, {search}, {replace})";
                        case "StartsWith":
                            var prefix = BuildCypherExpression(mce.Arguments[0], varName);
                            return $"{target} STARTS WITH {prefix}";
                        case "EndsWith":
                            var suffix = BuildCypherExpression(mce.Arguments[0], varName);
                            return $"{target} ENDS WITH {suffix}";
                        case "Contains":
                            var substring = BuildCypherExpression(mce.Arguments[0], varName);
                            return $"{target} CONTAINS {substring}";
                    }
                }
                else if (mce.Method.DeclaringType == typeof(Math))
                {
                    // Math functions
                    switch (mce.Method.Name)
                    {
                        case "Abs":
                            var val = BuildCypherExpression(mce.Arguments[0], varName);
                            return $"abs({val})";
                        case "Ceiling":
                            var val2 = BuildCypherExpression(mce.Arguments[0], varName);
                            return $"ceil({val2})";
                        case "Floor":
                            var val3 = BuildCypherExpression(mce.Arguments[0], varName);
                            return $"floor({val3})";
                        case "Round" when mce.Arguments.Count == 1:
                            var val4 = BuildCypherExpression(mce.Arguments[0], varName);
                            return $"round({val4})";
                        case "Sqrt":
                            var val5 = BuildCypherExpression(mce.Arguments[0], varName);
                            return $"sqrt({val5})";
                        case "Sin":
                            var val6 = BuildCypherExpression(mce.Arguments[0], varName);
                            return $"sin({val6})";
                        case "Cos":
                            var val7 = BuildCypherExpression(mce.Arguments[0], varName);
                            return $"cos({val7})";
                        case "Tan":
                            var val8 = BuildCypherExpression(mce.Arguments[0], varName);
                            return $"tan({val8})";
                        case "Log" when mce.Arguments.Count == 1:
                            var val9 = BuildCypherExpression(mce.Arguments[0], varName);
                            return $"log({val9})";
                        case "Log10":
                            var val10 = BuildCypherExpression(mce.Arguments[0], varName);
                            return $"log10({val10})";
                        case "Exp":
                            var val11 = BuildCypherExpression(mce.Arguments[0], varName);
                            return $"exp({val11})";
                        case "Pow":
                            var base1 = BuildCypherExpression(mce.Arguments[0], varName);
                            var exp1 = BuildCypherExpression(mce.Arguments[1], varName);
                            return $"({base1} ^ {exp1})";
                        case "Max" when mce.Arguments.Count == 2:
                            var v1 = BuildCypherExpression(mce.Arguments[0], varName);
                            var v2 = BuildCypherExpression(mce.Arguments[1], varName);
                            return $"CASE WHEN {v1} > {v2} THEN {v1} ELSE {v2} END";
                        case "Min" when mce.Arguments.Count == 2:
                            var v3 = BuildCypherExpression(mce.Arguments[0], varName);
                            var v4 = BuildCypherExpression(mce.Arguments[1], varName);
                            return $"CASE WHEN {v3} < {v4} THEN {v3} ELSE {v4} END";
                    }
                }
                throw new NotSupportedException($"Method {mce.Method.Name} is not supported in projections");

            case ConditionalExpression cond:
                // Ternary operator -> CASE WHEN
                var test = BuildCypherExpression(cond.Test, varName);
                var ifTrue = BuildCypherExpression(cond.IfTrue, varName);
                var ifFalse = BuildCypherExpression(cond.IfFalse, varName);
                return $"CASE WHEN {test} THEN {ifTrue} ELSE {ifFalse} END";

            case UnaryExpression ue:
                var operand = BuildCypherExpression(ue.Operand, varName);
                return ue.NodeType switch
                {
                    ExpressionType.Not => $"NOT {operand}",
                    ExpressionType.Negate => $"-{operand}",
                    ExpressionType.Convert or ExpressionType.ConvertChecked => operand, // Ignore conversions
                    _ => throw new NotSupportedException($"Unary operator {ue.NodeType} is not supported")
                };

            default:
                throw new NotSupportedException($"Expression type {expr.GetType().Name} is not supported in projections");
        }
    }

    public static string? TryMapMethodCallToCypherFull(MethodCallExpression mcex, string varName)
    {
        // String methods
        if (mcex.Method.Name == "ToUpper" && mcex.Object is MemberExpression me)
            return $"toUpper({varName}.{me.Member.Name})";
        if (mcex.Method.Name == "ToLower" && mcex.Object is MemberExpression me2)
            return $"toLower({varName}.{me2.Member.Name})";
        if (mcex.Method.Name == "Trim" && mcex.Object is MemberExpression me3)
            return $"trim({varName}.{me3.Member.Name})";
        if (mcex.Method.Name == "Substring" && mcex.Object is MemberExpression me4 && mcex.Arguments.Count > 0)
        {
            var start = BuildCypherExpression(mcex.Arguments[0], varName);
            var len = mcex.Arguments.Count > 1 ? ", " + BuildCypherExpression(mcex.Arguments[1], varName) : "";
            return $"substring({varName}.{me4.Member.Name}, {start}{len})";
        }
        if (mcex.Method.Name == "Replace" && mcex.Object is MemberExpression me5 && mcex.Arguments.Count == 2)
        {
            var oldVal = BuildCypherExpression(mcex.Arguments[0], varName);
            var newVal = BuildCypherExpression(mcex.Arguments[1], varName);
            return $"replace({varName}.{me5.Member.Name}, {oldVal}, {newVal})";
        }
        if (mcex.Method.Name == "Contains" && mcex.Object is MemberExpression me6 && mcex.Arguments.Count == 1)
        {
            var val = BuildCypherExpression(mcex.Arguments[0], varName);
            return $"{varName}.{me6.Member.Name} CONTAINS {val}";
        }
        if (mcex.Method.Name == "StartsWith" && mcex.Object is MemberExpression me7 && mcex.Arguments.Count == 1)
        {
            var val = BuildCypherExpression(mcex.Arguments[0], varName);
            return $"{varName}.{me7.Member.Name} STARTS WITH {val}";
        }
        if (mcex.Method.Name == "EndsWith" && mcex.Object is MemberExpression me8 && mcex.Arguments.Count == 1)
        {
            var val = BuildCypherExpression(mcex.Arguments[0], varName);
            return $"{varName}.{me8.Member.Name} ENDS WITH {val}";
        }
        if (mcex.Method.Name == "PadLeft" && mcex.Object is MemberExpression mePad && mcex.Arguments.Count == 2)
        {
            var totalWidth = BuildCypherExpression(mcex.Arguments[0], varName);
            var padCharExpr = mcex.Arguments[1];
            string padChar;
            if (padCharExpr is ConstantExpression ce && ce.Type == typeof(char))
                padChar = $"'{ce.Value}'";
            else
                padChar = BuildCypherExpression(padCharExpr, varName); // fallback, may need improvement
            return $"lpad({varName}.{mePad.Member.Name}, {totalWidth}, {padChar})";
        }
        // Math functions
        if (mcex.Method.DeclaringType == typeof(Math))
        {
            var args = string.Join(", ", mcex.Arguments.Select(a => BuildCypherExpression(a, varName)));
            return $"{mcex.Method.Name.ToLower()}({args})";
        }
        // Date/time functions (add more as needed)
        // ...
        // Fallback: null
        return null;
    }

    public static (string cypher, Dictionary<string, object?> parameters) BuildGraphQuery(
            Expression expression,
            Type elementType,
            Neo4jGraphProvider provider)
    {
        var builder = new CypherExpressionBuilder();
        var context = new CypherBuildContext();

        // Process the expression tree
        builder.ProcessExpression(expression, elementType, context, provider);

        // Build the final Cypher query
        var cypher = builder.BuildCypherQuery(context);

        return (cypher, context.Parameters);
    }

    private void ProcessExpression(
        Expression expression,
        Type elementType,
        CypherBuildContext context,
        Neo4jGraphProvider provider)
    {
        switch (expression)
        {
            case MethodCallExpression methodCall:
                ProcessMethodCall(methodCall, elementType, context, provider);
                break;
            case ConstantExpression constant when constant.Type.IsGenericType &&
                constant.Type.GetGenericTypeDefinition() == typeof(Neo4jQueryable<>):
                // Root queryable
                context.CurrentAlias = "n";
                context.RootType = elementType;
                context.Match.Append($"({context.CurrentAlias}:{Neo4jTypeManager.GetLabel(elementType)})");
                break;
            default:
                throw new NotSupportedException($"Expression type {expression.GetType()} is not supported");
        }
    }

    private void ProcessMethodCall(
        MethodCallExpression methodCall,
        Type elementType,
        CypherBuildContext context,
        Neo4jGraphProvider provider)
    {
        // First, process the source
        if (methodCall.Arguments.Count > 0 && methodCall.Arguments[0] is Expression source)
        {
            ProcessExpression(source, GetSourceElementType(methodCall), context, provider);
        }

        // Handle graph-specific methods
        if (methodCall.Method.DeclaringType == typeof(GraphQueryExtensions))
        {
            ProcessGraphExtensionMethod(methodCall, elementType, context, provider);
        }
        else if (methodCall.Method.DeclaringType == typeof(Queryable))
        {
            ProcessStandardLinqMethod(methodCall, elementType, context, provider);
        }
        else if (IsGraphTraversalMethod(methodCall))
        {
            ProcessGraphTraversalMethod(methodCall, elementType, context, provider);
        }
    }

    private void ProcessGraphExtensionMethod(
        MethodCallExpression methodCall,
        Type elementType,
        CypherBuildContext context,
        Neo4jGraphProvider provider)
    {
        switch (methodCall.Method.Name)
        {
            case nameof(GraphQueryExtensions.ConnectedBy):
                ProcessConnectedBy(methodCall, context);
                break;
            case nameof(GraphQueryExtensions.ShortestPath):
                ProcessShortestPath(methodCall, context);
                break;
            default:
                throw new NotSupportedException($"Graph method {methodCall.Method.Name} is not supported");
        }
    }

    private void ProcessConnectedBy(MethodCallExpression methodCall, CypherBuildContext context)
    {
        var genericArgs = methodCall.Method.GetGenericArguments();
        var sourceType = genericArgs[0];
        var relationshipType = genericArgs[1];
        var targetType = genericArgs[2];

        var sourceAlias = context.CurrentAlias;
        var relAlias = context.GetNextAlias("r");
        var targetAlias = context.GetNextAlias("n");

        // Build the relationship pattern
        var relLabel = Neo4jTypeManager.GetRelationshipType(relationshipType);
        var targetLabel = Neo4jTypeManager.GetLabel(targetType);

        if (context.Match.Length > 0) context.Match.AppendLine();
        context.Match.Append($"MATCH ({sourceAlias})-[{relAlias}:{relLabel}]->({targetAlias}:{targetLabel})");

        // Handle relationship filter if present
        if (methodCall.Arguments.Count > 1)
        {
            var filterExpression = ExtractLambdaFromQuote(methodCall.Arguments[1]);
            if (filterExpression != null && !IsConstantTrue(filterExpression.Body))
            {
                var oldAlias = context.CurrentAlias;
                context.CurrentAlias = relAlias;

                if (context.Where.Length > 0) context.Where.Append(" AND ");
                ProcessWhereClause(filterExpression.Body, context);

                context.CurrentAlias = oldAlias;
            }
        }

        context.CurrentAlias = targetAlias;
        context.Return = targetAlias;
    }

    private void ProcessShortestPath(MethodCallExpression methodCall, CypherBuildContext context)
    {
        var sourceAlias = context.CurrentAlias;
        var targetAlias = context.GetNextAlias("target");
        var pathAlias = context.GetNextAlias("path");

        // Extract target filter
        var targetFilter = ExtractLambdaFromQuote(methodCall.Arguments[1]);
        var maxHops = methodCall.Arguments[2] is ConstantExpression constExpr ?
            constExpr.Value as int? : null;

        // Build shortest path pattern
        if (context.Match.Length > 0) context.Match.AppendLine();
        context.Match.Append($"MATCH {pathAlias} = shortestPath(({sourceAlias})-[*");
        if (maxHops.HasValue)
        {
            context.Match.Append($"..{maxHops}");
        }
        context.Match.Append($"]->({targetAlias}))");

        // Apply target filter
        if (targetFilter != null)
        {
            var oldAlias = context.CurrentAlias;
            context.CurrentAlias = targetAlias;

            if (context.Where.Length > 0) context.Where.Append(" AND ");
            ProcessWhereClause(targetFilter.Body, context);

            context.CurrentAlias = oldAlias;
        }

        // Return path structure
        context.Return = $"{{ nodes: nodes({pathAlias}), relationships: relationships({pathAlias}) }}";
        context.IsPathResult = true;
    }

    private void ProcessStandardLinqMethod(
        MethodCallExpression methodCall,
        Type elementType,
        CypherBuildContext context,
        Neo4jGraphProvider provider)
    {
        switch (methodCall.Method.Name)
        {
            case "Where":
                var predicate = ExtractLambdaFromQuote(methodCall.Arguments[1]);
                if (predicate != null)
                {
                    if (context.Where.Length > 0) context.Where.Append(" AND ");
                    ProcessWhereClause(predicate.Body, context);
                }
                break;

            case "Select":
                var selector = ExtractLambdaFromQuote(methodCall.Arguments[1]);
                if (selector != null)
                {
                    ProcessSelectClause(selector, context);
                }
                break;

            case "OrderBy":
            case "OrderByDescending":
                var orderSelector = ExtractLambdaFromQuote(methodCall.Arguments[1]);
                if (orderSelector != null)
                {
                    ProcessOrderByClause(orderSelector, methodCall.Method.Name == "OrderByDescending", context);
                }
                break;

            case "Take":
                if (methodCall.Arguments[1] is ConstantExpression limit)
                {
                    context.Limit = (int)limit.Value!;
                }
                break;

            case "Skip":
                if (methodCall.Arguments[1] is ConstantExpression skip)
                {
                    context.Skip = (int)skip.Value!;
                }
                break;

            case "Distinct":
                context.IsDistinct = true;
                break;

            case "Count":
                context.Return = $"COUNT({context.CurrentAlias})";
                context.IsCountQuery = true;
                break;

            case "Any":
                if (methodCall.Arguments.Count > 1)
                {
                    var anyPredicate = ExtractLambdaFromQuote(methodCall.Arguments[1]);
                    if (anyPredicate != null)
                    {
                        if (context.Where.Length > 0) context.Where.Append(" AND ");
                        ProcessWhereClause(anyPredicate.Body, context);
                    }
                }
                context.Return = $"COUNT({context.CurrentAlias}) > 0";
                context.IsBooleanQuery = true;
                break;

            case "First":
            case "FirstOrDefault":
            case "Single":
            case "SingleOrDefault":
                context.Limit = methodCall.Method.Name.StartsWith("Single") ? 2 : 1;
                if (methodCall.Arguments.Count > 1)
                {
                    var firstPredicate = ExtractLambdaFromQuote(methodCall.Arguments[1]);
                    if (firstPredicate != null)
                    {
                        if (context.Where.Length > 0) context.Where.Append(" AND ");
                        ProcessWhereClause(firstPredicate.Body, context);
                    }
                }
                context.IsSingleResult = true;
                break;
        }
    }

    private bool IsGraphTraversalMethod(MethodCallExpression methodCall)
    {
        return methodCall.Method.DeclaringType?.IsGenericType == true &&
               methodCall.Method.DeclaringType.GetGenericTypeDefinition() == typeof(GraphTraversal<,>);
    }

    private void ProcessGraphTraversalMethod(
        MethodCallExpression methodCall,
        Type elementType,
        CypherBuildContext context,
        Neo4jGraphProvider provider)
    {
        // Handle methods from GraphTraversal<TNode, TRelationship>
        switch (methodCall.Method.Name)
        {
            case "TraversalToInternal":
                ProcessTraversalTo(methodCall, context);
                break;
            case "TraversalRelationshipsInternal":
                ProcessTraversalRelationships(methodCall, context);
                break;
            case "TraversalPathsInternal":
                ProcessTraversalPaths(methodCall, context);
                break;
        }
    }

    private void ProcessTraversalTo(MethodCallExpression methodCall, CypherBuildContext context)
    {
        // Extract traversal parameters
        var source = methodCall.Arguments[0];
        var direction = (TraversalDirection)((ConstantExpression)methodCall.Arguments[1]).Value!;
        var nodeFilter = ExtractLambdaFromConstant(methodCall.Arguments[2]);
        var relationshipFilter = ExtractLambdaFromConstant(methodCall.Arguments[3]);
        var targetFilter = ExtractLambdaFromConstant(methodCall.Arguments[4]);
        var minDepth = (int)((ConstantExpression)methodCall.Arguments[5]).Value!;
        var maxDepth = (int)((ConstantExpression)methodCall.Arguments[6]).Value!;

        var genericArgs = methodCall.Method.GetGenericArguments();
        var relationshipType = genericArgs[1];
        var targetType = genericArgs[2];

        var sourceAlias = context.CurrentAlias;
        var relAlias = context.GetNextAlias("r");
        var targetAlias = context.GetNextAlias("n");

        // Build traversal pattern
        var relLabel = Neo4jTypeManager.GetRelationshipType(relationshipType);
        var targetLabel = Neo4jTypeManager.GetLabel(targetType);

        if (context.Match.Length > 0) context.Match.AppendLine();
        context.Match.Append($"MATCH ({sourceAlias})");

        var relPattern = $"[{relAlias}:{relLabel}*{minDepth}..{maxDepth}]";
        switch (direction)
        {
            case TraversalDirection.Outgoing:
                context.Match.Append($"-{relPattern}->");
                break;
            case TraversalDirection.Incoming:
                context.Match.Append($"<-{relPattern}-");
                break;
            case TraversalDirection.Both:
                context.Match.Append($"-{relPattern}-");
                break;
        }
        context.Match.Append($"({targetAlias}:{targetLabel})");

        // Apply filters
        if (nodeFilter != null && !IsConstantTrue(nodeFilter.Body))
        {
            var oldAlias = context.CurrentAlias;
            context.CurrentAlias = sourceAlias;
            if (context.Where.Length > 0) context.Where.Append(" AND ");
            ProcessWhereClause(nodeFilter.Body, context);
            context.CurrentAlias = oldAlias;
        }

        if (relationshipFilter != null && !IsConstantTrue(relationshipFilter.Body))
        {
            if (context.Where.Length > 0) context.Where.Append(" AND ");
            context.Where.Append($"ALL(r IN {relAlias} WHERE ");

            var oldAlias = context.CurrentAlias;
            context.CurrentAlias = "r";
            ProcessWhereClause(relationshipFilter.Body, context);
            context.CurrentAlias = oldAlias;

            context.Where.Append(")");
        }

        if (targetFilter != null && !IsConstantTrue(targetFilter.Body))
        {
            var oldAlias = context.CurrentAlias;
            context.CurrentAlias = targetAlias;
            if (context.Where.Length > 0) context.Where.Append(" AND ");
            ProcessWhereClause(targetFilter.Body, context);
            context.CurrentAlias = oldAlias;
        }

        context.CurrentAlias = targetAlias;
        context.Return = targetAlias;
    }

    private void ProcessTraversalRelationships(MethodCallExpression methodCall, CypherBuildContext context)
    {
        // Similar to ProcessTraversalTo but returns relationships
        // Implementation details...
        throw new NotImplementedException("ProcessTraversalRelationships");
    }

    private void ProcessTraversalPaths(MethodCallExpression methodCall, CypherBuildContext context)
    {
        // Similar to ProcessTraversalTo but returns paths
        // Implementation details...
        throw new NotImplementedException("ProcessTraversalPaths");
    }

    private string BuildCypherQuery(CypherBuildContext context)
    {
        var query = new StringBuilder();

        // MATCH clause
        query.Append("MATCH ").AppendLine(context.Match.ToString());

        // WHERE clause
        if (context.Where.Length > 0)
        {
            query.Append("WHERE ").AppendLine(context.Where.ToString());
        }

        // RETURN clause
        query.Append("RETURN ");
        if (context.IsDistinct)
        {
            query.Append("DISTINCT ");
        }
        query.AppendLine(context.Return ?? context.CurrentAlias);

        // ORDER BY clause
        if (context.OrderBy.Length > 0)
        {
            query.Append("ORDER BY ").AppendLine(context.OrderBy.ToString());
        }

        // SKIP/LIMIT
        if (context.Skip > 0)
        {
            query.AppendLine($"SKIP {context.Skip}");
        }
        if (context.Limit > 0)
        {
            query.AppendLine($"LIMIT {context.Limit}");
        }

        return query.ToString();
    }

    private static void ProcessWhereClause(Expression expression, CypherBuildContext context)
    {
        // Process WHERE clause expressions
        // This would handle property access, comparisons, etc.
        // Implementation would be similar to existing expression processing
        throw new NotImplementedException("ProcessWhereClause implementation needed");
    }

    private static void ProcessSelectClause(LambdaExpression selector, CypherBuildContext context)
    {
        // Process SELECT projections
        throw new NotImplementedException("ProcessSelectClause implementation needed");
    }

    private static void ProcessOrderByClause(LambdaExpression selector, bool descending, CypherBuildContext context)
    {
        // Process ORDER BY
        throw new NotImplementedException("ProcessOrderByClause implementation needed");
    }

    private static Type GetSourceElementType(MethodCallExpression methodCall)
    {
        // Extract element type from the source expression
        if (methodCall.Arguments[0].Type.IsGenericType)
        {
            return methodCall.Arguments[0].Type.GetGenericArguments()[0];
        }
        return methodCall.Type;
    }

    private static LambdaExpression? ExtractLambdaFromQuote(Expression expression)
    {
        if (expression is UnaryExpression { NodeType: ExpressionType.Quote } unary)
        {
            return unary.Operand as LambdaExpression;
        }
        return expression as LambdaExpression;
    }

    private static LambdaExpression? ExtractLambdaFromConstant(Expression expression)
    {
        if (expression is ConstantExpression constant)
        {
            return constant.Value as LambdaExpression;
        }
        return null;
    }

    private static bool IsConstantTrue(Expression expression)
    {
        return expression is ConstantExpression { Value: true };
    }

    internal static string FormatValueForCypher(object? value)
    {
        return value switch
        {
            null => "null",
            string s => $"'{s.Replace("'", "\\'")}'",
            bool b => b ? "true" : "false",
            int or long or double or float or decimal => value.ToString()!,
            DateTime dt => $"datetime('{dt:O}')",
            DateTimeOffset dto => $"datetime('{dto:O}')",
            TimeSpan ts => $"duration('PT{ts.TotalSeconds}S')",
            Enum e => $"'{e}'",
            _ => $"'{value}'"
        };
    }

    private static string BuildCypherExpressionForRelationshipProjection(Expression expr, string relVar, string targetVar)
    {
        switch (expr)
        {
            case MemberExpression me:
                // Handle k.Target.FirstName -> target.FirstName
                if (me.Expression is MemberExpression parentMe && parentMe.Member.Name == "Target")
                {
                    return $"{targetVar}.{me.Member.Name}";
                }
                // Handle k.Source.FirstName -> source.FirstName (if needed)
                else if (me.Expression is MemberExpression parentMe2 && parentMe2.Member.Name == "Source")
                {
                    // For now, we'll focus on Target
                    return $"source.{me.Member.Name}";
                }
                else if (me.Expression is ParameterExpression)
                {
                    // Direct relationship property access
                    return $"{relVar}.{me.Member.Name}";
                }
                return BuildCypherExpression(expr, relVar);

            default:
                return BuildCypherExpression(expr, relVar);
        }
    }
}
