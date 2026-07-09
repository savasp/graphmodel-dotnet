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

namespace Cvoya.Graph.Model.CompatibilityTests.Tests.Fakes;

/// <summary>
/// The minimal concrete <see cref="CompatibilityTest"/> needed to drive the base class's
/// <see cref="CompatibilityTest.InitializeAsync"/>/<see cref="CompatibilityTest.DisposeAsync"/>
/// choreography directly from a test body (rather than relying on xUnit's own dispatch), so
/// lifecycle ordering can be asserted deterministically in one place.
/// </summary>
internal sealed class TestableCompatibilityTest(IGraphProviderTestHarness harness) : CompatibilityTest(harness)
{
    public IGraph GraphForAssertions => Graph;
}
