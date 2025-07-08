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

using Microsoft.Extensions.Logging;

namespace Cvoya.Graph.Model.Neo4j.Tests;

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
                logger.LogError(ex, "Error executing scheduled task");
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
                logger.LogError(ex, "Error executing scheduled task");
                tcs.SetException(ex);
            }
        });

        return tcs.Task;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}