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

namespace Cvoya.Graph.Provider.Neo4j.Linq;

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
