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

namespace Cvoya.Graph.CompatibilityTests.Tests.Fakes;

/// <summary>
/// A <see cref="IGraphProviderTestHarness"/> that appends a label to <see cref="Events"/> at each
/// lifecycle point, so a test can drive it (and a <see cref="CompatibilityTest"/> bound to it)
/// directly and assert the resulting order.
/// </summary>
internal sealed class RecordingFakeHarness : IGraphProviderTestHarness
{
    public List<string> Events { get; } = [];

    public string ProviderName => "Cvoya.Graph.CompatibilityTests.Tests.RecordingFakeProvider";

    public CapabilitySet Capabilities => CapabilitySet.All;

    public ValueTask InitializeAsync()
    {
        Events.Add("harness-init");
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        Events.Add("harness-dispose");
        return ValueTask.CompletedTask;
    }

    public ValueTask<IGraph> GetGraphAsync(StoreIsolation isolation, CancellationToken cancellationToken)
    {
        Events.Add("store-acquire");
        return ValueTask.FromResult<IGraph>(new FakeGraph(() => Events.Add("store-dispose")));
    }
}
