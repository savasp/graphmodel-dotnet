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

namespace Cvoya.Graph.Model.Neo4j.Querying.Linq.Queryables;

using System.Collections;
using System.Linq.Expressions;
using Cvoya.Graph.Model.Neo4j.Core;
using Cvoya.Graph.Model.Neo4j.Querying.Linq.Visitors;

/// <summary>
/// Represents the context for a graph query execution.
/// </summary>
internal sealed class GraphQueryContext
{
    public GraphQueryContext()
    {
    }

    public GraphQueryContext(Expression expression)
    {
        SourceExpression = expression;
        ResultType = DetermineResultType(expression);
    }

    public Expression? SourceExpression { get; init; }
    public GraphTransaction? Transaction { get; set; }
    public GraphResultType ResultType { get; init; } = GraphResultType.Unknown;

    public static GraphQueryContext FromExpression(Expression expression)
    {
        var context = new GraphQueryContext(expression);

        // Extract transaction if present
        var visitor = new TransactionExtractionVisitor();
        visitor.Visit(expression);

        if (visitor.Transactions.Count > 0)
        {
            context.Transaction = visitor.Transactions.First();
        }

        return context;
    }

    private static GraphResultType DetermineResultType(Expression expression)
    {
        var type = expression.Type;

        // Strip away IQueryable/IEnumerable wrappers to get the actual element type
        if (type.IsGenericType)
        {
            var genericDef = type.GetGenericTypeDefinition();
            if (genericDef == typeof(IQueryable<>) ||
                genericDef == typeof(IEnumerable<>) ||
                genericDef == typeof(IAsyncEnumerable<>))
            {
                type = type.GetGenericArguments()[0];
            }
        }

        // Now check what kind of result we're dealing with
        if (typeof(INode).IsAssignableFrom(type))
            return GraphResultType.Node;

        if (typeof(IRelationship).IsAssignableFrom(type))
            return GraphResultType.Relationship;

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IGraphPathSegment<,,>))
            return GraphResultType.PathSegment;

        // Check for collections
        if (type.IsArray || (type.IsGenericType && typeof(IEnumerable).IsAssignableFrom(type)))
        {
            var elementType = type.IsArray ? type.GetElementType()! : type.GetGenericArguments()[0];

            if (typeof(INode).IsAssignableFrom(elementType))
                return GraphResultType.NodeCollection;

            if (typeof(IRelationship).IsAssignableFrom(elementType))
                return GraphResultType.RelationshipCollection;

            if (elementType.IsGenericType && elementType.GetGenericTypeDefinition() == typeof(IGraphPathSegment<,,>))
                return GraphResultType.PathSegmentCollection;
        }

        return GraphResultType.Scalar;
    }
}

internal enum GraphResultType
{
    Unknown,
    Node,
    NodeCollection,
    Relationship,
    RelationshipCollection,
    PathSegment,
    PathSegmentCollection,
    Scalar
}