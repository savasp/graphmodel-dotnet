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

namespace Cvoya.Graph.CompatibilityTests.SampleHarness;

using Cvoya.Graph.CompatibilityTests;
using Xunit;

/// <summary>
/// The intermediate base a real provider's test project declares once, then never touches again -
/// every <c>I*Tests</c> binding class derives from it. Mirrors <c>Neo4jTest</c> in
/// <c>tests/Graph.Model.Neo4j.Tests</c>, the in-tree reference implementation.
/// </summary>
/// <param name="harness">The sample harness, injected by xUnit as a class fixture.</param>
public abstract class SampleProviderTest(SampleHarness harness)
    : CompatibilityTest(harness), IClassFixture<SampleHarness>;
