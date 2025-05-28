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
using Cvoya.Graph.Model;

namespace Cvoya.Graph.Provider.Neo4j.Linq;

/// <summary>
/// Provides graph-specific LINQ extension methods for querying graph data.
/// </summary>
public static class GraphQueryExtensions
{
    /// <summary>
    /// Traverses outgoing relationships from nodes.
    /// </summary>
    public static IGraphTraversal<TNode, TRelationship> Traverse<TNode, TRelationship>(
        this IQueryable<TNode> source,
        Expression<Func<TNode, bool>>? nodeFilter = null)
        where TNode : class, INode, new()
        where TRelationship : class, IRelationship, new()
    {
        return new GraphTraversal<TNode, TRelationship>(source, TraversalDirection.Outgoing, nodeFilter);
    }

    /// <summary>
    /// Traverses incoming relationships to nodes.
    /// </summary>
    public static IGraphTraversal<TNode, TRelationship> TraverseFrom<TNode, TRelationship>(
        this IQueryable<TNode> source,
        Expression<Func<TNode, bool>>? nodeFilter = null)
        where TNode : class, INode, new()
        where TRelationship : class, IRelationship, new()
    {
        return new GraphTraversal<TNode, TRelationship>(source, TraversalDirection.Incoming, nodeFilter);
    }

    /// <summary>
    /// Traverses relationships in any direction.
    /// </summary>
    public static IGraphTraversal<TNode, TRelationship> TraverseAny<TNode, TRelationship>(
        this IQueryable<TNode> source,
        Expression<Func<TNode, bool>>? nodeFilter = null)
        where TNode : class, INode, new()
        where TRelationship : class, IRelationship, new()
    {
        return new GraphTraversal<TNode, TRelationship>(source, TraversalDirection.Both, nodeFilter);
    }

    /// <summary>
    /// Finds nodes connected by a specific relationship type.
    /// </summary>
    public static IQueryable<TTargetNode> ConnectedBy<TNode, TRelationship, TTargetNode>(
            this IQueryable<TNode> source,
            Expression<Func<TRelationship, bool>>? relationshipFilter = null)
            where TNode : class, INode, new()
            where TRelationship : class, IRelationship, new()
            where TTargetNode : class, INode, new()
    {
        var provider = (source.Provider as Neo4jQueryProvider)
            ?? throw new InvalidOperationException("Query provider must be Neo4jQueryProvider");

        // Create the method call expression properly
        var expression = Expression.Call(
            null,
            ConnectedByMethod.MakeGenericMethod(typeof(TNode), typeof(TRelationship), typeof(TTargetNode)),
            source.Expression,
            Expression.Quote(relationshipFilter ?? Expression.Lambda<Func<TRelationship, bool>>(
                Expression.Constant(true),
                Expression.Parameter(typeof(TRelationship), "r")))
        );

        return provider.CreateQuery<TTargetNode>(expression);
    }
    /// <summary>
    /// Expands a node query to include related entities.
    /// </summary>
    public static IGraphExpansion<TNode> Expand<TNode>(this IQueryable<TNode> source)
        where TNode : class, INode, new()
    {
        return new GraphExpansion<TNode>(source);
    }

    /// <summary>
    /// Performs a pattern match in the graph.
    /// </summary>
    public static IGraphPattern<TNode> Match<TNode>(this IQueryable<TNode> source, string pattern)
        where TNode : class, INode, new()
    {
        return new GraphPattern<TNode>(source, pattern);
    }

    /// <summary>
    /// Finds shortest path between nodes.
    /// </summary>
    public static IQueryable<GraphPath<TNode>> ShortestPath<TNode>(
        this IQueryable<TNode> source,
        Expression<Func<TNode, bool>> targetFilter,
        int? maxHops = null)
        where TNode : class, INode, new()
    {
        var provider = (source.Provider as Neo4jQueryProvider)
            ?? throw new InvalidOperationException("Query provider must be Neo4jQueryProvider");

        var expression = Expression.Call(
            null,
            ShortestPathMethod.MakeGenericMethod(typeof(TNode)),
            source.Expression,
            Expression.Quote(targetFilter),
            Expression.Constant(maxHops, typeof(int?))
        );

        return provider.CreateQuery<GraphPath<TNode>>(expression);
    }

    // Method info for reflection
    private static readonly System.Reflection.MethodInfo ConnectedByMethod =
        typeof(GraphQueryExtensions).GetMethod(nameof(ConnectedBy))!;

    private static readonly System.Reflection.MethodInfo ShortestPathMethod =
        typeof(GraphQueryExtensions).GetMethod(nameof(ShortestPath))!;
}

/// <summary>
/// Represents the direction of a graph traversal.
/// </summary>
public enum TraversalDirection
{
    Outgoing,
    Incoming,
    Both
}