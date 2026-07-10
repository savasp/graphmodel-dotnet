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

namespace Cvoya.Graph.Neo4j.Tests;

using Cvoya.Graph.CompatibilityTests;
using Microsoft.Extensions.Logging;

/// <summary>
/// The Neo4j binding of <see cref="IGraphProviderTestHarness"/>. Delegates to the existing
/// <see cref="TestInfrastructureFixture"/>/<see cref="DatabasePoolManager"/> machinery rather than
/// duplicating it: this class is the reference implementation of the compatibility suite's SPI.
/// </summary>
public sealed class Neo4jHarness : IGraphProviderTestHarness
{
    private readonly TestInfrastructureFixture fixture = new();

    /// <inheritdoc/>
    public string ProviderName => "Cvoya.Graph.Neo4j";

    /// <inheritdoc/>
    public CapabilitySet Capabilities => CapabilitySet.All;

    /// <summary>
    /// Gets the logger factory shared with the underlying <see cref="TestInfrastructureFixture"/>,
    /// so <c>Neo4jTest</c> can keep logging/correlation behavior consistent with test infrastructure.
    /// </summary>
    public ILoggerFactory LoggerFactory => fixture.LoggerFactory;

    /// <inheritdoc/>
    public ValueTask InitializeAsync() => fixture.InitializeAsync();

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => fixture.DisposeAsync();

    /// <inheritdoc/>
    public async ValueTask<IGraph> GetGraphAsync(StoreIsolation isolation, CancellationToken cancellationToken)
    {
        try
        {
            return await fixture.GetGraph(getNewDatabase: isolation == StoreIsolation.FreshStore);
        }
        catch (TestInfrastructureFixture.Neo4jTestInfrastructureUnavailableException ex)
        {
            throw new GraphProviderUnavailableException(ex.Message, ex);
        }
    }
}
