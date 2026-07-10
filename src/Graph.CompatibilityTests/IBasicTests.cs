// Copyright CVOYA. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.
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

using System.Threading.Tasks;

namespace Cvoya.Graph.CompatibilityTests;

public interface IBasicTests : IGraphModelTest
{
    [Fact]
    public async Task CanCreateAndGetNodeWithPrimitiveProperties()
    {
        var person = new Person { FirstName = "John", LastName = "Doe" };
        await this.Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);
        var fetched = await this.Graph.GetNodeAsync<Person>(person.Id, null, TestContext.Current.CancellationToken);
        Assert.Equal("John", fetched.FirstName);
        Assert.Equal("Doe", fetched.LastName);
    }

    [Fact]
    public async Task CanCreateAndGetNodeWithComplexProperties()
    {
        var person = new PersonWithComplexProperty { FirstName = "John", LastName = "Doe", Address = new AddressValue { Street = "123 Main St", City = "Somewhere" } };
        await this.Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);
        var fetched = await this.Graph.GetNodeAsync<PersonWithComplexProperty>(person.Id, null, TestContext.Current.CancellationToken);
        Assert.Equal("John", fetched.FirstName);
        Assert.Equal("Doe", fetched.LastName);
        Assert.Equal("123 Main St", fetched.Address.Street);
        Assert.Equal("Somewhere", fetched.Address.City);
    }


    [Fact]
    public async Task CanCreateAndGetRelationship()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);

        var dateTime = DateTime.UtcNow;
        var knows = new Knows { StartNodeId = p1.Id, EndNodeId = p2.Id, Since = dateTime };

        await this.Graph.CreateRelationshipAsync(knows, null, TestContext.Current.CancellationToken);

        var fetched = await this.Graph.GetRelationshipAsync<Knows>(knows.Id, null, TestContext.Current.CancellationToken);
        Assert.Equal(p1.Id, fetched.StartNodeId);
        Assert.Equal(p2.Id, fetched.EndNodeId);
        Assert.Equal(dateTime, fetched.Since);
    }

    [Fact]
    public async Task RelationshipDirection_Outgoing_RoundTrips()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);

        var knows = new Knows
        {
            StartNodeId = p1.Id,
            EndNodeId = p2.Id,
            Direction = RelationshipDirection.Outgoing,
            Since = DateTime.UtcNow
        };

        await this.Graph.CreateRelationshipAsync(knows, null, TestContext.Current.CancellationToken);

        var fetched = await this.Graph.GetRelationshipAsync<Knows>(knows.Id, null, TestContext.Current.CancellationToken);
        Assert.Equal(p1.Id, fetched.StartNodeId);
        Assert.Equal(p2.Id, fetched.EndNodeId);
        Assert.Equal(RelationshipDirection.Outgoing, fetched.Direction);
    }

    [Fact]
    public async Task RelationshipDirection_Incoming_RoundTrips()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);

        var knows = new Knows
        {
            StartNodeId = p1.Id,
            EndNodeId = p2.Id,
            Direction = RelationshipDirection.Incoming,
            Since = DateTime.UtcNow
        };

        await this.Graph.CreateRelationshipAsync(knows, null, TestContext.Current.CancellationToken);

        var fetched = await this.Graph.GetRelationshipAsync<Knows>(knows.Id, null, TestContext.Current.CancellationToken);
        Assert.Equal(p1.Id, fetched.StartNodeId);
        Assert.Equal(p2.Id, fetched.EndNodeId);
        Assert.Equal(RelationshipDirection.Incoming, fetched.Direction);
    }

    [Fact]
    public async Task RelationshipDirection_ChangedOnUpdate_Throws()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);

        var originalSince = DateTime.UtcNow;
        var knows = new Knows
        {
            StartNodeId = p1.Id,
            EndNodeId = p2.Id,
            Direction = RelationshipDirection.Outgoing,
            Since = originalSince
        };

        await this.Graph.CreateRelationshipAsync(knows, null, TestContext.Current.CancellationToken);

        var changedDirection = knows with
        {
            Direction = RelationshipDirection.Incoming,
            Since = originalSince.AddDays(1)
        };

        var exception = await Assert.ThrowsAsync<GraphException>(async () =>
            await this.Graph.UpdateRelationshipAsync(changedDirection, null, TestContext.Current.CancellationToken));

        Assert.Contains(
            "Direction cannot be changed on update; delete and recreate the relationship",
            exception.Message);

        var fetched = await this.Graph.GetRelationshipAsync<Knows>(knows.Id, null, TestContext.Current.CancellationToken);
        Assert.Equal(RelationshipDirection.Outgoing, fetched.Direction);
        Assert.Equal(p1.Id, fetched.StartNodeId);
        Assert.Equal(p2.Id, fetched.EndNodeId);
        Assert.Equal(originalSince, fetched.Since);
    }

    [Fact]
    public async Task RelationshipDirection_ChangedOnUpdate_IncomingToOutgoing_Throws()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);

        var originalSince = DateTime.UtcNow;
        var knows = new Knows
        {
            StartNodeId = p1.Id,
            EndNodeId = p2.Id,
            Direction = RelationshipDirection.Incoming,
            Since = originalSince
        };

        await this.Graph.CreateRelationshipAsync(knows, null, TestContext.Current.CancellationToken);

        var changedDirection = knows with
        {
            Direction = RelationshipDirection.Outgoing,
            Since = originalSince.AddDays(1)
        };

        var exception = await Assert.ThrowsAsync<GraphException>(async () =>
            await this.Graph.UpdateRelationshipAsync(changedDirection, null, TestContext.Current.CancellationToken));

        Assert.Contains(
            "Direction cannot be changed on update; delete and recreate the relationship",
            exception.Message);

        var fetched = await this.Graph.GetRelationshipAsync<Knows>(knows.Id, null, TestContext.Current.CancellationToken);
        Assert.Equal(RelationshipDirection.Incoming, fetched.Direction);
        Assert.Equal(p1.Id, fetched.StartNodeId);
        Assert.Equal(p2.Id, fetched.EndNodeId);
        Assert.Equal(originalSince, fetched.Since);
    }

    [Fact]
    public async Task RelationshipDirection_UnchangedOnUpdate_Succeeds()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);

        var knows = new Knows
        {
            StartNodeId = p1.Id,
            EndNodeId = p2.Id,
            Direction = RelationshipDirection.Incoming,
            Since = DateTime.UtcNow
        };

        await this.Graph.CreateRelationshipAsync(knows, null, TestContext.Current.CancellationToken);

        var updatedSince = knows.Since.AddDays(1);
        var updatedKnows = knows with { Since = updatedSince };
        await this.Graph.UpdateRelationshipAsync(updatedKnows, null, TestContext.Current.CancellationToken);

        var fetched = await this.Graph.GetRelationshipAsync<Knows>(knows.Id, null, TestContext.Current.CancellationToken);
        Assert.Equal(RelationshipDirection.Incoming, fetched.Direction);
        Assert.Equal(p1.Id, fetched.StartNodeId);
        Assert.Equal(p2.Id, fetched.EndNodeId);
        Assert.Equal(updatedSince, fetched.Since);
    }

    [Fact]
    public async Task BareRelationshipProjection_IncomingEdge_HasLogicalEndpointIds()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);

        var knows = new Knows
        {
            StartNodeId = p1.Id,
            EndNodeId = p2.Id,
            Direction = RelationshipDirection.Incoming,
            Since = DateTime.UtcNow
        };

        await this.Graph.CreateRelationshipAsync(knows, null, TestContext.Current.CancellationToken);

        // Exercises the relationship-valued anonymous projection path:
        // CypherResultProcessor.CreateEntityInfoFromProjection(... value is relationship-shaped map ...).
        var projection = await this.Graph.Relationships<Knows>()
            .Where(r => r.Id == knows.Id)
            .Select(r => new { Relationship = r })
            .FirstAsync(TestContext.Current.CancellationToken);

        Assert.Equal(knows.Id, projection.Relationship.Id);
        Assert.Equal(p1.Id, projection.Relationship.StartNodeId);
        Assert.Equal(p2.Id, projection.Relationship.EndNodeId);
        Assert.Equal(RelationshipDirection.Incoming, projection.Relationship.Direction);
    }

    [Fact]
    public async Task BareRelationshipProjection_OutgoingEdge_HasLogicalEndpointIds()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);

        var knows = new Knows
        {
            StartNodeId = p1.Id,
            EndNodeId = p2.Id,
            Direction = RelationshipDirection.Outgoing,
            Since = DateTime.UtcNow
        };

        await this.Graph.CreateRelationshipAsync(knows, null, TestContext.Current.CancellationToken);

        var projection = await this.Graph.Relationships<Knows>()
            .Where(r => r.Id == knows.Id)
            .Select(r => new { Relationship = r })
            .FirstAsync(TestContext.Current.CancellationToken);

        Assert.Equal(knows.Id, projection.Relationship.Id);
        Assert.Equal(p1.Id, projection.Relationship.StartNodeId);
        Assert.Equal(p2.Id, projection.Relationship.EndNodeId);
        Assert.Equal(RelationshipDirection.Outgoing, projection.Relationship.Direction);
    }

    [Fact]
    public async Task BareRelationshipProjection_MixedIncomingEdge_HasLogicalEndpointIds()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);

        var since = DateTime.UtcNow;
        var knows = new Knows
        {
            StartNodeId = p1.Id,
            EndNodeId = p2.Id,
            Direction = RelationshipDirection.Incoming,
            Since = since
        };

        await this.Graph.CreateRelationshipAsync(knows, null, TestContext.Current.CancellationToken);

        var projection = await this.Graph.Relationships<Knows>()
            .Where(r => r.Id == knows.Id)
            .Select(r => new { r.Since, Relationship = r })
            .FirstAsync(TestContext.Current.CancellationToken);

        Assert.Equal(since, projection.Since);
        Assert.Equal(knows.Id, projection.Relationship.Id);
        Assert.Equal(p1.Id, projection.Relationship.StartNodeId);
        Assert.Equal(p2.Id, projection.Relationship.EndNodeId);
        Assert.Equal(RelationshipDirection.Incoming, projection.Relationship.Direction);
    }

    [Fact]
    public async Task CanUpdateNode()
    {
        var person = new Person { FirstName = "John", LastName = "Doe" };
        await this.Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);
        person.LastName = "Smith";

        await this.Graph.UpdateNodeAsync(person, null, TestContext.Current.CancellationToken);

        var updated = await this.Graph.GetNodeAsync<Person>(person.Id, null, TestContext.Current.CancellationToken);

        Assert.Equal("Smith", updated.LastName);
    }

    [Fact]
    public async Task CanCreateAndDeleteNode()
    {
        var person = new Person { FirstName = "ToDelete" };
        await this.Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);
        await this.Graph.DeleteNodeAsync(person.Id, false, null, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<EntityNotFoundException>(async () => await this.Graph.GetNodeAsync<Person>(person.Id, null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CanCreateAndDeleteRelationship()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);

        var knows = new Knows { StartNodeId = p1.Id, EndNodeId = p2.Id, Since = DateTime.UtcNow };

        await this.Graph.CreateRelationshipAsync(knows, null, TestContext.Current.CancellationToken);
        await this.Graph.DeleteRelationshipAsync(knows.Id, null, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<EntityNotFoundException>(async () => await this.Graph.GetRelationshipAsync<Knows>(knows.Id, null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CanGetMultipleNodes()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);
        var ids = new[] { p1.Id, p2.Id };
        var fetched = await this.Graph.Nodes<Person>().Where(x => ids.Contains(x.Id)).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, ((ICollection<Person>)fetched).Count);
        Assert.Contains(fetched, x => x.Id == p1.Id);
        Assert.Contains(fetched, x => x.Id == p2.Id);
    }

    [Fact]
    public async Task CanGetMultipleRelationships()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        var p3 = new Person { FirstName = "C" };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p3, null, TestContext.Current.CancellationToken);
        var knows1 = new Knows { StartNodeId = p1.Id, EndNodeId = p2.Id, Since = DateTime.UtcNow };
        var knows2 = new Knows { StartNodeId = p2.Id, EndNodeId = p3.Id, Since = DateTime.UtcNow };
        await this.Graph.CreateRelationshipAsync(knows1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(knows2, null, TestContext.Current.CancellationToken);
        var rels = await this.Graph.Relationships<Knows>()
            .Where(r => r.StartNodeId == p1.Id || r.StartNodeId == p2.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, rels.Count);
        Assert.Contains(rels, r => r.Id == knows1.Id);
        Assert.Contains(rels, r => r.Id == knows2.Id);
    }

    [Fact]
    public async Task CanUpdateRelationship()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);

        var knows = new Knows { StartNodeId = p1.Id, EndNodeId = p2.Id, Since = DateTime.UtcNow };
        await this.Graph.CreateRelationshipAsync(knows, null, TestContext.Current.CancellationToken);
        knows.Since = DateTime.UtcNow.AddYears(-1);
        await this.Graph.UpdateRelationshipAsync(knows, null, TestContext.Current.CancellationToken);
        var updated = await this.Graph.GetRelationshipAsync<Knows>(knows.Id, null, TestContext.Current.CancellationToken);
        Assert.Equal(knows.Id, updated.Id);
        Assert.Equal(p1.Id, updated.StartNodeId);
        Assert.Equal(p2.Id, updated.EndNodeId);
        Assert.Equal(knows.Since, updated.Since);
    }

    [Fact]
    public async Task CanBeginTransactionAndRollback()
    {
        var tx = await this.Graph.GetTransactionAsync(TestContext.Current.CancellationToken);
        var person = new Person { FirstName = "TxTest" };
        await this.Graph.CreateNodeAsync(person, tx, TestContext.Current.CancellationToken);
        await tx.DisposeAsync(); // Rollback
        await Assert.ThrowsAsync<EntityNotFoundException>(async () => await this.Graph.GetNodeAsync<Person>(person.Id, null, TestContext.Current.CancellationToken));
    }

    public record PersonWithCycle : Node
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public Foo Foo { get; set; } = new();
    }

    public record Foo
    {
        public Foo? Bar { get; set; } = null;
    }

    [Fact]
    public async Task CannotAddNodeWithCycle()
    {
        var person = new PersonWithCycle { FirstName = "A" };
        person.Foo.Bar = new()
        {
            Bar = person.Foo
        };

        await Assert.ThrowsAsync<GraphException>(async () => await this.Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken));
    }

    public record PersonWithGenericCollectionOfPrimitiveProperty : Node
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public List<string> GenericProperty { get; set; } = [];
    }

    [Fact]
    public async Task CanAddAndGetNodeWithGenericCollectionOfPrimitiveProperty()
    {
        var person = new PersonWithGenericCollectionOfPrimitiveProperty { FirstName = "A", GenericProperty = ["B", "C"] };

        await this.Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);

        var fetched = await this.Graph.GetNodeAsync<PersonWithGenericCollectionOfPrimitiveProperty>(person.Id, null, TestContext.Current.CancellationToken);
        Assert.Equal("A", fetched.FirstName);
        Assert.Equal(person.GenericProperty.Count, fetched.GenericProperty.Count);
        Assert.All(fetched.GenericProperty, item => Assert.Contains(item, person.GenericProperty));
    }

    [Fact]
    public async Task CanQueryNodesLinq()
    {
        var queryable = this.Graph.Nodes<Person>();
        Assert.NotNull(queryable);
    }

    [Fact]
    public async Task CanCreateRelationshipsQuery()
    {
        var queryable = this.Graph.Relationships<Knows>();
        Assert.NotNull(queryable);
    }

    [Fact]
    public async Task CanQueryForMemories()
    {
        var memory = new Memory
        {
            Id = Guid.NewGuid().ToString("N"),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CapturedBy = new MemorySource
            {
                Name = "UnitTest",
                Description = "Unit Test",
                Version = "1.0.0",
                Device = "TestDevice"
            },
            Location = new Point { Latitude = 10, Longitude = 20, Height = 5 },
            Deleted = false,
            Text = "This is a test memory"
        };

        await this.Graph.CreateNodeAsync(memory, null, TestContext.Current.CancellationToken);

        var memories = await this.Graph.Nodes<Memory>().ToListAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(memories);

        var retrievedMemory = await this.Graph.Nodes<Memory>()
            .Where(m => m.Id == memory.Id)
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(retrievedMemory);
        Assert.Equal("UnitTest", retrievedMemory.CapturedBy.Name);
        Assert.Equal(10, retrievedMemory.Location.Latitude);
    }

    [Fact]
    public async Task CanCreateAndRetrieveUserNode()
    {
        var user = new User
        {
            Name = "Alice",
            Email = "alice@example.com",
            GoogleId = "alice_google_id",
            DateOfBirth = new DateTime(1990, 1, 1),
            Job = "Engineer"
        };

        await this.Graph.CreateNodeAsync(user, null, TestContext.Current.CancellationToken);

        var fetchedUser = await this.Graph.Nodes<User>()
            .Where(u => u.Id == user.Id)
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(fetchedUser);
        Assert.Equal("Alice", fetchedUser.Name);
        Assert.Equal("alice@example.com", fetchedUser.Email);
    }

    // ---- Query-surface v2 (issue #94) contract tests: sync roots, await foreach ----

    [Fact]
    public void SyncQueryRoots_ConstructWithoutIO()
    {
        // IGraph.Nodes<N>/Relationships<R>/DynamicNodes/DynamicRelationships (issue #94 scope item
        // 4) are synchronous and must not require an await to build the queryable - constructing
        // them (without executing) should not throw and should not need a running transaction.
        var nodes = this.Graph.Nodes<Person>();
        var relationships = this.Graph.Relationships<Knows>();
        var dynamicNodes = this.Graph.DynamicNodes();
        var dynamicRelationships = this.Graph.DynamicRelationships();

        Assert.NotNull(nodes);
        Assert.NotNull(relationships);
        Assert.NotNull(dynamicNodes);
        Assert.NotNull(dynamicRelationships);
    }

    [Fact]
    public async Task AwaitForeach_EnumeratesQueryResults()
    {
        // IGraphQueryable<T> : IAsyncEnumerable<T> (issue #94 scope item 5) - await foreach must
        // work directly on a query, without ToListAsync as an intermediate step.
        var alice = new Person { FirstName = "AwaitForeachAlice", LastName = "Smith" };
        var bob = new Person { FirstName = "AwaitForeachBob", LastName = "Jones" };
        await this.Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);

        var seen = new List<string>();
        await foreach (var person in this.Graph.Nodes<Person>().Where(p => p.FirstName == "AwaitForeachAlice" || p.FirstName == "AwaitForeachBob"))
        {
            seen.Add(person.FirstName);
        }

        Assert.Contains("AwaitForeachAlice", seen);
        Assert.Contains("AwaitForeachBob", seen);
    }
}
