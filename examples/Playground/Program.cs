// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph;
using Cvoya.Graph.Neo4j;
using Neo4j.Driver;

// Playground to explore ideas and the interface

const string databaseName = "GraphModelPlayground";


// ==== SETUP a new database ====
var driver = GraphDatabase.Driver("bolt://localhost:7687", AuthTokens.Basic("neo4j", "password"));
await using (var session = driver.AsyncSession(sc => sc.WithDatabase("system")))
{
    await session.RunAsync($"CREATE OR REPLACE DATABASE {databaseName}");

    // Add a delay to ensure the database is fully created
    await Task.Delay(1000);
}
