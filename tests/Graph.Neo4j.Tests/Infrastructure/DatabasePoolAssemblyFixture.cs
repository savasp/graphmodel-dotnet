// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

[assembly: AssemblyFixture(typeof(Cvoya.Graph.Neo4j.Tests.DatabasePoolAssemblyFixture))]

namespace Cvoya.Graph.Neo4j.Tests;

/// <summary>
/// Disposes the process-wide database pool after all Neo4j tests have released their leases.
/// </summary>
public sealed class DatabasePoolAssemblyFixture : IAsyncDisposable
{
    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await DatabasePoolManager.DisposeInstanceAsync();
    }
}
