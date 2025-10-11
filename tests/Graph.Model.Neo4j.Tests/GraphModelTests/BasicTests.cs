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

using Cvoya.Graph.Model.Tests;

namespace Cvoya.Graph.Model.Neo4j.Tests.GraphModelTests;

public class BasicTests(TestInfrastructureFixture fixture) :
    Neo4jTest(fixture),
    Model.Tests.IBasicTests
{
    [Fact]
    public async Task RuntimeMetadataProperties_AreStoredAndRetrievedCorrectly()
    {
        // Test node labels
        var testNode = new Person { FirstName = "John", LastName = "Doe" };

        // Before save, labels should be empty
        Assert.Empty(testNode.Labels);

        await Graph.CreateNodeAsync(testNode, null, TestContext.Current.CancellationToken);

        testNode = await Graph.GetNodeAsync<Person>(testNode.Id, null, TestContext.Current.CancellationToken);

        // When retrieving a node, labels should be populated with the actual Neo4j labels
        Assert.NotEmpty(testNode.Labels);
        Assert.Contains("Person", testNode.Labels);

        // Test relationship type
        var testRel = new Knows { StartNodeId = testNode.Id, EndNodeId = testNode.Id, Since = DateTime.UtcNow };

        // Before save, type should be empty
        Assert.Empty(testRel.Type);

        await Graph.CreateRelationshipAsync(testRel, null, TestContext.Current.CancellationToken);

        testRel = await Graph.GetRelationshipAsync<Knows>(testRel.Id, null, TestContext.Current.CancellationToken);

        // When retrieving a relationship, type should be populated with the actual Neo4j relationship type
        Assert.NotEmpty(testRel.Type);
        Assert.Equal("KNOWS", testRel.Type);
    }
}