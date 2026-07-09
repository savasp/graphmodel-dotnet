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

namespace Cvoya.Graph.Model.Querying;

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
