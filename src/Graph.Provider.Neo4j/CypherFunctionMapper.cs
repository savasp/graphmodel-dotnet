using System;
using System.Linq.Expressions;

namespace Cvoya.Graph.Client.Neo4j
{
    // Maps C# methods to Cypher functions
    internal static class CypherFunctionMapper
    {
        public static string? TryMapMethodCall(MethodCallExpression mce)
        {
            // String functions
            if (mce.Method.Name == "Contains" && mce.Object != null)
                return $"{GetCypherExpr(mce.Object)} CONTAINS {GetCypherExpr(mce.Arguments[0])}";
            if (mce.Method.Name == "StartsWith" && mce.Object != null)
                return $"{GetCypherExpr(mce.Object)} STARTS WITH {GetCypherExpr(mce.Arguments[0])}";
            if (mce.Method.Name == "EndsWith" && mce.Object != null)
                return $"{GetCypherExpr(mce.Object)} ENDS WITH {GetCypherExpr(mce.Arguments[0])}";
            if (mce.Method.Name == "ToLower" && mce.Object != null)
                return $"toLower({GetCypherExpr(mce.Object)})";
            if (mce.Method.Name == "ToUpper" && mce.Object != null)
                return $"toUpper({GetCypherExpr(mce.Object)})";
            if (mce.Method.Name == "Substring" && mce.Object != null)
                return $"substring({GetCypherExpr(mce.Object)}, {GetCypherExpr(mce.Arguments[0])}, {GetCypherExpr(mce.Arguments[1])})";
            // Math functions
            if (mce.Method.Name == "Abs")
                return $"abs({GetCypherExpr(mce.Arguments[0])})";
            if (mce.Method.Name == "Round")
                return $"round({GetCypherExpr(mce.Arguments[0])})";
            if (mce.Method.Name == "Ceiling")
                return $"ceil({GetCypherExpr(mce.Arguments[0])})";
            if (mce.Method.Name == "Floor")
                return $"floor({GetCypherExpr(mce.Arguments[0])})";
            // Date/time functions
            if (mce.Method.Name == "Now" && mce.Method.DeclaringType?.Name == "DateTime")
                return "datetime()";
            // Coalesce
            if (mce.Method.Name == "Coalesce")
                return $"coalesce({string.Join(", ", mce.Arguments.Select(GetCypherExpr))})";
            // Labels
            if (mce.Method.Name == "Labels")
                return $"labels({GetCypherExpr(mce.Arguments[0])})";
            // Length (for collections/paths)
            if (mce.Method.Name == "Count" && mce.Arguments.Count == 1)
                return $"size({GetCypherExpr(mce.Arguments[0])})";
            // Add more as needed
            return null;
        }

        private static string GetCypherExpr(Expression expr)
        {
            if (expr is MemberExpression me && me.Expression is ParameterExpression)
                return $"n.{me.Member.Name}";
            if (expr is ConstantExpression ce)
                return ce.Value is string ? $"'{ce.Value}'" : ce.Value?.ToString() ?? "null";
            // Fallback: call ToString
            return expr.ToString();
        }
    }
}
