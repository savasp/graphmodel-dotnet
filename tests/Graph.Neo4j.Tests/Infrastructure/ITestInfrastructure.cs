// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Tests;

/// <summary>
/// Interface for Neo4j test infrastructure.
/// </summary>
public interface ITestInfrastructure : IAsyncDisposable, IAsyncLifetime
{
    /// <summary>
    /// Gets the connection string for the Neo4j database.
    /// </summary>
    string ConnectionString { get; }

    /// <summary>
    /// Gets the username for the Neo4j database.
    /// </summary>
    string Username { get; }

    /// <summary>
    /// Gets the password for the Neo4j database.
    /// </summary>
    string Password { get; }
}
