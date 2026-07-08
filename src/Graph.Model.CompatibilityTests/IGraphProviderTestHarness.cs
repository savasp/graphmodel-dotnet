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

namespace Cvoya.Graph.Model.CompatibilityTests;

/// <summary>
/// The seam a provider implements to run the GraphModel compatibility suite against its own
/// backing store.
/// </summary>
/// <remarks>
/// One instance is created per test class (an xUnit class fixture). xUnit parallelizes test
/// classes, so implementations must tolerate concurrent instances and share expensive
/// process-wide state (containers, connection pools) statically, the way the in-tree Neo4j
/// harness does.
/// </remarks>
public interface IGraphProviderTestHarness : IAsyncLifetime
{
    /// <summary>
    /// Gets the provider's display name, used in capability skip reasons and compliance reports.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Gets the set of <see cref="GraphCapability"/> values this provider declares support for.
    /// Read once per test, before the store is acquired, to decide whether the test should skip.
    /// </summary>
    CapabilitySet Capabilities { get; }

    /// <summary>
    /// Gets an <see cref="IGraph"/> over an empty store, called once per test.
    /// </summary>
    /// <param name="isolation">
    /// The isolation the returned store must provide - see <see cref="StoreIsolation"/>.
    /// </param>
    /// <param name="cancellationToken">A cancellation token for the acquisition.</param>
    /// <returns>An <see cref="IGraph"/> backed by an empty store.</returns>
    /// <exception cref="GraphProviderUnavailableException">
    /// The backing infrastructure (for example, a Docker-hosted database) could not be started or
    /// reached.
    /// </exception>
    ValueTask<IGraph> GetGraphAsync(StoreIsolation isolation, CancellationToken cancellationToken);
}
