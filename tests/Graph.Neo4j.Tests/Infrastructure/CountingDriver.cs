// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Tests;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using global::Neo4j.Driver;

/// <summary>
/// A shared, mutable count of <c>RunAsync</c> invocations issued through a <see cref="CountingDriver"/>,
/// used to prove how many statements a provider operation sends to the database.
/// </summary>
internal sealed class RunAsyncCounter
{
    public int Count { get; private set; }

    public void Increment() => Count++;

    public void Reset() => Count = 0;
}

/// <summary>
/// A delegating <see cref="IDriver"/> that counts every <c>RunAsync</c> issued on the sessions and
/// transactions it hands out. Everything else is forwarded unchanged to the wrapped driver.
/// </summary>
internal sealed class CountingDriver(IDriver inner, RunAsyncCounter counter) : IDriver
{
    public Config Config => inner.Config;

    public bool Encrypted => inner.Encrypted;

    public IAsyncSession AsyncSession() => new CountingSession(inner.AsyncSession(), counter);

    public IAsyncSession AsyncSession(Action<SessionConfigBuilder> action) =>
        new CountingSession(inner.AsyncSession(action), counter);

    public Task<IServerInfo> GetServerInfoAsync() => inner.GetServerInfoAsync();

    public Task<bool> TryVerifyConnectivityAsync() => inner.TryVerifyConnectivityAsync();

    public Task VerifyConnectivityAsync() => inner.VerifyConnectivityAsync();

    public Task<bool> SupportsMultiDbAsync() => inner.SupportsMultiDbAsync();

    public Task<bool> SupportsSessionAuthAsync() => inner.SupportsSessionAuthAsync();

    public IExecutableQuery<IRecord, IRecord> ExecutableQuery(string cypher) => inner.ExecutableQuery(cypher);

    public Task<bool> VerifyAuthenticationAsync(IAuthToken authToken) => inner.VerifyAuthenticationAsync(authToken);

    public IBookmarkManager GetExecutableQueryBookmarkManager() => inner.GetExecutableQueryBookmarkManager();

    public void Dispose() => inner.Dispose();

    public ValueTask DisposeAsync() => inner.DisposeAsync();
}

/// <summary>Delegating <see cref="IAsyncSession"/> that counts <c>RunAsync</c> and wraps transactions.</summary>
internal sealed class CountingSession(IAsyncSession inner, RunAsyncCounter counter) : IAsyncSession
{
    public Bookmarks LastBookmarks => inner.LastBookmarks;

    public SessionConfig SessionConfig => inner.SessionConfig;

    public async Task<IAsyncTransaction> BeginTransactionAsync() =>
        new CountingTransaction(await inner.BeginTransactionAsync(), counter);

    public async Task<IAsyncTransaction> BeginTransactionAsync(Action<TransactionConfigBuilder> action) =>
        new CountingTransaction(await inner.BeginTransactionAsync(action), counter);

    public async Task<IAsyncTransaction> BeginTransactionAsync(AccessMode mode) =>
        new CountingTransaction(await inner.BeginTransactionAsync(mode), counter);

    public async Task<IAsyncTransaction> BeginTransactionAsync(AccessMode mode, Action<TransactionConfigBuilder> action) =>
        new CountingTransaction(await inner.BeginTransactionAsync(mode, action), counter);

    public Task<TResult> ExecuteReadAsync<TResult>(Func<IAsyncQueryRunner, Task<TResult>> work, Action<TransactionConfigBuilder> action) =>
        inner.ExecuteReadAsync(work, action);

    public Task<TResult> ExecuteWriteAsync<TResult>(Func<IAsyncQueryRunner, Task<TResult>> work, Action<TransactionConfigBuilder> action) =>
        inner.ExecuteWriteAsync(work, action);

    public Task ExecuteReadAsync(Func<IAsyncQueryRunner, Task> work, Action<TransactionConfigBuilder> action) =>
        inner.ExecuteReadAsync(work, action);

    public Task ExecuteWriteAsync(Func<IAsyncQueryRunner, Task> work, Action<TransactionConfigBuilder> action) =>
        inner.ExecuteWriteAsync(work, action);

    public Task<IResultCursor> RunAsync(string query, Action<TransactionConfigBuilder> action)
    {
        counter.Increment();
        return inner.RunAsync(query, action);
    }

    public Task<IResultCursor> RunAsync(string query, object parameters, Action<TransactionConfigBuilder> action)
    {
        counter.Increment();
        return inner.RunAsync(query, parameters, action);
    }

    public Task<IResultCursor> RunAsync(string query, IDictionary<string, object> parameters, Action<TransactionConfigBuilder> action)
    {
        counter.Increment();
        return inner.RunAsync(query, parameters, action);
    }

    public Task<IResultCursor> RunAsync(Query query, Action<TransactionConfigBuilder> action)
    {
        counter.Increment();
        return inner.RunAsync(query, action);
    }

    public Task<IResultCursor> RunAsync(string query)
    {
        counter.Increment();
        return inner.RunAsync(query);
    }

    public Task<IResultCursor> RunAsync(string query, object parameters)
    {
        counter.Increment();
        return inner.RunAsync(query, parameters);
    }

    public Task<IResultCursor> RunAsync(string query, IDictionary<string, object> parameters)
    {
        counter.Increment();
        return inner.RunAsync(query, parameters);
    }

    public Task<IResultCursor> RunAsync(Query query)
    {
        counter.Increment();
        return inner.RunAsync(query);
    }

    public Task CloseAsync() => inner.CloseAsync();

    public void Dispose() => inner.Dispose();

    public ValueTask DisposeAsync() => inner.DisposeAsync();
}

/// <summary>Delegating <see cref="IAsyncTransaction"/> that counts every <c>RunAsync</c>.</summary>
internal sealed class CountingTransaction(IAsyncTransaction inner, RunAsyncCounter counter) : IAsyncTransaction
{
    public TransactionConfig TransactionConfig => inner.TransactionConfig;

    public Task<IResultCursor> RunAsync(string query)
    {
        counter.Increment();
        return inner.RunAsync(query);
    }

    public Task<IResultCursor> RunAsync(string query, object parameters)
    {
        counter.Increment();
        return inner.RunAsync(query, parameters);
    }

    public Task<IResultCursor> RunAsync(string query, IDictionary<string, object> parameters)
    {
        counter.Increment();
        return inner.RunAsync(query, parameters);
    }

    public Task<IResultCursor> RunAsync(Query query)
    {
        counter.Increment();
        return inner.RunAsync(query);
    }

    public Task CommitAsync() => inner.CommitAsync();

    public Task RollbackAsync() => inner.RollbackAsync();

    public void Dispose() => inner.Dispose();

    public ValueTask DisposeAsync() => inner.DisposeAsync();
}
