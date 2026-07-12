// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Querying.Cypher.Execution;

using System.Runtime.CompilerServices;
using Cvoya.Graph.Neo4j.Core;
using global::Neo4j.Driver;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;


internal sealed class CypherExecutor
{
    private readonly ILogger<CypherExecutor> _logger;

    public CypherExecutor(ILoggerFactory? loggerFactory = null)
    {
        _logger = loggerFactory?.CreateLogger<CypherExecutor>()
            ?? NullLogger<CypherExecutor>.Instance;
    }

    public async Task<List<IRecord>> ExecuteAsync(
        string cypher,
        IReadOnlyDictionary<string, object?> parameters,
        GraphTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogDebugCypherExecutor31(cypher);

        var cursor = await transaction.Transaction.RunAsync(cypher, parameters).WaitAsync(cancellationToken).ConfigureAwait(false);
        var records = await cursor.ToListAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogDebugCypherExecutor36(records.Count);

        return records;
    }

    public async IAsyncEnumerable<IRecord> StreamAsync(
        string cypher,
        IReadOnlyDictionary<string, object?> parameters,
        GraphTransaction transaction,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogDebugCypherExecutor49(cypher);

        var cursor = await transaction.Transaction.RunAsync(cypher, parameters).WaitAsync(cancellationToken).ConfigureAwait(false);
        var count = 0;

        while (await cursor.FetchAsync().WaitAsync(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            count++;
            yield return cursor.Current;
        }

        _logger.LogDebugCypherExecutor61(count);
    }
}
