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

public class AdvancedQueryTests(TestInfrastructureFixture fixture) :
    AgeTest(fixture),
    IAdvancedQueryTests
{
    // See docs/age/pattern-comprehension-limitations.md for root cause analysis and
    // estimated fix effort for these failing pattern comprehension tests.

    [Fact(Skip = "Pattern comprehensions not supported in AGE. See docs/age/pattern-comprehension-limitations.md")]
    public async Task CanQueryWithBasicPatternComprehension() => await Task.CompletedTask;

    [Fact(Skip = "ORDER BY with pattern expressions not supported in AGE. See docs/age/pattern-comprehension-limitations.md")]
    public async Task CanQueryWithOrderedPatternComprehension() => await Task.CompletedTask;


    [Fact(Skip = "Nested GroupBy not supported in AGE. See docs/age/pattern-comprehension-limitations.md")]
    public async Task CanQueryWithGroupedPatternComprehension() => await Task.CompletedTask;

    [Fact(Skip = "size() with pattern expressions not supported in AGE. See docs/age/pattern-comprehension-limitations.md")]
    public async Task CanProjectRelationshipCounts() => await Task.CompletedTask;
}
