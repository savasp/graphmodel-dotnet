using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Cvoya.Graph.Client.Neo4j
{
    // Helper to extract deep relationship filter info from a LINQ expression
    internal static class DeepRelationshipFilterHelper
    {
        public class DeepRelationshipFilterInfo
        {
            public string RelationshipProperty { get; set; } = string.Empty;
            public string RelatedNodeProperty { get; set; } = string.Empty;
            public object? Value { get; set; }
            public ExpressionType ComparisonType { get; set; }
            public bool CountComparison { get; set; } = false;
        }

        // Supports: .Any(predicate), .Where(predicate).Any(), .Count(predicate), .All(predicate), .FirstOrDefault(predicate)
        public static DeepRelationshipFilterInfo? ExtractDeepRelationshipFilter(Expression expr, Type rootType)
        {
            // Handle .Where(...).Any(...)
            if (expr is MethodCallExpression mce)
            {
                // .Where(...).Any(...)
                if (mce.Method.Name == "Any" && mce.Arguments.Count == 1 && mce.Arguments[0] is UnaryExpression anyUnary)
                {
                    // Check for .Where(...)
                    if (mce.Object is MethodCallExpression whereCall && whereCall.Method.Name == "Where")
                    {
                        var relProp = (whereCall.Arguments[0] as MemberExpression)?.Member.Name;
                        var innerLambda = (whereCall.Arguments[1] as UnaryExpression)?.Operand as LambdaExpression;
                        if (relProp != null && innerLambda?.Body is BinaryExpression be)
                        {
                            var filter = ExtractFromBinary(relProp, be);
                            if (filter != null) return filter;
                        }
                    }
                }
                // .Any(predicate)
                if (mce.Method.Name == "Any" && mce.Arguments.Count == 1)
                {
                    var relProp = (mce.Object as MemberExpression)?.Member.Name;
                    var innerLambda = (mce.Arguments[0] as UnaryExpression)?.Operand as LambdaExpression;
                    if (relProp != null && innerLambda?.Body is BinaryExpression be)
                    {
                        var filter = ExtractFromBinary(relProp, be);
                        if (filter != null) return filter;
                    }
                }
                // .Count(predicate)
                if (mce.Method.Name == "Count" && mce.Arguments.Count == 1)
                {
                    var relProp = (mce.Object as MemberExpression)?.Member.Name;
                    var innerLambda = (mce.Arguments[0] as UnaryExpression)?.Operand as LambdaExpression;
                    if (relProp != null && innerLambda?.Body is BinaryExpression be)
                    {
                        var filter = ExtractFromBinary(relProp, be);
                        if (filter != null)
                        {
                            filter.CountComparison = true;
                            return filter;
                        }
                    }
                }
                // .All(predicate)
                if (mce.Method.Name == "All" && mce.Arguments.Count == 1)
                {
                    var relProp = (mce.Object as MemberExpression)?.Member.Name;
                    var innerLambda = (mce.Arguments[0] as UnaryExpression)?.Operand as LambdaExpression;
                    if (relProp != null && innerLambda?.Body is BinaryExpression be)
                    {
                        var filter = ExtractFromBinary(relProp, be);
                        if (filter != null) return filter;
                    }
                }
                // .FirstOrDefault(predicate)
                if (mce.Method.Name == "FirstOrDefault" && mce.Arguments.Count == 1)
                {
                    var relProp = (mce.Object as MemberExpression)?.Member.Name;
                    var innerLambda = (mce.Arguments[0] as UnaryExpression)?.Operand as LambdaExpression;
                    if (relProp != null && innerLambda?.Body is BinaryExpression be)
                    {
                        var filter = ExtractFromBinary(relProp, be);
                        if (filter != null) return filter;
                    }
                }
                // Fallback: .Where(...)
                if (mce.Method.Name == "Where" && mce.Arguments.Count == 2)
                {
                    var relProp = (mce.Arguments[0] as MemberExpression)?.Member.Name;
                    var innerLambda = (mce.Arguments[1] as UnaryExpression)?.Operand as LambdaExpression;
                    if (relProp != null && innerLambda?.Body is BinaryExpression be)
                    {
                        var filter = ExtractFromBinary(relProp, be);
                        if (filter != null) return filter;
                    }
                }
            }
            // Original fallback: .Where(Any(...))
            if (expr is MethodCallExpression mce2 && mce2.Method.Name == "Where")
            {
                var lambda = (mce2.Arguments[1] as UnaryExpression)?.Operand as LambdaExpression;
                if (lambda != null)
                {
                    if (lambda.Body is MethodCallExpression anyCall && anyCall.Method.Name == "Any")
                    {
                        var relProp = (anyCall.Object as MemberExpression)?.Member.Name;
                        if (relProp != null && anyCall.Arguments.Count == 1)
                        {
                            var innerLambda = (anyCall.Arguments[0] as UnaryExpression)?.Operand as LambdaExpression;
                            if (innerLambda?.Body is BinaryExpression be)
                            {
                                var filter = ExtractFromBinary(relProp, be);
                                if (filter != null) return filter;
                            }
                        }
                    }
                }
            }
            return null;
        }

        private static DeepRelationshipFilterInfo? ExtractFromBinary(string relProp, BinaryExpression be)
        {
            // e.g. bp.Target.CreatedAt > someDate
            if (be.Left is MemberExpression left && left.Expression is MemberExpression targetExpr)
            {
                var relatedNodeProp = left.Member.Name;
                var value = (be.Right as ConstantExpression)?.Value;
                return new DeepRelationshipFilterInfo
                {
                    RelationshipProperty = relProp,
                    RelatedNodeProperty = relatedNodeProp,
                    Value = value,
                    ComparisonType = be.NodeType
                };
            }
            return null;
        }
    }
}
