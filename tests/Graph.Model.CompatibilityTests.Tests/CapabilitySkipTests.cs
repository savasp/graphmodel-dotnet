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

namespace Cvoya.Graph.Model.CompatibilityTests.Tests;

using Cvoya.Graph.Model.CompatibilityTests.Tests.Fakes;

/// <summary>
/// Pins <see cref="CompatibilityTest.InitializeAsync"/>'s capability-skip behavior end to end,
/// through the real xUnit trait-discovery path: <see cref="FakeHarness"/> declares everything
/// except <see cref="GraphCapability.FullTextSearch"/>, and this class carries one method that
/// requires it (which must be reported skipped, never executed) alongside one plain method (which
/// must execute and successfully acquire the fake store).
/// </summary>
/// <remarks>
/// In the shared <see cref="ComplianceLedgerCollection"/> collection because
/// <see cref="ExecutesAndAcquiresGraph"/> increments <see cref="ComplianceGuard"/>'s static
/// ledger; see that collection's definition for why.
/// </remarks>
[Collection(ComplianceLedgerCollection.Name)]
public sealed class CapabilitySkipTests(FakeHarness harness) : FakeProviderTest(harness)
{
    [Fact]
    [RequiresCapability(GraphCapability.FullTextSearch)]
    public void RequiresUndeclaredCapability_IsSkippedNotExecuted()
    {
        // FakeHarness does not declare FullTextSearch, so CompatibilityTest.InitializeAsync must
        // have called Assert.Skip and returned before this body ever ran. If this executes, the
        // skip mechanism is broken.
        Assert.Fail(
            "This test requires GraphCapability.FullTextSearch, which FakeHarness does not " +
            "declare. It should have been skipped by CompatibilityTest.InitializeAsync, not executed.");
    }

    [Fact]
    public void ExecutesAndAcquiresGraph()
    {
        // No capability requirement: this must run normally and have a usable Graph.
        Assert.NotNull(Graph);
        Assert.NotNull(Graph.SchemaRegistry);
    }
}
