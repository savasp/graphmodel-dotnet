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
        var cursor = await transaction.Transaction.RunAsync(
            cypher,
            parameters,
            projectionColumns,
            cancellationToken).ConfigureAwait(false);
        var records = await cursor.ToListAsync(cancellationToken).ConfigureAwait(false);
        logger.LogDebug("AGE query returned {Count} records", records.Count);
        return records;
    }

    public async IAsyncEnumerable<AgeRecord> StreamAsync(
        string cypher,
        IReadOnlyDictionary<string, object?> parameters,
        IReadOnlyList<string> projectionColumns,
        AgeGraphTransaction transaction,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var cursor = await transaction.Transaction.RunAsync(
            cypher,
            parameters,
            projectionColumns,
            cancellationToken).ConfigureAwait(false);
        while (await cursor.FetchAsync().ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return cursor.Current;
        }
    }
}
