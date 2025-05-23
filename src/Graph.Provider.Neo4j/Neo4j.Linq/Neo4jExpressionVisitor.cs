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
using Cvoya.Graph.Provider.Neo4j;
using Neo4j.Driver;
using Neo4jDriver = Neo4j.Driver;

namespace Cvoya.Graph.Provider.Neo4j.Linq;

public class Neo4jExpressionVisitor : ExpressionVisitor
{
    private readonly Neo4jGraphProvider _provider;
    private readonly Type _rootType;
    private readonly Type _elementType;
    private readonly IGraphTransaction? _transaction;

    public Neo4jExpressionVisitor(Neo4jGraphProvider provider, Type rootType, Type elementType, IGraphTransaction? transaction)
    {
        _provider = provider;
        _rootType = rootType;
        _elementType = elementType;
        _transaction = transaction;
    }

    public string Translate(Expression expression)
    {
        // Cypher query builder with support for Where, Select, OrderBy, Take, navigation, and Neo4j functions
        var label = GetLabel(_rootType);
        var varName = "n";
        string? whereClause = null;
        string? returnClause = $"RETURN {varName}";
        string? limitClause = null;
        string? matchClause = null;
        string? skipClause = null;
        bool useDistinct = false;
        var orderings = new List<string>();

        // Add navigation property support
        var navigationMatches = new List<string>();
        var navigationReturns = new List<string>();

        // Helper to recursively process navigation property chains
        void ProcessNavigation(MemberExpression memberExpr, string parentVar, string parentLabel, int depth = 1)
        {
            var prop = memberExpr.Member;
            var propType = (prop as PropertyInfo)?.PropertyType;
            if (propType == null) return;
            if (typeof(System.Collections.IEnumerable).IsAssignableFrom(propType) && propType != typeof(string))
            {
                // Collection navigation (e.g., List<Knows>)
                var relType = propType.IsArray ? propType.GetElementType() : propType.GetGenericArguments().FirstOrDefault();
                if (relType == null) return;
                var relLabel = GetLabel(relType);
                var relVar = $"r{depth}";
                var targetType = relType.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition().Name.StartsWith("IRelationship"))?.GetGenericArguments().ElementAtOrDefault(1);
                var targetLabel = targetType != null ? GetLabel(targetType) : "Target";
                var targetVar = $"t{depth}";
                navigationMatches.Add($"OPTIONAL MATCH ({parentVar})-[{relVar}:{relLabel}]->({targetVar}:{targetLabel})");
                navigationReturns.Add($"collect({targetVar}) AS {prop.Name}");
            }
            else if (typeof(Cvoya.Graph.Model.IRelationship).IsAssignableFrom(propType))
            {
                // Relationship navigation (e.g., Knows)
                var relLabel = GetLabel(propType);
                var relVar = $"r{depth}";
                var targetType = propType.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition().Name.StartsWith("IRelationship"))?.GetGenericArguments().ElementAtOrDefault(1);
                var targetLabel = targetType != null ? GetLabel(targetType) : "Target";
                var targetVar = $"t{depth}";
                navigationMatches.Add($"OPTIONAL MATCH ({parentVar})-[{relVar}:{relLabel}]->({targetVar}:{targetLabel})");
                navigationReturns.Add($"{relVar} AS {prop.Name}");
            }
            else if (typeof(Cvoya.Graph.Model.INode).IsAssignableFrom(propType))
            {
                // Node navigation (e.g., Target)
                var nodeLabel = GetLabel(propType);
                var nodeVar = $"n{depth}";
                navigationMatches.Add($"OPTIONAL MATCH ({parentVar})-->{nodeVar}:{nodeLabel}");
                navigationReturns.Add($"{nodeVar} AS {prop.Name}");
            }
        }

        Expression current = expression;
        LambdaExpression? selectLambda = null;
        int? takeCount = null;
        bool reverseOrderForLast = false;

        // Walk the expression tree for supported LINQ methods
        while (current is MethodCallExpression mce)
        {
            var method = mce.Method.Name;
            if (method == "Count")
            {
                if (mce.Arguments.Count == 2 && mce.Arguments[1] is UnaryExpression ue && ue.Operand is LambdaExpression lambda)
                {
                    if (lambda.Body is BinaryExpression be)
                    {
                        string BuildWhere(BinaryExpression expr)
                        {
                            if (expr.NodeType == ExpressionType.AndAlso || expr.NodeType == ExpressionType.OrElse)
                            {
                                var left = expr.Left is BinaryExpression leftBin ? BuildWhere(leftBin) : BuildSimpleCondition(expr.Left);
                                var right = expr.Right is BinaryExpression rightBin ? BuildWhere(rightBin) : BuildSimpleCondition(expr.Right);
                                var op = expr.NodeType == ExpressionType.AndAlso ? "AND" : "OR";
                                return $"({left} {op} {right})";
                            }
                            return BuildSimpleCondition(expr);
                        }

                        string BuildSimpleCondition(Expression expr)
                        {
                            if (expr is BinaryExpression cond)
                            {
                                string op = cond.NodeType switch
                                {
                                    ExpressionType.Equal => "=",
                                    ExpressionType.NotEqual => "!=",
                                    ExpressionType.GreaterThan => ">",
                                    ExpressionType.LessThan => "<",
                                    ExpressionType.GreaterThanOrEqual => ">=",
                                    ExpressionType.LessThanOrEqual => "<=",
                                    _ => throw new NotSupportedException($"Operator {cond.NodeType} not supported")
                                };
                                if (cond.Left is MemberExpression me && ExpressionUtils.IsParameterOrPropertyOfLambda(me, lambda.Parameters[0]))
                                {
                                    var propName = me.Member.Name;
                                    var value = ExpressionUtils.EvaluateExpression(cond.Right);
                                    var formattedValue = FormatValueForCypher(value);
                                    return $"{varName}.{propName} {op} {formattedValue}";
                                }
                                else if (cond.Right is MemberExpression me2 && ExpressionUtils.IsParameterOrPropertyOfLambda(me2, lambda.Parameters[0]))
                                {
                                    var propName = me2.Member.Name;
                                    var value = ExpressionUtils.EvaluateExpression(cond.Left);
                                    var formattedValue = FormatValueForCypher(value);
                                    return $"{varName}.{propName} {op} {formattedValue}";
                                }
                                // Support for method calls (e.g. StartsWith)
                                else if (cond.Left is MethodCallExpression mcex && cond.Right is ConstantExpression ce3)
                                {
                                    if (mcex.Method.Name == "StartsWith" && mcex.Object is MemberExpression me3)
                                    {
                                        var propName = me3.Member.Name;
                                        var value = ce3.Value;
                                        var formattedValue = FormatValueForCypher(value);
                                        return $"{varName}.{propName} STARTS WITH {formattedValue}";
                                    }
                                    if (mcex.Method.Name == "EndsWith" && mcex.Object is MemberExpression me4)
                                    {
                                        var propName = me4.Member.Name;
                                        var value = ce3.Value;
                                        var formattedValue = FormatValueForCypher(value);
                                        return $"{varName}.{propName} ENDS WITH {formattedValue}";
                                    }
                                    if (mcex.Method.Name == "Contains" && mcex.Object is MemberExpression me5)
                                    {
                                        var propName = me5.Member.Name;
                                        var value = ce3.Value;
                                        var formattedValue = FormatValueForCypher(value);
                                        return $"{varName}.{propName} CONTAINS {formattedValue}";
                                    }
                                }
                            }
                            // Direct method call as condition (e.g. p.FirstName.StartsWith('A'))
                            if (expr is MethodCallExpression mcex2 && mcex2.Object is MemberExpression me6 && mcex2.Arguments.Count == 1 && mcex2.Arguments[0] is ConstantExpression ce4)
                            {
                                var propName = me6.Member.Name;
                                var value = ce4.Value;
                                var formattedValue = FormatValueForCypher(value);
                                if (mcex2.Method.Name == "StartsWith")
                                    return $"{varName}.{propName} STARTS WITH {formattedValue}";
                                if (mcex2.Method.Name == "EndsWith")
                                    return $"{varName}.{propName} ENDS WITH {formattedValue}";
                                if (mcex2.Method.Name == "Contains")
                                    return $"{varName}.{propName} CONTAINS {formattedValue}";
                            }
                            throw new NotSupportedException($"Unsupported condition expression: {expr}");
                        }

                        whereClause = $"WHERE {BuildWhere(be)}";
                    }
                }
                returnClause = "RETURN count(n) AS count";
                current = mce.Arguments[0];
                continue;
            }
            if (method == "Any")
            {
                // Any: return true if count(n) > 0
                if (mce.Arguments.Count == 2 && mce.Arguments[1] is UnaryExpression ue && ue.Operand is LambdaExpression lambda)
                {
                    if (lambda.Body is BinaryExpression be)
                    {
                        string BuildWhere(BinaryExpression expr)
                        {
                            if (expr.NodeType == ExpressionType.AndAlso || expr.NodeType == ExpressionType.OrElse)
                            {
                                var left = expr.Left is BinaryExpression leftBin ? BuildWhere(leftBin) : BuildSimpleCondition(expr.Left);
                                var right = expr.Right is BinaryExpression rightBin ? BuildWhere(rightBin) : BuildSimpleCondition(expr.Right);
                                var op = expr.NodeType == ExpressionType.AndAlso ? "AND" : "OR";
                                return $"({left} {op} {right})";
                            }
                            return BuildSimpleCondition(expr);
                        }
                        string BuildSimpleCondition(Expression expr)
                        {
                            if (expr is BinaryExpression cond)
                            {
                                string op = cond.NodeType switch
                                {
                                    ExpressionType.Equal => "=",
                                    ExpressionType.NotEqual => "!=",
                                    ExpressionType.GreaterThan => ">",
                                    ExpressionType.LessThan => "<",
                                    ExpressionType.GreaterThanOrEqual => ">=",
                                    ExpressionType.LessThanOrEqual => "<=",
                                    _ => throw new NotSupportedException($"Operator {cond.NodeType} not supported")
                                };
                                if (cond.Left is MemberExpression me && ExpressionUtils.IsParameterOrPropertyOfLambda(me, lambda.Parameters[0]))
                                {
                                    var propName = me.Member.Name;
                                    var value = ExpressionUtils.EvaluateExpression(cond.Right);
                                    var formattedValue = FormatValueForCypher(value);
                                    return $"{varName}.{propName} {op} {formattedValue}";
                                }
                                else if (cond.Right is MemberExpression me2 && ExpressionUtils.IsParameterOrPropertyOfLambda(me2, lambda.Parameters[0]))
                                {
                                    var propName = me2.Member.Name;
                                    var value = ExpressionUtils.EvaluateExpression(cond.Left);
                                    var formattedValue = FormatValueForCypher(value);
                                    return $"{varName}.{propName} {op} {formattedValue}";
                                }
                                // Support for method calls (e.g. StartsWith)
                                else if (cond.Left is MethodCallExpression mcex && cond.Right is ConstantExpression ce3)
                                {
                                    if (mcex.Method.Name == "StartsWith" && mcex.Object is MemberExpression me3)
                                    {
                                        var propName = me3.Member.Name;
                                        var value = ce3.Value;
                                        var formattedValue = FormatValueForCypher(value);
                                        return $"{varName}.{propName} STARTS WITH {formattedValue}";
                                    }
                                    if (mcex.Method.Name == "EndsWith" && mcex.Object is MemberExpression me4)
                                    {
                                        var propName = me4.Member.Name;
                                        var value = ce3.Value;
                                        var formattedValue = FormatValueForCypher(value);
                                        return $"{varName}.{propName} ENDS WITH {formattedValue}";
                                    }
                                    if (mcex.Method.Name == "Contains" && mcex.Object is MemberExpression me5)
                                    {
                                        var propName = me5.Member.Name;
                                        var value = ce3.Value;
                                        var formattedValue = FormatValueForCypher(value);
                                        return $"{varName}.{propName} CONTAINS {formattedValue}";
                                    }
                                }
                            }
                            // Direct method call as condition (e.g. p.FirstName.StartsWith('A'))
                            if (expr is MethodCallExpression mcex2 && mcex2.Object is MemberExpression me6 && mcex2.Arguments.Count == 1 && mcex2.Arguments[0] is ConstantExpression ce4)
                            {
                                var propName = me6.Member.Name;
                                var value = ce4.Value;
                                var formattedValue = FormatValueForCypher(value);
                                if (mcex2.Method.Name == "StartsWith")
                                    return $"{varName}.{propName} STARTS WITH {formattedValue}";
                                if (mcex2.Method.Name == "EndsWith")
                                    return $"{varName}.{propName} ENDS WITH {formattedValue}";
                                if (mcex2.Method.Name == "Contains")
                                    return $"{varName}.{propName} CONTAINS {formattedValue}";
                            }
                            throw new NotSupportedException($"Unsupported condition expression: {expr}");
                        }
                        whereClause = $"WHERE {BuildWhere(be)}";
                    }
                }
                returnClause = "RETURN count(n) > 0 AS result";
                current = mce.Arguments[0];
                continue;
            }
            if (method == "All")
            {
                // All: return true if count(n) > 0 and count(n where not predicate) == 0
                if (mce.Arguments.Count == 2 && mce.Arguments[1] is UnaryExpression ue && ue.Operand is LambdaExpression lambda)
                {
                    if (lambda.Body is BinaryExpression be)
                    {
                        string BuildWhere(BinaryExpression expr)
                        {
                            if (expr.NodeType == ExpressionType.AndAlso || expr.NodeType == ExpressionType.OrElse)
                            {
                                var left = expr.Left is BinaryExpression leftBin ? BuildWhere(leftBin) : BuildSimpleCondition(expr.Left);
                                var right = expr.Right is BinaryExpression rightBin ? BuildWhere(rightBin) : BuildSimpleCondition(expr.Right);
                                var op = expr.NodeType == ExpressionType.AndAlso ? "AND" : "OR";
                                return $"({left} {op} {right})";
                            }
                            return BuildSimpleCondition(expr);
                        }
                        string BuildSimpleCondition(Expression expr)
                        {
                            if (expr is BinaryExpression cond)
                            {
                                string op = cond.NodeType switch
                                {
                                    ExpressionType.Equal => "=",
                                    ExpressionType.NotEqual => "!=",
                                    ExpressionType.GreaterThan => ">",
                                    ExpressionType.LessThan => "<",
                                    ExpressionType.GreaterThanOrEqual => ">=",
                                    ExpressionType.LessThanOrEqual => "<=",
                                    _ => throw new NotSupportedException($"Operator {cond.NodeType} not supported")
                                };
                                if (cond.Left is MemberExpression me && ExpressionUtils.IsParameterOrPropertyOfLambda(me, lambda.Parameters[0]))
                                {
                                    var propName = me.Member.Name;
                                    var value = ExpressionUtils.EvaluateExpression(cond.Right);
                                    var formattedValue = FormatValueForCypher(value);
                                    return $"{varName}.{propName} {op} {formattedValue}";
                                }
                                else if (cond.Right is MemberExpression me2 && ExpressionUtils.IsParameterOrPropertyOfLambda(me2, lambda.Parameters[0]))
                                {
                                    var propName = me2.Member.Name;
                                    var value = ExpressionUtils.EvaluateExpression(cond.Left);
                                    var formattedValue = FormatValueForCypher(value);
                                    return $"{varName}.{propName} {op} {formattedValue}";
                                }
                                // Support for method calls (e.g. StartsWith)
                                else if (cond.Left is MethodCallExpression mcex && cond.Right is ConstantExpression ce3)
                                {
                                    if (mcex.Method.Name == "StartsWith" && mcex.Object is MemberExpression me3)
                                    {
                                        var propName = me3.Member.Name;
                                        var value = ce3.Value;
                                        var formattedValue = FormatValueForCypher(value);
                                        return $"{varName}.{propName} STARTS WITH {formattedValue}";
                                    }
                                    if (mcex.Method.Name == "EndsWith" && mcex.Object is MemberExpression me4)
                                    {
                                        var propName = me4.Member.Name;
                                        var value = ce3.Value;
                                        var formattedValue = FormatValueForCypher(value);
                                        return $"{varName}.{propName} ENDS WITH {formattedValue}";
                                    }
                                    if (mcex.Method.Name == "Contains" && mcex.Object is MemberExpression me5)
                                    {
                                        var propName = me5.Member.Name;
                                        var value = ce3.Value;
                                        var formattedValue = FormatValueForCypher(value);
                                        return $"{varName}.{propName} CONTAINS {formattedValue}";
                                    }
                                }
                            }
                            // Direct method call as condition (e.g. p.FirstName.StartsWith('A'))
                            if (expr is MethodCallExpression mcex2 && mcex2.Object is MemberExpression me6 && mcex2.Arguments.Count == 1 && mcex2.Arguments[0] is ConstantExpression ce4)
                            {
                                var propName = me6.Member.Name;
                                var value = ce4.Value;
                                var formattedValue = FormatValueForCypher(value);
                                if (mcex2.Method.Name == "StartsWith")
                                    return $"{varName}.{propName} STARTS WITH {formattedValue}";
                                if (mcex2.Method.Name == "EndsWith")
                                    return $"{varName}.{propName} ENDS WITH {formattedValue}";
                                if (mcex2.Method.Name == "Contains")
                                    return $"{varName}.{propName} CONTAINS {formattedValue}";
                            }
                            throw new NotSupportedException($"Unsupported condition expression: {expr}");
                        }
                        whereClause = $"WHERE {BuildWhere(be)}";
                    }
                }
                // All: count(n) > 0 AND count(n WHERE NOT predicate) = 0
                returnClause = "WITH count(n) AS total MATCH (n:"
                    + label + ") "
                    + (whereClause != null ? whereClause + " " : "")
                    + "WITH total, count(n) AS matching RETURN total > 0 AND total = matching AS result";
                // Prevent double MATCH, so break
                break;
            }
            if (method == "Where")
            {
                if (mce.Arguments[1] is UnaryExpression ue && ue.Operand is LambdaExpression lambda)
                {
                    if (lambda.Body is BinaryExpression be)
                    {
                        // Recursively build Cypher for multiple conditions
                        string BuildWhere(BinaryExpression expr)
                        {
                            if (expr.NodeType == ExpressionType.AndAlso || expr.NodeType == ExpressionType.OrElse)
                            {
                                var left = expr.Left is BinaryExpression leftBin ? BuildWhere(leftBin) : BuildSimpleCondition(expr.Left);
                                var right = expr.Right is BinaryExpression rightBin ? BuildWhere(rightBin) : BuildSimpleCondition(expr.Right);
                                var op = expr.NodeType == ExpressionType.AndAlso ? "AND" : "OR";
                                return $"({left} {op} {right})";
                            }
                            return BuildSimpleCondition(expr);
                        }
                        string BuildSimpleCondition(Expression expr)
                        {
                            if (expr is BinaryExpression cond)
                            {
                                string op = cond.NodeType switch
                                {
                                    ExpressionType.Equal => "=",
                                    ExpressionType.NotEqual => "!=",
                                    ExpressionType.GreaterThan => ">",
                                    ExpressionType.LessThan => "<",
                                    ExpressionType.GreaterThanOrEqual => ">=",
                                    ExpressionType.LessThanOrEqual => "<=",
                                    _ => throw new NotSupportedException($"Operator {cond.NodeType} not supported")
                                };
                                if (cond.Left is MemberExpression me && ExpressionUtils.IsParameterOrPropertyOfLambda(me, lambda.Parameters[0]))
                                {
                                    var propName = me.Member.Name;
                                    var value = ExpressionUtils.EvaluateExpression(cond.Right);
                                    var formattedValue = FormatValueForCypher(value);
                                    return $"{varName}.{propName} {op} {formattedValue}";
                                }
                                else if (cond.Right is MemberExpression me2 && ExpressionUtils.IsParameterOrPropertyOfLambda(me2, lambda.Parameters[0]))
                                {
                                    var propName = me2.Member.Name;
                                    var value = ExpressionUtils.EvaluateExpression(cond.Left);
                                    var formattedValue = FormatValueForCypher(value);
                                    return $"{varName}.{propName} {op} {formattedValue}";
                                }
                                // Support for method calls (e.g. StartsWith)
                                else if (cond.Left is MethodCallExpression mcex && cond.Right is ConstantExpression ce3)
                                {
                                    if (mcex.Method.Name == "StartsWith" && mcex.Object is MemberExpression me3)
                                    {
                                        var propName = me3.Member.Name;
                                        var value = ce3.Value;
                                        var formattedValue = FormatValueForCypher(value);
                                        return $"{varName}.{propName} STARTS WITH {formattedValue}";
                                    }
                                    if (mcex.Method.Name == "EndsWith" && mcex.Object is MemberExpression me4)
                                    {
                                        var propName = me4.Member.Name;
                                        var value = ce3.Value;
                                        var formattedValue = FormatValueForCypher(value);
                                        return $"{varName}.{propName} ENDS WITH {formattedValue}";
                                    }
                                    if (mcex.Method.Name == "Contains" && mcex.Object is MemberExpression me5)
                                    {
                                        var propName = me5.Member.Name;
                                        var value = ce3.Value;
                                        var formattedValue = FormatValueForCypher(value);
                                        return $"{varName}.{propName} CONTAINS {formattedValue}";
                                    }
                                }
                            }
                            // Direct method call as condition (e.g. p.FirstName.StartsWith('A'))
                            if (expr is MethodCallExpression mcex2 && mcex2.Object is MemberExpression me6 && mcex2.Arguments.Count == 1 && mcex2.Arguments[0] is ConstantExpression ce4)
                            {
                                var propName = me6.Member.Name;
                                var value = ce4.Value;
                                var formattedValue = FormatValueForCypher(value);
                                if (mcex2.Method.Name == "StartsWith")
                                    return $"{varName}.{propName} STARTS WITH {formattedValue}";
                                if (mcex2.Method.Name == "EndsWith")
                                    return $"{varName}.{propName} ENDS WITH {formattedValue}";
                                if (mcex2.Method.Name == "Contains")
                                    return $"{varName}.{propName} CONTAINS {formattedValue}";
                            }
                            throw new NotSupportedException($"Unsupported condition expression: {expr}");
                        }
                        whereClause = $"WHERE {BuildWhere(be)}";
                    }
                }
                current = mce.Arguments[0];
            }
            else if (method == "OrderBy" || method == "OrderByDescending" || method == "ThenBy" || method == "ThenByDescending")
            {
                if (mce.Arguments[1] is UnaryExpression ue && ue.Operand is LambdaExpression lambda && lambda.Body is MemberExpression me)
                {
                    var propName = me.Member.Name;
                    var dir = (method == "OrderBy" || method == "ThenBy") ? "ASC" : "DESC";
                    if (reverseOrderForLast)
                    {
                        dir = dir == "ASC" ? "DESC" : "ASC";
                        reverseOrderForLast = false;
                    }
                    orderings.Insert(0, $"{varName}.{propName} {dir}"); // Insert at front to preserve LINQ order
                }
                current = mce.Arguments[0];
            }
            else if (method == "Take")
            {
                if (mce.Arguments[1] is ConstantExpression ce && ce.Value is int count)
                {
                    takeCount = count;
                    limitClause = $"LIMIT {count}";
                }
                current = mce.Arguments[0];
            }
            else if (method == "Skip")
            {
                if (mce.Arguments[1] is ConstantExpression ce && ce.Value is int count)
                {
                    skipClause = $"SKIP {count}";
                }
                current = mce.Arguments[0];
            }
            else if (method == "Distinct")
            {
                useDistinct = true;
                current = mce.Arguments[0];
            }
            else if (method == "Select")
            {
                if (mce.Arguments[1] is UnaryExpression ue && ue.Operand is LambdaExpression lambda)
                {
                    selectLambda = lambda;
                    // Support simple and anonymous projections, and navigation
                    if (lambda.Body is MemberExpression me)
                    {
                        // Simple property projection: n.Prop AS Prop
                        var propName = me.Member.Name;
                        returnClause = $"RETURN {(useDistinct ? "DISTINCT " : "")}{varName}.{propName} AS {propName}";
                    }
                    else if (lambda.Body is NewExpression ne)
                    {
                        // Anonymous type projection: new { ... }
                        var members = ne.Members ?? new System.Collections.ObjectModel.ReadOnlyCollection<System.Reflection.MemberInfo>(new System.Reflection.MemberInfo[0]);
                        var props = new List<string>();

                        // Check if any arguments access navigation properties
                        for (int i = 0; i < ne.Arguments.Count; i++)
                        {
                            var arg = ne.Arguments[i];
                            var member = members[i];

                            // Check if this argument accesses navigation properties
                            if (HasNavigationPropertyAccess(arg))
                            {
                                // We need to load relationships for this projection
                                // Add OPTIONAL MATCH for the Knows relationship
                                if (!navigationMatches.Any(m => m.Contains("[:KNOWS]")))
                                {
                                    navigationMatches.Add($"OPTIONAL MATCH ({varName})-[r1:KNOWS]->(t1)");
                                }
                            }

                            string cypherExpr = CypherExpressionBuilder.BuildCypherExpression(arg, varName);
                            props.Add($"{cypherExpr} AS {member.Name}");
                        }
                        returnClause = $"RETURN {(useDistinct ? "DISTINCT " : "")}{string.Join(", ", props)}";
                    }
                    else if (lambda.Body is MemberInitExpression mie)
                    {
                        // Support for new { ... } with initializers
                        var bindings = mie.Bindings.OfType<MemberAssignment>();
                        var props = string.Join(", ", bindings.Select(b => $"{varName}.{b.Member.Name} AS {b.Member.Name}"));
                        returnClause = $"RETURN {(useDistinct ? "DISTINCT " : "")}{props}";
                    }
                    // Navigation/deep traversal: detect navigation property and generate MATCH/OPTIONAL MATCH for relationships
                    if (lambda.Body is MemberExpression navMe)
                    {
                        ProcessNavigation(navMe, varName, label, 1);
                    }
                }
                current = mce.Arguments[0];
            }
            else if (method == "SelectMany")
            {
                // Support for navigation collections (deep traversal)
                if (mce.Arguments[1] is UnaryExpression ue && ue.Operand is LambdaExpression lambda)
                {
                    if (lambda.Body is MemberExpression navMe)
                    {
                        ProcessNavigation(navMe, varName, label, 1);
                    }
                }
                current = mce.Arguments[0];
            }
            else if (method == "Last")
            {
                reverseOrderForLast = true;
                limitClause = "LIMIT 1";
                current = mce.Arguments[0];
                continue;
            }
            else
            {
                // Not supported, break
                break;
            }
        }

        // At the end, build the Cypher query
        // Detect if this is a relationship type
        bool isRelationship = _rootType.IsRelationshipType();
        string cypher;
        if (isRelationship)
        {
            // Relationship scan: MATCH ()-[n:Label]->()
            cypher = $"MATCH ()-[{varName}:{label}]->() ";
        }
        else
        {
            // Node scan: MATCH (n:Label)
            cypher = $"MATCH ({varName}:{label}) ";
        }
        if (matchClause != null) cypher += matchClause + " ";
        if (whereClause != null) cypher += whereClause + " ";
        if (orderings.Count > 0)
            cypher += $"ORDER BY {string.Join(", ", orderings)} ";
        // After building the main MATCH, add navigation matches
        if (navigationMatches.Count > 0)
        {
            cypher += string.Join(" ", navigationMatches) + " ";
        }
        cypher += returnClause;
        // In the RETURN clause, add navigation returns if present
        if (navigationReturns.Count > 0)
        {
            cypher = cypher.Replace(returnClause, $"RETURN {string.Join(", ", navigationReturns)}");
        }
        if (skipClause != null) cypher += " " + skipClause;
        if (limitClause != null) cypher += " " + limitClause;
        return cypher;
    }

    public object ExecuteQuery(string cypher, Type elementType)
    {
        Console.WriteLine($"Executing Cypher: {cypher}");
        // Block execution!
        return this.ExecuteQueryAsync(cypher, elementType).Result;
    }

    internal async Task<object> ExecuteQueryAsync(string cypher, Type elementType)
    {
        var results = await _provider.ExecuteCypher(cypher, null, _transaction);

        static bool IsSimpleType(Type t) =>
            (t.IsPrimitive || t == typeof(string) || t == typeof(decimal) || t == typeof(DateTime) || t == typeof(DateTimeOffset) || t == typeof(Guid) || t == typeof(bool))
            && !typeof(Neo4jDriver.INode).IsAssignableFrom(t)
            && !typeof(Neo4jDriver.IRelationship).IsAssignableFrom(t);

        // If elementType is a simple type, just extract the value from the record
        if (IsSimpleType(elementType))
        {
            // Special case: if the result is a single record with a single value (e.g., count, bool), return the value directly
            if (results is IList resultList && resultList.Count == 1 && resultList[0] is Neo4jDriver.IRecord rec && rec.Values.Count == 1)
            {
                var val = rec.Values.Values.First();
                // If expecting a bool, handle Neo4j boolean result
                if (elementType == typeof(bool))
                {
                    if (val is bool b) return b;
                    if (val is long l) return l != 0;
                    if (val is int i) return i != 0;
                    if (val is string s && bool.TryParse(s, out var parsed)) return parsed;
                }
                return SerializationExtensions.ConvertFromNeo4jValue(val, elementType) ?? default!;
            }
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;
            foreach (var record in results.Select(r => r as Neo4jDriver.IRecord).Where(r => r != null))
            {
                foreach (var val in record!.Values.Values)
                {
                    list.Add(SerializationExtensions.ConvertFromNeo4jValue(val, elementType) ?? default!);
                }
            }
            return list;
        }

        // If elementType is an anonymous type (has CompilerGeneratedAttribute and is not a node/relationship)
        if (elementType.IsClass && elementType.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false) && !typeof(Neo4jDriver.INode).IsAssignableFrom(elementType) && !typeof(Neo4jDriver.IRelationship).IsAssignableFrom(elementType))
        {
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;
            var ctor = elementType.GetConstructors().FirstOrDefault();
            var ctorParams = ctor?.GetParameters();
            foreach (var record in results.Select(r => r as IRecord).Where(r => r != null))
            {
                var args = new object?[ctorParams!.Length];
                for (int i = 0; i < ctorParams.Length; i++)
                {
                    var param = ctorParams[i];
                    // Try to match by name (case-insensitive)
                    var kvp = record!.Values.FirstOrDefault(kv => string.Equals(kv.Key, param.Name, StringComparison.OrdinalIgnoreCase));
                    object? value = kvp.Value;
                    if (value == null)
                    {
                        args[i] = param.ParameterType.IsValueType && Nullable.GetUnderlyingType(param.ParameterType) == null
                            ? Activator.CreateInstance(param.ParameterType)
                            : null;
                    }
                    else if (param.ParameterType.IsClass && value is Neo4jDriver.INode nodeVal)
                    {
                        // Navigation: hydrate related node as the parameter type
                        var navConvertToGraphEntityMethodName = nameof(SerializationExtensions.ConvertToGraphEntity);
                        var navMethod = typeof(SerializationExtensions).GetMethod(
                                navConvertToGraphEntityMethodName,
                                BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic)
                            ?? throw new GraphException($"{navConvertToGraphEntityMethodName} method not found");
                        var navConvertToGraphEntity = navMethod.MakeGenericMethod(param.ParameterType);
                        args[i] = navConvertToGraphEntity.Invoke(null, new object[] { nodeVal });
                    }
                    else if (value is IList neo4jList && typeof(IEnumerable).IsAssignableFrom(param.ParameterType))
                    {
                        // Handle collections (e.g., Friends list)
                        var collectionElementType = param.ParameterType.IsArray
                            ? param.ParameterType.GetElementType()
                            : param.ParameterType.GetGenericArguments().FirstOrDefault();

                        // Default to object if we can't determine the element type
                        if (collectionElementType == null)
                        {
                            collectionElementType = typeof(object);
                        }

                        var typedList = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(collectionElementType))!;

                        foreach (var item in neo4jList)
                        {
                            if (item != null)
                            {
                                // Convert each item to the expected type
                                if (collectionElementType == typeof(string))
                                {
                                    typedList.Add(item.ToString());
                                }
                                else if (collectionElementType.IsPrimitive || collectionElementType == typeof(decimal))
                                {
                                    typedList.Add(Convert.ChangeType(item, collectionElementType));
                                }
                                else
                                {
                                    typedList.Add(item);
                                }
                            }
                        }

                        // Convert to array if needed
                        if (param.ParameterType.IsArray)
                        {
                            var array = Array.CreateInstance(collectionElementType, typedList.Count);
                            typedList.CopyTo(array, 0);
                            args[i] = array;
                        }
                        else
                        {
                            args[i] = typedList;
                        }
                    }
                    else if (value != null && (param.ParameterType.IsPrimitive || param.ParameterType == typeof(string) || param.ParameterType == typeof(decimal)))
                    {
                        // Handle simple types
                        args[i] = Convert.ChangeType(value, param.ParameterType);
                    }
                    else
                    {
                        // For other types, try direct assignment
                        args[i] = value;
                    }
                }
                var anon = ctor!.Invoke(args);
                list.Add(anon);
            }
            return list;
        }

        // Default: hydrate as node/relationship
        var convertToGraphEntityMethodName = nameof(SerializationExtensions.ConvertToGraphEntity);
        var method = typeof(SerializationExtensions).GetMethod(
                convertToGraphEntityMethodName,
                BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new GraphException($"{convertToGraphEntityMethodName} method not found");
        var convertToGraphEntity = method.MakeGenericMethod(elementType);

        var entityList = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;
        foreach (var record in results.Select(r => r as IRecord).Where(r => r != null))
        {
            if (record!.TryGetValue("n", out var nodeValue))
            {
                if (nodeValue is Neo4jDriver.INode node)
                {
                    var entity = convertToGraphEntity.Invoke(null, new object[] { node });
                    entityList.Add(entity);
                }
                else if (nodeValue is Neo4jDriver.IRelationship rel)
                {
                    var entity = convertToGraphEntity.Invoke(null, new object[] { rel });
                    entityList.Add(entity);
                }
            }
            else if (record.TryGetValue("r", out var relValue) && relValue is Neo4jDriver.IRelationship rel)
            {
                var entity = convertToGraphEntity.Invoke(null, new object[] { rel });
                entityList.Add(entity);
            }
        }
        return entityList;
    }

    internal static string FormatValueForCypher(object? value)
    {
        return value switch
        {
            null => "null",
            string s => $"'{s.Replace("'", "\\'")}'",
            bool b => b ? "true" : "false",
            int or long or short or byte or sbyte or uint or ulong or ushort => value.ToString()!,
            float or double or decimal => value.ToString()!,
            DateTime dt => $"datetime('{dt:yyyy-MM-ddTHH:mm:ss.fffZ}')",
            DateTimeOffset dto => $"datetime('{dto:yyyy-MM-ddTHH:mm:ss.fffzzz}')",
            Guid g => $"'{g}'",
            _ => $"'{value}'"
        };
    }

    private static string GetLabel(Type type)
    {
        var getLabelMethodName = nameof(Neo4jGraphProvider.GetLabel);
        var method = typeof(Neo4jGraphProvider).GetMethod(
                getLabelMethodName,
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            ?? throw new InvalidOperationException($"{getLabelMethodName} method not found");
        return (string)method.Invoke(null, [type])!;
    }

    private bool HasNavigationPropertyAccess(Expression expr)
    {
        switch (expr)
        {
            case MemberExpression me:
                if (me.Member is PropertyInfo prop)
                {
                    var propType = prop.PropertyType;
                    if (typeof(IEnumerable).IsAssignableFrom(propType) && propType != typeof(string))
                    {
                        // This is a collection navigation property
                        return true;
                    }
                    if (typeof(Model.IRelationship).IsAssignableFrom(propType) || typeof(Model.INode).IsAssignableFrom(propType))
                    {
                        // This is a relationship or node navigation property
                        return true;
                    }
                }
                // Check the parent expression
                if (me.Expression != null)
                {
                    return HasNavigationPropertyAccess(me.Expression);
                }
                break;
            case MethodCallExpression mce:
                // Check if any argument accesses navigation properties
                if (mce.Object != null && HasNavigationPropertyAccess(mce.Object))
                {
                    return true;
                }
                foreach (var arg in mce.Arguments)
                {
                    if (HasNavigationPropertyAccess(arg))
                    {
                        return true;
                    }
                }
                break;
            case NewExpression ne:
                // Check all arguments
                foreach (var arg in ne.Arguments)
                {
                    if (HasNavigationPropertyAccess(arg))
                    {
                        return true;
                    }
                }
                break;
        }
        return false;
    }
}