// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Tests.GraphTests;

using Cvoya.Graph.CompatibilityTests;

public class ComplexObjectGraphSerializationTests(AgeHarness harness) : AgeTest(harness), IComplexObjectGraphSerializationTests
{
    async Task IComplexObjectGraphSerializationTests.Navigation_ComplexPropertyPredicate_MatchesCollidingDomainRelationship()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var owner = new ContractAddressOwner
        {
            Address = new ContractAddressValue { City = "Complex Austin", Street = "1st Ave" },
        };
        await Graph.CreateNodeAsync(owner, cancellationToken: cancellationToken);
        var domainNode = new ContractAddressNode { City = "Domain Boulder", Street = "2nd Ave" };
        await Graph.CreateNodeAsync(domainNode, cancellationToken: cancellationToken);
        await Graph.ConnectAsync(
            owner,
            new ContractPrimaryAddress(),
            domainNode,
            cancellationToken: cancellationToken);

        var viaDomainValue = await Graph.Nodes<ContractAddressOwner>()
            .Where(candidate => candidate.Address.City == "Domain Boulder")
            .ToListAsync(cancellationToken);
        Assert.DoesNotContain(viaDomainValue, candidate => candidate.TestKey == owner.TestKey);

        var viaComplexValue = await Graph.Nodes<ContractAddressOwner>()
            .Where(candidate => candidate.Address.City == "Complex Austin")
            .ToListAsync(cancellationToken);
        var fetched = Assert.Single(viaComplexValue, candidate => candidate.TestKey == owner.TestKey);
        Assert.Equal("Complex Austin", fetched.Address.City);
    }

    [Fact]
    public async Task ComplexPropertyCollection_LargeCollection_RoundTrips()
    {
        var animals = Enumerable.Range(0, 50)
            .Select(i => new PoliceDogDescription
            {
                Name = $"Dog {i:D2}",
                Breed = $"Breed {i:D2}",
                Badge = $"Badge {i:D2}",
                Handler = new HandlerDescription { Name = $"Handler {i:D2}" },
            })
            .ToList<AnimalDescription>();

        var node = new Kennel { Name = "Large Kennel", Animals = animals };
        await Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken);

        var fetched = await Graph.FindNodeAsync(
            node,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(animals.Count, fetched.Animals.Count);
        for (var index = 0; index < animals.Count; index++)
        {
            var expected = Assert.IsType<PoliceDogDescription>(animals[index]);
            var actual = Assert.IsType<PoliceDogDescription>(fetched.Animals[index]);
            Assert.Equal(expected.Name, actual.Name);
            Assert.Equal(expected.Breed, actual.Breed);
            Assert.Equal(expected.Badge, actual.Badge);
            Assert.Equal(expected.Handler?.Name, actual.Handler?.Name);
        }
    }
}
