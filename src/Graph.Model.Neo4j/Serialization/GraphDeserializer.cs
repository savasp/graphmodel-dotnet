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
using Neo4j.Driver;

namespace Cvoya.Graph.Model.Neo4j.Serialization;

/// <summary>
/// High-level interface for deserializing Neo4j query results back to .NET objects.
/// </summary>
internal class GraphDeserializer(GraphContext context)
{
    private readonly ILogger<GraphDeserializer> _logger = context.LoggerFactory?.CreateLogger<GraphDeserializer>()
        ?? throw new ArgumentNullException(nameof(context.LoggerFactory), "LoggerFactory cannot be null");

    /// <summary>
    /// Deserializes a collection of Neo4j records to strongly-typed objects.
    /// </summary>
    /// <typeparam name="T">The target .NET type</typeparam>
    /// <param name="records">Neo4j query results</param>
    /// <param name="mainNodeKey">Key for the main node in each record</param>
    /// <returns>Collection of deserialized objects</returns>
    public IEnumerable<T> DeserializeRecords<T>(IEnumerable<IRecord> records, string mainNodeKey)
    {
        var serializer = EntitySerializerRegistry.GetSerializer(typeof(T))
            ?? throw new InvalidOperationException($"No serializer registered for type {typeof(T).Name}");

        foreach (var record in records)
        {
            // Convert Neo4j result to intermediate Entity representation
            var entity = context.EntityFactory.ConvertToIntermediateRepresentation(record, mainNodeKey);

            // Use the serializer to convert Entity back to the .NET object
            var deserializedObject = serializer.Deserialize(entity);

            yield return (T)deserializedObject;
        }
    }

    /// <summary>
    /// Deserializes a single Neo4j record to a strongly-typed object.
    /// </summary>
    /// <typeparam name="T">The target .NET type</typeparam>
    /// <param name="record">Neo4j query result</param>
    /// <param name="mainNodeKey">Key for the main node in the record</param>
    /// <returns>Deserialized object or null if record is empty</returns>
    public T? DeserializeRecord<T>(IRecord? record, string mainNodeKey)
    {
        if (record == null)
        {
            return default;
        }

        return DeserializeRecords<T>([record], mainNodeKey).FirstOrDefault();
    }
}