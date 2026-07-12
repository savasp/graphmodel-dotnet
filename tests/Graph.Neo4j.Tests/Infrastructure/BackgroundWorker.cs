// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Microsoft.Extensions.Logging;

namespace Cvoya.Graph.Neo4j.Tests;

internal sealed class BackgroundWorker : IAsyncDisposable
{
    private readonly ILogger<BackgroundWorker> logger;

    public BackgroundWorker(ILoggerFactory loggerFactory)
    {
        logger = loggerFactory.CreateLogger<BackgroundWorker>();
    }

    public Task<T> Schedule<T>(Func<Task<T>> taskFactory)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        _ = Task.Run(async () =>
        {
            try
            {
                var result = await taskFactory().ConfigureAwait(false);
                tcs.SetResult(result);
            }
            catch (Exception ex)
            {
                logger.LogErrorBackgroundWorker30(ex);
                tcs.SetException(ex);
            }
        });

        return tcs.Task;
    }

    public Task Schedule(Func<Task> taskFactory)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _ = Task.Run(async () =>
        {
            try
            {
                await taskFactory().ConfigureAwait(false);
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                logger.LogErrorBackgroundWorker51(ex);
                tcs.SetException(ex);
            }
        });

        return tcs.Task;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}