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

namespace Cvoya.Graph.Client.Neo4j;

internal static class CypherExpressionTranslator
{
    public static string Translate(Expression expression, Type elementType)
    {
        try
        {
            if (expression is MethodCallExpression mce)
            {
                var cypher = string.Empty;
                var label = elementType.FullName ?? elementType.Name;
                var whereClauses = new List<string>();
                var orderByClauses = new List<string>();
                string selectClause = "n";
                var limitClause = string.Empty;
                var skipClause = string.Empty;

                Expression source = mce;
                while (source is MethodCallExpression call)
                {
                    if (call.Method.Name == "Where")
                    {
                        var lambda = (LambdaExpression)((UnaryExpression)call.Arguments[1]).Operand;
                        var clause = ParseWhere(lambda.Body);
                        whereClauses.Add(clause);
                        source = call.Arguments[0];
                    }
                    else if (call.Method.Name == "Take")
                    {
                        var takeCount = (int)((ConstantExpression)call.Arguments[1]).Value!;
                        limitClause = $"LIMIT {takeCount}";
                        source = call.Arguments[0];
                    }
                    else if (call.Method.Name == "Skip")
                    {
                        var skipCount = (int)((ConstantExpression)call.Arguments[1]).Value!;
                        skipClause = $"SKIP {skipCount}";
                        source = call.Arguments[0];
                    }
                    else if (call.Method.Name == "Select")
                    {
                        var lambda = (LambdaExpression)((UnaryExpression)call.Arguments[1]).Operand;
                        if (lambda.Body is MemberExpression member)
                        {
                            selectClause = $"n.{member.Member.Name}";
                        }
                        else if (lambda.Body is NewExpression newExpr)
                        {
                            // Anonymous type or tuple: new { n.Prop1, n.Prop2 }
                            var fields = newExpr.Arguments
                                .Select(arg =>
                                {
                                    if (arg is MemberExpression mem)
                                        return $"n.{mem.Member.Name} AS {mem.Member.Name}";
                                    return arg.ToString();
                                });
                            selectClause = string.Join(", ", fields);
                        }
                        else if (lambda.Body is ParameterExpression)
                        {
                            selectClause = "n";
                        }
                        else
                        {
                            throw new NotSupportedException("Only identity, single property, or anonymous type Select supported.");
                        }
                        source = call.Arguments[0];
                    }
                    else if (call.Method.Name == "OrderBy" || call.Method.Name == "OrderByDescending" || call.Method.Name == "ThenBy" || call.Method.Name == "ThenByDescending")
                    {
                        var lambda = (LambdaExpression)((UnaryExpression)call.Arguments[1]).Operand;
                        if (lambda.Body is MemberExpression member)
                        {
                            var dir = (call.Method.Name == "OrderBy" || call.Method.Name == "ThenBy") ? "ASC" : "DESC";
                            orderByClauses.Add($"n.{member.Member.Name} {dir}");
                        }
                        source = call.Arguments[0];
                    }
                    else
                    {
                        source = call.Arguments[0];
                    }
                }
                // Compose Cypher
                whereClauses.Reverse();
                var where = whereClauses.Count > 0 ? $"WHERE {string.Join(" AND ", whereClauses)}" : string.Empty;
                var orderBy = orderByClauses.Count > 0 ? $"ORDER BY {string.Join(", ", orderByClauses)}" : string.Empty;
                var cypherQuery = $"MATCH (n:`{label}`) {where} RETURN {selectClause} {orderBy} {skipClause} {limitClause}".Trim();
                return cypherQuery;
            }
            // If not a supported method, just return all
            var labelAll = elementType.FullName ?? elementType.Name;
            return $"MATCH (n:`{labelAll}`) RETURN n";
        }
        catch (Exception ex)
        {
            DiagnosticsHelper.ReportUnsupported(
                "Translate LINQ to Cypher",
                ex,
                "Check if your LINQ query uses only supported patterns. For unsupported queries, use ExecuteCypher for raw Cypher."
            );
            throw;
        }
    }

    public static (string cypher, Dictionary<string, object?> parameters) TranslateWithParameters(Expression expression, Type elementType)
    {
        var parameters = new Dictionary<string, object?>();
        string ParamName(string baseName)
        {
            var name = baseName;
            int i = 1;
            while (parameters.ContainsKey(name))
                name = baseName + (++i);
            return name;
        }
        string ParseWhereWithParams(Expression expr)
        {
            if (expr is BinaryExpression be)
            {
                // Support AndAlso/OrElse
                if (be.NodeType == ExpressionType.AndAlso || be.NodeType == ExpressionType.OrElse)
                {
                    var left = ParseWhereWithParams(be.Left);
                    var right = ParseWhereWithParams(be.Right);
                    var op = be.NodeType == ExpressionType.AndAlso ? "AND" : "OR";
                    return $"({left} {op} {right})";
                }
                if (be.Right is ConstantExpression ce)
                {
                    var paramName = ParamName(be.Left is MemberExpression me ? me.Member.Name : "param");
                    parameters[paramName] = ce.Value;
                    var left = ParseWhereWithParams(be.Left);
                    var op = be.NodeType switch
                    {
                        ExpressionType.Equal => "=",
                        ExpressionType.NotEqual => "<>",
                        ExpressionType.GreaterThan => ">",
                        ExpressionType.GreaterThanOrEqual => ">=",
                        ExpressionType.LessThan => "<",
                        ExpressionType.LessThanOrEqual => "<=",
                        _ => throw new NotSupportedException()
                    };
                    return $"{left} {op} ${paramName}";
                }
            }
            if (expr is MethodCallExpression mce)
            {
                var mapped = CypherFunctionMapper.TryMapMethodCall(mce);
                if (mapped != null)
                {
                    // Try to parameterize the argument if it's a constant
                    if (mce.Arguments.Count > 0 && mce.Arguments[0] is ConstantExpression ce)
                    {
                        var paramName = ParamName("param");
                        parameters[paramName] = ce.Value;
                        return mapped.Replace($"'{ce.Value}'", $"${paramName}");
                    }
                    return mapped;
                }
                return mce.ToString();
            }
            if (expr is MemberExpression me2 && me2.Expression is ParameterExpression)
            {
                return $"n.{me2.Member.Name}";
            }
            if (expr is ConstantExpression ce2)
            {
                var paramName = ParamName("const");
                parameters[paramName] = ce2.Value;
                return $"${paramName}";
            }
            if (expr is UnaryExpression ue && ue.NodeType == ExpressionType.Convert)
            {
                return ParseWhereWithParams(ue.Operand);
            }
            throw new NotSupportedException($"Unsupported expression: {expr}");
        }
        // Only support simple Where for now
        if (expression is MethodCallExpression mce && mce.Method.Name == "Where")
        {
            var label = elementType.FullName ?? elementType.Name;
            var lambda = (LambdaExpression)((UnaryExpression)mce.Arguments[1]).Operand;
            var where = ParseWhereWithParams(lambda.Body);
            var cypher = $"MATCH (n:`{label}`) WHERE {where} RETURN n";
            return (cypher, parameters);
        }
        // Fallback
        var labelAll = elementType.FullName ?? elementType.Name;
        return ($"MATCH (n:`{labelAll}`) RETURN n", parameters);
    }

    public static string TranslateWithDeepRelationshipFilter(Expression expression, Type elementType, DeepRelationshipFilterHelper.DeepRelationshipFilterInfo filterInfo)
    {
        var label = elementType.FullName ?? elementType.Name;
        var relProp = filterInfo.RelationshipProperty;
        var relatedNodeProp = filterInfo.RelatedNodeProperty;
        var value = filterInfo.Value;
        var comparison = filterInfo.ComparisonType;

        // Support for Count-based filtering
        if (filterInfo is { CountComparison: true })
        {
            // e.g. p.BlogPosts.Count(bp => bp.Target.CreatedAt > cutoff) > 2
            string op = comparison switch
            {
                ExpressionType.GreaterThan => ">",
                ExpressionType.GreaterThanOrEqual => ">=",
                ExpressionType.LessThan => "<",
                ExpressionType.LessThanOrEqual => "<=",
                ExpressionType.Equal => "=",
                ExpressionType.NotEqual => "<>",
                _ => throw new NotSupportedException($"Unsupported comparison: {comparison}")
            };
            string valueStr = value is string ? $"'{value}'" : value?.ToString() ?? "null";
            // Cypher: MATCH (n:`Person`)-[r:`BlogPosts`]->(t) WHERE COUNT {t WHERE t.CreatedAt > ...} > 2 RETURN n
            // For now, simple pattern:
            var cypher = $"MATCH (n:`{label}`)-[r:`{relProp}`]->(t) WHERE COUNT(t) {op} {valueStr} RETURN n";
            return cypher;
        }

        string op2 = comparison switch
        {
            ExpressionType.GreaterThan => ">",
            ExpressionType.GreaterThanOrEqual => ">=",
            ExpressionType.LessThan => "<",
            ExpressionType.LessThanOrEqual => "<=",
            ExpressionType.Equal => "=",
            ExpressionType.NotEqual => "<>",
            _ => throw new NotSupportedException($"Unsupported comparison: {comparison}")
        };
        string valueStr2 = value is string ? $"'{value}'" : value?.ToString() ?? "null";
        var cypher2 = $"MATCH (n:`{label}`)-[r:`{relProp}`]->(t) WHERE t.{relatedNodeProp} {op2} {valueStr2} RETURN n";
        return cypher2;
    }

    // Helper to parse Where body (supports ==, !=, >, >=, <, <=, &&, ||)
    public static string ParseWhere(Expression expr)
    {
        try
        {
            if (expr is MethodCallExpression mce)
            {
                // Handle collection.Contains(x) as x IN collection
                if (mce.Method.Name == "Contains" && mce.Object == null && mce.Arguments.Count == 2)
                {
                    var collection = mce.Arguments[0];
                    var value = mce.Arguments[1];
                    return $"{ParseWhere(value)} IN {ParseWhere(collection)}";
                }
                var mapped = CypherFunctionMapper.TryMapMethodCall(mce);
                if (mapped != null) return mapped;
                return mce.ToString();
            }
            if (expr is BinaryExpression be)
            {
                // Null checks
                if (be.Right is ConstantExpression ceNull && ceNull.Value == null)
                {
                    if (be.NodeType == ExpressionType.Equal)
                        return $"{ParseWhere(be.Left)} IS NULL";
                    if (be.NodeType == ExpressionType.NotEqual)
                        return $"{ParseWhere(be.Left)} IS NOT NULL";
                }
                var left = ParseWhere(be.Left);
                var right = ParseWhere(be.Right);
                switch (be.NodeType)
                {
                    case ExpressionType.Equal:
                        return $"{left} = {right}";
                    case ExpressionType.NotEqual:
                        return $"{left} <> {right}";
                    case ExpressionType.GreaterThan:
                        return $"{left} > {right}";
                    case ExpressionType.GreaterThanOrEqual:
                        return $"{left} >= {right}";
                    case ExpressionType.LessThan:
                        return $"{left} < {right}";
                    case ExpressionType.LessThanOrEqual:
                        return $"{left} <= {right}";
                    case ExpressionType.AndAlso:
                        return $"({left} AND {right})";
                    case ExpressionType.OrElse:
                        return $"({left} OR {right})";
                    default:
                        throw new NotSupportedException($"Unsupported binary operator: {be.NodeType}");
                }
            }
            if (expr is MemberExpression me && me.Expression is ParameterExpression)
            {
                return $"n.{me.Member.Name}";
            }
            if (expr is ConstantExpression ce)
            {
                if (ce.Value is string)
                    return $"'{ce.Value}'";
                if (ce.Value is bool b)
                    return b ? "true" : "false";
                return ce.Value?.ToString() ?? "null";
            }
            if (expr is UnaryExpression ue && ue.NodeType == ExpressionType.Convert)
            {
                return ParseWhere(ue.Operand);
            }
            throw new NotSupportedException($"Unsupported expression: {expr}");
        }
        catch (Exception ex)
        {
            DiagnosticsHelper.ReportUnsupported(
                "ParseWhere",
                ex,
                "Check if your LINQ expression uses only supported operators and method calls. For advanced filtering, consider using raw Cypher."
            );
            throw;
        }
    }

    // Helper: Build relationship pattern for variable-length and bidirectional
    private static string BuildRelationshipPattern(string relType, int? minHops = null, int? maxHops = null, bool bidirectional = false)
    {
        string length = (minHops.HasValue || maxHops.HasValue)
            ? $"*{minHops?.ToString() ?? ""}..{maxHops?.ToString() ?? ""}".TrimEnd('.')
            : string.Empty;
        string arrow = bidirectional ? "-" : ">";
        return $"-[r:`{relType}`{length}]-{arrow}";
    }

    // Example usage in translation (pseudo):
    // var relPattern = BuildRelationshipPattern("FRIEND", 1, 3, true); // -[r:`FRIEND`*1..3]-
    // var cypher = $"MATCH (n){relPattern}(t) ..."
    //
    // TODO: Integrate this into Translate/TranslateWithDeepRelationshipFilter when variable-length or bidirectional is detected in the expression tree.
    //
    // For now, this is a utility for future steps.
}
