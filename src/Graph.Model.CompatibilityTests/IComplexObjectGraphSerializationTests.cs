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

namespace Cvoya.Graph.Model.CompatibilityTests;

public interface IComplexObjectGraphSerializationTests : IGraphModelTest
{
    [Fact]
    public async Task CanCreateAndGetNodeWithComplexProperty()
    {
        var n1 = new Class1 { Property1 = "Value A", Property2 = "Value B", A = new ComplexClassA { Property1 = "Nested A", Property2 = "Nested B" } };

        await this.Graph.CreateNodeAsync(n1, null, TestContext.Current.CancellationToken);
        var fetched = await this.Graph.GetNodeAsync<Class1>(n1.Id, null, TestContext.Current.CancellationToken);
        Assert.Equal(n1.Property1, fetched.Property1);
        Assert.Equal(n1.Property2, fetched.Property2);
        Assert.Equal(n1.A?.Property1, fetched.A?.Property1);
        Assert.Equal(n1.A?.Property2, fetched.A?.Property2);
    }

    [Fact]
    public async Task CanCreateAndGetNodeWithComplexPropertyTree()
    {
        // Create n1 -> A -> B
        var n1 = new Class1 { Property1 = "Value A", Property2 = "Value B", A = new ComplexClassA { Property1 = "Nested A", Property2 = "Nested B" } };
        n1.A.B = new ComplexClassB { Property1 = "Nested B1" };

        await this.Graph.CreateNodeAsync(n1, null, TestContext.Current.CancellationToken);
        var fetched = await this.Graph.GetNodeAsync<Class1>(n1.Id, null, TestContext.Current.CancellationToken);
        Assert.Equal(n1.Property1, fetched.Property1);
        Assert.Equal(n1.Property2, fetched.Property2);
        Assert.Equal(n1.A?.Property1, fetched.A?.Property1);
        Assert.Equal(n1.A?.Property2, fetched.A?.Property2);
        Assert.Equal(n1.A?.B?.Property1, fetched.A?.B?.Property1);
    }

    [Fact]
    public async Task CanCreateAndGetNodeWithComplexGraph()
    {
        // Create
        // n1 -> A
        var n1 = new Class1 { Property1 = "Value A", Property2 = "Value B", A = new ComplexClassA { Property1 = "Nested A", Property2 = "Nested B" } };
        // A -> B
        n1.A.B = new ComplexClassB { Property1 = "Nested B1" };
        // A -> C
        n1.A.C = new ComplexClassC { Property1 = "Nested C1" };
        // C -> B
        n1.A.C.B = new ComplexClassB();

        await this.Graph.CreateNodeAsync(n1, null, TestContext.Current.CancellationToken);
        var fetched = await this.Graph.GetNodeAsync<Class1>(n1.Id, null, TestContext.Current.CancellationToken);
        Assert.Equal(n1.Property1, fetched.Property1);
        Assert.Equal(n1.Property2, fetched.Property2);
        Assert.Equal(n1.A?.Property1, fetched.A?.Property1);
        Assert.Equal(n1.A?.Property2, fetched.A?.Property2);
        Assert.Equal(n1.A?.B?.Property1, fetched.A?.B?.Property1);
        Assert.Equal(n1.A?.C?.Property1, fetched.A?.C?.Property1);
        Assert.NotNull(fetched.A?.C?.B);
        Assert.Equal(n1.A?.C?.B?.Property1, fetched.A?.C?.B?.Property1);
    }

    [Fact]
    public async Task CannotCreateNodeWithObjectGraphWithCycles()
    {
        // Create n1 -> A -> B -> A
        var n1 = new Class1 { Property1 = "Value A", Property2 = "Value B", A = new ComplexClassA { Property1 = "Nested A", Property2 = "Nested B" } };
        n1.A.B = new ComplexClassB { Property1 = "Nested B1" };
        n1.A.B.A = n1.A; // Cycle

        await Assert.ThrowsAsync<GraphException>(() => this.Graph.CreateNodeAsync(n1, null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CanCreateAndGetNodeWithListOfComplexProperties()
    {
        // Create
        var n1 = new Class2 { Property1 = "Value A", Property2 = "Value B" };
        n1.A.Add(new ComplexClassA { Property1 = "Nested A1", Property2 = "Nested B1" });
        n1.A.Add(new ComplexClassA { Property1 = "Nested A2", Property2 = "Nested B2" });
        n1.B.Add(new ComplexClassB { Property1 = "Nested B1" });
        n1.B.Add(new ComplexClassB { Property1 = "Nested B2" });
        n1.A[0].B = new ComplexClassB { Property1 = "Nested B3" };
        n1.A[0].C = new ComplexClassC { Property1 = "Nested C1" };
        n1.A[1].B = n1.A[0].B; // Share B between A[0] and A[1]
        n1.B[0].A = n1.A[0]; // Share A[0] with B[0]

        await this.Graph.CreateNodeAsync(n1, null, TestContext.Current.CancellationToken);
        var fetched = await this.Graph.GetNodeAsync<Class2>(n1.Id, null, TestContext.Current.CancellationToken);

        Assert.Equal(n1.Property1, fetched.Property1);
        Assert.Equal(n1.Property2, fetched.Property2);
        Assert.Equal(n1.A.Count, fetched.A.Count);
        Assert.Equal(n1.B.Count, fetched.B.Count);
        Assert.Equal(n1.A[0].Property1, fetched.A[0].Property1);
        Assert.Equal(n1.A[0].Property2, fetched.A[0].Property2);
        Assert.Equal(n1.A[0].B?.Property1, fetched.A[0].B?.Property1);
        Assert.Equal(n1.B[0].Property1, fetched.B[0].Property1);
        Assert.Equal(n1.B[1].Property1, fetched.B[1].Property1);
        Assert.Equal(n1.A[1].Property1, fetched.A[1].Property1);
        Assert.Equal(n1.A[1].Property2, fetched.A[1].Property2);
    }

    [Fact]
    public async Task CanCreateAndGetNodeWithMixedDerivedInstancesInBaseTypedComplexPropertyCollection()
    {
        // 3-level hierarchy (AnimalDescription <- DogDescription <- PoliceDogDescription), mixed order,
        // and a nested complex property (Handler) only present on the most-derived level - all three
        // must survive the round trip through a `List<AnimalDescription>` complex-property collection (#146).
        var kennel = new Kennel
        {
            Name = "Central Kennel",
            Animals =
            [
                new DogDescription { Name = "Rex", Breed = "Labrador" },
                new PoliceDogDescription
                {
                    Name = "K9",
                    Breed = "Shepherd",
                    Badge = "K9-42",
                    Handler = new HandlerDescription { Name = "Officer Diaz" },
                },
                new AnimalDescription { Name = "Generic Animal" },
                new DogDescription { Name = "Fido", Breed = "Poodle" },
            ],
        };

        await this.Graph.CreateNodeAsync(kennel, null, TestContext.Current.CancellationToken);
        var fetched = await this.Graph.GetNodeAsync<Kennel>(kennel.Id, null, TestContext.Current.CancellationToken);

        Assert.Equal(kennel.Animals.Count, fetched.Animals.Count);

        // Order preserved.
        for (var i = 0; i < kennel.Animals.Count; i++)
        {
            Assert.Equal(kennel.Animals[i].GetType(), fetched.Animals[i].GetType());
            Assert.Equal(kennel.Animals[i].Name, fetched.Animals[i].Name);
        }

        // Types preserved, including the mixed derived instances at every level of the hierarchy.
        Assert.IsType<DogDescription>(fetched.Animals[0]);
        var policeDog = Assert.IsType<PoliceDogDescription>(fetched.Animals[1]);
        Assert.Equal("K9-42", policeDog.Badge);

        // Nested complex property on the derived element is intact.
        Assert.NotNull(policeDog.Handler);
        Assert.Equal("Officer Diaz", policeDog.Handler!.Name);

        Assert.IsType<AnimalDescription>(fetched.Animals[2]);
        Assert.IsType<DogDescription>(fetched.Animals[3]);
    }
}
