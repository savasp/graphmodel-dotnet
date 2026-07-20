// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using System.Collections;
using System.Globalization;
using System.Threading.Tasks;

namespace Cvoya.Graph.CompatibilityTests;

public interface IBasicTests : IGraphTest
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
    public async Task BareRelationshipProjection_IncomingEdge_DoesNotReconstructEndpoints()
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

        // Exercises the relationship-valued anonymous projection path. Endpoint and orientation
        // information is deliberately available only from a path segment.
        var projection = await this.Graph.Relationships<Knows>()
            .Where(r => r.Id == knows.Id)
            .Select(r => new { Relationship = r })
            .FirstAsync(TestContext.Current.CancellationToken);

        Assert.Equal(knows.Id, projection.Relationship.Id);
        Assert.Empty(projection.Relationship.StartNodeId);
        Assert.Empty(projection.Relationship.EndNodeId);
        Assert.Equal(knows.Since, projection.Relationship.Since);
    }

    [Fact]
    public async Task BareRelationshipRoot_DoesNotReconstructEndpoints()
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
            .FirstAsync(TestContext.Current.CancellationToken);

        Assert.Equal(knows.Id, projection.Id);
        Assert.Empty(projection.StartNodeId);
        Assert.Empty(projection.EndNodeId);
        Assert.Equal(knows.Since, projection.Since);
    }

    [Fact]
    public async Task BareRelationshipProjection_MixedShape_PreservesModeledPropertiesOnly()
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
        Assert.Empty(projection.Relationship.StartNodeId);
        Assert.Empty(projection.Relationship.EndNodeId);
        Assert.Equal(since, projection.Relationship.Since);
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
        var relationshipIds = new[] { knows1.Id, knows2.Id };
        var rels = await this.Graph.Relationships<Knows>()
            .Where(r => relationshipIds.Contains(r.Id))
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
    public async Task RelationshipType_ChangedOnUpdate_ThrowsAndPreservesOriginalRelationship()
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

        var friend = new Friend(p1.Id, p2.Id)
        {
            Id = knows.Id,
            Direction = RelationshipDirection.Incoming,
            Since = originalSince.AddDays(1)
        };

        var exception = await Assert.ThrowsAsync<GraphException>(() =>
            this.Graph.UpdateRelationshipAsync(friend, null, TestContext.Current.CancellationToken));

        Assert.StartsWith(
            "Relationship type or concrete CLR type cannot be changed on update; delete and recreate the relationship.",
            exception.Message);

        var fetched = await this.Graph.GetRelationshipAsync<Knows>(
            knows.Id,
            null,
            TestContext.Current.CancellationToken);
        Assert.Equal(Labels.GetLabelFromType(typeof(Knows)), fetched.Type);
        Assert.Equal(p1.Id, fetched.StartNodeId);
        Assert.Equal(p2.Id, fetched.EndNodeId);
        Assert.Equal(RelationshipDirection.Incoming, fetched.Direction);
        Assert.Equal(originalSince, fetched.Since);

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            this.Graph.GetRelationshipAsync<Friend>(knows.Id, null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DynamicRelationshipType_ChangedAfterValidUpdate_ThrowsAndPreservesLastSuccessfulUpdate()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);

        var relationship = new DynamicRelationship(
            p1.Id,
            p2.Id,
            "DYNAMIC_TYPE_A",
            new Dictionary<string, object?> { ["status"] = "original" });
        await this.Graph.CreateRelationshipAsync(relationship, null, TestContext.Current.CancellationToken);

        var validUpdate = new DynamicRelationship(
            p1.Id,
            p2.Id,
            "DYNAMIC_TYPE_A",
            new Dictionary<string, object?> { ["status"] = "updated" })
        {
            Id = relationship.Id
        };
        await this.Graph.UpdateRelationshipAsync(validUpdate, null, TestContext.Current.CancellationToken);

        var changedType = new DynamicRelationship(
            p1.Id,
            p2.Id,
            "DYNAMIC_TYPE_B",
            new Dictionary<string, object?> { ["status"] = "rejected" })
        {
            Id = relationship.Id
        };

        var exception = await Assert.ThrowsAsync<GraphException>(() =>
            this.Graph.UpdateRelationshipAsync(changedType, null, TestContext.Current.CancellationToken));

        Assert.StartsWith(
            "Relationship type or concrete CLR type cannot be changed on update; delete and recreate the relationship.",
            exception.Message);

        var fetched = await this.Graph.GetDynamicRelationshipAsync(
            relationship.Id,
            null,
            TestContext.Current.CancellationToken);
        Assert.Equal("DYNAMIC_TYPE_A", fetched.Type);
        Assert.Equal(p1.Id, fetched.StartNodeId);
        Assert.Equal(p2.Id, fetched.EndNodeId);
        Assert.Equal(RelationshipDirection.Outgoing, fetched.Direction);
        Assert.Equal("updated", fetched.Properties["status"]);
    }

    [Fact]
    public async Task RelationshipConcreteClrType_ChangedWithSameStoredType_ThrowsAndPreservesOriginalRelationship()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);

        var originalSince = DateTime.UtcNow;
        var knows = new Knows(p1.Id, p2.Id) { Since = originalSince };
        await this.Graph.CreateRelationshipAsync(knows, null, TestContext.Current.CancellationToken);

        var friend = new Friend(p1.Id, p2.Id)
        {
            Id = knows.Id,
            Type = Labels.GetLabelFromType(typeof(Knows)),
            Direction = RelationshipDirection.Outgoing,
            Since = originalSince.AddDays(1)
        };

        var exception = await Assert.ThrowsAsync<GraphException>(() =>
            this.Graph.UpdateRelationshipAsync(friend, null, TestContext.Current.CancellationToken));

        Assert.StartsWith(
            "Relationship type or concrete CLR type cannot be changed on update; delete and recreate the relationship.",
            exception.Message);

        var fetched = await this.Graph.GetRelationshipAsync<Knows>(
            knows.Id,
            null,
            TestContext.Current.CancellationToken);
        Assert.Equal(Labels.GetLabelFromType(typeof(Knows)), fetched.Type);
        Assert.Equal(p1.Id, fetched.StartNodeId);
        Assert.Equal(p2.Id, fetched.EndNodeId);
        Assert.Equal(RelationshipDirection.Outgoing, fetched.Direction);
        Assert.Equal(originalSince, fetched.Since);

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            this.Graph.GetRelationshipAsync<Friend>(knows.Id, null, TestContext.Current.CancellationToken));
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
        public Foo? Bar { get; set; }
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

    public enum ValueCollectionKind
    {
        First,
        Second,
    }

    public record ValueTypeCollectionNode : Node
    {
        public List<int> IntegerList { get; set; } = [];
        public int[] IntegerArray { get; set; } = [];
        public List<Guid> Guids { get; set; } = [];
        public ValueCollectionKind[] Kinds { get; set; } = [];
        public int?[] NullableIntegers { get; set; } = [];
    }

    [Fact]
    public async Task ValueTypeSimpleCollections_RoundTripThroughGetAndQuery()
    {
        var firstId = Guid.Parse("7a2ef43f-dadf-4c88-a2f6-af730f87a963");
        var secondId = Guid.Parse("69bd4638-166e-428f-8fd2-3993338e865f");
        var node = new ValueTypeCollectionNode
        {
            IntegerList = [1, 2, 3],
            IntegerArray = [4, 5],
            Guids = [firstId, secondId],
            Kinds = [ValueCollectionKind.First, ValueCollectionKind.Second],
            // Neo4j rejects null entries in stored property collections. Focused serialization and
            // materialization tests cover null preservation; the provider contract still verifies
            // that nullable element types themselves round-trip through every provider.
            NullableIntegers = [6, 7],
        };

        await Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken);

        var fetched = await Graph.GetNodeAsync<ValueTypeCollectionNode>(
            node.Id,
            null,
            TestContext.Current.CancellationToken);
        var queried = await Graph.Nodes<ValueTypeCollectionNode>()
            .Where(candidate => candidate.Id == node.Id)
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        AssertValueTypeCollections(node, fetched);
        Assert.NotNull(queried);
        AssertValueTypeCollections(node, queried);
    }

    [Fact]
    public async Task DynamicSimpleCollections_RoundTripForNodesAndRelationships()
    {
        var firstId = Guid.Parse("7a2ef43f-dadf-4c88-a2f6-af730f87a963");
        var secondId = Guid.Parse("69bd4638-166e-428f-8fd2-3993338e865f");
        var properties = new Dictionary<string, object?>
        {
            ["strings"] = new[] { "first", "second" },
            ["integers"] = new List<int> { 1, 2, 3 },
            ["guids"] = new[] { firstId, secondId },
            ["kinds"] = new[] { ValueCollectionKind.First, ValueCollectionKind.Second },
            ["nullableIntegers"] = new int?[] { 4, 5 },
            ["emptyIntegers"] = Array.Empty<int>(),
        };
        var start = new DynamicNode(["DynamicCollectionStart"], properties);
        var end = new DynamicNode(["DynamicCollectionEnd"], new Dictionary<string, object?>());

        await Graph.CreateNodeAsync(start, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(end, null, TestContext.Current.CancellationToken);
        var relationship = new DynamicRelationship(
            start.Id,
            end.Id,
            "DYNAMIC_COLLECTION_RELATIONSHIP",
            properties);
        await Graph.CreateRelationshipAsync(relationship, null, TestContext.Current.CancellationToken);

        var fetchedNode = await Graph.GetDynamicNodeAsync(
            start.Id,
            null,
            TestContext.Current.CancellationToken);
        var fetchedRelationship = await Graph.GetDynamicRelationshipAsync(
            relationship.Id,
            null,
            TestContext.Current.CancellationToken);

        AssertDynamicCollections(fetchedNode.Properties, firstId, secondId);
        AssertDynamicCollections(fetchedRelationship.Properties, firstId, secondId);
    }

    [Fact]
    public async Task FreshTypedEntitiesConvertedToDynamic_RoundTrip()
    {
        INode startNode = new SpacedLabelVenue { Name = "Start" };
        INode endNode = new SpacedLabelVenue { Name = "End" };
        var dynamicStart = startNode.ToDynamic();
        var dynamicEnd = endNode.ToDynamic();

        await Graph.CreateNodeAsync(dynamicStart, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(dynamicEnd, null, TestContext.Current.CancellationToken);

        IRelationship relationship = new KnowsWell(dynamicStart.Id, dynamicEnd.Id)
        {
            HowWell = "Very well",
        };
        var dynamicRelationship = relationship.ToDynamic();
        await Graph.CreateRelationshipAsync(dynamicRelationship, null, TestContext.Current.CancellationToken);

        var fetchedNode = await Graph.GetDynamicNodeAsync(
            dynamicStart.Id,
            null,
            TestContext.Current.CancellationToken);
        var fetchedRelationship = await Graph.GetDynamicRelationshipAsync(
            dynamicRelationship.Id,
            null,
            TestContext.Current.CancellationToken);

        Assert.Contains(Labels.GetLabelFromType(startNode.GetType()), fetchedNode.Labels);
        Assert.Equal("Start", fetchedNode.Properties[nameof(SpacedLabelVenue.Name)]);
        Assert.Equal(Labels.GetLabelFromType(relationship.GetType()), fetchedRelationship.Type);
        Assert.Equal(relationship.StartNodeId, fetchedRelationship.StartNodeId);
        Assert.Equal(relationship.EndNodeId, fetchedRelationship.EndNodeId);
        Assert.Equal(
            ((KnowsWell)relationship).HowWell,
            fetchedRelationship.Properties[nameof(KnowsWell.HowWell)]);
    }

    [Fact]
    public async Task DynamicNodeComplexProperty_WithNestedSimpleCollection_RoundTrips()
    {
        // #405: a simple collection nested inside a dynamic node's dictionary/complex property value
        // must survive create/get. It was previously mangled on write (reflected over as an opaque
        // collection object) or dropped on read (only nested scalar members were materialized).
        var expectedTags = new[] { "home", "billing" };
        var node = new DynamicNode(
            ["DynamicNestedComplex"],
            new Dictionary<string, object?>
            {
                ["address"] = new Dictionary<string, object?>
                {
                    ["street"] = "1 Main",
                    ["tags"] = expectedTags,
                    ["location"] = new Dictionary<string, object?>
                    {
                        ["country"] = "UK",
                    },
                },
            });

        await Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken);

        var fetched = await Graph.GetDynamicNodeAsync(
            node.Id,
            null,
            TestContext.Current.CancellationToken);

        var address = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(fetched.Properties["address"]);
        Assert.Equal(["location", "street", "tags"], address.Keys.Order(StringComparer.Ordinal));
        Assert.Equal("1 Main", address["street"]?.ToString());
        Assert.Equal(
            expectedTags,
            CollectionValues(address["tags"]).Select(value => value?.ToString()));
        var location = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(address["location"]);
        Assert.Equal(["country"], location.Keys);
        Assert.Equal("UK", location["country"]?.ToString());
    }

    private static void AssertValueTypeCollections(ValueTypeCollectionNode expected, ValueTypeCollectionNode actual)
    {
        Assert.Equal(expected.IntegerList, actual.IntegerList);
        Assert.Equal(expected.IntegerArray, actual.IntegerArray);
        Assert.Equal(expected.Guids, actual.Guids);
        Assert.Equal(expected.Kinds, actual.Kinds);
        Assert.Equal(expected.NullableIntegers, actual.NullableIntegers);
    }

    private static void AssertDynamicCollections(
        IReadOnlyDictionary<string, object?> properties,
        Guid firstId,
        Guid secondId)
    {
        Assert.Collection(
            CollectionValues(properties["strings"]),
            value => Assert.Equal("first", value?.ToString()),
            value => Assert.Equal("second", value?.ToString()));
        Assert.Collection(
            CollectionValues(properties["integers"]),
            value => Assert.Equal(1, Convert.ToInt32(value, CultureInfo.InvariantCulture)),
            value => Assert.Equal(2, Convert.ToInt32(value, CultureInfo.InvariantCulture)),
            value => Assert.Equal(3, Convert.ToInt32(value, CultureInfo.InvariantCulture)));
        Assert.Equal(new[] { firstId, secondId }, CollectionValues(properties["guids"]).Select(value => Guid.Parse(value!.ToString()!)));
        Assert.Equal(
            new[] { nameof(ValueCollectionKind.First), nameof(ValueCollectionKind.Second) },
            CollectionValues(properties["kinds"]).Select(value => value?.ToString()));
        Assert.Equal(
            new int?[] { 4, 5 },
            CollectionValues(properties["nullableIntegers"]).Select(value => value is null
                ? (int?)null
                : Convert.ToInt32(value, CultureInfo.InvariantCulture)));
        Assert.Empty(CollectionValues(properties["emptyIntegers"]));
    }

    private static List<object?> CollectionValues(object? value)
    {
        var collection = Assert.IsAssignableFrom<IEnumerable>(value);
        return collection.Cast<object?>().ToList();
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
