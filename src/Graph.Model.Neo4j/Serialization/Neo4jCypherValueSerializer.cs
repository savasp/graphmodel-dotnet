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

namespace Cvoya.Graph.Model.Neo4j.Serialization;

using Cvoya.Graph.Model.Cypher.Serialization;

/// <summary>
/// Neo4j-specific implementation of ICypherValueSerializer that delegates to SerializationBridge.
/// </summary>
internal sealed class Neo4jCypherValueSerializer : ICypherValueSerializer
{
    /// <summary>
    /// Converts a CLR value into a Neo4j-compatible representation.
    /// </summary>
    /// <param name="value">The CLR value.</param>
    /// <returns>The Neo4j-compatible equivalent.</returns>
    public object? ConvertValue(object? value)
    {
        return SerializationBridge.ToNeo4jValue(value);
    }

    /// <summary>
    /// Creates metadata entries for a CLR type that should accompany serialized entities.
    /// </summary>
    /// <param name="type">The CLR type being serialized.</param>
    /// <returns>Metadata entries keyed by property name.</returns>
    public IReadOnlyDictionary<string, object?> CreateMetadata(Type type)
    {
        var metadata = SerializationBridge.CreateMetadata(type);
        return new Dictionary<string, object?>(metadata.Select(kvp => 
            new KeyValuePair<string, object?>(kvp.Key, kvp.Value)));
    }
}