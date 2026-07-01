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

namespace Cvoya.Graph.Model.Neo4j.Tests.GraphModelTests;

public class AdvancedQueryTests(TestInfrastructureFixture fixture) :
    Neo4jTest(fixture),
    Model.Tests.IAdvancedQueryTests
{
    // Pattern comprehension features are implemented in the AGE provider which has full
    // Cypher collect() support for grouped projections with nested Select().ToList() calls.
    // The Neo4j provider skips these until equivalently refactored.
    // See docs/age-provider-branch-diff.md for the overall migration strategy.

    [Fact(Skip = "Pattern comprehensions with nested collections not yet implemented")]
    public async Task CanQueryWithBasicPatternComprehension() => await Task.CompletedTask;

    [Fact(Skip = "Pattern comprehensions with nested collections not yet implemented")]
    public async Task CanQueryWithFilteredPatternComprehension() => await Task.CompletedTask;

    [Fact(Skip = "Pattern comprehensions with nested collections not yet implemented")]
    public async Task CanQueryWithTimeBasedPatternComprehension() => await Task.CompletedTask;

    [Fact(Skip = "Too complex for now")]
    public async Task CanQueryWithOrderedPatternComprehension() => await Task.CompletedTask;

    [Fact(Skip = "Too complex for now")]
    public async Task CanQueryWithGroupedPatternComprehension() => await Task.CompletedTask;

    [Fact(Skip = "Pattern comprehensions with nested Select().ToList() not yet implemented")]
    public async Task CanQueryWithTraversePathAndGroupBy() => await Task.CompletedTask;

    [Fact(Skip = "Cross-collection correlation in projections not yet implemented")]
    public async Task CanProjectRelationshipCounts() => await Task.CompletedTask;

    [Fact(Skip = "Pattern comprehensions with nested Select().ToList() not yet implemented")]
    public async Task CanCombineNodeAndRelationshipQueries() => await Task.CompletedTask;
}
