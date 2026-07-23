// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Tests.GraphTests;

using Cvoya.Graph.CompatibilityTests;

public class ComplexObjectGraphSerializationTests(AgeHarness harness) : AgeTest(harness), IComplexObjectGraphSerializationTests
{
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
