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

namespace Cvoya.Graph.Model.Age.Querying.Linq.Providers;

using System.Linq.Expressions;
using System.Transactions;
using Cvoya.Graph.Model.Age.Core;
using Cvoya.Graph.Model.Age.Querying.Cypher.Execution;
using Cvoya.Graph.Model.Age.Querying.Linq.Queryables;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Provides LINQ query execution capabilities for AGE graph operations.
/// </summary>
internal sealed class AgeGraphQueryProvider : IGraphQueryProvider, IAsyncDisposable
{
    private readonly AgeGraphContext _graphContext;
    private readonly AgeGraphTransaction? _transaction;
    private readonly ILogger<AgeGraphQueryProvider> _logger;
    private readonly AgeCypherEngine _cypherEngine;

    public AgeGraphQueryProvider(AgeGraphContext context, AgeGraphTransaction? transaction = null)
    {
        _graphContext = context ?? throw new ArgumentNullException(nameof(context));
        _transaction = transaction;
        _logger = context.LoggerFactory?.CreateLogger<AgeGraphQueryProvider>() ?? NullLogger<AgeGraphQueryProvider>.Instance;
        _cypherEngine = new AgeCypherEngine(context, context.LoggerFactory);
    }

    public IGraph Graph => _graphContext.Graph;

    #region IQueryProvider Implementation

    public IQueryable CreateQuery(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        var elementType = GetElementType(expression.Type);

        try
        {
            return (IQueryable)GetType()
                .GetMethod(nameof(CreateQuery), 1, [typeof(Expression)])!
                .MakeGenericMethod(elementType)
                .Invoke(this, [expression])!;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to create query for type {elementType}", ex);
        }
    }

    public IGraphQueryable<TElement> CreateQuery<TElement>(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        // Determine the queryable type based on TElement
        if (typeof(INode).IsAssignableFrom(typeof(TElement)))
        {
            var nodeQueryableType = typeof(AgeGraphNodeQueryable<>).MakeGenericType(typeof(TElement));
            return (IGraphQueryable<TElement>)Activator.CreateInstance(
                nodeQueryableType,
                this,
                _graphContext,
                expression)!;
        }

        if (typeof(IRelationship).IsAssignableFrom(typeof(TElement)))
        {
            var relQueryableType = typeof(AgeGraphRelationshipQueryable<>).MakeGenericType(typeof(TElement));
            return (IGraphQueryable<TElement>)Activator.CreateInstance(
                relQueryableType,
                this,
                _graphContext,
                expression)!;
        }

        // For other types (projections, anonymous types, etc.)
        return new AgeGraphQueryable<TElement>(this, _graphContext, expression);
    }

    public object? Execute(Expression expression)
    {
        return ExecuteInternal<object>(expression);
    }

    public TResult Execute<TResult>(Expression expression)
    {
        return ExecuteInternal<TResult>(expression);
    }

    #endregion

    public Task<object?> ExecuteAsync(Expression expression, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync<object?>(expression, cancellationToken);
    }

    public async Task<TResult> ExecuteAsync<TResult>(
        Expression expression,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expression);

        _logger.LogDebug("Executing async query for result type: {ResultType}", typeof(TResult).Name);

        try
        {
            // Execute using the CypherEngine
            var result = await _cypherEngine.ExecuteAsync<TResult>(
                expression,
                _transaction,
                cancellationToken);

            return result!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing query");
            throw;
        }
    }

    private TResult ExecuteInternal<TResult>(Expression expression)
    {
        return ExecuteAsync<TResult>(expression, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
    }

    IQueryable<TElement> IQueryProvider.CreateQuery<TElement>(Expression expression)
    {
        return CreateQuery<TElement>(expression);
    }

    private static Type GetElementType(Type sequenceType)
    {
        var ienum = FindIEnumerable(sequenceType);
        if (ienum == null) return sequenceType;
        return ienum.GetGenericArguments()[0];
    }

    private static Type? FindIEnumerable(Type? seqType)
    {
        if (seqType == null || seqType == typeof(string))
            return null;

        if (seqType.IsArray)
            return typeof(IEnumerable<>).MakeGenericType(seqType.GetElementType()!);

        if (seqType.IsGenericType)
        {
            foreach (var arg in seqType.GetGenericArguments())
            {
                var ienum = typeof(IEnumerable<>).MakeGenericType(arg);
                if (ienum.IsAssignableFrom(seqType))
                    return ienum;
            }
        }

        var ifaces = seqType.GetInterfaces();
        if (ifaces.Length > 0)
        {
            foreach (var iface in ifaces)
            {
                var ienum = FindIEnumerable(iface);
                if (ienum != null) return ienum;
            }
        }

        if (seqType.BaseType != null && seqType.BaseType != typeof(object))
            return FindIEnumerable(seqType.BaseType);

        return null;
    }

    public async ValueTask DisposeAsync()
    {
        if(_transaction is not null){
            await _transaction.DisposeAsync();
        }
        
    }
}
