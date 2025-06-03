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

namespace Cvoya.Graph.Model.Tests;

public abstract class ClassHierarchyTestsBase : ITestBase
{
    public abstract IGraph Graph { get; }

    [Fact]
    public async Task CanCreateAndRetrieveNodeViaBaseType()
    {
        var manager = new Manager
        {
            FirstName = "John",
            LastName = "Doe",
            Age = 40,
            Department = "Engineering",
            TeamSize = 10
        };

        await this.Graph.CreateNode(manager);

        var retrieved = await this.Graph.GetNode<Person>(manager.Id);

        // Even though we are retrieving as Person, we should still get the full Manager object
        Assert.NotNull(retrieved);
        Assert.IsType<Manager>(retrieved);
        Assert.Equal(manager.Id, retrieved.Id);
        Assert.Equal(manager.FirstName, retrieved.FirstName);
        Assert.Equal(manager.LastName, retrieved.LastName);
        Assert.Equal(manager.Age, retrieved.Age);
        Assert.Equal(manager.Department, ((Manager)retrieved).Department);
        Assert.Equal(manager.TeamSize, ((Manager)retrieved).TeamSize);
    }

    [Fact]
    public async Task CanCreateNodeViaBaseTypeAndRetrieveItViaDerivedType()
    {
        var manager = new Manager
        {
            FirstName = "Jane",
            LastName = "Smith",
            Age = 35,
            Department = "Marketing",
            TeamSize = 5
        };

        Person person = manager; // Implicit conversion to base type

        await this.Graph.CreateNode(person);

        var retrieved = await this.Graph.GetNode<Manager>(person.Id);

        Assert.NotNull(retrieved);
        Assert.IsType<Manager>(retrieved);
        Assert.Equal(manager.Id, retrieved.Id);
        Assert.Equal(manager.FirstName, retrieved.FirstName);
        Assert.Equal(manager.LastName, retrieved.LastName);
        Assert.Equal(manager.Age, retrieved.Age);
        Assert.Equal(manager.Department, retrieved.Department);
        Assert.Equal(manager.TeamSize, retrieved.TeamSize);
    }

    [Fact]
    public async Task CanCreateNodeViaBaseTypeAndRetrieveItViaBaseType()
    {
        var manager = new Manager
        {
            FirstName = "Jane",
            LastName = "Smith",
            Age = 35,
            Department = "Marketing",
            TeamSize = 5
        };

        Person person = manager; // Implicit conversion to base type

        await this.Graph.CreateNode(person);

        var retrieved = await this.Graph.GetNode<Person>(person.Id);

        Assert.NotNull(retrieved);
        Assert.IsType<Manager>(retrieved);
        Assert.Equal(manager.Id, retrieved.Id);
        Assert.Equal(manager.FirstName, retrieved.FirstName);
        Assert.Equal(manager.LastName, retrieved.LastName);
        Assert.Equal(manager.Age, retrieved.Age);
        Assert.Equal(manager.Department, ((Manager)retrieved).Department);
        Assert.Equal(manager.TeamSize, ((Manager)retrieved).TeamSize);
    }
}