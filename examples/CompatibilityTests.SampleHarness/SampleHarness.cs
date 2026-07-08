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

namespace Cvoya.Graph.Model.CompatibilityTests.SampleHarness;

using Cvoya.Graph.Model.CompatibilityTests;

/// <summary>
/// Doc-shaped skeleton showing what a provider implements to certify against the GraphModel
/// compatibility suite. This harness has no real backing store - <see cref="GetGraphAsync"/>
/// always throws <see cref="GraphProviderUnavailableException"/> - so binding classes here compile
/// against the suite but every test they inherit skips or fails when actually run. See
/// <c>docs/provider-implementers-guide.md</c> ("Certifying a provider") for the real workflow.
/// </summary>
public sealed class SampleHarness : IGraphProviderTestHarness
{
    /// <inheritdoc/>
    public string ProviderName => "Cvoya.Graph.Model.SampleProvider";

    /// <summary>
    /// A real provider declares exactly the capabilities its backing store supports. This sample
    /// declares everything except full-text search, purely as an illustration.
    /// </summary>
    public CapabilitySet Capabilities { get; } = CapabilitySet.All.Except(GraphCapability.FullTextSearch);

    /// <inheritdoc/>
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// A real provider would start/connect to its backing store here and return an
    /// <see cref="IGraph"/> over an empty instance of it. This sample has none, so it always
    /// throws - the same way a real harness would if its infrastructure (e.g. Docker) were
    /// unavailable.
    /// </summary>
    public ValueTask<IGraph> GetGraphAsync(StoreIsolation isolation, CancellationToken cancellationToken) =>
        throw new GraphProviderUnavailableException("The sample harness has no backing store.");
}
