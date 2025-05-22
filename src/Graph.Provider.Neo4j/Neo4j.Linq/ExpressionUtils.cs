using System;
using System.Linq.Expressions;

namespace Cvoya.Graph.Provider.Neo4j.Linq
{
    internal static class ExpressionUtils
    {
        public static bool IsParameterOrPropertyOfLambda(MemberExpression me, ParameterExpression lambdaParam)
        {
            if (me.Expression is ParameterExpression pe && pe == lambdaParam)
                return true;
            // Support for nested property (e.g., r.Foo.Bar)
            if (me.Expression is MemberExpression innerMe)
                return IsParameterOrPropertyOfLambda(innerMe, lambdaParam);
            return false;
        }

        public static object? EvaluateExpression(Expression expr)
        {
            if (expr is ConstantExpression ce) return ce.Value;
            try { return Expression.Lambda(expr).Compile().DynamicInvoke(); } catch { return null; }
        }
    }
}
