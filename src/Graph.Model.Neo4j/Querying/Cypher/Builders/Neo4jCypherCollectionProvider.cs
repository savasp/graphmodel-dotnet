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

namespace Cvoya.Graph.Model.Neo4j.Querying.Cypher.Builders;

using Cvoya.Graph.Model.Cypher.Querying.Cypher.Builders;

/// <summary>
/// Neo4j-specific implementation of ICypherCollectionProvider that uses APOC functions.
/// This provides Neo4j's extended collection capabilities through APOC procedures.
/// </summary>
internal class Neo4jCypherCollectionProvider : ICypherCollectionProvider
{
    /// <summary>
    /// Generates Cypher expression to convert a collection to a set using APOC.
    /// </summary>
    /// <param name="collectionExpression">The Cypher expression representing the collection.</param>
    /// <returns>APOC expression that returns a set with unique values.</returns>
    public string ToSet(string collectionExpression)
    {
        return $"apoc.coll.toSet({collectionExpression})";
    }

    /// <summary>
    /// Generates Cypher expression to get the size/length of a collection.
    /// </summary>
    /// <param name="collectionExpression">The Cypher expression representing the collection.</param>
    /// <returns>Cypher expression that returns the collection size.</returns>
    public string Size(string collectionExpression)
    {
        return $"size({collectionExpression})";
    }

    /// <summary>
    /// Generates Cypher expression to check if a collection contains a specific value.
    /// </summary>
    /// <param name="collectionExpression">The Cypher expression representing the collection.</param>
    /// <param name="valueExpression">The Cypher expression representing the value to search for.</param>
    /// <returns>Cypher expression that returns true if the value is found.</returns>
    public string Contains(string collectionExpression, string valueExpression)
    {
        return $"{valueExpression} IN {collectionExpression}";
    }
}
