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

using Cvoya.Graph.Model.Neo4j.Serialization;
using Cvoya.Graph.Model.Serialization;
using global::Neo4j.Driver;
using Microsoft.Extensions.Logging;

/// <summary>
/// Neo4j-specific result materializer that processes IRecord objects and delegates to the shared ResultMaterializer.
/// This acts as a bridge between Neo4j's IRecord format and the shared materialization infrastructure.
/// </summary>
internal sealed class ResultMaterializer
{
    private readonly CypherResultProcessor _resultProcessor;
    private readonly ResultMaterializer<Neo4jValueConverter> _sharedMaterializer;

    public ResultMaterializer(EntityFactory entityFactory, ILoggerFactory? loggerFactory)
    {
        _resultProcessor = new CypherResultProcessor(entityFactory, loggerFactory);
        
        var valueConverter = new Neo4jValueConverter();
        _sharedMaterializer = new ResultMaterializer<Neo4jValueConverter>(entityFactory, valueConverter, loggerFactory);
    }

    /// <summary>
    /// Materializes Neo4j IRecord objects into the target type T.
    /// Converts IRecord objects to EntityInfo and delegates to shared ResultMaterializer.
    /// </summary>
    /// <typeparam name="T">Target type for materialization</typeparam>
    /// <param name="records">List of Neo4j IRecord objects to materialize</param>
    /// <param name="cancellationToken">Cancellation token for async operations</param>
    /// <returns>Materialized object(s) of type T</returns>
    public async Task<T?> MaterializeAsync<T>(
        List<IRecord> records,
        CancellationToken cancellationToken = default)
    {
        var targetType = typeof(T);
        var elementType = Helpers.GetTargetTypeIfCollection(targetType);

        // Convert Neo4j IRecord objects to EntityInfo using CypherResultProcessor
        var entityInfos = await _resultProcessor.ProcessAsync(records, elementType, cancellationToken);

        // Delegate to shared ResultMaterializer for final object construction
        return await _sharedMaterializer.MaterializeAsync<T>(entityInfos, cancellationToken);
    }
}