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
using Cvoya.Graph.Model;

namespace Cvoya.Graph.Provider.Neo4j.Linq;

internal class GraphQueryable<T> : IGraphQueryable<T>, IOrderedQueryable<T> where T : class
{
    private readonly IGraphQueryContext _context;

    public GraphQueryable(GraphQueryProvider provider, IGraphTransaction? transaction = null)
    {
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        Expression = Expression.Constant(this);
        Transaction = transaction;
        _context = new GraphQueryContext();
    }

    internal GraphQueryable(GraphQueryProvider provider, Expression expression, IGraphTransaction? transaction = null, IGraphQueryContext? context = null)
    {
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        Transaction = transaction;
        _context = context ?? new GraphQueryContext();
    }

    public IGraphQueryContext Context => _context;
    public Type ElementType => typeof(T);
    public Expression Expression { get; }
    public IQueryProvider Provider { get; }
    public IGraphTransaction? Transaction { get; }

    public IEnumerator<T> GetEnumerator()
    {
        var result = Provider.Execute<IEnumerable<T>>(Expression);
        return result.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // IGraphQueryable<T> implementation
    public IGraphQueryable<T> WithDepth(int depth)
    {
        // Depth control is handled through the query context, not options
        // For now, we'll store this in the expression for the query provider to handle
        var methodInfo = typeof(IGraphQueryable<T>).GetMethod(nameof(WithDepth), [typeof(int)])!;
        var newExpression = Expression.Call(Expression, methodInfo, Expression.Constant(depth));
        return new GraphQueryable<T>((GraphQueryProvider)Provider, newExpression, Transaction, Context);
    }

    public IGraphQueryable<T> WithDepth(int minDepth, int maxDepth)
    {
        // Depth control is handled through the query context, not options
        // For now, we'll store this in the expression for the query provider to handle
        var methodInfo = typeof(IGraphQueryable<T>).GetMethod(nameof(WithDepth), [typeof(int), typeof(int)])!;
        var newExpression = Expression.Call(Expression, methodInfo, Expression.Constant(minDepth), Expression.Constant(maxDepth));
        return new GraphQueryable<T>((GraphQueryProvider)Provider, newExpression, Transaction, Context);
    }

    public IGraphQueryable<T> InTransaction(IGraphTransaction transaction)
    {
        return new GraphQueryable<T>((GraphQueryProvider)Provider, Expression, transaction, Context);
    }

    public IGraphQueryable<T> WithHint(string hint)
    {
        var newContext = ((GraphQueryContext)_context).WithHint(hint);
        return new GraphQueryable<T>((GraphQueryProvider)Provider, Expression, Transaction, newContext);
    }

    public IGraphQueryable<T> WithHints(params string[] hints)
    {
        var newContext = ((GraphQueryContext)_context).WithHints(hints);
        return new GraphQueryable<T>((GraphQueryProvider)Provider, Expression, Transaction, newContext);
    }

    public IGraphQueryable<T> UseIndex(string indexName)
    {
        var newContext = ((GraphQueryContext)_context).WithIndexHint(indexName);
        return new GraphQueryable<T>((GraphQueryProvider)Provider, Expression, Transaction, newContext);
    }

    public IGraphTraversal<TSource, TRel> Traverse<TSource, TRel>()
        where TSource : class, INode, new()
        where TRel : class, IRelationship, new()
    {
        // Create a graph traversal using the copied GraphTraversal class
        return new GraphTraversal<TSource, TRel>(
            this as IQueryable<TSource> ?? throw new InvalidOperationException("Cannot traverse from non-matching source type"),
            TraversalDirection.Outgoing);
    }

    public IGraphPattern<TEntity> Match<TEntity>(string pattern) where TEntity : class, IEntity, new()
    {
        return new GraphPattern<TEntity>((GraphQueryProvider)Provider, pattern, Transaction);
    }

    public IGraphQueryBuilder<TEntity> Query<TEntity>() where TEntity : class, IEntity, new()
    {
        return new GraphQueryBuilder<TEntity>((GraphQueryProvider)Provider, Transaction);
    }

    public IGraphQueryable<T> Cached(TimeSpan duration)
    {
        var newContext = ((GraphQueryContext)_context).WithCaching(duration);
        return new GraphQueryable<T>((GraphQueryProvider)Provider, Expression, Transaction, newContext);
    }

    public IGraphQueryable<T> Cached(string cacheKey, TimeSpan duration)
    {
        var newContext = ((GraphQueryContext)_context).WithCaching(cacheKey, duration);
        return new GraphQueryable<T>((GraphQueryProvider)Provider, Expression, Transaction, newContext);
    }

    public IGraphQueryable<T> IncludeMetadata(GraphMetadataTypes metadata)
    {
        var newContext = ((GraphQueryContext)_context).WithMetadata(metadata);
        return new GraphQueryable<T>((GraphQueryProvider)Provider, Expression, Transaction, newContext);
    }

    public IGraphQueryable<T> WithTimeout(TimeSpan timeout)
    {
        var newContext = ((GraphQueryContext)_context).WithTimeout(timeout);
        return new GraphQueryable<T>((GraphQueryProvider)Provider, Expression, Transaction, newContext);
    }

    public IGraphQueryable<T> WithProfiling()
    {
        var newContext = ((GraphQueryContext)_context).WithProfiling(true);
        return new GraphQueryable<T>((GraphQueryProvider)Provider, Expression, Transaction, newContext);
    }

    public IGraphQueryable<T> WithCascadeDelete()
    {
        var newContext = ((GraphQueryContext)_context).WithCascadeDelete(true);
        return new GraphQueryable<T>((GraphQueryProvider)Provider, Expression, Transaction, newContext);
    }
}
