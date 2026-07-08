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
/// Proves the per-test choreography <see cref="CompatibilityTest"/> runs happens in the order a
/// provider's harness contract promises: harness class-init, per-test store acquisition, per-test
/// store disposal, harness class-dispose.
/// </summary>
/// <remarks>
/// Drives <see cref="RecordingFakeHarness"/> and a <see cref="TestableCompatibilityTest"/> bound
/// to it directly (rather than going through xUnit's own <c>IClassFixture</c>/<c>IAsyncLifetime</c>
/// dispatch), so the full sequence - including harness disposal, which normally only happens after
/// every test in a real test class has finished - can be observed and asserted from one place. It
/// still exercises the real production types end to end.
/// <para>
/// In the shared <see cref="ComplianceLedgerCollection"/> collection because the acquisition below
/// increments <see cref="ComplianceGuard"/>'s static ledger.
/// </para>
/// </remarks>
[Collection(ComplianceLedgerCollection.Name)]
public sealed class LifecycleOrderTests
{
    [Fact]
    public async Task ClassInit_ThenPerTestAcquire_ThenPerTestDispose_ThenClassDispose()
    {
        var harness = new RecordingFakeHarness();

        await harness.InitializeAsync();
        var sut = new TestableCompatibilityTest(harness);

        await sut.InitializeAsync();
        Assert.NotNull(sut.GraphForAssertions);

        await sut.DisposeAsync();
        await harness.DisposeAsync();

        Assert.Equal(
            ["harness-init", "store-acquire", "store-dispose", "harness-dispose"],
            harness.Events);
    }
}
