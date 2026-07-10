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

/// <summary>
/// A single example binding: implementing one <c>I*Tests</c> interface is a one-line class. A
/// real provider's test project has one of these per suite interface it wants to run (typically
/// all of them) - see <c>tests/Graph.Model.Neo4j.Tests/GraphModelTests</c> for the full set.
/// </summary>
public sealed class SampleBasicTests(SampleHarness harness) : SampleProviderTest(harness), IBasicTests;
