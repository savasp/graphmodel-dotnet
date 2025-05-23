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

namespace Cvoya.Graph.Provider.Neo4j.Linq
{
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
                    // Special case: string.Length => size(n.Prop)
                    if (me.Member.Name == "Length" && me.Expression != null && me.Expression.Type == typeof(string))
                    {
                        var inner = BuildCypherExpression(me.Expression, varName);
                        return $"size({inner})";
                    }
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
    }
}
