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
using Cvoya.Graph.Model;
using Cvoya.Graph.Model.Age.Core;
using Cvoya.Graph.Model.Age.Querying.Cypher.Execution;
using Cvoya.Graph.Model.Age.Querying.Linq.Queryables;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

internal sealed class AgeGraphQueryProvider : IGraphQueryProvider
{
    private readonly AgeGraphContext _graphContext;
    private readonly AgeGraphTransaction? _transaction;
    private readonly ILogger<AgeGraphQueryProvider> _logger;
    private readonly AgeCypherEngine _cypherEngine;

    public AgeGraphQueryProvider(AgeGraphContext context, AgeGraphTransaction? transaction = null)
    {
        _graphContext = context ?? throw new ArgumentNullException(nameof(context));
        _transaction = transaction;
        _logger = context.LoggerFactory.CreateLogger<AgeGraphQueryProvider>() ?? NullLogger<AgeGraphQueryProvider>.Instance;
        _cypherEngine = new AgeCypherEngine(context, context.LoggerFactory);
    }

    public IGraph Graph => _graphContext.Graph;

    IQueryable IQueryProvider.CreateQuery(Expression expression) => throw new NotSupportedException("Non-generic CreateQuery not supported.");
    IQueryable<TElement> IQueryProvider.CreateQuery<TElement>(Expression expression) => CreateQuery<TElement>(expression);
    object? IQueryProvider.Execute(Expression expression) => ExecuteInternal<object>(expression);
    TResult IQueryProvider.Execute<TResult>(Expression expression) => ExecuteInternal<TResult>(expression);

    public IGraphQueryable<TElement> CreateQuery<TElement>(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        if (typeof(INode).IsAssignableFrom(typeof(TElement)))
        {
            var nodeQueryableType = typeof(AgeGraphNodeQueryable<>).MakeGenericType(typeof(TElement));
            return (IGraphQueryable<TElement>)Activator.CreateInstance(nodeQueryableType, this, _graphContext, expression)!;
        }

        if (typeof(IRelationship).IsAssignableFrom(typeof(TElement)))
        {
            var relQueryableType = typeof(AgeGraphRelationshipQueryable<>).MakeGenericType(typeof(TElement));
            return (IGraphQueryable<TElement>)Activator.CreateInstance(relQueryableType, this, _graphContext, expression)!;
        }

        return new AgeGraphQueryable<TElement>(this, _graphContext, expression);
    }

    public async Task<TResult> ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expression);
        _logger.LogDebug("Executing async query for result type: {ResultType}", typeof(TResult).Name);

        try
        {
            var result = await _cypherEngine.ExecuteAsync<TResult>(expression, _transaction, cancellationToken);
            return result!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing query");
            throw;
        }
    }

    public Task<object?> ExecuteAsync(Expression expression, CancellationToken cancellationToken = default)
        => ExecuteAsync<object?>(expression, cancellationToken);

    private TResult ExecuteInternal<TResult>(Expression expression)
        => ExecuteAsync<TResult>(expression, CancellationToken.None).GetAwaiter().GetResult();

}
