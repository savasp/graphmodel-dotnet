// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Tests;

public sealed class DatabasePoolManagerTests
{
    [Fact]
    public void DatabasePoolManager_NamespacesAreProcessUnique()
    {
        var firstRunNames = Enumerable.Range(0, 20)
            .Select(index => DatabasePoolManager.GetDatabaseName(index, "p1tfirst"))
            .ToHashSet(StringComparer.Ordinal);
        var secondRunNames = Enumerable.Range(0, 20)
            .Select(index => DatabasePoolManager.GetDatabaseName(index, "p2tsecond"))
            .ToHashSet(StringComparer.Ordinal);

        Assert.Empty(firstRunNames.Intersect(secondRunNames, StringComparer.Ordinal));
        Assert.All(
            firstRunNames.Concat(secondRunNames),
            databaseName =>
            {
                // Neo4j database names must be 3-63 characters and start with an ASCII letter.
                Assert.InRange(databaseName.Length, 3, 63);
                Assert.Matches("^[a-z][a-z0-9-]*$", databaseName);
            });
    }

    [Fact]
    public void DatabasePoolManager_ProcessRunIdProducesNeo4jSafeNames()
    {
        var databaseName = DatabasePoolManager.GetDatabaseName(0);

        Assert.InRange(databaseName.Length, 3, 63);
        Assert.Matches("^graphtests-[a-z0-9]+-000$", databaseName);
    }
}
