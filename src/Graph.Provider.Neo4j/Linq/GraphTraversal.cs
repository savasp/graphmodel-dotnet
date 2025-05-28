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

internal class GraphTraversal<TNode, TRelationship> : IGraphTraversal<TNode, TRelationship>
    where TNode : class, INode, new()
    where TRelationship : class, IRelationship, new()
{
    private readonly IQueryable<TNode> _source;
    private readonly TraversalDirection _direction;
    private readonly Expression<Func<TNode, bool>>? _nodeFilter;
    private Expression<Func<TRelationship, bool>>? _relationshipFilter;
    private int _minDepth = 1;
    private int _maxDepth = 1;

    public GraphTraversal(
        IQueryable<TNode> source,
        TraversalDirection direction,
        Expression<Func<TNode, bool>>? nodeFilter = null)
    {
        _source = source;
        _direction = direction;
        _nodeFilter = nodeFilter;
    }

    public IGraphTraversal<TNode, TRelationship> Where(Expression<Func<TRelationship, bool>> predicate)
    {
        _relationshipFilter = _relationshipFilter == null
            ? predicate
            : Expression.Lambda<Func<TRelationship, bool>>(
                Expression.AndAlso(_relationshipFilter.Body, predicate.Body),
                predicate.Parameters[0]);
        return this;
    }

    public IQueryable<TTargetNode> To<TTargetNode>() where TTargetNode : class, INode, new()
    {
        return To<TTargetNode>(null);
    }

    public IQueryable<TTargetNode> To<TTargetNode>(Expression<Func<TTargetNode, bool>>? targetFilter)
        where TTargetNode : class, INode, new()
    {
        var provider = (_source.Provider as Neo4jQueryProvider)
            ?? throw new InvalidOperationException("Query provider must be Neo4jQueryProvider");

        // Build the traversal expression
        var expression = Expression.Call(
            null,
            TraversalToMethod.MakeGenericMethod(typeof(TNode), typeof(TRelationship), typeof(TTargetNode)),
            _source.Expression,
            Expression.Constant(_direction),
            Expression.Constant(_nodeFilter, typeof(Expression<Func<TNode, bool>>)),
            Expression.Constant(_relationshipFilter, typeof(Expression<Func<TRelationship, bool>>)),
            Expression.Constant(targetFilter, typeof(Expression<Func<TTargetNode, bool>>)),
            Expression.Constant(_minDepth),
            Expression.Constant(_maxDepth)
        );

        return provider.CreateQuery<TTargetNode>(expression);
    }

    public IQueryable<TRelationship> Relationships()
    {
        var provider = (_source.Provider as Neo4jQueryProvider)
            ?? throw new InvalidOperationException("Query provider must be Neo4jQueryProvider");

        var expression = Expression.Call(
            null,
            TraversalRelationshipsMethod.MakeGenericMethod(typeof(TNode), typeof(TRelationship)),
            _source.Expression,
            Expression.Constant(_direction),
            Expression.Constant(_nodeFilter, typeof(Expression<Func<TNode, bool>>)),
            Expression.Constant(_relationshipFilter, typeof(Expression<Func<TRelationship, bool>>)),
            Expression.Constant(_minDepth),
            Expression.Constant(_maxDepth)
        );

        return provider.CreateQuery<TRelationship>(expression);
    }

    public IQueryable<GraphPath<TNode>> Paths()
    {
        var provider = (_source.Provider as Neo4jQueryProvider)
            ?? throw new InvalidOperationException("Query provider must be Neo4jQueryProvider");

        var expression = Expression.Call(
            null,
            TraversalPathsMethod.MakeGenericMethod(typeof(TNode), typeof(TRelationship)),
            _source.Expression,
            Expression.Constant(_direction),
            Expression.Constant(_nodeFilter, typeof(Expression<Func<TNode, bool>>)),
            Expression.Constant(_relationshipFilter, typeof(Expression<Func<TRelationship, bool>>)),
            Expression.Constant(_minDepth),
            Expression.Constant(_maxDepth)
        );

        return provider.CreateQuery<GraphPath<TNode>>(expression);
    }

    public IGraphTraversal<TNode, TRelationship> WithDepth(int minDepth, int maxDepth)
    {
        if (minDepth < 0) throw new ArgumentException("Minimum depth must be non-negative", nameof(minDepth));
        if (maxDepth < minDepth) throw new ArgumentException("Maximum depth must be greater than or equal to minimum depth", nameof(maxDepth));

        _minDepth = minDepth;
        _maxDepth = maxDepth;
        return this;
    }

    // Static methods for expression tree building
    private static readonly System.Reflection.MethodInfo TraversalToMethod =
        typeof(GraphTraversal<TNode, TRelationship>).GetMethod(nameof(TraversalToInternal),
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

    private static readonly System.Reflection.MethodInfo TraversalRelationshipsMethod =
        typeof(GraphTraversal<TNode, TRelationship>).GetMethod(nameof(TraversalRelationshipsInternal),
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

    private static readonly System.Reflection.MethodInfo TraversalPathsMethod =
        typeof(GraphTraversal<TNode, TRelationship>).GetMethod(nameof(TraversalPathsInternal),
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

    // Internal methods that will be called via expression trees
    private static IQueryable<TTargetNode> TraversalToInternal<TSourceNode, TRel, TTargetNode>(
        IQueryable<TSourceNode> source,
        TraversalDirection direction,
        Expression<Func<TSourceNode, bool>>? nodeFilter,
        Expression<Func<TRel, bool>>? relationshipFilter,
        Expression<Func<TTargetNode, bool>>? targetFilter,
        int minDepth,
        int maxDepth)
        where TSourceNode : class, INode, new()
        where TRel : class, IRelationship, new()
        where TTargetNode : class, INode, new()
    {
        throw new NotImplementedException("This method should not be called directly");
    }

    private static IQueryable<TRel> TraversalRelationshipsInternal<TSourceNode, TRel>(
        IQueryable<TSourceNode> source,
        TraversalDirection direction,
        Expression<Func<TSourceNode, bool>>? nodeFilter,
        Expression<Func<TRel, bool>>? relationshipFilter,
        int minDepth,
        int maxDepth)
        where TSourceNode : class, INode, new()
        where TRel : class, IRelationship, new()
    {
        throw new NotImplementedException("This method should not be called directly");
    }

    private static IQueryable<GraphPath<TSourceNode>> TraversalPathsInternal<TSourceNode, TRel>(
        IQueryable<TSourceNode> source,
        TraversalDirection direction,
        Expression<Func<TSourceNode, bool>>? nodeFilter,
        Expression<Func<TRel, bool>>? relationshipFilter,
        int minDepth,
        int maxDepth)
        where TSourceNode : class, INode, new()
        where TRel : class, IRelationship, new()
    {
        throw new NotImplementedException("This method should not be called directly");
    }
}