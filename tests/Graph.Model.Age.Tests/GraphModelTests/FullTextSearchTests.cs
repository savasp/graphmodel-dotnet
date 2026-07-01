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

using Cvoya.Graph.Model.Tests;

namespace Cvoya.Graph.Model.Age.Tests.GraphModelTests;

/// <summary>
/// Full-text search tests for the AGE provider.
/// AGE does not provide native full-text search within its Cypher implementation.
/// See <see href="docs/age/age-fulltext-search-limitations.md"/> for details.
/// </summary>
public class FullTextSearchTests(TestInfrastructureFixture fixture) :
    AgeTest(fixture),
    IFullTextSearchTests
{
    /// <inheritdoc cref="IFullTextSearchTests.CanSearchNodesWithFullTextSearch"/>
    [Fact(Skip = "AGE does not support full-text search within Cypher. See docs/age/age-fulltext-search-limitations.md")]
    public async Task CanSearchNodesWithFullTextSearch() => await Task.CompletedTask;

    /// <inheritdoc cref="IFullTextSearchTests.CanSearchNodesWithComplexPropertiesAndFullTextSearch"/>
    [Fact(Skip = "AGE does not support full-text search within Cypher. See docs/age/age-fulltext-search-limitations.md")]
    public async Task CanSearchNodesWithComplexPropertiesAndFullTextSearch() => await Task.CompletedTask;

    /// <inheritdoc cref="IFullTextSearchTests.CanSearchRelationshipsWithFullTextSearch"/>
    [Fact(Skip = "AGE does not support full-text search within Cypher. See docs/age/age-fulltext-search-limitations.md")]
    public async Task CanSearchRelationshipsWithFullTextSearch() => await Task.CompletedTask;

    /// <inheritdoc cref="IFullTextSearchTests.CanSearchAllEntitiesWithFullTextSearch"/>
    [Fact(Skip = "AGE does not support full-text search within Cypher. See docs/age/age-fulltext-search-limitations.md")]
    public async Task CanSearchAllEntitiesWithFullTextSearch() => await Task.CompletedTask;

    /// <inheritdoc cref="IFullTextSearchTests.CanSearchNodesWithGenericInterface"/>
    [Fact(Skip = "AGE does not support full-text search within Cypher. See docs/age/age-fulltext-search-limitations.md")]
    public async Task CanSearchNodesWithGenericInterface() => await Task.CompletedTask;

    /// <inheritdoc cref="IFullTextSearchTests.CanSearchRelationshipsWithGenericInterface"/>
    [Fact(Skip = "AGE does not support full-text search within Cypher. See docs/age/age-fulltext-search-limitations.md")]
    public async Task CanSearchRelationshipsWithGenericInterface() => await Task.CompletedTask;

    /// <inheritdoc cref="IFullTextSearchTests.SearchReturnsEmptyResultsForNonMatchingQuery"/>
    [Fact(Skip = "AGE does not support full-text search within Cypher. See docs/age/age-fulltext-search-limitations.md")]
    public async Task SearchReturnsEmptyResultsForNonMatchingQuery() => await Task.CompletedTask;

    /// <inheritdoc cref="IFullTextSearchTests.SearchIsNotCaseSensitive"/>
    [Fact(Skip = "AGE does not support full-text search within Cypher. See docs/age/age-fulltext-search-limitations.md")]
    public async Task SearchIsNotCaseSensitive() => await Task.CompletedTask;

    /// <inheritdoc cref="IFullTextSearchTests.SearchWithWhereClause"/>
    [Fact(Skip = "AGE does not support full-text search within Cypher. See docs/age/age-fulltext-search-limitations.md")]
    public async Task SearchWithWhereClause() => await Task.CompletedTask;

    /// <inheritdoc cref="IFullTextSearchTests.SearchWithWhereClauseAndMultipleProperties"/>
    [Fact(Skip = "AGE does not support full-text search within Cypher. See docs/age/age-fulltext-search-limitations.md")]
    public async Task SearchWithWhereClauseAndMultipleProperties() => await Task.CompletedTask;

    /// <inheritdoc cref="IFullTextSearchTests.SearchWithSelectClause"/>
    [Fact(Skip = "AGE does not support full-text search within Cypher. See docs/age/age-fulltext-search-limitations.md")]
    public async Task SearchWithSelectClause() => await Task.CompletedTask;

    /// <inheritdoc cref="IFullTextSearchTests.CanSearchInLinqChain"/>
    [Fact(Skip = "AGE does not support full-text search within Cypher. See docs/age/age-fulltext-search-limitations.md")]
    public async Task CanSearchInLinqChain() => await Task.CompletedTask;

    /// <inheritdoc cref="IFullTextSearchTests.CanSearchInPathSegmentsChain"/>
    [Fact(Skip = "AGE does not support full-text search within Cypher. See docs/age/age-fulltext-search-limitations.md")]
    public async Task CanSearchInPathSegmentsChain() => await Task.CompletedTask;

    /// <inheritdoc cref="IFullTextSearchTests.CanSearchWithMultipleConditions"/>
    [Fact(Skip = "AGE does not support full-text search within Cypher. See docs/age/age-fulltext-search-limitations.md")]
    public async Task CanSearchWithMultipleConditions() => await Task.CompletedTask;

    /// <inheritdoc cref="IFullTextSearchTests.CanSearchWithSelectProjection"/>
    [Fact(Skip = "AGE does not support full-text search within Cypher. See docs/age/age-fulltext-search-limitations.md")]
    public async Task CanSearchWithSelectProjection() => await Task.CompletedTask;

    /// <inheritdoc cref="IFullTextSearchTests.SearchInLinqChainReturnsEmptyForNonMatchingQuery"/>
    [Fact(Skip = "AGE does not support full-text search within Cypher. See docs/age/age-fulltext-search-limitations.md")]
    public async Task SearchInLinqChainReturnsEmptyForNonMatchingQuery() => await Task.CompletedTask;

    /// <inheritdoc cref="IFullTextSearchTests.SearchInLinqChainIsNotCaseSensitive"/>
    [Fact(Skip = "AGE does not support full-text search within Cypher. See docs/age/age-fulltext-search-limitations.md")]
    public async Task SearchInLinqChainIsNotCaseSensitive() => await Task.CompletedTask;

    /// <inheritdoc cref="IFullTextSearchTests.SearchWorksWithInheritance"/>
    [Fact(Skip = "AGE does not support full-text search within Cypher. See docs/age/age-fulltext-search-limitations.md")]
    public async Task SearchWorksWithInheritance() => await Task.CompletedTask;

    /// <inheritdoc cref="IFullTextSearchTests.CanSearchDynamicNodeWithFullTextSearch"/>
    [Fact(Skip = "AGE does not support full-text search within Cypher. See docs/age/age-fulltext-search-limitations.md")]
    public async Task CanSearchDynamicNodeWithFullTextSearch() => await Task.CompletedTask;

    /// <inheritdoc cref="IFullTextSearchTests.CanSearchDynamicRelationshipWithFullTextSearch"/>
    [Fact(Skip = "AGE does not support full-text search within Cypher. See docs/age/age-fulltext-search-limitations.md")]
    public async Task CanSearchDynamicRelationshipWithFullTextSearch() => await Task.CompletedTask;
}
