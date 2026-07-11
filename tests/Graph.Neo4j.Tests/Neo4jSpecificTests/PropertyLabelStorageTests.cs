// Copyright CVOYA. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Tests;

using Cvoya.Graph.CompatibilityTests;
using Cvoya.Graph.Neo4j.Core;
using global::Neo4j.Driver;

public sealed class PropertyLabelStorageTests(Neo4jHarness harness) : Neo4jTest(harness)
{
    [Fact]
    public async Task SchemaInitialization_UsesPhysicalPropertyLabelForIndex()
    {
        var person = new IAttributeValidationTests.PersonWithCustomPropertyLabels
        {
            FirstName = "Ada",
            LastName = "Lovelace",
            Email = "ada@example.com",
            Age = 36
        };
        await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);

        await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);
        var neo4jTransaction = (GraphTransaction)transaction;
        var result = await neo4jTransaction.Transaction.RunAsync(
            "SHOW INDEXES YIELD name, properties " +
            "WHERE name = 'idx_indexedperson_last_name' RETURN properties");
        var records = await result.ToListAsync(TestContext.Current.CancellationToken);
        await transaction.CommitAsync();

        var indexedProperties = records
            .SelectMany(record => record["properties"].As<List<string>>())
            .ToArray();
        Assert.Contains("last_name", indexedProperties);
        Assert.DoesNotContain(nameof(IAttributeValidationTests.PersonWithCustomPropertyLabels.LastName), indexedProperties);
    }
}
