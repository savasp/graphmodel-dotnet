// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Tests.GraphTests;

using Cvoya.Graph.CompatibilityTests;

public class BasicTests(Neo4jHarness harness) :
    Neo4jTest(harness),
    IBasicTests
{
    [Fact]
    public async Task RuntimeMetadataProperties_AreStoredAndRetrievedCorrectly()
    {
        // Test node labels
        var testNode = new Person { FirstName = "John", LastName = "Doe" };

        // Before save, labels should be empty
        Assert.Empty(testNode.Labels);

        await Graph.CreateNodeAsync(testNode, null, TestContext.Current.CancellationToken);

        testNode = await Graph.Nodes<Person>()
            .Where(person => person.TestKey == testNode.TestKey)
            .SingleAsync(TestContext.Current.CancellationToken);

        // When retrieving a node, labels should be populated with the actual Neo4j labels
        Assert.NotEmpty(testNode.Labels);
        Assert.Contains("Person", testNode.Labels);

        // Test relationship type
        var testRel = new Knows { Since = DateTime.UtcNow };

        // Before save, type should be empty
        Assert.Empty(testRel.Type);

        await Graph.CreateRelationshipAsync(
            Graph.Nodes<Person>().Where(person => person.TestKey == testNode.TestKey),
            testRel,
            Graph.Nodes<Person>().Where(person => person.TestKey == testNode.TestKey),
            cancellationToken: TestContext.Current.CancellationToken);

        testRel = await Graph.Relationships<Knows>()
            .Where(relationship => relationship.TestKey == testRel.TestKey)
            .SingleAsync(TestContext.Current.CancellationToken);

        // When retrieving a relationship, type should be populated with the actual Neo4j relationship type
        Assert.NotEmpty(testRel.Type);
        Assert.Equal("KNOWS", testRel.Type);
    }
}
