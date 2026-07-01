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

public class TakeOperatorTests(TestInfrastructureFixture fixture) :
    AgeTest(fixture),
    ITakeOperatorTests
{
    /// <summary>
    /// Skipped: Full-text search is not supported in Apache AGE.
    /// See docs/age/age-fulltext-search-limitations.md
    /// </summary>
    [Fact(Skip = "Full-text search is not supported in Apache AGE.")]
    public async Task TakeOperator_WithFullTextSearch_GeneratesCorrectCypher()
    {
        await Task.CompletedTask;
    }
}
