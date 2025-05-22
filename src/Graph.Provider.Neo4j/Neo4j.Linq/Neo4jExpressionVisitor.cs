using System;
using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using Cvoya.Graph.Provider.Model;
using Cvoya.Graph.Provider.Neo4j;
using Neo4j.Driver;
using Neo4jDriver = Neo4j.Driver;

namespace Cvoya.Graph.Provider.Neo4j.Linq
{
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
            string? orderByClause = null;
            string? returnClause = $"RETURN {varName}";
            string? limitClause = null;
            string? matchClause = null;
            string? skipClause = null;
            bool useDistinct = false;

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
                    // If Count(predicate), treat as Where + Count
                    if (mce.Arguments.Count == 2 && mce.Arguments[1] is UnaryExpression ue && ue.Operand is LambdaExpression lambda)
                    {
                        // Add WHERE clause for predicate
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
                                    if (cond.Left is MemberExpression me && IsParameterOrPropertyOfLambda(me, lambda.Parameters[0]))
                                    {
                                        var propName = me.Member.Name;
                                        var value = EvaluateExpression(cond.Right);
                                        return $"{varName}.{propName} {op} '{value}'";
                                    }
                                    else if (cond.Right is MemberExpression me2 && IsParameterOrPropertyOfLambda(me2, lambda.Parameters[0]))
                                    {
                                        var propName = me2.Member.Name;
                                        var value = EvaluateExpression(cond.Left);
                                        return $"{varName}.{propName} {op} '{value}'";
                                    }
                                    // Support for method calls (e.g. StartsWith)
                                    else if (cond.Left is MethodCallExpression mcex && cond.Right is ConstantExpression ce3)
                                    {
                                        if (mcex.Method.Name == "StartsWith" && mcex.Object is MemberExpression me3)
                                        {
                                            var propName = me3.Member.Name;
                                            var value = ce3.Value;
                                            return $"{varName}.{propName} STARTS WITH '{value}'";
                                        }
                                        if (mcex.Method.Name == "EndsWith" && mcex.Object is MemberExpression me4)
                                        {
                                            var propName = me4.Member.Name;
                                            var value = ce3.Value;
                                            return $"{varName}.{propName} ENDS WITH '{value}'";
                                        }
                                        if (mcex.Method.Name == "Contains" && mcex.Object is MemberExpression me5)
                                        {
                                            var propName = me5.Member.Name;
                                            var value = ce3.Value;
                                            return $"{varName}.{propName} CONTAINS '{value}'";
                                        }
                                    }
                                }
                                // Direct method call as condition (e.g. p.FirstName.StartsWith('A'))
                                if (expr is MethodCallExpression mcex2 && mcex2.Object is MemberExpression me6 && mcex2.Arguments.Count == 1 && mcex2.Arguments[0] is ConstantExpression ce4)
                                {
                                    var propName = me6.Member.Name;
                                    var value = ce4.Value;
                                    if (mcex2.Method.Name == "StartsWith")
                                        return $"{varName}.{propName} STARTS WITH '{value}'";
                                    if (mcex2.Method.Name == "EndsWith")
                                        return $"{varName}.{propName} ENDS WITH '{value}'";
                                    if (mcex2.Method.Name == "Contains")
                                        return $"{varName}.{propName} CONTAINS '{value}'";
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
                                    if (cond.Left is MemberExpression me && IsParameterOrPropertyOfLambda(me, lambda.Parameters[0]))
                                    {
                                        var propName = me.Member.Name;
                                        var value = EvaluateExpression(cond.Right);
                                        return $"{varName}.{propName} {op} '{value}'";
                                    }
                                    else if (cond.Right is MemberExpression me2 && IsParameterOrPropertyOfLambda(me2, lambda.Parameters[0]))
                                    {
                                        var propName = me2.Member.Name;
                                        var value = EvaluateExpression(cond.Left);
                                        return $"{varName}.{propName} {op} '{value}'";
                                    }
                                    // Support for method calls (e.g. StartsWith)
                                    else if (cond.Left is MethodCallExpression mcex && cond.Right is ConstantExpression ce3)
                                    {
                                        if (mcex.Method.Name == "StartsWith" && mcex.Object is MemberExpression me3)
                                        {
                                            var propName = me3.Member.Name;
                                            var value = ce3.Value;
                                            return $"{varName}.{propName} STARTS WITH '{value}'";
                                        }
                                        if (mcex.Method.Name == "EndsWith" && mcex.Object is MemberExpression me4)
                                        {
                                            var propName = me4.Member.Name;
                                            var value = ce3.Value;
                                            return $"{varName}.{propName} ENDS WITH '{value}'";
                                        }
                                        if (mcex.Method.Name == "Contains" && mcex.Object is MemberExpression me5)
                                        {
                                            var propName = me5.Member.Name;
                                            var value = ce3.Value;
                                            return $"{varName}.{propName} CONTAINS '{value}'";
                                        }
                                    }
                                }
                                // Direct method call as condition (e.g. p.FirstName.StartsWith('A'))
                                if (expr is MethodCallExpression mcex2 && mcex2.Object is MemberExpression me6 && mcex2.Arguments.Count == 1 && mcex2.Arguments[0] is ConstantExpression ce4)
                                {
                                    var propName = me6.Member.Name;
                                    var value = ce4.Value;
                                    if (mcex2.Method.Name == "StartsWith")
                                        return $"{varName}.{propName} STARTS WITH '{value}'";
                                    if (mcex2.Method.Name == "EndsWith")
                                        return $"{varName}.{propName} ENDS WITH '{value}'";
                                    if (mcex2.Method.Name == "Contains")
                                        return $"{varName}.{propName} CONTAINS '{value}'";
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
                                    if (cond.Left is MemberExpression me && IsParameterOrPropertyOfLambda(me, lambda.Parameters[0]))
                                    {
                                        var propName = me.Member.Name;
                                        var value = EvaluateExpression(cond.Right);
                                        return $"{varName}.{propName} {op} '{value}'";
                                    }
                                    else if (cond.Right is MemberExpression me2 && IsParameterOrPropertyOfLambda(me2, lambda.Parameters[0]))
                                    {
                                        var propName = me2.Member.Name;
                                        var value = EvaluateExpression(cond.Left);
                                        return $"{varName}.{propName} {op} '{value}'";
                                    }
                                    // Support for method calls (e.g. StartsWith)
                                    else if (cond.Left is MethodCallExpression mcex && cond.Right is ConstantExpression ce3)
                                    {
                                        if (mcex.Method.Name == "StartsWith" && mcex.Object is MemberExpression me3)
                                        {
                                            var propName = me3.Member.Name;
                                            var value = ce3.Value;
                                            return $"{varName}.{propName} STARTS WITH '{value}'";
                                        }
                                        if (mcex.Method.Name == "EndsWith" && mcex.Object is MemberExpression me4)
                                        {
                                            var propName = me4.Member.Name;
                                            var value = ce3.Value;
                                            return $"{varName}.{propName} ENDS WITH '{value}'";
                                        }
                                        if (mcex.Method.Name == "Contains" && mcex.Object is MemberExpression me5)
                                        {
                                            var propName = me5.Member.Name;
                                            var value = ce3.Value;
                                            return $"{varName}.{propName} CONTAINS '{value}'";
                                        }
                                    }
                                }
                                // Direct method call as condition (e.g. p.FirstName.StartsWith('A'))
                                if (expr is MethodCallExpression mcex2 && mcex2.Object is MemberExpression me6 && mcex2.Arguments.Count == 1 && mcex2.Arguments[0] is ConstantExpression ce4)
                                {
                                    var propName = me6.Member.Name;
                                    var value = ce4.Value;
                                    if (mcex2.Method.Name == "StartsWith")
                                        return $"{varName}.{propName} STARTS WITH '{value}'";
                                    if (mcex2.Method.Name == "EndsWith")
                                        return $"{varName}.{propName} ENDS WITH '{value}'";
                                    if (mcex2.Method.Name == "Contains")
                                        return $"{varName}.{propName} CONTAINS '{value}'";
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
                                    if (cond.Left is MemberExpression me && IsParameterOrPropertyOfLambda(me, lambda.Parameters[0]))
                                    {
                                        var propName = me.Member.Name;
                                        var value = EvaluateExpression(cond.Right);
                                        return $"{varName}.{propName} {op} '{value}'";
                                    }
                                    else if (cond.Right is MemberExpression me2 && IsParameterOrPropertyOfLambda(me2, lambda.Parameters[0]))
                                    {
                                        var propName = me2.Member.Name;
                                        var value = EvaluateExpression(cond.Left);
                                        return $"{varName}.{propName} {op} '{value}'";
                                    }
                                    // Support for method calls (e.g. StartsWith)
                                    else if (cond.Left is MethodCallExpression mcex && cond.Right is ConstantExpression ce3)
                                    {
                                        if (mcex.Method.Name == "StartsWith" && mcex.Object is MemberExpression me3)
                                        {
                                            var propName = me3.Member.Name;
                                            var value = ce3.Value;
                                            return $"{varName}.{propName} STARTS WITH '{value}'";
                                        }
                                        if (mcex.Method.Name == "EndsWith" && mcex.Object is MemberExpression me4)
                                        {
                                            var propName = me4.Member.Name;
                                            var value = ce3.Value;
                                            return $"{varName}.{propName} ENDS WITH '{value}'";
                                        }
                                        if (mcex.Method.Name == "Contains" && mcex.Object is MemberExpression me5)
                                        {
                                            var propName = me5.Member.Name;
                                            var value = ce3.Value;
                                            return $"{varName}.{propName} CONTAINS '{value}'";
                                        }
                                    }
                                }
                                // Direct method call as condition (e.g. p.FirstName.StartsWith('A'))
                                if (expr is MethodCallExpression mcex2 && mcex2.Object is MemberExpression me6 && mcex2.Arguments.Count == 1 && mcex2.Arguments[0] is ConstantExpression ce4)
                                {
                                    var propName = me6.Member.Name;
                                    var value = ce4.Value;
                                    if (mcex2.Method.Name == "StartsWith")
                                        return $"{varName}.{propName} STARTS WITH '{value}'";
                                    if (mcex2.Method.Name == "EndsWith")
                                        return $"{varName}.{propName} ENDS WITH '{value}'";
                                    if (mcex2.Method.Name == "Contains")
                                        return $"{varName}.{propName} CONTAINS '{value}'";
                                }
                                throw new NotSupportedException($"Unsupported condition expression: {expr}");
                            }
                            whereClause = $"WHERE {BuildWhere(be)}";
                        }
                    }
                    current = mce.Arguments[0];
                }
                else if (method == "OrderBy" || method == "OrderByDescending")
                {
                    if (mce.Arguments[1] is UnaryExpression ue && ue.Operand is LambdaExpression lambda && lambda.Body is MemberExpression me)
                    {
                        var propName = me.Member.Name;
                        var dir = method == "OrderBy" ? "ASC" : "DESC";
                        if (reverseOrderForLast)
                        {
                            dir = dir == "ASC" ? "DESC" : "ASC";
                            reverseOrderForLast = false;
                        }
                        orderByClause = $"ORDER BY {varName}.{propName} {dir}";
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
                            for (int i = 0; i < ne.Arguments.Count; i++)
                            {
                                var arg = ne.Arguments[i];
                                var member = members[i];
                                string cypherExpr = BuildCypherExpression(arg, varName);
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
                        // TODO: Navigation/deep traversal: detect navigation property and generate MATCH/OPTIONAL MATCH for relationships
                    }
                    current = mce.Arguments[0];
                }
                else if (method == "SelectMany")
                {
                    // TODO: Support for navigation collections (deep traversal)
                    // This will require generating additional MATCH clauses and handling collections in hydration
                    // For now, break
                    break;
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
            if (orderByClause != null) cypher += orderByClause + " ";
            cypher += returnClause;
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

        private async Task<object> ExecuteQueryAsync(string cypher, Type elementType)
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
                                ?? throw new GraphProviderException($"{navConvertToGraphEntityMethodName} method not found");
                            var navConvertToGraphEntity = navMethod.MakeGenericMethod(param.ParameterType);
                            args[i] = navConvertToGraphEntity.Invoke(null, new object[] { nodeVal });
                        }
                        else
                        {
                            args[i] = Convert.ChangeType(value, param.ParameterType);
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
                ?? throw new GraphProviderException($"{convertToGraphEntityMethodName} method not found");
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

        private static string GetLabel(Type type)
        {
            var getLabelMethodName = nameof(Neo4jGraphProvider.GetLabel);
            var method = typeof(Neo4jGraphProvider).GetMethod(
                    getLabelMethodName,
                    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                ?? throw new InvalidOperationException($"{getLabelMethodName} method not found");
            return (string)method.Invoke(null, [type])!;
        }

        // Helper for string concatenation in Cypher
        private static string BuildCypherConcat(Expression expr, string varName)
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

        // Helper for mapping .NET string methods to Cypher
        private static string? TryMapMethodCallToCypher(MethodCallExpression mcex, string varName)
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

        // Recursively build Cypher expressions for projections, supporting all Neo4j functions and computed expressions
        private static string BuildCypherExpression(Expression expr, string varName)
        {
            switch (expr)
            {
                case MemberExpression me:
                    return $"{varName}.{me.Member.Name}";
                case ConstantExpression ce:
                    return ce.Type == typeof(string) ? $"'{ce.Value}'" : ce.Value?.ToString() ?? "null";
                case BinaryExpression bin:
                    var left = BuildCypherExpression(bin.Left, varName);
                    var right = BuildCypherExpression(bin.Right, varName);
                    var op = bin.NodeType switch
                    {
                        ExpressionType.Add => bin.Type == typeof(string) ? "+" : "+",
                        ExpressionType.Subtract => "-",
                        ExpressionType.Multiply => "*",
                        ExpressionType.Divide => "/",
                        ExpressionType.Modulo => "%",
                        ExpressionType.AndAlso => "AND",
                        ExpressionType.OrElse => "OR",
                        ExpressionType.Equal => "=",
                        ExpressionType.NotEqual => "!=",
                        ExpressionType.GreaterThan => ">",
                        ExpressionType.LessThan => "<",
                        ExpressionType.GreaterThanOrEqual => ">=",
                        ExpressionType.LessThanOrEqual => "<=",
                        _ => throw new NotSupportedException($"Operator {bin.NodeType} not supported in projection")
                    };
                    return $"({left} {op} {right})";
                case ConditionalExpression cond:
                    var test = BuildCypherExpression(cond.Test, varName);
                    var ifTrue = BuildCypherExpression(cond.IfTrue, varName);
                    var ifFalse = BuildCypherExpression(cond.IfFalse, varName);
                    return $"CASE WHEN {test} THEN {ifTrue} ELSE {ifFalse} END";
                case MethodCallExpression mcex:
                    var mapped = TryMapMethodCallToCypherFull(mcex, varName);
                    if (mapped != null) return mapped;
                    // Special case: .Trim() with no arguments (should be trim(expr) not trim(expr, ))
                    if (mcex.Method.Name == "Trim" && mcex.Arguments.Count == 0 && mcex.Object != null)
                    {
                        var objExpr = BuildCypherExpression(mcex.Object, varName);
                        return $"trim({objExpr})";
                    }
                    // Fallback: try to render as function call
                    var args = string.Join(", ", mcex.Arguments.Select(a => BuildCypherExpression(a, varName)));
                    var obj = mcex.Object != null ? BuildCypherExpression(mcex.Object, varName) + (args.Length > 0 ? ", " : "") : "";
                    return $"{mcex.Method.Name.ToLower()}({obj}{args})";
                case UnaryExpression ue:
                    var operand = BuildCypherExpression(ue.Operand, varName);
                    if (ue.NodeType == ExpressionType.Negate) return $"(-{operand})";
                    if (ue.NodeType == ExpressionType.Not) return $"NOT {operand}";
                    return operand;
                default:
                    return "''"; // fallback
            }
        }

        // Map .NET method calls and property accesses to Cypher functions (full mapping for common Neo4j functions)
        private static string? TryMapMethodCallToCypherFull(MethodCallExpression mcex, string varName)
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

        // Helper: check if a MemberExpression is a property of the lambda parameter
        static bool IsParameterOrPropertyOfLambda(MemberExpression me, ParameterExpression lambdaParam)
        {
            if (me.Expression is ParameterExpression pe && pe == lambdaParam)
                return true;
            // Support for nested property (e.g., r.Foo.Bar)
            if (me.Expression is MemberExpression innerMe)
                return IsParameterOrPropertyOfLambda(innerMe, lambdaParam);
            return false;
        }

        // Helper: evaluate an expression to a value (for captured variables/constants)
        static object? EvaluateExpression(Expression expr)
        {
            if (expr is ConstantExpression ce) return ce.Value;
            try { return Expression.Lambda(expr).Compile().DynamicInvoke(); } catch { return null; }
        }
    }
}
