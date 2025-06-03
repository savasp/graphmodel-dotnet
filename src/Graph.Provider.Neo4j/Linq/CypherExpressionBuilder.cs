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

    public static string BuildCypherExpression(Expression expr, string varName, CypherBuildContext? context = null)
    {
        switch (expr)
        {
            case ParameterExpression pe:
                // For parameter expressions, return the variable name
                // This handles cases like 'g' in GroupBy expressions
                return varName;

            case MemberExpression me:
                // Handle DateTime static properties first
                if (DateTimeExpressionHandler.TryHandleDateTimeExpression(me, out string cypherExpression))
                {
                    return cypherExpression;
                }

                // Handle TraversalPath property access for WHERE clauses
                if (me.Expression is MemberExpression parentMe &&
                    parentMe.Expression is ParameterExpression paramExpr &&
                    paramExpr.Type.IsGenericType &&
                    paramExpr.Type.GetGenericTypeDefinition().FullName == "Cvoya.Graph.Model.TraversalPath`3")
                {
                    // This is accessing properties like path.Target.Age or path.Source.Name
                    var pathProperty = parentMe.Member.Name; // "Target", "Source", "Relationship"
                    var entityProperty = me.Member.Name; // "Age", "Name", etc.

                    return pathProperty switch
                    {
                        "Source" => $"n.{entityProperty}",      // Source maps to 'n'
                        "Target" => $"t2.{entityProperty}",     // Target maps to 't2'
                        "Relationship" => $"r1.{entityProperty}", // Relationship maps to 'r1'
                        _ => $"{varName}.{me.Member.Name}"
                    };
                }

                // Handle closure access first
                if (IsClosureAccess(me))
                {
                    // Evaluate the closure value
                    var value = EvaluateMemberExpression(me);
                    // If we have a context, use it to store parameters
                    if (context != null)
                    {
                        var paramName = AddParameter(value, context);
                        return $"${paramName}";
                    }
                    else
                    {
                        // Fallback: inline the value directly when no context is available
                        return FormatValueForCypher(value);
                    }
                }

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
                else if (me.Expression is MemberExpression pm)
                {
                    // Nested property access (e.g., p.Address.City)
                    var parent = BuildCypherExpression(pm, varName);
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
                // Handle DateTime method calls first
                if (DateTimeExpressionHandler.TryHandleDateTimeMethod(mce, out string cypherExpr))
                {
                    return cypherExpr;
                }

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
                else if (mce.Method.DeclaringType == typeof(Enumerable))
                {
                    // LINQ collection methods
                    switch (mce.Method.Name)
                    {
                        case "First" when mce.Arguments.Count >= 1:
                            var collection = BuildCypherExpression(mce.Arguments[0], varName);
                            return $"head({collection})";
                        case "Last" when mce.Arguments.Count >= 1:
                            var collection2 = BuildCypherExpression(mce.Arguments[0], varName);
                            return $"last({collection2})";
                        case "Count" when mce.Arguments.Count >= 1:
                            var collection3 = BuildCypherExpression(mce.Arguments[0], varName);
                            return $"size({collection3})";
                        case "Take" when mce.Arguments.Count >= 2:
                            var collection4 = BuildCypherExpression(mce.Arguments[0], varName);
                            var count = BuildCypherExpression(mce.Arguments[1], varName);
                            return $"head({collection4}, {count})";
                        case "Skip" when mce.Arguments.Count >= 2:
                            var collection5 = BuildCypherExpression(mce.Arguments[0], varName);
                            var count2 = BuildCypherExpression(mce.Arguments[1], varName);
                            return $"tail({collection5}, {count2})";
                        case "Reverse" when mce.Arguments.Count >= 1:
                            var collection6 = BuildCypherExpression(mce.Arguments[0], varName);
                            return $"reverse({collection6})";
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
                        case "TrimStart":
                            return $"ltrim({target})";
                        case "TrimEnd":
                            return $"rtrim({target})";
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
                        case "Split" when mce.Arguments.Count >= 1:
                            var delimiter = BuildCypherExpression(mce.Arguments[0], varName);
                            return $"split({target}, {delimiter})";
                        case "ToString":
                            return $"toString({target})";
                    }
                }
                else if (mce.Method.DeclaringType == typeof(Math))
                {
                    // Math functions with improved handling
                    switch (mce.Method.Name)
                    {
                        case "Abs":
                            var val = BuildCypherExpression(mce.Arguments[0], varName);
                            return $"abs({val})";
                        case "Acos":
                            var val2 = BuildCypherExpression(mce.Arguments[0], varName);
                            return $"acos({val2})";
                        case "Asin":
                            var val3 = BuildCypherExpression(mce.Arguments[0], varName);
                            return $"asin({val3})";
                        case "Atan":
                            var val4 = BuildCypherExpression(mce.Arguments[0], varName);
                            return $"atan({val4})";
                        case "Atan2":
                            var y = BuildCypherExpression(mce.Arguments[0], varName);
                            var x = BuildCypherExpression(mce.Arguments[1], varName);
                            return $"atan2({y}, {x})";
                        case "Ceiling":
                            var val5 = BuildCypherExpression(mce.Arguments[0], varName);
                            return $"ceil({val5})";
                        case "Cos":
                            var val6 = BuildCypherExpression(mce.Arguments[0], varName);
                            return $"cos({val6})";
                        case "Exp":
                            var val7 = BuildCypherExpression(mce.Arguments[0], varName);
                            return $"exp({val7})";
                        case "Floor":
                            var val8 = BuildCypherExpression(mce.Arguments[0], varName);
                            return $"floor({val8})";
                        case "Log" when mce.Arguments.Count == 1:
                            var val9 = BuildCypherExpression(mce.Arguments[0], varName);
                            return $"log({val9})";
                        case "Log10":
                            var val10 = BuildCypherExpression(mce.Arguments[0], varName);
                            return $"log10({val10})";
                        case "Max" when mce.Arguments.Count == 2:
                            var v1 = BuildCypherExpression(mce.Arguments[0], varName);
                            var v2 = BuildCypherExpression(mce.Arguments[1], varName);
                            return $"CASE WHEN {v1} > {v2} THEN {v1} ELSE {v2} END";
                        case "Min" when mce.Arguments.Count == 2:
                            var v3 = BuildCypherExpression(mce.Arguments[0], varName);
                            var v4 = BuildCypherExpression(mce.Arguments[1], varName);
                            return $"CASE WHEN {v3} < {v4} THEN {v3} ELSE {v4} END";
                        case "Pow":
                            var base1 = BuildCypherExpression(mce.Arguments[0], varName);
                            var exp1 = BuildCypherExpression(mce.Arguments[1], varName);
                            return $"({base1} ^ {exp1})";
                        case "Round" when mce.Arguments.Count == 1:
                            var val11 = BuildCypherExpression(mce.Arguments[0], varName);
                            return $"round({val11})";
                        case "Round" when mce.Arguments.Count == 2:
                            var val12 = BuildCypherExpression(mce.Arguments[0], varName);
                            var precision = BuildCypherExpression(mce.Arguments[1], varName);
                            return $"round({val12}, {precision})";
                        case "Sign":
                            var val13 = BuildCypherExpression(mce.Arguments[0], varName);
                            return $"sign({val13})";
                        case "Sin":
                            var val14 = BuildCypherExpression(mce.Arguments[0], varName);
                            return $"sin({val14})";
                        case "Sqrt":
                            var val15 = BuildCypherExpression(mce.Arguments[0], varName);
                            return $"sqrt({val15})";
                        case "Tan":
                            var val16 = BuildCypherExpression(mce.Arguments[0], varName);
                            return $"tan({val16})";
                    }
                }
                else if (mce.Method.DeclaringType == typeof(DateTime))
                {
                    // DateTime functions - use native Neo4j duration functions
                    var target = BuildCypherExpression(mce.Object!, varName);
                    switch (mce.Method.Name)
                    {
                        case "AddYears":
                            var years = BuildCypherExpression(mce.Arguments[0], varName);
                            return $"datetime({target} + duration({{years: {years}}}))";
                        case "AddMonths":
                            var months = BuildCypherExpression(mce.Arguments[0], varName);
                            return $"datetime({target} + duration({{months: {months}}}))";
                        case "AddDays":
                            var days = BuildCypherExpression(mce.Arguments[0], varName);
                            return $"datetime({target} + duration({{days: {days}}}))";
                        case "AddHours":
                            var hours = BuildCypherExpression(mce.Arguments[0], varName);
                            return $"datetime({target} + duration({{hours: {hours}}}))";
                        case "AddMinutes":
                            var minutes = BuildCypherExpression(mce.Arguments[0], varName);
                            return $"datetime({target} + duration({{minutes: {minutes}}}))";
                        case "AddSeconds":
                            var seconds = BuildCypherExpression(mce.Arguments[0], varName);
                            return $"datetime({target} + duration({{seconds: {seconds}}}))";
                        case "AddMilliseconds":
                            var milliseconds = BuildCypherExpression(mce.Arguments[0], varName);
                            return $"datetime({target} + duration({{milliseconds: {milliseconds}}}))";
                        case "ToString":
                            // Format datetime as string - use default ISO format
                            return $"toString({target})";
                    }
                }
                else if (mce.Method.Name == "ToString" && mce.Object != null)
                {
                    // Generic ToString() on any object
                    var obj = BuildCypherExpression(mce.Object, varName);
                    return $"toString({obj})";
                }
                throw new NotSupportedException($"Method {mce.Method.Name} on type {mce.Method.DeclaringType?.Name} is not supported in projections");

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

            case NewExpression ne:
                // Handle anonymous type creation: new { Name = p.FirstName, Age = p.Age }
                // Convert to Cypher map: { Name: n.FirstName, Age: n.Age }
                if (ne.Members != null && ne.Arguments.Count == ne.Members.Count)
                {
                    var properties = new List<string>();
                    for (int i = 0; i < ne.Members.Count; i++)
                    {
                        var memberName = ne.Members[i].Name;
                        var valueExpression = BuildCypherExpression(ne.Arguments[i], varName);
                        properties.Add($"{memberName}: {valueExpression}");
                    }
                    return $"{{ {string.Join(", ", properties)} }}";
                }
                throw new NotSupportedException($"NewExpression without member initialization is not supported");

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

        // Preprocess to detect Last operations
        context.IsLastOperation = DetectLastOperation(expression);
        Console.WriteLine($"BuildGraphQuery - Detected Last operation: {context.IsLastOperation}");

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

        // If we have a client-side projection, we need to use the source entity type for Cypher
        // and let the projection be applied on the client side
        var actualElementType = elementType;
        if (context.ClientSideProjection != null)
        {
            // Find the source entity type by walking up the expression tree
            var sourceEntityType = FindSourceEntityType(expression);
            if (sourceEntityType != null)
            {
                actualElementType = sourceEntityType;
                context.RootType = actualElementType;
            }
        }

        // Build the final Cypher query
        var cypher = builder.BuildCypherQuery(context);

        return (cypher, context.Parameters, context);
    }

    private static bool DetectLastOperation(Expression expression)
    {
        if (expression is MethodCallExpression methodCall)
        {
            if (methodCall.Method.Name is "Last" or "LastOrDefault")
            {
                return true;
            }

            // Check the source expression recursively
            if (methodCall.Object != null)
            {
                return DetectLastOperation(methodCall.Object);
            }

            if (methodCall.Arguments.Count > 0)
            {
                return DetectLastOperation(methodCall.Arguments[0]);
            }
        }

        return false;
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

    private bool IsSelectAfterGroupBy(MethodCallExpression methodCall)
    {
        // Check if this Select follows a GroupBy
        var source = methodCall.Object ?? (methodCall.Arguments.Count > 0 ? methodCall.Arguments[0] : null);
        return source is MethodCallExpression sourceMethod && sourceMethod.Method.Name == "GroupBy";
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
            // TODO: Check if this is needed. ProcessTraverse was removed because it
            // was just throwing a NotImplementedException.
            //case "Traverse":
            //    ProcessTraverse(methodCall, elementType, context, provider);
            //    break;
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

        // Return separate columns instead of a map
        context.Return = $"{sourceAlias} AS source, {relationshipAlias} AS relationship, {targetAlias} AS target";

        // Mark as path result for proper result handling
        context.IsPathResult = true;
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

            case "GroupBy":
                ProcessGroupByClause(methodCall, context);
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

            case "All":
                if (methodCall.Arguments.Count > 1)
                {
                    var allPredicate = ExtractLambdaFromQuote(methodCall.Arguments[1]);
                    if (allPredicate != null)
                    {
                        // All(predicate) - check no items violate the predicate
                        var predicateCondition = BuildCypherExpression(allPredicate.Body, context.CurrentAlias, context);

                        // Add a negated predicate to WHERE to find violating items
                        if (context.Where.Length > 0) context.Where.Append(" AND ");
                        context.Where.Append($"NOT ({predicateCondition})");

                        // If we find any violating items, All() should return false
                        // So we return: COUNT(violating items) = 0
                        context.Return = $"COUNT({context.CurrentAlias}) = 0";
                    }
                }
                else
                {
                    // All() without predicate
                    context.Return = $"COUNT({context.CurrentAlias}) > 0";
                }
                context.IsBooleanQuery = true;
                break;

            case "Last":
            case "LastOrDefault":
                context.Limit = 1;
                context.IsLastOperation = true; // Flag to reverse ORDER BY direction

                if (methodCall.Arguments.Count > 1)
                {
                    var lastPredicate = ExtractLambdaFromQuote(methodCall.Arguments[1]);
                    if (lastPredicate != null)
                    {
                        if (context.Where.Length > 0) context.Where.Append(" AND ");
                        ProcessWhereClause(lastPredicate.Body, context);
                    }
                }
                context.IsSingleResult = true;
                Console.WriteLine($"Set IsSingleResult = true for {methodCall.Method.Name}");
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
                Console.WriteLine($"Set IsSingleResult = true for {methodCall.Method.Name}");
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

        // WITH clause for grouping and aggregations
        if (!string.IsNullOrEmpty(context.With))
        {
            Console.WriteLine($"BuildCypherQuery - Adding WITH clause: {context.With}");
            query.AppendLine(context.With);
        }

        // RETURN clause
        query.Append("RETURN ");
        if (context.IsDistinct)
        {
            query.Append("DISTINCT ");
        }
        query.AppendLine(context.Return ?? context.CurrentAlias);
        Console.WriteLine($"BuildCypherQuery - RETURN clause: {context.Return ?? context.CurrentAlias}");

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

        var finalQuery = query.ToString();
        Console.WriteLine($"BuildCypherQuery - Final query: {finalQuery}");
        return finalQuery;
    }

    private void ProcessWhereClause(Expression expression, CypherBuildContext context)
    {
        // Convert expression to Cypher WHERE clause syntax
        var cypherCondition = BuildCypherExpression(expression, context.CurrentAlias, context);
        context.Where.Append(cypherCondition);
    }

    private void ProcessSelectClause(LambdaExpression selector, CypherBuildContext context)
    {
        Console.WriteLine($"ProcessSelectClause - IsGroupByQuery: {context.IsGroupByQuery}");

        // For simple selectors like x => x, just return the current alias
        if (selector.Body is ParameterExpression)
        {
            // Identity selector, keep current return
            return;
        }

        // Special handling for GroupBy + Select patterns
        if (context.IsGroupByQuery)
        {
            // This is a Select after GroupBy - handle aggregations
            ProcessGroupBySelectClause(selector, context);
            return;
        }

        // Check if this is a complex projection that needs client-side processing
        if (RequiresClientSideProjection(selector.Body))
        {
            // For complex projections (like anonymous types), we'll return the raw nodes
            // and apply the projection on the client side during result processing
            context.ClientSideProjection = selector;
            // Keep the default return (the current alias) for now
            return;
        }

        // For simple projections, build the Cypher expression
        var projection = BuildCypherExpression(selector.Body, context.CurrentAlias, context);
        context.Return = projection;
    }

    private void ProcessTraversalPathGroupBySelect(LambdaExpression selector, CypherBuildContext context)
    {
        // Clear IsPathResult since we're projecting to a map result
        context.IsPathResult = false;

        if (selector.Body is NewExpression newExpr)
        {
            var returnProjections = new List<string>();

            Console.WriteLine($"ProcessTraversalPathGroupBySelect - Processing {newExpr.Arguments.Count} arguments");
            Console.WriteLine($"Members: {string.Join(", ", newExpr.Members?.Select(m => m.Name) ?? new[] { "null" })}");

            for (int i = 0; i < newExpr.Arguments.Count; i++)
            {
                var argument = newExpr.Arguments[i];
                var memberName = newExpr.Members?[i].Name ?? $"Field{i}";

                Console.WriteLine($"Processing member {memberName}, argument type: {argument.GetType().Name}");
                Console.WriteLine($"  Full expression: {argument}");

                bool handled = false;

                if (IsGroupingKeyAccess(argument))
                {
                    // g.Key -> the grouped node
                    returnProjections.Add($"{memberName}: {context.GroupByKey ?? "n"}");
                    handled = true;
                    Console.WriteLine($"  Handled as Key access");
                }
                else if (argument is MemberExpression me && me.Expression is MemberExpression keyAccess &&
                         keyAccess.Member.Name == "Key")
                {
                    // g.Key.SomeProperty -> access property of the grouped key
                    var propertyName = me.Member.Name;
                    returnProjections.Add($"{memberName}: {context.GroupByKey ?? "n"}.{propertyName}");
                    handled = true;
                    Console.WriteLine($"  Handled as Key.Property access");
                }
                else if (IsGroupingAggregation(argument, out var aggregationType))
                {
                    Console.WriteLine($"  Detected aggregation type: {aggregationType}");
                    switch (aggregationType)
                    {
                        case "Count":
                            returnProjections.Add($"{memberName}: size(paths)");
                            handled = true;
                            break;
                        case "Select":
                            // Handle g.Select(k => k.Target.FirstName)
                            if (argument is MethodCallExpression mce && mce.Arguments.Count > 0)
                            {
                                var selectLambda = ExtractLambdaFromQuote(mce.Arguments[0]);
                                if (selectLambda != null)
                                {
                                    // Build list comprehension for the selection
                                    var listExpr = BuildTraversalPathListComprehension(selectLambda, context);
                                    returnProjections.Add($"{memberName}: {listExpr}");
                                    handled = true;
                                }
                            }
                            break;
                    }
                }

                if (!handled && argument is MethodCallExpression methodCall)
                {
                    Console.WriteLine($"  Checking method call: {methodCall.Method.Name}");

                    // Check if this is group.Select(k => k.Target.FirstName).ToList()
                    if (methodCall.Method.Name == "ToList" && methodCall.Arguments.Count == 1)
                    {
                        // Get the Select call that's being converted to a list
                        if (methodCall.Arguments[0] is MethodCallExpression selectCall &&
                            selectCall.Method.Name == "Select")
                        {
                            Console.WriteLine($"    Found Select().ToList() pattern");
                            Console.WriteLine($"    Select method is static: {selectCall.Method.IsStatic}");
                            Console.WriteLine($"    Select arguments count: {selectCall.Arguments.Count}");

                            // For extension method Select, it's a static method with 2 args
                            LambdaExpression? selectLambda = null;
                            bool isGroupingSource = false;

                            if (selectCall.Method.IsStatic && selectCall.Arguments.Count == 2)
                            {
                                // Static extension method: Enumerable.Select(source, lambda)
                                var sourceType = selectCall.Arguments[0].Type;
                                isGroupingSource = sourceType.IsGenericType &&
                                                 sourceType.GetGenericTypeDefinition() == typeof(IGrouping<,>);
                                selectLambda = ExtractLambdaFromQuote(selectCall.Arguments[1]);
                            }
                            else if (!selectCall.Method.IsStatic && selectCall.Arguments.Count == 1)
                            {
                                // Instance method: source.Select(lambda)
                                var sourceType = selectCall.Object?.Type;
                                isGroupingSource = sourceType?.IsGenericType == true &&
                                                 sourceType.GetGenericTypeDefinition() == typeof(IGrouping<,>);
                                selectLambda = ExtractLambdaFromQuote(selectCall.Arguments[0]);
                            }

                            if (isGroupingSource && selectLambda != null)
                            {
                                Console.WriteLine($"    Confirmed grouping source and extracted lambda");
                                var listExpr = BuildTraversalPathListComprehension(selectLambda, context);
                                returnProjections.Add($"{memberName}: {listExpr}");
                                handled = true;
                                Console.WriteLine($"    Added projection: {memberName}: {listExpr}");
                            }
                            else
                            {
                                Console.WriteLine($"    Failed to process: isGroupingSource={isGroupingSource}, lambda={selectLambda}");
                            }
                        }
                    }
                }

                // Fallback: try to build a general Cypher expression
                if (!handled)
                {
                    Console.WriteLine($"  Member {memberName} was not handled by specific cases, trying fallback");
                    try
                    {
                        var cypherExpr = BuildCypherExpression(argument, context.GroupByKey ?? context.CurrentAlias, context);
                        returnProjections.Add($"{memberName}: {cypherExpr}");
                        Console.WriteLine($"  Fallback succeeded: {memberName}: {cypherExpr}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  Fallback failed: {ex.Message}");
                    }
                }
            }

            // Update the RETURN clause
            context.Return = $"{{ {string.Join(", ", returnProjections)} }}";
            Console.WriteLine($"Final RETURN clause: {context.Return}");
        }
    }

    private bool IsGroupParameter(Expression expression, LambdaExpression selector)
    {
        // Check if this expression is the group parameter (e.g., 'g' in g => new { ... })
        return expression is ParameterExpression param &&
               selector.Parameters.Contains(param) &&
               param.Type.IsGenericType &&
               param.Type.GetGenericTypeDefinition() == typeof(IGrouping<,>);
    }

    private string BuildTraversalPathListComprehension(LambdaExpression lambda, CypherBuildContext context)
    {
        // The lambda parameter (e.g., 'i') needs to be mapped to the list comprehension variable
        var lambdaParam = lambda.Parameters.FirstOrDefault()?.Name; // Handle null case safely
        if (string.IsNullOrEmpty(lambdaParam))
        {
            throw new InvalidOperationException("Lambda expression must have at least one parameter");
        }

        var listComprehensionVar = "p"; // This is what we use in the Cypher

        // Create a context for variable substitution
        var originalAlias = context.CurrentAlias;

        // Process the lambda body with variable mapping
        var projectionBody = BuildCypherExpressionWithVariableMapping(lambda.Body, lambdaParam, listComprehensionVar, context);

        // Restore original alias
        context.CurrentAlias = originalAlias;

        return $"[{listComprehensionVar} IN paths | {projectionBody}]";
    }

    private string BuildCypherExpressionWithVariableMapping(Expression expression, string fromParam, string toVar, CypherBuildContext context)
    {
        switch (expression)
        {
            case NewExpression newExpr:
                // Handle anonymous type creation: new { FriendName = i.Target.FirstName, ... }
                var members = new List<string>();

                for (int i = 0; i < newExpr.Arguments.Count; i++)
                {
                    var memberName = newExpr.Members?[i]?.Name ?? $"Item{i + 1}";
                    var memberExpr = BuildCypherExpressionWithVariableMapping(newExpr.Arguments[i], fromParam, toVar, context);
                    members.Add($"{memberName}: {memberExpr}");
                }

                return "{ " + string.Join(", ", members) + " }";

            case MemberExpression memberExpr:
                return BuildMemberExpressionWithMapping(memberExpr, fromParam, toVar, context);

            case BinaryExpression binaryExpr:
                var leftExpr = BuildCypherExpressionWithVariableMapping(binaryExpr.Left, fromParam, toVar, context);
                var rightExpr = BuildCypherExpressionWithVariableMapping(binaryExpr.Right, fromParam, toVar, context);
                var op = GetCypherOperator(binaryExpr.NodeType);
                return $"({leftExpr} {op} {rightExpr})";

            case ParameterExpression paramExpr when paramExpr.Name == fromParam:
                return toVar;

            case ConstantExpression constantExpr:
                // Handle constants directly
                return FormatValueForCypher(constantExpr.Value);

            default:
                // For other expressions, use the regular builder but with current context
                return BuildCypherExpression(expression, context.CurrentAlias, context);
        }
    }

    private string BuildMemberExpressionWithMapping(MemberExpression memberExpr, string fromParam, string toVar, CypherBuildContext context)
    {
        if (memberExpr.Expression is MemberExpression nestedMember)
        {
            // Handle cases like i.Target.FirstName or i.Relationship.Since
            if (nestedMember.Expression is ParameterExpression paramExpr && paramExpr.Name == fromParam)
            {
                // Convert i.Target.FirstName to p.target.FirstName
                // Convert i.Relationship.Since to p.relationship.Since
                var nestedProperty = nestedMember.Member.Name.ToLowerInvariant(); // Target -> target, Relationship -> relationship
                var finalProperty = memberExpr.Member.Name; // FirstName, Since, etc.
                return $"{toVar}.{nestedProperty}.{finalProperty}";
            }
        }
        else if (memberExpr.Expression is ParameterExpression directParam && directParam.Name == fromParam)
        {
            // Handle direct parameter access like i.SomeProperty
            var property = memberExpr.Member.Name.ToLowerInvariant();
            return $"{toVar}.{property}";
        }
        else if (memberExpr.Expression is BinaryExpression binaryExpr)
        {
            // Handle cases like (DateTime.UtcNow - i.Relationship.Since).Days
            if (memberExpr.Member.Name == "Days" &&
                binaryExpr.NodeType == ExpressionType.Subtract &&
                binaryExpr.Left.Type == typeof(DateTime))
            {
                var left = BuildCypherExpressionWithVariableMapping(binaryExpr.Left, fromParam, toVar, context);
                var right = BuildCypherExpressionWithVariableMapping(binaryExpr.Right, fromParam, toVar, context);

                // Use Neo4j's duration.between() function for datetime arithmetic
                return $"duration.between({right}, {left}).days";
            }
        }
        else if (memberExpr.Expression == null && memberExpr.Member.DeclaringType == typeof(DateTime))
        {
            // Handle DateTime.UtcNow
            return memberExpr.Member.Name switch
            {
                "UtcNow" => "datetime()",           // UTC time - correct
                "Now" => "localdatetime()",         // Local time - this was the bug!
                "Today" => "date()",                // Date only - this should also be added
                _ => BuildCypherExpression(memberExpr, context.CurrentAlias, context)
            };
        }

        // Fallback to regular expression building
        return BuildCypherExpression(memberExpr, context.CurrentAlias, context);
    }

    private string GetCypherOperator(ExpressionType nodeType)
    {
        return nodeType switch
        {
            ExpressionType.Add => "+",
            ExpressionType.Subtract => "-",
            ExpressionType.Multiply => "*",
            ExpressionType.Divide => "/",
            ExpressionType.Equal => "=",
            ExpressionType.NotEqual => "<>",
            ExpressionType.LessThan => "<",
            ExpressionType.GreaterThan => ">",
            ExpressionType.LessThanOrEqual => "<=",
            ExpressionType.GreaterThanOrEqual => ">=",
            _ => throw new NotSupportedException($"Operator {nodeType} not supported")
        };
    }

    private void ProcessGroupBySelectClause(LambdaExpression selector, CypherBuildContext context)
    {
        Console.WriteLine($"ProcessGroupBySelectClause called");
        Console.WriteLine($"GroupByKey: '{context.GroupByKey}'");
        Console.WriteLine($"IsTraversalPathGroupBy: {context.IsTraversalPathGroupBy}");

        // Clear IsPathResult since we're now projecting to a different shape
        context.IsPathResult = false;

        if (context.IsTraversalPathGroupBy)
        {
            // Special handling for TraversalPath grouping
            ProcessTraversalPathGroupBySelect(selector, context);
            return;
        }

        // For GroupBy + Select, we need to build WITH and aggregation clauses
        // e.g., .GroupBy(p => p.FirstName).Select(g => new { Name = g.Key, Count = g.Count() })
        // should generate: WITH p.FirstName as name, COUNT(p) as count RETURN { Name: name, Count: count }

        if (selector.Body is NewExpression newExpr)
        {
            Console.WriteLine($"Processing NewExpression with {newExpr.Arguments.Count} arguments");

            // Handle anonymous type projections like new { Name = g.Key, Count = g.Count() }
            var withClauseItems = new List<string>();
            var returnProjections = new List<string>();

            for (int i = 0; i < newExpr.Arguments.Count; i++)
            {
                var argument = newExpr.Arguments[i];
                var memberName = newExpr.Members?[i].Name ?? $"Field{i}";
                var aliasName = memberName.ToLower();

                Console.WriteLine($"Processing argument {i}: {argument.GetType().Name}, member: {memberName}");
                Console.WriteLine($"  Expression: {argument}");

                if (IsGroupingKeyAccess(argument))
                {
                    Console.WriteLine($"  - Is Key access");
                    // g.Key -> use the GroupBy key expression
                    withClauseItems.Add($"{context.GroupByKey} as {aliasName}");
                    returnProjections.Add($"{memberName}: {aliasName}");
                }
                else if (IsGroupingAggregation(argument, out var aggregationType))
                {
                    Console.WriteLine($"  - Is aggregation: {aggregationType}");
                    // g.Count(), g.Sum(x => x.Property), etc.
                    var aggregationExpr = BuildGroupingAggregation(argument, aggregationType, context);
                    withClauseItems.Add($"{aggregationExpr} as {aliasName}");
                    returnProjections.Add($"{memberName}: {aliasName}");
                }
                else
                {
                    Console.WriteLine($"  - Unknown expression type, falling back to client-side");
                    // Other expressions - for now, these should be client-side
                    context.ClientSideProjection = selector;
                    return;
                }
            }

            // Build the WITH clause for grouping
            context.With = $"WITH {string.Join(", ", withClauseItems)}";
            Console.WriteLine($"Setting WITH clause: {context.With}");

            // Build the final RETURN clause
            context.Return = $"{{ {string.Join(", ", returnProjections)} }}";
            Console.WriteLine($"Setting RETURN clause: {context.Return}");
        }
        else
        {
            Console.WriteLine($"Not a NewExpression, selector body type: {selector.Body.GetType().Name}");
            // For non-anonymous types, fall back to client-side projection
            context.ClientSideProjection = selector;
        }
    }

    private static bool IsGroupingKeyAccess(Expression expression)
    {
        // Check if this is accessing g.Key from IGrouping<TKey, TElement>
        if (expression is MemberExpression memberExpr && memberExpr.Member.Name == "Key")
        {
            // Check the expression type (like ParameterExpression 'g')
            var expressionType = memberExpr.Expression?.Type;
            if (expressionType != null && expressionType.IsGenericType &&
                expressionType.GetGenericTypeDefinition() == typeof(IGrouping<,>))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsGroupingAggregation(Expression expression, out string aggregationType)
    {
        aggregationType = "";

        if (expression is MethodCallExpression methodCall)
        {
            // Check if this is calling an aggregation method on IGrouping
            // The object might be a ParameterExpression (like 'g') with type IGrouping<,>
            var objectType = methodCall.Object?.Type;

            if (objectType != null && objectType.IsGenericType &&
                objectType.GetGenericTypeDefinition() == typeof(IGrouping<,>))
            {
                aggregationType = methodCall.Method.Name;
                return aggregationType is "Count" or "Sum" or "Average" or "Min" or "Max";
            }

            // Also check if the method is a static method on Enumerable/Queryable with IGrouping as source
            if (methodCall.Method.IsStatic && methodCall.Arguments.Count > 0)
            {
                var firstArgType = methodCall.Arguments[0].Type;
                if (firstArgType.IsGenericType &&
                    firstArgType.GetGenericTypeDefinition() == typeof(IGrouping<,>))
                {
                    aggregationType = methodCall.Method.Name;
                    return aggregationType is "Count" or "Sum" or "Average" or "Min" or "Max";
                }
            }
        }

        return false;
    }

    private string BuildGroupingAggregation(Expression expression, string aggregationType, CypherBuildContext context)
    {
        if (expression is MethodCallExpression methodCall)
        {
            switch (aggregationType)
            {
                case "Count":
                    return $"COUNT({context.CurrentAlias})";
                case "Sum":
                    if (methodCall.Arguments.Count > 0)
                    {
                        var sumSelector = ExtractLambdaFromQuote(methodCall.Arguments[0]);
                        if (sumSelector != null)
                        {
                            var property = BuildCypherExpression(sumSelector.Body, context.CurrentAlias);
                            return $"SUM({property})";
                        }
                    }
                    break;
                case "Average":
                    if (methodCall.Arguments.Count > 0)
                    {
                        var avgSelector = ExtractLambdaFromQuote(methodCall.Arguments[0]);
                        if (avgSelector != null)
                        {
                            var property = BuildCypherExpression(avgSelector.Body, context.CurrentAlias);
                            return $"AVG({property})";
                        }
                    }
                    break;
                case "Min":
                    if (methodCall.Arguments.Count > 0)
                    {
                        var minSelector = ExtractLambdaFromQuote(methodCall.Arguments[0]);
                        if (minSelector != null)
                        {
                            var property = BuildCypherExpression(minSelector.Body, context.CurrentAlias);
                            return $"MIN({property})";
                        }
                    }
                    break;
                case "Max":
                    if (methodCall.Arguments.Count > 0)
                    {
                        var maxSelector = ExtractLambdaFromQuote(methodCall.Arguments[0]);
                        if (maxSelector != null)
                        {
                            var property = BuildCypherExpression(maxSelector.Body, context.CurrentAlias);
                            return $"MAX({property})";
                        }
                    }
                    break;
            }
        }

        return $"COUNT({context.CurrentAlias})"; // fallback
    }

    private string BuildAggregationsFromProjections(NewExpression newExpr, CypherBuildContext context)
    {
        var aggregations = new List<string>();

        for (int i = 0; i < newExpr.Arguments.Count; i++)
        {
            var argument = newExpr.Arguments[i];

            if (IsGroupingAggregation(argument, out var aggregationType))
            {
                var aggregationExpr = BuildGroupingAggregation(argument, aggregationType, context);
                var memberName = newExpr.Members?[i].Name ?? $"agg{i}";
                aggregations.Add($"{aggregationExpr} as {memberName.ToLower()}");
            }
        }

        return aggregations.Count > 0 ? string.Join(", ", aggregations) : $"COUNT({context.CurrentAlias}) as count";
    }

    private static bool RequiresClientSideProjection(Expression expression)
    {
        // Anonymous type constructors (new { ... }) - check if all arguments can be handled server-side
        if (expression is NewExpression newExpr)
        {
            // Check if all arguments can be handled server-side
            return newExpr.Arguments.Any(arg => RequiresClientSideProjection(arg));
        }

        // Member init expressions (new SomeType { Prop = value }) require client-side projection  
        if (expression is MemberInitExpression)
        {
            return true;
        }

        // Binary expressions (like string concatenation, arithmetic) can often be handled server-side
        if (expression is BinaryExpression binaryExpr)
        {
            // Check if both operands can be handled server-side
            return RequiresClientSideProjection(binaryExpr.Left) || RequiresClientSideProjection(binaryExpr.Right);
        }

        // Simple member access (p.Name) can be handled server-side
        if (expression is MemberExpression me)
        {
            // DateTime static properties can be handled server-side
            if (me.Type == typeof(DateTime) && me.Member is PropertyInfo propertyInfo && propertyInfo.DeclaringType == typeof(DateTime))
            {
                return false; // DateTime.Now, DateTime.Today, etc. can be handled server-side
            }

            // Simple property access can be handled server-side
            return false;
        }

        // Simple constants can be handled server-side
        if (expression is ConstantExpression)
        {
            return false;
        }

        // Method calls might be handled server-side (string methods, math methods, etc.)
        if (expression is MethodCallExpression methodCall)
        {
            // Check if the method can be mapped to Cypher
            if (CanMapMethodToCypher(methodCall))
            {
                // Check if all arguments can be handled server-side
                var allArgumentsServerSide = methodCall.Arguments.All(arg => !RequiresClientSideProjection(arg));
                var objectServerSide = methodCall.Object == null || !RequiresClientSideProjection(methodCall.Object);
                return !(allArgumentsServerSide && objectServerSide);
            }
            return true; // Unknown methods require client-side
        }

        // For other complex expressions, default to client-side for safety
        return true;
    }

    private static bool CanMapMethodToCypher(MethodCallExpression methodCall)
    {
        // String methods that can be mapped to Cypher
        if (methodCall.Method.DeclaringType == typeof(string))
        {
            return methodCall.Method.Name switch
            {
                "ToUpper" or "ToLower" or "Trim" or "TrimStart" or "TrimEnd" or
                "Substring" or "Replace" or "StartsWith" or "EndsWith" or
                "Contains" or "Split" or "ToString" => true,
                _ => false
            };
        }

        // Math methods that can be mapped to Cypher
        if (methodCall.Method.DeclaringType == typeof(Math))
        {
            return methodCall.Method.Name switch
            {
                "Abs" or "Acos" or "Asin" or "Atan" or "Atan2" or
                "Ceiling" or "Cos" or "Exp" or "Floor" or "Log" or "Log10" or
                "Max" or "Min" or "Pow" or "Round" or "Sign" or "Sin" or
                "Sqrt" or "Tan" => true,
                _ => false
            };
        }

        // DateTime methods that can be mapped to Cypher
        if (methodCall.Method.DeclaringType == typeof(DateTime))
        {
            return methodCall.Method.Name switch
            {
                "AddYears" or "AddMonths" or "AddDays" or "AddHours" or
                "AddMinutes" or "AddSeconds" or "AddMilliseconds" or
                "ToString" => true,
                _ => false
            };
        }

        // Collection methods
        if (methodCall.Method.DeclaringType == typeof(Enumerable) ||
            (methodCall.Method.DeclaringType?.IsGenericType == true &&
             methodCall.Method.DeclaringType.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
        {
            return methodCall.Method.Name switch
            {
                "First" or "Last" or "Count" or "Take" or "Skip" => true,
                _ => false
            };
        }

        return false;
    }

    private void ProcessOrderByClause(LambdaExpression selector, bool descending, CypherBuildContext context)
    {
        var orderExpression = BuildCypherExpression(selector.Body, context.CurrentAlias, context);

        if (context.OrderBy.Length > 0)
            context.OrderBy.Append(", ");

        context.OrderBy.Append(orderExpression);

        // For Last operations, flip the order direction to get the last item efficiently
        if (context.IsLastOperation)
        {
            descending = !descending;
        }

        if (descending)
            context.OrderBy.Append(" DESC");
    }

    private void ProcessGroupByClause(MethodCallExpression methodCall, CypherBuildContext context)
    {
        LambdaExpression? keySelector = null;
        keySelector = ExtractLambdaFromQuote(methodCall.Arguments[1]);
        if (keySelector != null)
        {
            // Check if we're grouping TraversalPath results
            var sourceType = GetSourceElementType(methodCall);
            if (sourceType.IsGenericType &&
                sourceType.GetGenericTypeDefinition().FullName == "Cvoya.Graph.Model.TraversalPath`3")
            {
                // Special handling for grouping TraversalPath results
                ProcessTraversalPathGroupBy(keySelector, context);
            }
            else
            {
                // Regular GroupBy handling
                var keyExpression = BuildCypherExpression(keySelector.Body, context.CurrentAlias);

                context.IsGroupByQuery = true;
                context.GroupByKey = keyExpression;
                context.GroupByKeySelector = keySelector;
            }
        }
        else
        {
            throw new InvalidOperationException("Failed to extract key selector from GroupBy expression");
        }
    }

    private void ProcessTraversalPathGroupBy(LambdaExpression keySelector, CypherBuildContext context)
    {
        // For TraversalPath grouping, we need to modify the query structure
        // If grouping by path.Source, we want to collect all the paths for each source

        if (keySelector.Body is MemberExpression memberExpr && memberExpr.Member.Name == "Source")
        {
            // We're grouping by the source node
            // Modify the query to use WITH clause for grouping
            context.IsGroupByQuery = true;
            context.IsTraversalPathGroupBy = true;

            // The key is the source node - use the actual alias from the query
            context.GroupByKey = "n"; // This is the alias used in TraversePath queries
            context.GroupByKeySelector = keySelector;

            // Set up the WITH clause to collect paths using the actual aliases
            context.With = "WITH n, collect({relationship: r1, target: t2}) as paths";
        }
        else if (keySelector.Body is MemberExpression memberExpr2 && memberExpr2.Member.Name == "Target")
        {
            // Grouping by target
            context.IsGroupByQuery = true;
            context.IsTraversalPathGroupBy = true;

            context.GroupByKey = "t2"; // Target alias
            context.GroupByKeySelector = keySelector;

            context.With = "WITH t2, collect({source: n, relationship: r1}) as paths";
        }
        // Add more cases as needed
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

        // If we have client-side projection, we need to fetch the original entity type instead of the projected type
        Type actualEntityType = elementType;

        Console.WriteLine($"ExecuteQueryAsync - elementType: {elementType}, actualEntityType: {actualEntityType}");
        Console.WriteLine($"Cypher: {cypher}");
        Console.WriteLine($"IsGroupByQuery: {context?.IsGroupByQuery}, IsPathResult: {context?.IsPathResult}");

        if (context?.ClientSideProjection != null)
        {
            // For client-side projection, find the source entity type from the lambda parameter
            var sourceEntityType = FindSourceEntityType(context.ClientSideProjection);
            if (sourceEntityType != null)
            {
                actualEntityType = sourceEntityType;
            }
        }

        // For path results where we expect IGraphPath, we'll create TraversalPath instances
        // Since TraversalPath implements IGraphPath, this will work seamlessly
        Type conversionType = actualEntityType;
        if (context?.IsPathResult == true && actualEntityType.IsGenericType &&
            actualEntityType.GetGenericTypeDefinition().FullName == "Cvoya.Graph.Model.IGraphPath`3")
        {
            // Create TraversalPath instances (which implement IGraphPath)
            var genericArgs = actualEntityType.GetGenericArguments();
            conversionType = typeof(TraversalPath<,,>).MakeGenericType(genericArgs);
        }

        // Create list with the expected interface type for proper compatibility
        var listType = typeof(List<>).MakeGenericType(actualEntityType);
        var items = (System.Collections.IList)Activator.CreateInstance(listType)!;

        // Check if this is a path result based on context
        bool isPathResult = context?.IsPathResult == true;

        // Check if this is a GroupBy query - we'll get map results
        bool isGroupByResult = context?.IsGroupByQuery == true;

        await foreach (var record in cursor)
        {
            Console.WriteLine($"Processing record with {record.Keys.Count} keys: {string.Join(", ", record.Keys)}");

            object? item = null;

            if (isPathResult)
            {
                // Handle path results - create TraversalPath instances that implement IGraphPath
                item = ConvertPathResult(record, conversionType);
                Console.WriteLine($"Path result conversion: {(item != null ? "success" : "failed")}");
            }
            else if (isGroupByResult && record.Keys.Count == 1)
            {
                // Handle GroupBy results - these come back as maps
                var key = record.Keys.First();
                var value = record[key];

                Console.WriteLine($"GroupBy result - key: {key}, value type: {value?.GetType().Name}");

                if (value is IDictionary<string, object> map)
                {
                    Console.WriteLine($"Map contents: {string.Join(", ", map.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");
                    // For GroupBy results, we need to create the anonymous type from the map
                    item = CreateAnonymousTypeInstance(map, actualEntityType);
                    Console.WriteLine($"Anonymous type creation: {(item != null ? "success" : "failed")}");
                }
                else
                {
                    Console.WriteLine($"Value is not a map, it's: {value?.GetType().Name}");
                }
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
                        item = ConvertNodeToEntity(node, actualEntityType);
                    }
                    else if (value is global::Neo4j.Driver.IRelationship rel)
                    {
                        // Convert Neo4j relationship to the target entity type
                        item = ConvertRelationshipToEntity(rel, actualEntityType);
                    }
                    else
                    {
                        // Direct value (e.g., for aggregations or simple projections)
                        item = ConvertValue(value, actualEntityType);
                    }
                }
                else
                {
                    // Multiple columns - might be a projection or complex result
                    // For now, assume it's a node if the first key matches a pattern
                    var nodeKey = record.Keys.FirstOrDefault(k => k.StartsWith('n'));
                    if (nodeKey != null && record[nodeKey] is global::Neo4j.Driver.INode node)
                    {
                        item = ConvertNodeToEntity(node, actualEntityType);
                    }
                }
            }

            if (item != null)
            {
                items.Add(item);
                Console.WriteLine($"Added item to results: {item}");
            }
            else
            {
                Console.WriteLine("Item was null, not added to results");
            }
        }

        Console.WriteLine($"Total items: {items.Count}");

        // Apply client-side projection if needed
        if (context?.ClientSideProjection != null)
        {
            items = ApplyClientSideProjection(items, context.ClientSideProjection, actualEntityType);
        }

        // If a single result is expected, return the first item or null
        if (context != null && (context.IsSingleResult || context.IsScalarResult || context.IsBooleanQuery))
        {
            if (items.Count == 0)
            {
                Console.WriteLine("Returning null - no items found");
                return null;
            }
            Console.WriteLine($"Returning single result: {items[0]}");
            return items[0];
        }

        return items;
    }

    private static System.Collections.IList ApplyClientSideProjection(System.Collections.IList sourceItems, LambdaExpression projection, Type elementType)
    {
        // Compile the projection expression
        var compiledProjection = projection.Compile();

        // Get the result type from the projection
        var resultType = projection.ReturnType;
        var resultListType = typeof(List<>).MakeGenericType(resultType);
        var resultItems = (System.Collections.IList)Activator.CreateInstance(resultListType)!;

        // Apply projection to each item
        foreach (var item in sourceItems)
        {
            if (item != null)
            {
                var projectedItem = compiledProjection.DynamicInvoke(item);
                if (projectedItem != null)
                {
                    resultItems.Add(projectedItem);
                }
            }
        }

        return resultItems;
    }

    private static Type? FindSourceEntityType(LambdaExpression lambdaExpression)
    {
        // For lambda expressions, get the type of the first parameter
        if (lambdaExpression.Parameters.Count > 0)
        {
            return lambdaExpression.Parameters[0].Type;
        }
        return null;
    }

    private static Type? FindSourceEntityType(Expression expression)
    {
        // Walk up the expression tree to find the source entity type
        switch (expression)
        {
            case MethodCallExpression methodCall:
                // For Select operations, get the source type from the first argument
                if (methodCall.Method.Name == "Select" && methodCall.Arguments.Count >= 1)
                {
                    return FindSourceEntityType(methodCall.Arguments[0]);
                }
                // For other methods, check the source (first argument for static methods, Object for instance methods)
                if (methodCall.Object != null)
                {
                    return FindSourceEntityType(methodCall.Object);
                }
                if (methodCall.Arguments.Count > 0)
                {
                    return FindSourceEntityType(methodCall.Arguments[0]);
                }
                break;

            case ConstantExpression constant:
                // Check if this is a GraphQueryable<T>
                if (constant.Type.IsGenericType &&
                    constant.Type.GetGenericTypeDefinition() == typeof(GraphQueryable<>))
                {
                    return constant.Type.GetGenericArguments()[0];
                }
                break;

            case UnaryExpression unary:
                return FindSourceEntityType(unary.Operand);
        }

        return null;
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

        // Handle Neo4j temporal types to DateTime conversion
        if (targetType == typeof(DateTime))
        {
            var valueType = value.GetType();
            if (valueType.FullName?.StartsWith("Neo4j.Driver") == true)
            {
                // Handle ZonedDateTime from Neo4j (from datetime() function)
                if (valueType.Name == "ZonedDateTime")
                {
                    // For ZonedDateTime, preserve the UTC time - don't convert to local
                    var toDateTimeOffsetMethod = valueType.GetMethod("ToDateTimeOffset");
                    if (toDateTimeOffsetMethod != null)
                    {
                        var dateTimeOffset = (DateTimeOffset)toDateTimeOffsetMethod.Invoke(value, null)!;
                        return dateTimeOffset.UtcDateTime; // Return the UTC DateTime
                    }

                    // Fallback: try to get the DateTime property (might be in local time)
                    var dateTimeProperty = valueType.GetProperty("DateTime");
                    if (dateTimeProperty != null)
                    {
                        return dateTimeProperty.GetValue(value);
                    }

                    // Last resort: string parsing
                    var toStringMethod = valueType.GetMethod("ToString", Type.EmptyTypes);
                    if (toStringMethod != null)
                    {
                        var sv = toStringMethod.Invoke(value, null) as string;
                        if (DateTime.TryParse(sv, out var parsedDateTime))
                        {
                            return parsedDateTime;
                        }
                    }
                }

                // Handle LocalDateTime from Neo4j (from localdatetime() function)
                if (valueType.Name == "LocalDateTime")
                {
                    var toDateTimeMethod = valueType.GetMethod("ToDateTime");
                    if (toDateTimeMethod != null)
                    {
                        return toDateTimeMethod.Invoke(value, null);
                    }
                }

                // Handle Date from Neo4j (from date() function)
                if (valueType.Name == "Date")
                {
                    var toDateTimeMethod = valueType.GetMethod("ToDateTime");
                    if (toDateTimeMethod != null)
                    {
                        return toDateTimeMethod.Invoke(value, null);
                    }
                }
            }

            // Handle string to DateTime conversion
            if (value is string stringValue && DateTime.TryParse(stringValue, out var dateTime))
            {
                return dateTime;
            }
        }

        // Handle DateTimeOffset conversion
        if (targetType == typeof(DateTimeOffset))
        {
            var valueType = value.GetType();
            if (valueType.FullName?.StartsWith("Neo4j.Driver") == true && valueType.Name == "ZonedDateTime")
            {
                var toDateTimeOffsetMethod = valueType.GetMethod("ToDateTimeOffset");
                if (toDateTimeOffsetMethod != null)
                {
                    return toDateTimeOffsetMethod.Invoke(value, null);
                }
            }
        }

        // Handle integer types properly (Neo4j often returns Int64 for integers)
        if (targetType == typeof(int) && value is long longValue)
        {
            return (int)longValue;
        }

        // Handle Neo4j maps to anonymous types
        if (targetType.IsAnonymousType() && value is IDictionary<string, object> dict)
        {
            return CreateAnonymousTypeInstance(dict, targetType);
        }

        // Convert common types
        try
        {
            return Convert.ChangeType(value, targetType);
        }
        catch (Exception ex)
        {
            throw new GraphException($"Failed to convert {value.GetType().Name} to {targetType.Name}: {ex.Message}", ex);
        }
    }

    private static object? CreateAnonymousTypeInstance(IDictionary<string, object> dict, Type targetType)
    {
        try
        {
            // Get the constructor of the anonymous type
            var constructors = targetType.GetConstructors();
            if (constructors.Length == 0) return null;

            var constructor = constructors[0];
            var parameters = constructor.GetParameters();
            var args = new object?[parameters.Length];

            // Map dictionary values to constructor parameters
            for (int i = 0; i < parameters.Length; i++)
            {
                var paramName = parameters[i].Name!;
                var paramType = parameters[i].ParameterType;

                if (dict.TryGetValue(paramName, out var value))
                {
                    // Handle List<T> types (like FriendDetails)
                    if (paramType.IsGenericType && paramType.GetGenericTypeDefinition() == typeof(List<>))
                    {
                        var elementType = paramType.GetGenericArguments()[0];

                        if (value is IList sourceList)
                        {
                            var typedListType = typeof(List<>).MakeGenericType(elementType);
                            var typedList = (IList)Activator.CreateInstance(typedListType)!;

                            foreach (var item in sourceList)
                            {
                                object? convertedItem = null;

                                // Handle nested anonymous types
                                if (elementType.IsAnonymousType() && item is IDictionary<string, object> itemDict)
                                {
                                    convertedItem = CreateAnonymousTypeInstance(itemDict, elementType);
                                }
                                else
                                {
                                    convertedItem = ConvertValue(item, elementType);
                                }

                                if (convertedItem != null)
                                {
                                    typedList.Add(convertedItem);
                                }
                            }

                            args[i] = typedList;
                        }
                        else
                        {
                            // Create empty list if value isn't a list
                            args[i] = Activator.CreateInstance(paramType);
                        }
                    }
                    else
                    {
                        // Regular property - just convert the value
                        args[i] = ConvertValue(value, paramType);
                    }
                }
                else
                {
                    // Parameter not found in dictionary - provide default value
                    args[i] = paramType.IsValueType ? Activator.CreateInstance(paramType) : null;
                }
            }

            // Create the instance
            return constructor.Invoke(args);
        }
        catch (Exception)
        {
            // Return null on any conversion errors - fail gracefully
            return null;
        }
    }

    private static object? ConvertPathResult(global::Neo4j.Driver.IRecord record, Type elementType)
    {
        Console.WriteLine($"ConvertPathResult - elementType: {elementType.FullName}");
        Console.WriteLine($"Record keys: {string.Join(", ", record.Keys)}");

        // Check if we have the expected three columns with aliases: source, relationship, target
        if (record.Keys.Count == 3 &&
            record.Keys.Contains("source") &&
            record.Keys.Contains("relationship") &&
            record.Keys.Contains("target"))
        {
            Console.WriteLine("Using explicit source/relationship/target columns");
            return ConvertPathColumnsToTraversalPath(record, elementType, "source", "relationship", "target");
        }

        // Check if we have three columns that look like path components (node, rel, node pattern)
        if (record.Keys.Count == 3)
        {
            var keys = record.Keys.ToList();
            Console.WriteLine($"Analyzing 3-column pattern: {string.Join(", ", keys)}");

            // Try to identify the pattern: should be node, relationship, node
            string? sourceKey = null;
            string? relationshipKey = null;
            string? targetKey = null;

            for (int i = 0; i < keys.Count; i++)
            {
                var key = keys[i];
                var value = record[key];
                Console.WriteLine($"  Key '{key}': {value?.GetType().Name} - {value}");

                if (value is global::Neo4j.Driver.INode && sourceKey == null)
                {
                    sourceKey = key;
                    Console.WriteLine($"    Identified as source key");
                }
                else if (value is global::Neo4j.Driver.IRelationship relationshipValue)
                {
                    relationshipKey = key;
                    Console.WriteLine($"    Identified as relationship key");
                }
                else if (value is IList relationshipList && relationshipList.Count > 0 && relationshipList[0] is global::Neo4j.Driver.IRelationship)
                {
                    relationshipKey = key;
                    Console.WriteLine($"    Identified as relationship list key with {relationshipList.Count} items");
                }
                else if (value is global::Neo4j.Driver.INode && sourceKey != null && relationshipKey != null)
                {
                    targetKey = key;
                    Console.WriteLine($"    Identified as target key");
                }
            }

            Console.WriteLine($"Key identification result: source='{sourceKey}', relationship='{relationshipKey}', target='{targetKey}'");

            // If we found all three components, convert the path
            if (sourceKey != null && relationshipKey != null && targetKey != null)
            {
                Console.WriteLine("All keys identified, proceeding with conversion");
                return ConvertPathColumnsToTraversalPath(record, elementType, sourceKey, relationshipKey, targetKey);
            }
            else
            {
                Console.WriteLine("Failed to identify all required keys");
            }
        }

        Console.WriteLine("No matching pattern found for path conversion");
        return null;
    }

    private static object? ConvertPathColumnsToTraversalPath(global::Neo4j.Driver.IRecord record, Type elementType, string sourceKey, string relationshipKey, string targetKey)
    {
        try
        {
            // Handle both TraversalPath and IGraphPath
            Type? pathInterface = null;

            if (elementType.IsGenericType && elementType.GetGenericTypeDefinition().FullName == "Cvoya.Graph.Model.TraversalPath`3")
            {
                pathInterface = elementType;
            }
            else if (elementType.IsGenericType && elementType.GetGenericTypeDefinition().FullName == "Cvoya.Graph.Model.IGraphPath`3")
            {
                // For IGraphPath, we need to find the concrete implementation
                // For now, let's assume TraversalPath implements IGraphPath
                var ga = elementType.GetGenericArguments();
                var traversalPathType = typeof(TraversalPath<,,>).MakeGenericType(ga);
                pathInterface = traversalPathType;
            }

            if (pathInterface == null)
            {
                Console.WriteLine($"Unknown path type: {elementType}");
                return null;
            }

            var genericArgs = pathInterface.GetGenericArguments();
            var sourceType = genericArgs[0];
            var relationshipType = genericArgs[1];
            var targetType = genericArgs[2];

            // Convert source node
            if (!(record[sourceKey] is global::Neo4j.Driver.INode sourceNode))
            {
                Console.WriteLine($"Source key '{sourceKey}' is not a node");
                return null;
            }
            var source = ConvertNodeToEntity(sourceNode, sourceType);

            // Convert target node
            if (!(record[targetKey] is global::Neo4j.Driver.INode targetNode))
            {
                Console.WriteLine($"Target key '{targetKey}' is not a node");
                return null;
            }
            var target = ConvertNodeToEntity(targetNode, targetType);

            // Handle relationship - could be single relationship or array for multi-hop
            object? relationship = null;
            var relationshipValue = record[relationshipKey];

            if (relationshipValue is global::Neo4j.Driver.IRelationship singleRel)
            {
                // Single relationship
                relationship = ConvertRelationshipToEntity(singleRel, relationshipType);
            }
            else if (relationshipValue is IList relationshipList && relationshipList.Count > 0)
            {
                // Multi-hop relationships - take the first one for now
                // TODO: This might need more sophisticated handling depending on requirements
                if (relationshipList[0] is global::Neo4j.Driver.IRelationship firstRel)
                {
                    relationship = ConvertRelationshipToEntity(firstRel, relationshipType);
                }
            }

            if (relationship == null)
            {
                Console.WriteLine($"Failed to convert relationship from key '{relationshipKey}'");
                return null;
            }

            // Create the path instance
            var ctor = pathInterface.GetConstructor(new[] { sourceType, relationshipType, targetType });
            if (ctor != null && source != null && target != null)
            {
                var result = ctor.Invoke(new[] { source, relationship, target });
                Console.WriteLine($"Successfully created path instance: {result}");
                return result;
            }

            Console.WriteLine("Failed to find constructor or null components");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to convert path columns to path: {ex.Message}");
            return null;
        }
    }

    private static bool IsClosureAccess(MemberExpression memberExpr)
    {
        // Walk up the expression tree to find the root
        Expression current = memberExpr;
        while (current is MemberExpression member && member.Expression != null)
        {
            current = member.Expression;
        }

        // If the root is a ConstantExpression, it's a closure
        return current is ConstantExpression;
    }

    private static object? EvaluateMemberExpression(MemberExpression memberExpr)
    {
        // Convert to object and compile to get the value
        var objectMember = Expression.Convert(memberExpr, typeof(object));
        var getterLambda = Expression.Lambda<Func<object?>>(objectMember);
        var getter = getterLambda.Compile();
        return getter();
    }

    private static string AddParameter(object? value, CypherBuildContext context)
    {
        var paramName = $"p{context.Parameters.Count}";
        context.Parameters[paramName] = value;
        return paramName;
    }
}
