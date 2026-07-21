// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Tests;

using global::Neo4j.Driver;

/// <summary>
/// Delegating test driver that can fail commands and blocks each independent driver immediately
/// before its first matching managed-transaction command. This makes schema races and failures
/// deterministic without adding a production synchronization mechanism.
/// </summary>
internal sealed class SchemaCommandBarrierDriver(
    IDriver inner,
    Barrier barrier,
    Func<string, bool> shouldSynchronize,
    Func<string, Exception?>? commandFailure = null) : IDriver
{
    private int synchronized;

    public Config Config => inner.Config;

    public bool Encrypted => inner.Encrypted;

    public IAsyncSession AsyncSession() =>
        new SchemaCommandBarrierSession(inner.AsyncSession(), Synchronize);

    public IAsyncSession AsyncSession(Action<SessionConfigBuilder> action) =>
        new SchemaCommandBarrierSession(inner.AsyncSession(action), Synchronize);

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

    private void Synchronize(string cypher)
    {
        var failure = commandFailure?.Invoke(cypher);
        if (failure is not null)
        {
            throw failure;
        }

        if (!shouldSynchronize(cypher) || Interlocked.Exchange(ref synchronized, 1) != 0)
        {
            return;
        }

        if (!barrier.SignalAndWait(TimeSpan.FromSeconds(30)))
        {
            throw new TimeoutException($"Timed out coordinating schema command: {cypher}");
        }
    }
}

internal sealed class SchemaCommandBarrierSession(
    IAsyncSession inner,
    Action<string> synchronize) : IAsyncSession
{
    public Bookmarks LastBookmarks => inner.LastBookmarks;

    public SessionConfig SessionConfig => inner.SessionConfig;

    public Task<IAsyncTransaction> BeginTransactionAsync() => inner.BeginTransactionAsync();

    public Task<IAsyncTransaction> BeginTransactionAsync(Action<TransactionConfigBuilder> action) =>
        inner.BeginTransactionAsync(action);

    public Task<IAsyncTransaction> BeginTransactionAsync(AccessMode mode) => inner.BeginTransactionAsync(mode);

    public Task<IAsyncTransaction> BeginTransactionAsync(
        AccessMode mode,
        Action<TransactionConfigBuilder> action) =>
        inner.BeginTransactionAsync(mode, action);

    public Task<TResult> ExecuteReadAsync<TResult>(
        Func<IAsyncQueryRunner, Task<TResult>> work,
        Action<TransactionConfigBuilder> action) =>
        inner.ExecuteReadAsync(work, action);

    public Task<TResult> ExecuteWriteAsync<TResult>(
        Func<IAsyncQueryRunner, Task<TResult>> work,
        Action<TransactionConfigBuilder> action) =>
        inner.ExecuteWriteAsync(
            transaction => work(new SchemaCommandBarrierQueryRunner(transaction, synchronize)),
            action);

    public Task ExecuteReadAsync(
        Func<IAsyncQueryRunner, Task> work,
        Action<TransactionConfigBuilder> action) =>
        inner.ExecuteReadAsync(work, action);

    public Task ExecuteWriteAsync(
        Func<IAsyncQueryRunner, Task> work,
        Action<TransactionConfigBuilder> action) =>
        inner.ExecuteWriteAsync(
            transaction => work(new SchemaCommandBarrierQueryRunner(transaction, synchronize)),
            action);

    public Task<IResultCursor> RunAsync(string query, Action<TransactionConfigBuilder> action) =>
        inner.RunAsync(query, action);

    public Task<IResultCursor> RunAsync(
        string query,
        object parameters,
        Action<TransactionConfigBuilder> action) =>
        inner.RunAsync(query, parameters, action);

    public Task<IResultCursor> RunAsync(
        string query,
        IDictionary<string, object> parameters,
        Action<TransactionConfigBuilder> action) =>
        inner.RunAsync(query, parameters, action);

    public Task<IResultCursor> RunAsync(Query query, Action<TransactionConfigBuilder> action) =>
        inner.RunAsync(query, action);

    public Task<IResultCursor> RunAsync(string query) => inner.RunAsync(query);

    public Task<IResultCursor> RunAsync(string query, object parameters) => inner.RunAsync(query, parameters);

    public Task<IResultCursor> RunAsync(string query, IDictionary<string, object> parameters) =>
        inner.RunAsync(query, parameters);

    public Task<IResultCursor> RunAsync(Query query) => inner.RunAsync(query);

    public Task CloseAsync() => inner.CloseAsync();

    public void Dispose() => inner.Dispose();

    public ValueTask DisposeAsync() => inner.DisposeAsync();
}

internal sealed class SchemaCommandBarrierQueryRunner(
    IAsyncQueryRunner inner,
    Action<string> synchronize) : IAsyncQueryRunner
{
    public Task<IResultCursor> RunAsync(string query)
    {
        synchronize(query);
        return inner.RunAsync(query);
    }

    public Task<IResultCursor> RunAsync(string query, object parameters)
    {
        synchronize(query);
        return inner.RunAsync(query, parameters);
    }

    public Task<IResultCursor> RunAsync(string query, IDictionary<string, object> parameters)
    {
        synchronize(query);
        return inner.RunAsync(query, parameters);
    }

    public Task<IResultCursor> RunAsync(Query query)
    {
        synchronize(query.Text);
        return inner.RunAsync(query);
    }

    public void Dispose() => inner.Dispose();

    public ValueTask DisposeAsync() => inner.DisposeAsync();
}
