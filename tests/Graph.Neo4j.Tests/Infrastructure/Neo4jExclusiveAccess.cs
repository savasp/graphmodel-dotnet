// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Tests;

/// <summary>
/// Groups tests that access the configured Neo4j database outside the provider test harness so
/// they cannot overlap harness-managed schema or data operations.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class Neo4jExclusiveAccess
{
    /// <summary>
    /// The xUnit collection name used by tests that require exclusive Neo4j access.
    /// </summary>
    public const string Name = "Neo4j exclusive access";
}
