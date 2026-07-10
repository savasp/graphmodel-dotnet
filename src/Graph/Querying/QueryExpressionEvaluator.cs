// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying;

using System.Linq.Expressions;

internal static class QueryExpressionEvaluator
{
    public static T Evaluate<T>(Expression expression, string description)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        if (expression is ConstantExpression { Value: T value })
        {
            return value;
        }

        if (ParameterReferenceDetector.ContainsParameter(expression))
        {
            throw new GraphQueryTranslationException(
                $"Cannot evaluate {description} during graph query translation because it references a query parameter. " +
                "Only parameter-free closure values and method calls are funcletized.");
        }

        var converted = Expression.Convert(expression, typeof(T));
        return Expression.Lambda<Func<T>>(converted).Compile().Invoke();
    }

    private sealed class ParameterReferenceDetector : ExpressionVisitor
    {
        private bool _containsParameter;

        public static bool ContainsParameter(Expression expression)
        {
            var detector = new ParameterReferenceDetector();
            detector.Visit(expression);
            return detector._containsParameter;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            _containsParameter = true;
            return node;
        }
    }
}
