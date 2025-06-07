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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cvoya.Graph.Model.Neo4j.Linq;

internal class GraphTraversalQueryable<TSource, TRel, TTarget> :
    GraphQueryable<TTarget>,
    IGraphTraversalQueryable<TSource, TRel, TTarget>
    where TSource : INode
    where TRel : IRelationship
    where TTarget : INode
{
    private readonly Expression _sourceExpression;
    private readonly TraversalDirection _direction = TraversalDirection.Outgoing;
    private readonly int? _minDepth;
    private readonly int? _maxDepth;
    private readonly TraversalOptions? _options;
    private readonly ILogger _logger;

    internal GraphTraversalQueryable(
        GraphQueryProvider provider,
        GraphContext graphContext,
        GraphQueryContext queryContext,
        Expression traversalExpression,
        Expression sourceExpression,
        GraphTransaction? transaction = null,
        TraversalDirection direction = TraversalDirection.Outgoing,
        int? minDepth = null,
        int? maxDepth = null,
        TraversalOptions? options = null) :
        base(provider, graphContext, queryContext, traversalExpression, transaction)
    {
        _sourceExpression = sourceExpression ?? throw new ArgumentNullException(nameof(sourceExpression));
        _direction = direction;
        _minDepth = minDepth;
        _maxDepth = maxDepth;
        _options = options;
        _logger = graphContext.LoggerFactory?.CreateLogger<GraphTraversalQueryable<TSource, TRel, TTarget>>()
                  ?? NullLogger<GraphTraversalQueryable<TSource, TRel, TTarget>>.Instance;
    }

    public IGraphTraversalQueryable<TSource, TRel, TTarget> InDirection(TraversalDirection direction)
    {
        return new GraphTraversalQueryable<TSource, TRel, TTarget>(
            Provider, GraphContext, QueryContext, Expression, _sourceExpression,
            Transaction, direction, _minDepth, _maxDepth, _options);
    }

    public IGraphTraversalQueryable<TSource, TRel, TTarget> WithDepth(int maxDepth)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxDepth);

        return new GraphTraversalQueryable<TSource, TRel, TTarget>(
            Provider, GraphContext, QueryContext, Expression, _sourceExpression,
            Transaction, _direction, 1, maxDepth, _options);
    }

    public IGraphTraversalQueryable<TSource, TRel, TTarget> WithDepth(int minDepth, int maxDepth)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minDepth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxDepth);
        if (minDepth > maxDepth)
            throw new ArgumentException("Minimum depth cannot be greater than maximum depth");

        return new GraphTraversalQueryable<TSource, TRel, TTarget>(
            Provider, GraphContext, QueryContext, Expression, _sourceExpression,
            Transaction, _direction, minDepth, maxDepth, _options);
    }

    public IGraphTraversalQueryable<TSource, TRel, TTarget> WithOptions(TraversalOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new GraphTraversalQueryable<TSource, TRel, TTarget>(
            Provider, GraphContext, QueryContext, Expression, _sourceExpression,
            Transaction, _direction, _minDepth, _maxDepth, options);
    }

    public IGraphTraversalQueryable<TSource, TNextRel, TNextTarget> ThenTraverse<TNextRel, TNextTarget>()
        where TNextRel : IRelationship
        where TNextTarget : INode
    {
        // Create an expression for chaining traversals
        var thenTraverseMethod = typeof(IGraphTraversalQueryable<TSource, TRel, TTarget>)
            .GetMethod(nameof(ThenTraverse))!
            .MakeGenericMethod(typeof(TNextRel), typeof(TNextTarget));

        var chainedExpression = Expression.Call(
            Expression,
            thenTraverseMethod);

        // The source stays the same, but we're now traversing through a different relationship
        return new GraphTraversalQueryable<TSource, TNextRel, TNextTarget>(
            Provider, GraphContext, QueryContext, chainedExpression, _sourceExpression, Transaction);
    }

    public IGraphQueryable<TRel> Relationships()
    {
        // Create an expression that represents getting relationships from this traversal
        var relationshipsMethod = typeof(IGraphTraversalQueryable<TSource, TRel, TTarget>)
            .GetMethod(nameof(Relationships))!;

        var relationshipsExpression = Expression.Call(
            Expression,
            relationshipsMethod);

        return new GraphQueryable<TRel>(
            Provider, GraphContext, QueryContext, relationshipsExpression, Transaction);
    }

    public IGraphQueryable<TNewTarget> To<TNewTarget>() where TNewTarget : INode
    {
        // Create an expression for projecting to a different node type
        var toMethod = typeof(IGraphTraversalQueryable<TSource, TRel, TTarget>)
            .GetMethod(nameof(To))!
            .MakeGenericMethod(typeof(TNewTarget));

        var projectionExpression = Expression.Call(
            Expression,
            toMethod);

        return new GraphQueryable<TNewTarget>(
            Provider, GraphContext, QueryContext, projectionExpression, Transaction);
    }

    // Internal properties for the provider to access traversal configuration
    internal TraversalDirection Direction => _direction;
    internal int? MinDepth => _minDepth;
    internal int? MaxDepth => _maxDepth;
    internal TraversalOptions? Options => _options;
    internal Expression SourceExpression => _sourceExpression;
}