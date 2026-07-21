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
        var fetched = await this.Graph.FindNodeByTestKeyAsync<Person>(person.TestKey, null, TestContext.Current.CancellationToken);
        Assert.Equal("John", fetched.FirstName);
        Assert.Equal("Doe", fetched.LastName);
    }

    [Fact]
    public async Task CanCreateAndGetNodeWithComplexProperties()
    {
        var person = new PersonWithComplexProperty { FirstName = "John", LastName = "Doe", Address = new AddressValue { Street = "123 Main St", City = "Somewhere" } };
        await this.Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);
        var fetched = await this.Graph.FindNodeByTestKeyAsync<PersonWithComplexProperty>(person.TestKey, null, TestContext.Current.CancellationToken);
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
        var knows = new Knows { Since = dateTime };

        await this.Graph.ConnectAsync(p1, knows, p2, cancellationToken: TestContext.Current.CancellationToken);

        var fetched = await this.Graph.FindRelationshipByTestKeyAsync<Knows>(
            knows.TestKey, null, TestContext.Current.CancellationToken);
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
            Since = DateTime.UtcNow
        };

        await this.Graph.ConnectAsync(p1, knows, p2, cancellationToken: TestContext.Current.CancellationToken);

        var segment = await this.Graph.Nodes<Person>()
            .Where(person => person.TestKey == p1.TestKey)
            .PathSegments<Person, Knows, Person>(GraphTraversalDirection.Both)
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(RelationshipDirection.Outgoing, segment.Direction);
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
            Since = DateTime.UtcNow
        };

        await this.Graph.ConnectAsync(
            p1,
            knows,
            p2,
            RelationshipDirection.Incoming,
            cancellationToken: TestContext.Current.CancellationToken);

        var segment = await this.Graph.Nodes<Person>()
            .Where(person => person.TestKey == p1.TestKey)
            .PathSegments<Person, Knows, Person>(GraphTraversalDirection.Both)
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(RelationshipDirection.Incoming, segment.Direction);
    }

    [Fact]
    public async Task RelationshipModeledProperty_UpdateAsync_Succeeds()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);

        var originalSince = DateTime.UtcNow;
        var knows = new Knows
        {
            Since = originalSince
        };

        await this.Graph.ConnectAsync(p1, knows, p2, cancellationToken: TestContext.Current.CancellationToken);

        var updatedSince = originalSince.AddDays(1);
        var affected = await this.Graph.RelationshipsByTestKey<Knows>(knows.TestKey)
            .UpdateAsync(
                setters => setters.SetProperty(relationship => relationship.Since, updatedSince),
                TestContext.Current.CancellationToken);

        var fetched = await this.Graph.FindRelationshipByTestKeyAsync<Knows>(knows.TestKey, null, TestContext.Current.CancellationToken);
        Assert.Equal(1, affected);
        Assert.Equal(updatedSince, fetched.Since);
    }

    [Fact]
    public async Task RelationshipModeledPropertyUpdate_PreservesPathOrientation()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);

        var originalSince = DateTime.UtcNow;
        var knows = new Knows
        {
            Since = originalSince
        };

        await this.Graph.ConnectAsync(
            p1,
            knows,
            p2,
            RelationshipDirection.Incoming,
            cancellationToken: TestContext.Current.CancellationToken);

        var updatedSince = originalSince.AddDays(1);
        var affected = await this.Graph.RelationshipsByTestKey<Knows>(knows.TestKey)
            .UpdateAsync(
                setters => setters.SetProperty(relationship => relationship.Since, updatedSince),
                TestContext.Current.CancellationToken);

        var fetched = await this.Graph.FindRelationshipByTestKeyAsync<Knows>(knows.TestKey, null, TestContext.Current.CancellationToken);
        var segment = await this.Graph.Nodes<Person>()
            .Where(person => person.TestKey == p1.TestKey)
            .PathSegments<Person, Knows, Person>(GraphTraversalDirection.Both)
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, affected);
        Assert.Equal(updatedSince, fetched.Since);
        Assert.Equal(RelationshipDirection.Incoming, segment.Direction);
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
            Since = DateTime.UtcNow
        };

        await this.Graph.ConnectAsync(p1, knows, p2, cancellationToken: TestContext.Current.CancellationToken);

        var updatedSince = knows.Since.AddDays(1);
        var affected = await this.Graph.RelationshipsByTestKey<Knows>(knows.TestKey)
            .UpdateAsync(
                setters => setters.SetProperty(relationship => relationship.Since, updatedSince),
                TestContext.Current.CancellationToken);

        var fetched = await this.Graph.FindRelationshipByTestKeyAsync<Knows>(knows.TestKey, null, TestContext.Current.CancellationToken);
        Assert.Equal(1, affected);
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
            Since = DateTime.UtcNow
        };

        await this.Graph.ConnectAsync(p1, knows, p2, cancellationToken: TestContext.Current.CancellationToken);

        // Exercises the relationship-valued anonymous projection path. Endpoint and orientation
        // information is deliberately available only from a path segment.
        var projection = await this.Graph.Relationships<Knows>()
            .Where(r => r.TestKey == knows.TestKey)
            .Select(r => new { Relationship = r })
            .FirstAsync(TestContext.Current.CancellationToken);

        Assert.Equal(knows.TestKey, projection.Relationship.TestKey);
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
            Since = DateTime.UtcNow
        };

        await this.Graph.ConnectAsync(p1, knows, p2, cancellationToken: TestContext.Current.CancellationToken);

        var projection = await this.Graph.Relationships<Knows>()
            .Where(r => r.TestKey == knows.TestKey)
            .FirstAsync(TestContext.Current.CancellationToken);

        Assert.Equal(knows.TestKey, projection.TestKey);
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
            Since = since
        };

        await this.Graph.ConnectAsync(p1, knows, p2, cancellationToken: TestContext.Current.CancellationToken);

        var projection = await this.Graph.Relationships<Knows>()
            .Where(r => r.TestKey == knows.TestKey)
            .Select(r => new { r.Since, Relationship = r })
            .FirstAsync(TestContext.Current.CancellationToken);

        Assert.Equal(since, projection.Since);
        Assert.Equal(knows.TestKey, projection.Relationship.TestKey);
        Assert.Equal(since, projection.Relationship.Since);
    }

    [Fact]
    public async Task CanUpdateNode()
    {
        var person = new Person { FirstName = "John", LastName = "Doe" };
        await this.Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);
        var affected = await this.Graph.SelectNode(person)
            .UpdateAsync(
                setters => setters.SetProperty(candidate => candidate.LastName, "Smith"),
                TestContext.Current.CancellationToken);

        var updated = await this.Graph.FindNodeByTestKeyAsync<Person>(person.TestKey, null, TestContext.Current.CancellationToken);

        Assert.Equal(1, affected);
        Assert.Equal("Smith", updated.LastName);
    }

    [Fact]
    public async Task CanCreateAndDeleteNode()
    {
        var person = new Person { FirstName = "ToDelete" };
        await this.Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);
        var affected = await this.Graph.SelectNode(person).DeleteAsync(cancellationToken: TestContext.Current.CancellationToken);
        var deleted = await this.Graph.NodesByTestKey<Person>(person.TestKey)
            .SingleOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, affected);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task CanCreateAndDeleteRelationship()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);

        var knows = new Knows { Since = DateTime.UtcNow };

        await this.Graph.ConnectAsync(p1, knows, p2, cancellationToken: TestContext.Current.CancellationToken);
        var affected = await this.Graph.SelectRelationship(knows)
            .DeleteAsync(TestContext.Current.CancellationToken);
        var deleted = await this.Graph.RelationshipsByTestKey<Knows>(knows.TestKey)
            .SingleOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, affected);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task CanGetMultipleNodes()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);
        var ids = new[] { p1.TestKey, p2.TestKey };
        var fetched = await this.Graph.Nodes<Person>().Where(x => ids.Contains(x.TestKey)).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, ((ICollection<Person>)fetched).Count);
        Assert.Contains(fetched, x => x.TestKey == p1.TestKey);
        Assert.Contains(fetched, x => x.TestKey == p2.TestKey);
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
        var knows1 = new Knows { Since = DateTime.UtcNow };
        var knows2 = new Knows { Since = DateTime.UtcNow };
        await this.Graph.ConnectAsync(p1, knows1, p2, cancellationToken: TestContext.Current.CancellationToken);
        await this.Graph.ConnectAsync(p2, knows2, p3, cancellationToken: TestContext.Current.CancellationToken);
        var relationshipIds = new[] { knows1.TestKey, knows2.TestKey };
        var rels = await this.Graph.Relationships<Knows>()
            .Where(r => relationshipIds.Contains(r.TestKey))
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, rels.Count);
        Assert.Contains(rels, r => r.TestKey == knows1.TestKey);
        Assert.Contains(rels, r => r.TestKey == knows2.TestKey);
    }

    [Fact]
    public async Task CanUpdateRelationship()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);

        var knows = new Knows { Since = DateTime.UtcNow };
        await this.Graph.ConnectAsync(p1, knows, p2, cancellationToken: TestContext.Current.CancellationToken);
        var updatedSince = DateTime.UtcNow.AddYears(-1);
        var affected = await this.Graph.SelectRelationship(knows)
            .UpdateAsync(
                setters => setters.SetProperty(relationship => relationship.Since, updatedSince),
                TestContext.Current.CancellationToken);
        var updated = await this.Graph.FindRelationshipByTestKeyAsync<Knows>(knows.TestKey, null, TestContext.Current.CancellationToken);
        Assert.Equal(1, affected);
        Assert.Equal(knows.TestKey, updated.TestKey);
        Assert.Equal(updatedSince, updated.Since);
    }

    [Fact]
    public async Task RelationshipType_SetMutationIsRejectedAndPreservesOriginalRelationship()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);

        var originalSince = DateTime.UtcNow;
        var knows = new Knows
        {
            Since = originalSince
        };
        await this.Graph.ConnectAsync(p1, knows, p2, cancellationToken: TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<GraphQueryTranslationException>(() =>
            this.Graph.SelectRelationship(knows).UpdateAsync(
                setters => setters.SetProperty(relationship => relationship.Type, "FRIENDOF"),
                TestContext.Current.CancellationToken));

        var fetched = await this.Graph.FindRelationshipByTestKeyAsync<Knows>(
            knows.TestKey,
            null,
            TestContext.Current.CancellationToken);
        Assert.Equal(Labels.GetLabelFromType(typeof(Knows)), fetched.Type);
        Assert.Equal(originalSince, fetched.Since);

        Assert.Null(await this.Graph.RelationshipsByTestKey<Friend>(knows.TestKey)
            .SingleOrDefaultAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DynamicRelationshipType_ChangedAfterValidUpdate_ThrowsAndPreservesLastSuccessfulUpdate()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);

        var relationship = new DynamicRelationship(
            "DYNAMIC_TYPE_A",
            new Dictionary<string, object?> { ["status"] = "original" });
        await this.Graph.ConnectAsync(p1, relationship, p2, cancellationToken: TestContext.Current.CancellationToken);

        var selected = this.Graph.DynamicRelationships().Where(candidate => candidate.Type == "DYNAMIC_TYPE_A");
        var affected = await selected.UpdateAsync(
            setters => setters.SetProperty(candidate => candidate.Properties["status"], "updated"),
            TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<GraphQueryTranslationException>(() => selected.UpdateAsync(
            setters => setters.SetProperty(candidate => candidate.Type, "DYNAMIC_TYPE_B"),
            TestContext.Current.CancellationToken));

        var fetched = await selected.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, affected);
        Assert.Equal("DYNAMIC_TYPE_A", fetched.Type);
        Assert.Equal("updated", fetched.Properties["status"]);
    }

    [Fact]
    public async Task RelationshipConcreteClrType_IsPreservedWhenMaterialized()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);

        var originalSince = DateTime.UtcNow;
        var knows = new Knows { Since = originalSince };
        await this.Graph.ConnectAsync(p1, knows, p2, cancellationToken: TestContext.Current.CancellationToken);

        var fetched = await this.Graph.FindRelationshipByTestKeyAsync<Knows>(
            knows.TestKey,
            null,
            TestContext.Current.CancellationToken);
        Assert.Equal(Labels.GetLabelFromType(typeof(Knows)), fetched.Type);
        Assert.Equal(originalSince, fetched.Since);

        Assert.IsType<Knows>(fetched);
        Assert.Null(await this.Graph.RelationshipsByTestKey<Friend>(knows.TestKey)
            .SingleOrDefaultAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CanBeginTransactionAndRollback()
    {
        var tx = await this.Graph.GetTransactionAsync(TestContext.Current.CancellationToken);
        var person = new Person { FirstName = "TxTest" };
        await this.Graph.CreateNodeAsync(person, tx, TestContext.Current.CancellationToken);
        await tx.DisposeAsync(); // Rollback
        Assert.Null(await this.Graph.NodesByTestKey<Person>(person.TestKey)
            .SingleOrDefaultAsync(TestContext.Current.CancellationToken));
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

        var fetched = await this.Graph.Nodes<PersonWithGenericCollectionOfPrimitiveProperty>()
            .SingleAsync(TestContext.Current.CancellationToken);
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

        var fetched = await Graph.Nodes<ValueTypeCollectionNode>()
            .SingleAsync(TestContext.Current.CancellationToken);
        var queried = await Graph.Nodes<ValueTypeCollectionNode>()
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
        var relationship = new DynamicRelationship("DYNAMIC_COLLECTION_RELATIONSHIP", properties);
        await Graph.CreateRelationshipAsync(
            Graph.DynamicNodes().OfLabel("DynamicCollectionStart"),
            relationship,
            Graph.DynamicNodes().OfLabel("DynamicCollectionEnd"),
            cancellationToken: TestContext.Current.CancellationToken);

        var fetchedNode = await Graph.FindDynamicNodeByTestKeyAsync(
            "DynamicCollectionStart",
            null,
            TestContext.Current.CancellationToken);
        var fetchedRelationship = await Graph.FindDynamicRelationshipByTestKeyAsync(
            "DYNAMIC_COLLECTION_RELATIONSHIP",
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
        var startSelector = $"DynamicStart-{Guid.NewGuid():N}";
        var endSelector = $"DynamicEnd-{Guid.NewGuid():N}";
        dynamicStart.Labels = [.. dynamicStart.Labels, startSelector];
        dynamicEnd.Labels = [.. dynamicEnd.Labels, endSelector];

        await Graph.CreateNodeAsync(dynamicStart, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(dynamicEnd, null, TestContext.Current.CancellationToken);

        IRelationship relationship = new KnowsWell
        {
            HowWell = "Very well",
        };
        var dynamicRelationship = relationship.ToDynamic();
        await Graph.CreateRelationshipAsync(
            Graph.DynamicNodes().OfLabel(startSelector),
            dynamicRelationship,
            Graph.DynamicNodes().OfLabel(endSelector),
            cancellationToken: TestContext.Current.CancellationToken);

        var fetchedNode = await Graph.FindDynamicNodeByTestKeyAsync(
            startSelector,
            null,
            TestContext.Current.CancellationToken);
        var fetchedRelationship = await Graph.FindDynamicRelationshipByTestKeyAsync(
            dynamicRelationship.Type,
            null,
            TestContext.Current.CancellationToken);

        Assert.Contains(Labels.GetLabelFromType(startNode.GetType()), fetchedNode.Labels);
        Assert.Equal("Start", fetchedNode.Properties[nameof(SpacedLabelVenue.Name)]);
        Assert.Equal(Labels.GetLabelFromType(relationship.GetType()), fetchedRelationship.Type);
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

        var fetched = await Graph.FindDynamicNodeByTestKeyAsync(
            "DynamicNestedComplex",
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
            .Where(m => m.Text == memory.Text)
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
            .Where(u => u.Email == user.Email)
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
