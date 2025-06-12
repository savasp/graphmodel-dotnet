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

using System.Collections;
using System.Linq.Expressions;
using Cvoya.Graph.Model.Neo4j.Serialization;

namespace Cvoya.Graph.Model.Neo4j.Linq;

/// <summary>
/// Neo4j implementation of graph query execution context
/// </summary>
internal class GraphQueryContext
{
    public enum QueryRootType
    {
        Node,
        Relationship,
        Path,
        Custom
    }

    public QueryRootType RootType { get; set; }
    public GraphTransaction? Transaction { get; set; }
    public bool IsScalarResult { get; set; }
    public bool IsProjection { get; set; }
    public Type? ProjectionType { get; set; }
    public Dictionary<string, object>? Parameters { get; set; }
    public Type? ResultType { get; set; }
    public EntitySchema? TargetEntitySchema { get; set; }

    /// <summary>
    /// Whether to use the most derived type when deserializing entities.
    /// Default is true for polymorphic behavior.
    /// </summary>
    public bool UseMostDerivedType { get; set; } = true;

    public void DetermineResultType(Expression expression)
    {
        // Walk the expression tree to find the final result type
        var resultType = GetResultType(expression);
        ResultType = resultType;

        // Scalar results are primitive types, strings, dates, etc.
        IsScalarResult = IsScalarType(resultType);

        // Projections are anonymous types or DTOs that aren't entities
        IsProjection = !IsScalarResult &&
                      !typeof(INode).IsAssignableFrom(resultType) &&
                      !typeof(IRelationship).IsAssignableFrom(resultType);
    }

    public void DetermineQueryType()
    {
        if (ResultType == null)
        {
            // If we don't know the result type, default to entity
            IsScalarResult = false;
            IsProjection = false;
            return;
        }

        // Check if it's a scalar type
        IsScalarResult = IsScalarType(ResultType);

        // Check if it's a projection (anonymous type or DTO)
        if (!IsScalarResult)
        {
            IsProjection = !typeof(INode).IsAssignableFrom(ResultType) &&
                          !typeof(IRelationship).IsAssignableFrom(ResultType) &&
                          !IsCollectionOfEntities(ResultType);
        }
    }

    private static bool IsScalarType(Type type)
    {
        // Handle nullable types
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

        return GraphDataModel.IsSimple(underlyingType);
    }

    private static bool IsCollectionOfEntities(Type type)
    {
        if (!typeof(IEnumerable).IsAssignableFrom(type) || type == typeof(string))
            return false;

        var elementType = type.IsArray
            ? type.GetElementType()
            : type.GetGenericArguments().FirstOrDefault();

        return elementType != null &&
               (typeof(INode).IsAssignableFrom(elementType) ||
                typeof(IRelationship).IsAssignableFrom(elementType));
    }

    private static Type GetResultType(Expression expression)
    {
        // This needs to walk the expression tree and figure out what the final type is
        return expression switch
        {
            MethodCallExpression methodCall => GetMethodResultType(methodCall),
            MemberExpression member => member.Type,
            ConstantExpression constant => constant.Type,
            _ => expression.Type
        };
    }

    private static Type GetMethodResultType(MethodCallExpression methodCall)
    {
        // For LINQ methods, we need to check what they return
        return methodCall.Method.Name switch
        {
            "Count" or "LongCount" => typeof(int),
            "Any" or "All" => typeof(bool),
            "Sum" or "Average" => methodCall.Method.ReturnType,
            "Min" or "Max" => methodCall.Method.ReturnType,
            "First" or "FirstOrDefault" or "Last" or "LastOrDefault" or "Single" or "SingleOrDefault"
                => methodCall.Method.ReturnType,
            _ => methodCall.Method.ReturnType
        };
    }
}