// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Querying.Cypher.Execution;

using System.Runtime.CompilerServices;
using Cvoya.Graph.Age.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

internal sealed class CypherExecutor
{
    private readonly ILogger<CypherExecutor> logger;

    public CypherExecutor(ILoggerFactory? loggerFactory = null)
    {
        logger = loggerFactory?.CreateLogger<CypherExecutor>() ?? NullLogger<CypherExecutor>.Instance;
    }

    public async Task<List<AgeRecord>> ExecuteAsync(
        string cypher,
        IReadOnlyDictionary<string, object?> parameters,
        IReadOnlyList<string> projectionColumns,
        AgeGraphTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var cursor = await transaction.Runner.RunAsync(
            cypher,
            parameters,
            projectionColumns,
            cancellationToken).ConfigureAwait(false);
        await using var cursorLease = cursor.ConfigureAwait(false);
        var records = await cursor.ToListAsync(cancellationToken).ConfigureAwait(false);
        logger.LogDebug("AGE query returned {Count} records", records.Count);
        return records;
    }

    public static async IAsyncEnumerable<AgeRecord> StreamAsync(
        string cypher,
        IReadOnlyDictionary<string, object?> parameters,
        IReadOnlyList<string> projectionColumns,
        AgeGraphTransaction transaction,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var cursor = await transaction.Runner.RunStreamingAsync(
            cypher,
            parameters,
            projectionColumns,
            cancellationToken).ConfigureAwait(false);
        await using var cursorLease = cursor.ConfigureAwait(false);
        while (await cursor.FetchAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return cursor.Current;
        }
    }
}
