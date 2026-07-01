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

namespace Cvoya.Graph.Model.Neo4j.Querying.Cypher.Execution;

using Cvoya.Graph.Model.Neo4j.Core;
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
        _logger.LogDebug("Executing Cypher query: {Query}", cypher);

        var cursor = await transaction.Transaction.RunAsync(cypher, parameters);
        var records = await cursor.ToListAsync(cancellationToken);

        _logger.LogDebug("Query returned {Count} records", records.Count);

        return records;
    }
}