// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

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
    public static ILoggerFactory LoggerFactory => TestInfrastructureFixture.LoggerFactory;

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
