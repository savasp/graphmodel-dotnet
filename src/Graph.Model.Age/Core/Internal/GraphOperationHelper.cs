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

namespace Cvoya.Graph.Model.Age.Core.Internal;

using System;
using System.Threading.Tasks;
using Cvoya.Graph.Model;
using Microsoft.Extensions.Logging;

/// <summary>
/// Helper to reduce repetitive try-catch-GraphException boilerplate in AgeGraph methods.
/// </summary>
internal static class GraphOperationHelper
{
    /// <summary>
    /// Executes an operation and wraps non-GraphException failures in a GraphException.
    /// </summary>
    public static async Task<T> ExecuteAsync<T>(
        ILogger logger,
        string errorMessage,
        Func<Task<T>> operation)
    {
        try
        {
            return await operation().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not GraphException)
        {
            logger.LogError(ex, errorMessage);
            throw new GraphException(errorMessage, ex);
        }
    }

    /// <summary>
    /// Executes a void operation and wraps non-GraphException failures in a GraphException.
    /// </summary>
    public static async Task ExecuteAsync(
        ILogger logger,
        string errorMessage,
        Func<Task> operation)
    {
        try
        {
            await operation().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not GraphException)
        {
            logger.LogError(ex, errorMessage);
            throw new GraphException(errorMessage, ex);
        }
    }

    /// <summary>
    /// Executes a synchronous operation and wraps non-GraphException failures in a GraphException.
    /// </summary>
    public static T ExecuteSync<T>(
        ILogger logger,
        string errorMessage,
        Func<T> operation)
    {
        try
        {
            return operation();
        }
        catch (Exception ex) when (ex is not GraphException)
        {
            logger.LogError(ex, errorMessage);
            throw new GraphException(errorMessage, ex);
        }
    }
}
