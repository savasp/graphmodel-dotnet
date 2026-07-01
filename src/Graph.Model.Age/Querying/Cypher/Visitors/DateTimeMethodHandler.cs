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

namespace Cvoya.Graph.Model.Age.Querying.Cypher.Visitors;

using System;
using System.Linq.Expressions;

/// <summary>
/// Handles translation of .NET DateTime members to Cypher expressions for AGE.
/// </summary>
internal sealed class DateTimeMethodHandler
{
    private readonly Func<Expression, string> _visitAndReturnCypher;
    private readonly Func<object?, string> _addParameter;

    public DateTimeMethodHandler(
        Func<Expression, string> visitAndReturnCypher,
        Func<object?, string> addParameter)
    {
        _visitAndReturnCypher = visitAndReturnCypher;
        _addParameter = addParameter;
    }

    public Expression HandleDateTimeMethod(MethodCallExpression node)
    {
        // Handle instance methods on DateTime objects (e.g., date.AddDays(7))
        if (node.Object != null)
        {
            try
            {
                var objectMember = Expression.Convert(node, typeof(object));
                var getterLambda = Expression.Lambda<Func<object>>(objectMember);
                var getter = getterLambda.Compile();
                var value = getter();

                var paramRef = _addParameter(value);
                return Expression.Constant(paramRef);
            }
            catch
            {
                throw new NotSupportedException($"DateTime method {node.Method.Name} is not supported");
            }
        }

        // Handle static methods on DateTime type
        return node.Method.Name switch
        {
            "get_Now" when node.Arguments.Count == 0 =>
                Expression.Constant("localdatetime()"),

            "get_Today" when node.Arguments.Count == 0 =>
                Expression.Constant("date()"),

            "get_UtcNow" when node.Arguments.Count == 0 =>
                Expression.Constant("datetime()"),

            _ => throw new NotSupportedException($"DateTime method {node.Method.Name} is not supported")
        };
    }
}
