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
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Cvoya.Graph.Model.Neo4j.Core;
using Cvoya.Graph.Model.Neo4j.Querying.Linq.Helpers;
using Cvoya.Graph.Model.Neo4j.Querying.Linq.Providers;

internal abstract class GraphQueryableBase<T> : IGraphQueryable<T>, IOrderedQueryable<T>, IAsyncEnumerable<T>
{
    protected readonly GraphQueryProvider Provider;
    protected readonly GraphContext Context;
    protected readonly Expression Expression;
    protected GraphTransaction? _transaction;

    protected GraphQueryableBase(
        Type elementType,
        GraphQueryProvider provider,
        GraphContext graphContext,
        Expression expression)
    {
        ElementType = elementType;
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        Context = graphContext ?? throw new ArgumentNullException(nameof(graphContext));
        Expression = expression ?? throw new ArgumentNullException(nameof(expression));
    }

    protected GraphTransaction? Transaction => _transaction;

    #region IQueryable Implementation

    public Type ElementType { get; }
    Expression IQueryable.Expression => Expression;
    IQueryProvider IQueryable.Provider => Provider;

    #endregion

    #region IGraphQueryable Implementation

    public IGraph Graph => Context.Graph;
    IGraphQueryProvider IGraphQueryable<T>.Provider => Provider;

    public IGraphQueryable<T> WithTransaction(GraphTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        _transaction = transaction;

        // Create a method call expression for WithTransaction
        var methodCall = Expression.Call(
            null,
            typeof(GraphQueryableExtensions)
                .GetMethod(nameof(GraphQueryableExtensions.WithTransaction))!
                .MakeGenericMethod(typeof(T)),
            Expression,
            Expression.Constant(transaction));

        // Create a new instance of the same type with the new expression
        var newQueryable = (GraphQueryableBase<T>)Activator.CreateInstance(
            GetType(),
            Provider,
            Context,
            methodCall)!;

        newQueryable._transaction = transaction;
        return newQueryable;
    }

    #endregion

    #region IEnumerable Implementation

    public IEnumerator<T> GetEnumerator()
    {
        var result = Provider.Execute<IEnumerable<T>>(Expression);

        // Handle the case where Execute returns null (no results)
        if (result is null)
            return Enumerable.Empty<T>().GetEnumerator();

        return result.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    #endregion

    #region IAsyncEnumerable Implementation

    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        // Execute the query asynchronously
        var results = await Provider.ExecuteAsync<IEnumerable<T>>(Expression, cancellationToken);

        // Yield each result
        foreach (var item in results)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
        }
    }

    #endregion

    #region Async Operations

    public Task<T> FirstAsync(CancellationToken cancellationToken = default)
    {
        var expression = Expression.Call(
            null,
            GetAsyncMethod(nameof(FirstAsync)),
            Expression,
            Expression.Constant(cancellationToken));

        return Provider.ExecuteAsync<T>(expression, cancellationToken);
    }

    public Task<T?> FirstOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        var expression = Expression.Call(
            null,
            GetAsyncMethod(nameof(FirstOrDefaultAsync)),
            Expression,
            Expression.Constant(cancellationToken));

        return Provider.ExecuteAsync<T?>(expression, cancellationToken);
    }

    public Task<T> SingleAsync(CancellationToken cancellationToken = default)
    {
        var expression = Expression.Call(
            null,
            GetAsyncMethod(nameof(SingleAsync)),
            Expression,
            Expression.Constant(cancellationToken));

        return Provider.ExecuteAsync<T>(expression, cancellationToken);
    }

    public Task<T?> SingleOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        var expression = Expression.Call(
            null,
            GetAsyncMethod(nameof(SingleOrDefaultAsync)),
            Expression,
            Expression.Constant(cancellationToken));

        return Provider.ExecuteAsync<T?>(expression, cancellationToken);
    }

    public Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        var expression = Expression.Call(
            null,
            GetAsyncMethod(nameof(CountAsync)),
            Expression,
            Expression.Constant(cancellationToken));

        return Provider.ExecuteAsync<int>(expression, cancellationToken);
    }

    public Task<long> LongCountAsync(CancellationToken cancellationToken = default)
    {
        var expression = Expression.Call(
            null,
            GetAsyncMethod(nameof(LongCountAsync)),
            Expression,
            Expression.Constant(cancellationToken));

        return Provider.ExecuteAsync<long>(expression, cancellationToken);
    }

    public Task<bool> AnyAsync(CancellationToken cancellationToken = default)
    {
        var expression = Expression.Call(
            null,
            GetAsyncMethod(nameof(AnyAsync)),
            Expression,
            Expression.Constant(cancellationToken));

        return Provider.ExecuteAsync<bool>(expression, cancellationToken);
    }

    public Task<bool> AllAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        var expression = Expression.Call(
            null,
            GetAsyncMethod(nameof(AllAsync)),
            Expression,
            Expression.Quote(predicate),
            Expression.Constant(cancellationToken));

        return Provider.ExecuteAsync<bool>(expression, cancellationToken);
    }

    public Task<T> MinAsync(CancellationToken cancellationToken = default)
    {
        var expression = Expression.Call(
            null,
            GetAsyncMethod(nameof(MinAsync)),
            Expression,
            Expression.Constant(cancellationToken));

        return Provider.ExecuteAsync<T>(expression, cancellationToken);
    }

    public Task<T> MaxAsync(CancellationToken cancellationToken = default)
    {
        var expression = Expression.Call(
            null,
            GetAsyncMethod(nameof(MaxAsync)),
            Expression,
            Expression.Constant(cancellationToken));

        return Provider.ExecuteAsync<T>(expression, cancellationToken);
    }

    public Task<List<T>> ToListAsync(CancellationToken cancellationToken = default)
    {
        var expression = Expression.Call(
            null,
            GetAsyncMethod(nameof(ToListAsync)),
            Expression,
            Expression.Constant(cancellationToken));

        return Provider.ExecuteAsync<List<T>>(expression, cancellationToken);
    }

    public Task<T[]> ToArrayAsync(CancellationToken cancellationToken = default)
    {
        var expression = Expression.Call(
            null,
            GetAsyncMethod(nameof(ToArrayAsync)),
            Expression,
            Expression.Constant(cancellationToken));

        return Provider.ExecuteAsync<T[]>(expression, cancellationToken);
    }

    public Task<Dictionary<TKey, T>> ToDictionaryAsync<TKey>(
        Expression<Func<T, TKey>> keySelector,
        CancellationToken cancellationToken = default) where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(keySelector);

        var expression = Expression.Call(
            null,
            GetAsyncMethod(nameof(ToDictionaryAsync)).MakeGenericMethod(typeof(TKey)),
            Expression,
            Expression.Quote(keySelector),
            Expression.Constant(cancellationToken));

        return Provider.ExecuteAsync<Dictionary<TKey, T>>(expression, cancellationToken);
    }

    public Task<HashSet<T>> ToHashSetAsync(CancellationToken cancellationToken = default)
    {
        var expression = Expression.Call(
            null,
            GetAsyncMethod(nameof(ToHashSetAsync)),
            Expression,
            Expression.Constant(cancellationToken));

        return Provider.ExecuteAsync<HashSet<T>>(expression, cancellationToken);
    }

    public Task<bool> ContainsAsync(T item, CancellationToken cancellationToken = default)
    {
        var expression = Expression.Call(
            null,
            GetAsyncMethod(nameof(ContainsAsync)),
            Expression,
            Expression.Constant(item, typeof(T)),
            Expression.Constant(cancellationToken));

        return Provider.ExecuteAsync<bool>(expression, cancellationToken);
    }

    private static MethodInfo GetAsyncMethod(string methodName)
    {
        // These async methods are extension methods we'll define
        return typeof(GraphQueryableAsyncExtensions)
            .GetMethod(methodName, BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException($"Async method {methodName} not found");
    }

    public Task<T> LastAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<T?> LastOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<int> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<TResult?> MaxAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<TResult?> MinAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    #endregion
}
