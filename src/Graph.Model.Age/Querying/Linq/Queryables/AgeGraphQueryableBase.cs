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

namespace Cvoya.Graph.Model.Age.Querying.Linq.Queryables;

using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Cvoya.Graph.Model;
using Cvoya.Graph.Model.Age.Core;
using Cvoya.Graph.Model.Age.Querying.Linq.Helpers;
using Cvoya.Graph.Model.Age.Querying.Linq.Providers;

internal abstract class AgeGraphQueryableBase<TElement> : IGraphQueryable<TElement>, IAsyncEnumerable<TElement>, IAsyncDisposable
{
    protected AgeGraphQueryableBase(
        Type elementType,
        AgeGraphQueryProvider provider,
        AgeGraphContext graphContext,
        Expression expression)
    {
        ElementType = elementType ?? throw new ArgumentNullException(nameof(elementType));
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        GraphContext = graphContext ?? throw new ArgumentNullException(nameof(graphContext));
        Expression = expression ?? throw new ArgumentNullException(nameof(expression));
    }

    public Type ElementType { get; }

    public Expression Expression { get; }

    public IGraphQueryProvider Provider { get; }

    public IGraph Graph => GraphContext.Graph;

    protected AgeGraphContext GraphContext { get; }

    IQueryProvider IQueryable.Provider => Provider;

    public IEnumerator<TElement> GetEnumerator()
    {
        return Provider.Execute<IEnumerable<TElement>>(Expression).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    #region IAsyncEnumerable Implementation

    public async IAsyncEnumerator<TElement> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        var results = await Provider.ExecuteAsync<IEnumerable<TElement>>(Expression, cancellationToken);
        foreach (var item in results)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
        }
    }

    #endregion

    #region Async Terminal Operations

    public Task<TElement> FirstAsync(CancellationToken cancellationToken = default)
    {
        var expression = Expression.Call(
            null,
            GetAsyncMethod(nameof(FirstAsync)),
            Expression,
            Expression.Constant(cancellationToken));

        return Provider.ExecuteAsync<TElement>(expression, cancellationToken);
    }

    public Task<TElement?> FirstOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        var expression = Expression.Call(
            null,
            GetAsyncMethod(nameof(FirstOrDefaultAsync)),
            Expression,
            Expression.Constant(cancellationToken));

        return Provider.ExecuteAsync<TElement?>(expression, cancellationToken);
    }

    public Task<TElement> SingleAsync(CancellationToken cancellationToken = default)
    {
        var expression = Expression.Call(
            null,
            GetAsyncMethod(nameof(SingleAsync)),
            Expression,
            Expression.Constant(cancellationToken));

        return Provider.ExecuteAsync<TElement>(expression, cancellationToken);
    }

    public Task<TElement?> SingleOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        var expression = Expression.Call(
            null,
            GetAsyncMethod(nameof(SingleOrDefaultAsync)),
            Expression,
            Expression.Constant(cancellationToken));

        return Provider.ExecuteAsync<TElement?>(expression, cancellationToken);
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

    public Task<bool> AnyAsync(CancellationToken cancellationToken = default)
    {
        var expression = Expression.Call(
            null,
            GetAsyncMethod(nameof(AnyAsync)),
            Expression,
            Expression.Constant(cancellationToken));

        return Provider.ExecuteAsync<bool>(expression, cancellationToken);
    }

    public Task<List<TElement>> ToListAsync(CancellationToken cancellationToken = default)
    {
        var expression = Expression.Call(
            null,
            GetAsyncMethod(nameof(ToListAsync)),
            Expression,
            Expression.Constant(cancellationToken));

        return Provider.ExecuteAsync<List<TElement>>(expression, cancellationToken);
    }

    public Task<TElement[]> ToArrayAsync(CancellationToken cancellationToken = default)
    {
        var expression = Expression.Call(
            null,
            GetAsyncMethod(nameof(ToArrayAsync)),
            Expression,
            Expression.Constant(cancellationToken));

        return Provider.ExecuteAsync<TElement[]>(expression, cancellationToken);
    }

    private static MethodInfo GetAsyncMethod(string methodName)
    {
        return typeof(AgeGraphQueryableAsyncExtensions)
            .GetMethod(methodName, BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException($"Async method {methodName} not found");
    }

    #endregion

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
