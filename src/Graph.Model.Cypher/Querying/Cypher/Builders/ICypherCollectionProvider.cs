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

namespace Cvoya.Graph.Model.Cypher.Querying.Cypher.Builders;

/// <summary>
/// Provides provider-specific collection operations for Cypher queries.
/// This abstraction allows different providers to implement collection functions
/// using their native capabilities (e.g., APOC for Neo4j, standard functions for AGE).
/// </summary>
public interface ICypherCollectionProvider
{
    /// <summary>
    /// Generates Cypher expression to convert a collection to a set (remove duplicates).
    /// </summary>
    /// <param name="collectionExpression">The Cypher expression representing the collection.</param>
    /// <returns>Cypher expression that returns a set with unique values.</returns>
    string ToSet(string collectionExpression);

    /// <summary>
    /// Generates Cypher expression to get the size/length of a collection.
    /// </summary>
    /// <param name="collectionExpression">The Cypher expression representing the collection.</param>
    /// <returns>Cypher expression that returns the collection size.</returns>
    string Size(string collectionExpression);

    /// <summary>
    /// Generates Cypher expression to check if a collection contains a specific value.
    /// </summary>
    /// <param name="collectionExpression">The Cypher expression representing the collection.</param>
    /// <param name="valueExpression">The Cypher expression representing the value to search for.</param>
    /// <returns>Cypher expression that returns true if the value is found.</returns>
    string Contains(string collectionExpression, string valueExpression);
}