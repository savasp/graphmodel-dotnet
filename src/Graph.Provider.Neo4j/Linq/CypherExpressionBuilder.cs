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
using System.Text;
using Cvoya.Graph.Model;
using Cvoya.Graph.Provider.Neo4j.Schema;
using Neo4j.Driver;

namespace Cvoya.Graph.Provider.Neo4j.Linq;

internal class CypherExpressionBuilder
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

                        if (elementType != null && typeof(Cvoya.Graph.Model.IRelationship).IsAssignableFrom(elementType))
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

                                if (elementType != null && typeof(Cvoya.Graph.Model.IRelationship).IsAssignableFrom(elementType))
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
                else if (mce.Method.DeclaringType == typeof(DateTime))
                {
                    // DateTime functions
                    var target = BuildCypherExpression(mce.Object!, varName);
                    switch (mce.Method.Name)
                    {
                        case "AddYears":
                            var years = BuildCypherExpression(mce.Arguments[0], varName);
                            return $"datetime({{epochMillis: {target}.epochMillis + ({years} * 365 * 24 * 60 * 60 * 1000)}})";
                        case "AddMonths":
                            var months = BuildCypherExpression(mce.Arguments[0], varName);
                            return $"datetime({{epochMillis: {target}.epochMillis + ({months} * 30 * 24 * 60 * 60 * 1000)}})";
                        case "AddDays":
                            var days = BuildCypherExpression(mce.Arguments[0], varName);
                            return $"datetime({{epochMillis: {target}.epochMillis + ({days} * 24 * 60 * 60 * 1000)}})";
                        case "AddHours":
                            var hours = BuildCypherExpression(mce.Arguments[0], varName);
                            return $"datetime({{epochMillis: {target}.epochMillis + ({hours} * 60 * 60 * 1000)}})";
                        case "AddMinutes":
                            var minutes = BuildCypherExpression(mce.Arguments[0], varName);
                            return $"datetime({{epochMillis: {target}.epochMillis + ({minutes} * 60 * 1000)}})";
                        case "AddSeconds":
                            var seconds = BuildCypherExpression(mce.Arguments[0], varName);
                            return $"datetime({{epochMillis: {target}.epochMillis + ({seconds} * 1000)}})";
                        case "AddMilliseconds":
                            var milliseconds = BuildCypherExpression(mce.Arguments[0], varName);
                            return $"datetime({{epochMillis: {target}.epochMillis + {milliseconds}}})";
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

    public static (string cypher, Dictionary<string, object?> parameters, CypherBuildContext context) BuildGraphQuery(
            Expression expression,
            Type elementType,
            Neo4jGraphProvider provider,
            GraphQueryContext? queryContext = null)
    {
        var builder = new CypherExpressionBuilder();
        var context = new CypherBuildContext();

        // Set context information from queryContext if available
        if (queryContext != null)
        {
            context.QueryRootType = queryContext.RootType;
        }

        // Set the root type from the element type
        context.RootType = elementType;

        // Detect aggregate/scalar operations
        if (expression is MethodCallExpression methodCall)
        {
            var methodName = methodCall.Method.Name;
            context.IsScalarResult = methodName switch
            {
                "Count" or "LongCount" or "Sum" or "Average" or "Min" or "Max" or "Any" or "All" or "First" or "FirstOrDefault" or "Single" or "SingleOrDefault" or "Last" or "LastOrDefault" => true,
                _ => false
            };
        }

        // Process the expression tree
        builder.ProcessExpression(expression, elementType, context, provider);

        // Build the final Cypher query
        var cypher = builder.BuildCypherQuery(context);

        return (cypher, context.Parameters, context);
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
            case UnaryExpression unary when unary.NodeType == ExpressionType.Convert:
                // Handle Convert expressions (like casting from IQueryable<T> to IGraphQueryable<T>)
                ProcessExpression(unary.Operand, elementType, context, provider);
                break;
            case ConstantExpression constant when constant.Type.IsGenericType &&
                constant.Type.GetGenericTypeDefinition() == typeof(GraphQueryable<>):
                // Root queryable - extract context from the queryable instance
                context.CurrentAlias = "n";
                context.RootType = elementType;

                // Try to extract the GraphQueryContext from the queryable instance
                if (constant.Value != null)
                {
                    var contextProperty = constant.Type.GetProperty("Context");
                    if (contextProperty?.GetValue(constant.Value) is GraphQueryContext queryContext)
                    {
                        context.QueryRootType = queryContext.RootType;
                    }
                }

                // Generate different patterns based on query root type
                var label = Neo4jTypeManager.GetLabel(elementType);
                if (context.QueryRootType == GraphQueryContext.QueryRootType.Relationship)
                {
                    // For relationship queries, generate: ()-[r:type]->()
                    context.Match.Append($"()-[{context.CurrentAlias}:{label}]->()");
                }
                else
                {
                    // For node queries (default), generate: (n:type)
                    context.Match.Append($"({context.CurrentAlias}:{label})");
                }
                break;
            case ConstantExpression constant:
                // Handle basic constant expressions (like integers, strings) - these are typically method parameters
                // For method parameters, we don't need to generate any Cypher, just store the value
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
        // Special handling for WithDepth - don't process the source as it should use existing context
        bool skipSourceProcessing = methodCall.Method.Name == "WithDepth" &&
                                   methodCall.Method.DeclaringType?.IsGenericType == true &&
                                   methodCall.Method.DeclaringType.GetGenericTypeDefinition() == typeof(IGraphQueryable<>);

        // For instance methods, process the object (this) as the source
        if (methodCall.Object != null)
        {
            ProcessExpression(methodCall.Object, GetSourceElementType(methodCall), context, provider);
        }
        // For static extension methods, process the first argument as the source
        else if (methodCall.Arguments.Count > 0 && methodCall.Arguments[0] is Expression source)
        {
            ProcessExpression(source, GetSourceElementType(methodCall), context, provider);
        }

        // Handle graph-specific methods
        if (methodCall.Method.DeclaringType == typeof(GraphQueryExtensions))
        {
            ProcessGraphExtensionMethod(methodCall, elementType, context, provider);
        }
        else if (methodCall.Method.DeclaringType?.FullName == "Cvoya.Graph.Model.GraphQueryableExtensions")
        {
            ProcessGraphQueryableExtensionMethod(methodCall, elementType, context, provider);
        }
        else if (methodCall.Method.DeclaringType == typeof(Queryable))
        {
            ProcessStandardLinqMethod(methodCall, elementType, context, provider);
        }
        else if (IsGraphTraversalMethod(methodCall))
        {
            ProcessGraphTraversalMethod(methodCall, elementType, context, provider);
        }
        else if (methodCall.Method.DeclaringType?.IsGenericType == true &&
                 methodCall.Method.DeclaringType.GetGenericTypeDefinition() == typeof(IGraphQueryable<>))
        {
            ProcessGraphQueryableMethod(methodCall, elementType, context, provider);
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

    private void ProcessGraphQueryableExtensionMethod(
        MethodCallExpression methodCall,
        Type elementType,
        CypherBuildContext context,
        Neo4jGraphProvider provider)
    {

        switch (methodCall.Method.Name)
        {
            case "TraversePath":
                ProcessTraversePath(methodCall, elementType, context, provider);
                break;
            case "Traverse":
                ProcessTraverse(methodCall, elementType, context, provider);
                break;
            default:
                throw new NotSupportedException($"Graph queryable method {methodCall.Method.Name} is not supported");
        }
    }

    private void ProcessGraphQueryableMethod(
        MethodCallExpression methodCall,
        Type elementType,
        CypherBuildContext context,
        Neo4jGraphProvider provider)
    {
        switch (methodCall.Method.Name)
        {
            case "WithDepth":
                ProcessWithDepth(methodCall, elementType, context, provider);
                break;
            default:
                throw new NotSupportedException($"Graph queryable method {methodCall.Method.Name} is not supported");
        }
    }

    private void ProcessWithDepth(
        MethodCallExpression methodCall,
        Type elementType,
        CypherBuildContext context,
        Neo4jGraphProvider provider)
    {
        // WithDepth is used to set traversal depth parameters for graph queries
        // The depth parameter should be stored in the context for use in path generation

        if (methodCall.Arguments.Count == 1) // WithDepth(int depth) - single argument with depth value
        {
            var depthExpression = methodCall.Arguments[0];
            if (depthExpression is ConstantExpression constantDepth)
            {
                var depth = (int)constantDepth.Value!;
                context.TraversalDepth = depth;
            }
        }
        else if (methodCall.Arguments.Count == 2) // WithDepth(int minDepth, int maxDepth) - two arguments
        {
            var minDepthExpression = methodCall.Arguments[0];
            var maxDepthExpression = methodCall.Arguments[1];

            if (minDepthExpression is ConstantExpression constantMinDepth &&
                maxDepthExpression is ConstantExpression constantMaxDepth)
            {
                var minDepth = (int)constantMinDepth.Value!;
                var maxDepth = (int)constantMaxDepth.Value!;
                context.MinTraversalDepth = minDepth;
                context.MaxTraversalDepth = maxDepth;
            }
        }

        // Now process the source expression (typically TraversePath) with the depth parameters set
        if (methodCall.Object != null)
        {
            ProcessExpression(methodCall.Object, elementType, context, provider);
        }
    }

    private void ProcessTraversePath(
        MethodCallExpression methodCall,
        Type elementType,
        CypherBuildContext context,
        Neo4jGraphProvider provider)
    {
        // Extract the generic arguments: TSource, TRelationship, TTarget
        var genericArgs = methodCall.Method.GetGenericArguments();
        var sourceType = genericArgs[0]; // TSource
        var relationshipType = genericArgs[1]; // TRelationship
        var targetType = genericArgs[2]; // TTarget

        // Get labels
        var relationshipLabel = Neo4jTypeManager.GetLabel(relationshipType);
        var targetLabel = Neo4jTypeManager.GetLabel(targetType);
        var sourceLabel = Neo4jTypeManager.GetLabel(sourceType);

        // Get current source alias and create new aliases
        var sourceAlias = context.CurrentAlias;
        var relationshipAlias = context.GetNextAlias("r");
        var targetAlias = context.GetNextAlias("t");

        // For TraversePath, we want to replace the existing match with a complete traversal pattern
        // Clear any existing MATCH content and build the complete pattern
        context.Match.Clear();

        // Determine the relationship pattern based on traversal depth
        string relationshipPattern;
        if (context.TraversalDepth.HasValue)
        {
            // Single depth specified
            var depth = context.TraversalDepth.Value;
            if (depth == 1)
            {
                relationshipPattern = $"[{relationshipAlias}:{relationshipLabel}]";
            }
            else
            {
                relationshipPattern = $"[{relationshipAlias}:{relationshipLabel}*{depth}]";
            }
        }
        else if (context.MinTraversalDepth.HasValue && context.MaxTraversalDepth.HasValue)
        {
            // Depth range specified
            relationshipPattern = $"[{relationshipAlias}:{relationshipLabel}*{context.MinTraversalDepth}..{context.MaxTraversalDepth}]";
        }
        else
        {
            // Default to single hop
            relationshipPattern = $"[{relationshipAlias}:{relationshipLabel}]";
        }

        var matchClause = $"({sourceAlias}:{sourceLabel})-{relationshipPattern}->({targetAlias}:{targetLabel})";
        context.Match.Append(matchClause);

        // Update the current alias to the target for further operations
        context.CurrentAlias = targetAlias;

        // The return should be constructed as a TraversalPath object with Source, Relationship, Target
        context.Return = $"{{ Source: {sourceAlias}, Relationship: {relationshipAlias}, Target: {targetAlias} }}";

        // Mark as path result for proper result handling
        context.IsPathResult = true;
    }

    private void ProcessTraverse(
        MethodCallExpression methodCall,
        Type elementType,
        CypherBuildContext context,
        Neo4jGraphProvider provider)
    {
        // For now, just handle basic traversal - can be expanded later
        throw new NotSupportedException("Traverse method is not yet implemented");
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
        var relLabel = Neo4jTypeManager.GetLabel(relationshipType);
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

            case "Average":
                if (methodCall.Arguments.Count > 1)
                {
                    var avgSelector = ExtractLambdaFromQuote(methodCall.Arguments[1]);
                    if (avgSelector != null)
                    {
                        var property = BuildCypherExpression(avgSelector.Body, context.CurrentAlias);
                        context.Return = $"AVG({property})";
                    }
                }
                else
                {
                    // No selector, average the whole entity (doesn't make sense, but handle gracefully)
                    throw new NotSupportedException("Average requires a property selector");
                }
                context.IsScalarResult = true;
                break;

            case "Sum":
                if (methodCall.Arguments.Count > 1)
                {
                    var sumSelector = ExtractLambdaFromQuote(methodCall.Arguments[1]);
                    if (sumSelector != null)
                    {
                        var property = BuildCypherExpression(sumSelector.Body, context.CurrentAlias);
                        context.Return = $"SUM({property})";
                    }
                }
                else
                {
                    throw new NotSupportedException("Sum requires a property selector");
                }
                context.IsScalarResult = true;
                break;

            case "Min":
                if (methodCall.Arguments.Count > 1)
                {
                    var minSelector = ExtractLambdaFromQuote(methodCall.Arguments[1]);
                    if (minSelector != null)
                    {
                        var property = BuildCypherExpression(minSelector.Body, context.CurrentAlias);
                        context.Return = $"MIN({property})";
                    }
                }
                else
                {
                    throw new NotSupportedException("Min requires a property selector");
                }
                context.IsScalarResult = true;
                break;

            case "Max":
                if (methodCall.Arguments.Count > 1)
                {
                    var maxSelector = ExtractLambdaFromQuote(methodCall.Arguments[1]);
                    if (maxSelector != null)
                    {
                        var property = BuildCypherExpression(maxSelector.Body, context.CurrentAlias);
                        context.Return = $"MAX({property})";
                    }
                }
                else
                {
                    throw new NotSupportedException("Max requires a property selector");
                }
                context.IsScalarResult = true;
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
        var relLabel = Neo4jTypeManager.GetLabel(relationshipType);
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
        // Extract traversal parameters - similar to ProcessTraversalTo but without targetFilter
        var source = methodCall.Arguments[0];
        var direction = (TraversalDirection)((ConstantExpression)methodCall.Arguments[1]).Value!;
        var nodeFilter = ExtractLambdaFromConstant(methodCall.Arguments[2]);
        var relationshipFilter = ExtractLambdaFromConstant(methodCall.Arguments[3]);
        var minDepth = (int)((ConstantExpression)methodCall.Arguments[4]).Value!;
        var maxDepth = (int)((ConstantExpression)methodCall.Arguments[5]).Value!;

        var genericArgs = methodCall.Method.GetGenericArguments();
        var relationshipType = genericArgs[1]; // Only TSourceNode and TRel for relationship queries
        // No targetType for relationship queries - we return relationships directly

        var sourceAlias = context.CurrentAlias;
        var relAlias = context.GetNextAlias("r");
        var targetAlias = context.GetNextAlias("n"); // Still need a target for the pattern

        // Build traversal pattern
        var relLabel = Neo4jTypeManager.GetLabel(relationshipType);
        // For relationship queries, we can traverse to any node type
        // We'll use a generic pattern without specific target labels

        if (context.Match.Length > 0) context.Match.AppendLine();
        context.Match.Append($"MATCH ({sourceAlias})");

        // For relationship queries, use single relationships not variable length patterns
        var relPattern = minDepth == 1 && maxDepth == 1
            ? $"[{relAlias}:{relLabel}]"
            : $"[{relAlias}:{relLabel}*{minDepth}..{maxDepth}]";

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
        context.Match.Append($"({targetAlias})"); // No specific label for target

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
            var oldAlias = context.CurrentAlias;
            context.CurrentAlias = relAlias;
            if (context.Where.Length > 0) context.Where.Append(" AND ");

            // For single relationships, apply filter directly; for variable length, use ALL
            if (minDepth == 1 && maxDepth == 1)
            {
                ProcessWhereClause(relationshipFilter.Body, context);
            }
            else
            {
                context.Where.Append($"ALL(r IN {relAlias} WHERE ");
                context.CurrentAlias = "r";
                ProcessWhereClause(relationshipFilter.Body, context);
                context.Where.Append(")");
            }

            context.CurrentAlias = oldAlias;
        }

        // Return the relationship alias instead of target alias
        context.CurrentAlias = relAlias;
        context.Return = relAlias;
    }

    private void ProcessTraversalPaths(MethodCallExpression methodCall, CypherBuildContext context)
    {
        // Extract traversal parameters - similar to ProcessTraversalTo
        var source = methodCall.Arguments[0];
        var direction = (TraversalDirection)((ConstantExpression)methodCall.Arguments[1]).Value!;
        var nodeFilter = ExtractLambdaFromConstant(methodCall.Arguments[2]);
        var relationshipFilter = ExtractLambdaFromConstant(methodCall.Arguments[3]);
        var minDepth = (int)((ConstantExpression)methodCall.Arguments[4]).Value!;
        var maxDepth = (int)((ConstantExpression)methodCall.Arguments[5]).Value!;

        var genericArgs = methodCall.Method.GetGenericArguments();
        var relationshipType = genericArgs[1];

        var sourceAlias = context.CurrentAlias;
        var relAlias = context.GetNextAlias("r");
        var targetAlias = context.GetNextAlias("n");

        // Build traversal pattern - similar to ProcessTraversalTo but structured for path results
        var relLabel = Neo4jTypeManager.GetLabel(relationshipType);

        if (context.Match.Length > 0) context.Match.AppendLine();
        context.Match.Append($"MATCH ({sourceAlias})");

        // For path queries, we typically want single-hop relationships to match the expected test structure
        var relPattern = minDepth == 1 && maxDepth == 1
            ? $"[{relAlias}:{relLabel}]"
            : $"[{relAlias}:{relLabel}*{minDepth}..{maxDepth}]";

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
        context.Match.Append($"({targetAlias})");

        // Apply filters - similar to ProcessTraversalTo
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
            var oldAlias = context.CurrentAlias;
            context.CurrentAlias = relAlias;
            if (context.Where.Length > 0) context.Where.Append(" AND ");
            ProcessWhereClause(relationshipFilter.Body, context);
            context.CurrentAlias = oldAlias;
        }

        // Return structured path data - source, relationship, target as separate columns
        context.Return = $"{sourceAlias}, {relAlias}, {targetAlias}";
        context.IsPathResult = true;
    }

    private string BuildCypherQuery(CypherBuildContext context)
    {
        var query = new StringBuilder();

        // MATCH clause - only add if we have content
        if (context.Match.Length > 0)
        {
            query.Append("MATCH ").AppendLine(context.Match.ToString());
        }
        else
        {
            // If no explicit MATCH clause was built, create a basic one from the root type
            if (context.RootType != null)
            {
                var label = Neo4jTypeManager.GetLabel(context.RootType);

                // Generate different patterns based on query root type
                if (context.QueryRootType == GraphQueryContext.QueryRootType.Relationship)
                {
                    // For relationship queries, generate: MATCH ()-[r:type]->()
                    query.Append("MATCH ").AppendLine($"()-[{context.CurrentAlias}:{label}]->()");
                }
                else
                {
                    // For node queries (default), generate: MATCH (n:type)
                    query.Append("MATCH ").AppendLine($"({context.CurrentAlias}:{label})");
                }
            }
            else
            {
                throw new InvalidOperationException("No MATCH clause was built and no root type is available");
            }
        }

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

    private void ProcessWhereClause(Expression expression, CypherBuildContext context)
    {
        // Convert expression to Cypher WHERE clause syntax
        var cypherCondition = BuildCypherExpression(expression, context.CurrentAlias);
        context.Where.Append(cypherCondition);
    }

    private void ProcessSelectClause(LambdaExpression selector, CypherBuildContext context)
    {
        // For simple selectors like x => x, just return the current alias
        if (selector.Body is ParameterExpression)
        {
            // Identity selector, keep current return
            return;
        }

        // For projections, build the Cypher expression
        var projection = BuildCypherExpression(selector.Body, context.CurrentAlias);
        context.Return = projection;
    }

    private void ProcessOrderByClause(LambdaExpression selector, bool descending, CypherBuildContext context)
    {
        var orderExpression = BuildCypherExpression(selector.Body, context.CurrentAlias);

        if (context.OrderBy.Length > 0)
            context.OrderBy.Append(", ");

        context.OrderBy.Append(orderExpression);
        if (descending)
            context.OrderBy.Append(" DESC");
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

    public static object? ExecuteQuery(string cypher, IDictionary<string, object> parameters, Type elementType, Neo4jGraphProvider provider, CypherBuildContext? context = null, IGraphTransaction? transaction = null)
    {
        return ExecuteQueryAsync(cypher, parameters, elementType, provider, context, transaction).GetAwaiter().GetResult();
    }

    public static async Task<object?> ExecuteQueryAsync(string cypher, IDictionary<string, object> parameters, Type elementType, Neo4jGraphProvider provider, CypherBuildContext? context = null, IGraphTransaction? transaction = null)
    {
        var (_, tx) = await provider.GetOrCreateTransaction(transaction);
        var cursor = await tx.RunAsync(cypher, parameters);

        // Handle different result types
        if (elementType.IsGenericType && elementType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            elementType = elementType.GetGenericArguments()[0];
        }

        var listType = typeof(List<>).MakeGenericType(elementType);
        var items = (System.Collections.IList)Activator.CreateInstance(listType)!;

        // Check if this is a path result based on context
        bool isPathResult = context?.IsPathResult == true;

        await foreach (var record in cursor)
        {
            object? item = null;

            if (isPathResult)
            {
                // Handle path results - these typically have multiple columns or a path object
                item = ConvertPathResult(record, elementType);
            }
            else
            {
                // Handle regular entity results
                if (record.Keys.Count == 1)
                {
                    var key = record.Keys.First();
                    var value = record[key];

                    if (value is global::Neo4j.Driver.INode node)
                    {
                        // Convert Neo4j node to the target entity type
                        item = ConvertNodeToEntity(node, elementType);
                    }
                    else if (value is global::Neo4j.Driver.IRelationship rel)
                    {
                        // Convert Neo4j relationship to the target entity type
                        item = ConvertRelationshipToEntity(rel, elementType);
                    }
                    else
                    {
                        // Direct value (e.g., for aggregations or simple projections)
                        item = ConvertValue(value, elementType);
                    }
                }
                else
                {
                    // Multiple columns - might be a projection or complex result
                    // For now, assume it's a node if the first key matches a pattern
                    var nodeKey = record.Keys.FirstOrDefault(k => k.StartsWith('n'));
                    if (nodeKey != null && record[nodeKey] is global::Neo4j.Driver.INode node)
                    {
                        item = ConvertNodeToEntity(node, elementType);
                    }
                }
            }

            if (item != null)
            {
                items.Add(item);
            }
        }

        // If a single result is expected, return the first item or null
        if (context != null && (context.IsSingleResult || context.IsScalarResult))
        {
            if (items.Count == 0) return null;
            return items[0];
        }

        return items;
    }

    private static object? ConvertNodeToEntity(global::Neo4j.Driver.INode node, Type entityType)
    {
        // Special handling for TraversalPath<,,> records
        if (entityType.IsGenericType && entityType.GetGenericTypeDefinition().FullName == "Cvoya.Graph.Model.TraversalPath`3")
        {
            // Try to get the three properties in order: Source, Relationship, Target
            var props = entityType.GetProperties();
            var sourceProp = props.FirstOrDefault(p => p.Name == "Source");
            var relProp = props.FirstOrDefault(p => p.Name == "Relationship");
            var targetProp = props.FirstOrDefault(p => p.Name == "Target");
            if (sourceProp != null && relProp != null && targetProp != null)
            {
                // Try to get the types
                var genericArgs = entityType.GetGenericArguments();
                var sourceType = genericArgs[0];
                var relType = genericArgs[1];
                var targetType = genericArgs[2];
                // Create dummy instances for now (or you can throw if you need real data)
                var source = Activator.CreateInstance(sourceType);
                var rel = Activator.CreateInstance(relType);
                var target = Activator.CreateInstance(targetType);
                // Use the record constructor
                var ctor = entityType.GetConstructor(new[] { sourceType, relType, targetType });
                if (ctor != null)
                {
                    return ctor.Invoke(new[] { source, rel, target });
                }
            }
        }
        // Regular handling
        var entity = Activator.CreateInstance(entityType);
        if (entity == null) return null;

        // Map properties from the Neo4j node to the entity
        var properties = entityType.GetProperties();
        foreach (var property in properties)
        {
            if (node.Properties.TryGetValue(property.Name, out var value))
            {
                var convertedValue = ConvertValue(value, property.PropertyType);
                property.SetValue(entity, convertedValue);
            }
        }

        return entity;
    }

    private static object? ConvertRelationshipToEntity(global::Neo4j.Driver.IRelationship relationship, Type entityType)
    {
        try
        {
            // Create an instance of the entity type
            var entity = Activator.CreateInstance(entityType);
            if (entity == null) return null;

            // Map properties from the Neo4j relationship to the entity
            var properties = entityType.GetProperties();
            foreach (var property in properties)
            {
                if (relationship.Properties.TryGetValue(property.Name, out var value))
                {
                    var convertedValue = value.ConvertFromNeo4jValue(property.PropertyType);
                    property.SetValue(entity, convertedValue);
                }
            }

            // Set the relationship IDs from Neo4j relationship metadata
            if (entity is Cvoya.Graph.Model.IRelationship rel)
            {
                // Set the Id if it exists in properties, otherwise use ElementId
                if (relationship.Properties.ContainsKey("Id"))
                {
                    rel.Id = relationship.Properties["Id"].ToString() ?? relationship.ElementId;
                }
                else
                {
                    rel.Id = relationship.ElementId;
                }

                // Note: SourceId and TargetId should be set by the caller
                // since they need to come from the source and target nodes
            }

            return entity;
        }
        catch (Exception)
        {
            // Return null on any conversion errors
            return null;
        }
    }

    private static object? ConvertValue(object? value, Type targetType)
    {
        if (value == null) return null;

        // Handle nullable types
        if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            targetType = Nullable.GetUnderlyingType(targetType)!;
        }

        // Direct assignment if types match
        if (targetType.IsAssignableFrom(value.GetType()))
        {
            return value;
        }

        // Convert common types
        try
        {
            return Convert.ChangeType(value, targetType);
        }
        catch
        {
            return null;
        }
    }

    private static object? ConvertPathResult(global::Neo4j.Driver.IRecord record, Type elementType)
    {
        // Check if there's a direct path object in the result
        foreach (var key in record.Keys)
        {
            var value = record[key];
            if (value is global::Neo4j.Driver.IPath path)
            {
                return ConvertNeo4jPathToGraphPath(path, elementType);
            }
        }

        // Check for multiple columns that represent a path structure
        // Common patterns: source, relationship, target OR start, end, path OR n, r1, n2
        if (record.Keys.Count >= 3)
        {
            // Look for source/start node - often the first node variable
            var sourceKey = record.Keys.FirstOrDefault(k =>
                k.Equals("source", StringComparison.OrdinalIgnoreCase) ||
                k.Equals("start", StringComparison.OrdinalIgnoreCase) ||
                k.Equals("from", StringComparison.OrdinalIgnoreCase) ||
                k.Equals("n", StringComparison.OrdinalIgnoreCase) ||  // First node is often just "n"
                (k.StartsWith('n') && !k.Contains('2') && !k.Contains('3'))); // n, n0, n1 but not n2, n3

            // Look for target/end node - often the second or later node variable
            var targetKey = record.Keys.FirstOrDefault(k =>
                k != sourceKey && ( // Make sure it's not the same as source
                k.Equals("target", StringComparison.OrdinalIgnoreCase) ||
                k.Equals("end", StringComparison.OrdinalIgnoreCase) ||
                k.Equals("to", StringComparison.OrdinalIgnoreCase) ||
                k.Equals("n2", StringComparison.OrdinalIgnoreCase) ||
                (k.StartsWith('n') && (k.Contains('2') || k.Contains('3') || k.Contains('1'))))); // n1, n2, n3

            // Look for relationship
            var relationshipKey = record.Keys.FirstOrDefault(k =>
                k.Equals("relationship", StringComparison.OrdinalIgnoreCase) ||
                k.Equals("rel", StringComparison.OrdinalIgnoreCase) ||
                k.StartsWith('r'));

            if (sourceKey != null && targetKey != null)
            {
                return ConvertPathComponentsToGraphPath(record, sourceKey, targetKey, relationshipKey, elementType);
            }
        }

        // Fallback: treat as regular single entity if no path structure detected
        if (record.Keys.Count == 1)
        {
            var key = record.Keys.First();
            var value = record[key];

            if (value is global::Neo4j.Driver.INode node)
            {
                return ConvertNodeToEntity(node, elementType);
            }
            else if (value is global::Neo4j.Driver.IRelationship rel)
            {
                return ConvertRelationshipToEntity(rel, elementType);
            }
        }

        return null;
    }

    private static object? ConvertNeo4jPathToGraphPath(global::Neo4j.Driver.IPath neo4jPath, Type elementType)
    {
        // TODO: Implement conversion from Neo4j IPath to our GraphPath types
        // For now, return the first node if the element type is a node type
        if (neo4jPath.Nodes.Any())
        {
            var firstNode = neo4jPath.Nodes.First();
            return ConvertNodeToEntity(firstNode, elementType);
        }

        return null;
    }

    private static object? ConvertPathComponentsToGraphPath(global::Neo4j.Driver.IRecord record, string sourceKey, string targetKey, string? relationshipKey, Type elementType)
    {
        try
        {
            // Check if we can extract the generic arguments from the element type
            if (!elementType.IsGenericType || elementType.GetGenericTypeDefinition() != typeof(IGraphPath<,,>))
            {
                // Fallback to source node if not a path type
                if (record[sourceKey] is global::Neo4j.Driver.INode sourceNode)
                {
                    return ConvertNodeToEntity(sourceNode, elementType);
                }
                return null;
            }

            var genericArgs = elementType.GetGenericArguments();
            var sourceType = genericArgs[0];  // TSource
            var relationshipType = genericArgs[1];  // TRel
            var targetType = genericArgs[2];  // TTarget

            // Convert the source node
            if (!(record[sourceKey] is global::Neo4j.Driver.INode sourceNodeValue))
            {
                return null;
            }
            var sourceEntity = ConvertNodeToEntity(sourceNodeValue, sourceType);
            if (sourceEntity == null) return null;

            // Convert the target node
            if (!(record[targetKey] is global::Neo4j.Driver.INode targetNodeValue))
            {
                return null;
            }
            var targetEntity = ConvertNodeToEntity(targetNodeValue, targetType);
            if (targetEntity == null) return null;

            // Convert the relationship (if present)
            object? relationshipEntity = null;
            if (!string.IsNullOrEmpty(relationshipKey) && record[relationshipKey] is global::Neo4j.Driver.IRelationship relationshipValue)
            {
                relationshipEntity = ConvertRelationshipToEntity(relationshipValue, relationshipType);
            }

            // If no relationship, create a basic one
            if (relationshipEntity == null)
            {
                relationshipEntity = Activator.CreateInstance(relationshipType);
                if (relationshipEntity is Cvoya.Graph.Model.IRelationship rel)
                {
                    rel.SourceId = (sourceEntity as Cvoya.Graph.Model.IEntity)?.Id ?? "";
                    rel.TargetId = (targetEntity as Cvoya.Graph.Model.IEntity)?.Id ?? "";
                }
            }

            // Create SimpleGraphPath instance using reflection
            var simpleGraphPathType = typeof(SimpleGraphPath<,,>).MakeGenericType(sourceType, relationshipType, targetType);
            var pathInstance = Activator.CreateInstance(simpleGraphPathType, sourceEntity, relationshipEntity, targetEntity);

            return pathInstance;
        }
        catch (Exception ex)
        {
            throw new GraphException(ex.Message, ex);
        }
    }
}
