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
    public async Task NullableComplexCollections_PreserveEverySlotAndRuntimeType()
    {
        var node = new NullableComplexCollectionNode
        {
            Animals =
            [
                null,
                new DogDescription { Name = "Rex", Breed = "Labrador" },
                null,
                null,
                new PoliceDogDescription { Name = "K9", Breed = "Shepherd", Badge = "42" },
                null,
            ],
            Addresses = [null, null, null],
            EmptyAddresses = [],
        };
        await Graph.CreateNodeAsync(node, cancellationToken: TestContext.Current.CancellationToken);

        var fetched = await Graph.Nodes<NullableComplexCollectionNode>()
            .Where(candidate => candidate.Marker == node.Marker)
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Collection(
            fetched.Animals,
            Assert.Null,
            animal => Assert.IsType<DogDescription>(animal),
            Assert.Null,
            Assert.Null,
            animal => Assert.IsType<PoliceDogDescription>(animal),
            Assert.Null);
        Assert.Equal(3, fetched.Addresses.Length);
        Assert.All(fetched.Addresses, Assert.Null);
        Assert.Empty(fetched.EmptyAddresses);

        var dynamicCandidates = await Graph.DynamicNodes()
            .OfLabel(Labels.GetLabelFromType(typeof(NullableComplexCollectionNode)))
            .ToListAsync(TestContext.Current.CancellationToken);
        var dynamicFetched = Assert.Single(
            dynamicCandidates,
            candidate => Equals(candidate.Properties[nameof(NullableComplexCollectionNode.Marker)], node.Marker));
        var dynamicAnimals = Assert.IsType<List<Dictionary<string, object?>?>>(
            dynamicFetched.Properties[nameof(NullableComplexCollectionNode.Animals)]);
        Assert.Collection(
            dynamicAnimals,
            Assert.Null,
            Assert.NotNull,
            Assert.Null,
            Assert.Null,
            Assert.NotNull,
            Assert.Null);
        Assert.DoesNotContain("NULLABLE_ANIMAL", dynamicFetched.Properties.Keys);
    }

    [Fact]
    public async Task DynamicNullableComplexCollection_PreservesAllNullAndMixedSlots()
    {
        var label = $"DynamicNullableComplex{Guid.NewGuid():N}";
        var node = new DynamicNode(
            [label],
            new Dictionary<string, object?>
            {
                ["mixed"] = new List<Dictionary<string, object?>?>
                {
                    null,
                    new() { ["name"] = "first" },
                    null,
                    new() { ["name"] = "last" },
                    null,
                },
                ["allNull"] = new List<Dictionary<string, object?>?> { null, null },
                ["empty"] = new List<Dictionary<string, object?>?>(),
            });
        await Graph.CreateNodeAsync(node, cancellationToken: TestContext.Current.CancellationToken);

        var fetched = await Graph.DynamicNodes().OfLabel(label)
            .SingleAsync(TestContext.Current.CancellationToken);
        var mixed = Assert.IsType<List<Dictionary<string, object?>?>>(fetched.Properties["mixed"]);
        Assert.Collection(
            mixed,
            Assert.Null,
            item => Assert.Equal("first", item!["name"]),
            Assert.Null,
            item => Assert.Equal("last", item!["name"]),
            Assert.Null);
        Assert.Equal(2, Assert.IsType<List<Dictionary<string, object?>?>>(fetched.Properties["allNull"]).Count);
        Assert.Empty(Assert.IsType<List<Dictionary<string, object?>?>>(fetched.Properties["empty"]));
    }

    [Fact]
    public async Task NullableComplexCollection_DeleteCascadesOnlyRealChildren()
    {
        var marker = $"nullable-complex-delete-{Guid.NewGuid():N}";
        var node = new NullableComplexCollectionNode
        {
            Marker = marker,
            Animals = [null, new DogDescription { Name = marker, Breed = "Labrador" }, null],
        };
        await Graph.CreateNodeAsync(node, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(
            1,
            await Harness.CountNodesByPropertyAsync(
                Graph,
                Labels.GetLabelFromType(typeof(DogDescription)),
                nameof(AnimalDescription.Name),
                [marker],
                TestContext.Current.CancellationToken));

        var affected = await Graph.Nodes<NullableComplexCollectionNode>()
            .Where(candidate => candidate.Marker == marker)
            .DeleteAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, affected);
        Assert.Equal(
            0,
            await Harness.CountNodesByPropertyAsync(
                Graph,
                Labels.GetLabelFromType(typeof(DogDescription)),
                nameof(AnimalDescription.Name),
                [marker],
                TestContext.Current.CancellationToken));
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
        public List<int?> NullableIntegerList { get; set; } = [];
        public string?[] NullableNames { get; set; } = [];
        public List<string?> AllNullNames { get; set; } = [];
        public int?[] EmptyNullableIntegers { get; set; } = [];
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
    [RequiresCapability(GraphCapability.NullElementsInSimpleCollections)]
    public async Task NullableSimpleCollections_RoundTripThroughGetAndQuery()
    {
        var firstId = Guid.Parse("7a2ef43f-dadf-4c88-a2f6-af730f87a963");
        var secondId = Guid.Parse("69bd4638-166e-428f-8fd2-3993338e865f");
        var node = new ValueTypeCollectionNode
        {
            IntegerList = [1, 2, 3],
            IntegerArray = [4, 5],
            Guids = [firstId, secondId],
            Kinds = [ValueCollectionKind.First, ValueCollectionKind.Second],
            NullableIntegers = [null, 6, null, null, 7, null],
            NullableIntegerList = [null, 8, null],
            NullableNames = [null, "first", null, null, "last", null],
            AllNullNames = [null, null, null],
            EmptyNullableIntegers = [],
        };

        await Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken);

        var fetched = await Graph.Nodes<ValueTypeCollectionNode>()
            .SingleAsync(TestContext.Current.CancellationToken);
        var queried = await Graph.Nodes<ValueTypeCollectionNode>()
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        AssertValueTypeCollections(node, fetched);
        Assert.NotNull(queried);
        AssertValueTypeCollections(node, queried);

        var containingNull = await Graph.Nodes<ValueTypeCollectionNode>()
            .Where(candidate => candidate.NullableNames.Contains(null))
            .SingleAsync(TestContext.Current.CancellationToken);
        var containingValue = await Graph.Nodes<ValueTypeCollectionNode>()
            .Where(candidate => candidate.NullableNames.Contains("first"))
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(node.NullableNames, containingNull.NullableNames);
        Assert.Equal(node.NullableNames, containingValue.NullableNames);
        Assert.Null(await Graph.Nodes<ValueTypeCollectionNode>()
            .Where(candidate => candidate.NullableNames.Contains("missing"))
            .SingleOrDefaultAsync(TestContext.Current.CancellationToken));
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

        AssertBasicDynamicCollections(fetchedNode.Properties, firstId, secondId);
        AssertBasicDynamicCollections(fetchedRelationship.Properties, firstId, secondId);
        Assert.Equal(
            new int?[] { 4, 5 },
            CollectionValues(fetchedNode.Properties["nullableIntegers"]).Select(value => value is null
                ? (int?)null
                : Convert.ToInt32(value, CultureInfo.InvariantCulture)));
        Assert.Empty(CollectionValues(fetchedNode.Properties["emptyIntegers"]));
        Assert.Equal(
            new int?[] { 4, 5 },
            CollectionValues(fetchedRelationship.Properties["nullableIntegers"]).Select(value => value is null
                ? (int?)null
                : Convert.ToInt32(value, CultureInfo.InvariantCulture)));
        Assert.Empty(CollectionValues(fetchedRelationship.Properties["emptyIntegers"]));
    }

    [Fact]
    [RequiresCapability(GraphCapability.NullElementsInSimpleCollections)]
    public async Task NullableDynamicSimpleCollections_RoundTripForNodesAndRelationships()
    {
        var firstId = Guid.Parse("7a2ef43f-dadf-4c88-a2f6-af730f87a963");
        var secondId = Guid.Parse("69bd4638-166e-428f-8fd2-3993338e865f");
        var properties = new Dictionary<string, object?>
        {
            ["strings"] = new[] { "first", "second" },
            ["integers"] = new List<int> { 1, 2, 3 },
            ["guids"] = new[] { firstId, secondId },
            ["kinds"] = new[] { ValueCollectionKind.First, ValueCollectionKind.Second },
            ["nullableIntegers"] = new int?[] { null, 4, null, null, 5, null },
            ["allNullIntegers"] = new int?[] { null, null },
            ["emptyIntegers"] = Array.Empty<int?>(),
            ["markerValues"] = new string?[]
            {
                "__cvoya_sc:v1:n:value",
                null,
                "__cvoya_sc:v1:t:value",
                "__cvoya_sc:v1:u:value",
            },
            ["__cvoya_sc:v1:n:c3RyaW5ncw"] = new string?[] { null, "null-index-name" },
            ["__cvoya_sc:v1:t:c3RyaW5ncw"] = new string?[] { "safe", null },
            ["__cvoya_sc:v1:u:c3RyaW5ncw"] = new string?[] { "user-name", null },
            ["__cvoya_sc:v1:scalar"] = "scalar-safe",
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

        var replacement = new int?[] { null, 9, null };
        var replacementCollision = new string?[] { "updated", null };
        var affected = await Graph.DynamicRelationships()
            .Where(candidate => candidate.Type == "DYNAMIC_COLLECTION_RELATIONSHIP")
            .UpdateAsync(
                setters => setters
                    .SetProperty(candidate => candidate.Properties["nullableIntegers"], replacement)
                    .SetProperty(
                        candidate => candidate.Properties["__cvoya_sc:v1:n:c3RyaW5ncw"],
                        replacementCollision),
                TestContext.Current.CancellationToken);
        fetchedRelationship = await Graph.FindDynamicRelationshipByTestKeyAsync(
            "DYNAMIC_COLLECTION_RELATIONSHIP",
            null,
            TestContext.Current.CancellationToken);
        Assert.Equal(1, affected);
        Assert.Equal(replacement, Assert.IsType<List<int?>>(fetchedRelationship.Properties["nullableIntegers"]));
        Assert.Equal(
            replacementCollision,
            Assert.IsType<List<string>>(fetchedRelationship.Properties["__cvoya_sc:v1:n:c3RyaW5ncw"]));

        var deleted = await Graph.DynamicRelationships()
            .Where(candidate => candidate.Type == "DYNAMIC_COLLECTION_RELATIONSHIP")
            .DeleteAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(1, deleted);
        Assert.Null(await Graph.DynamicRelationships()
            .Where(candidate => candidate.Type == "DYNAMIC_COLLECTION_RELATIONSHIP")
            .SingleOrDefaultAsync(TestContext.Current.CancellationToken));
    }

    public record NullableCollectionRelationship : Relationship
    {
        public List<string?> Names { get; set; } = [];
        public int?[] Scores { get; set; } = [];
    }

    [Fact]
    [RequiresCapability(GraphCapability.NullElementsInSimpleCollections)]
    public async Task TypedRelationship_NullableCollections_RoundTrip()
    {
        var start = new Person { FirstName = "collection-start" };
        var end = new Person { FirstName = "collection-end" };
        var relationship = new NullableCollectionRelationship
        {
            Names = [null, "middle", null, null],
            Scores = [null, 1, null, 2, null],
        };
        await Graph.CreateNodeAsync(start, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(end, null, TestContext.Current.CancellationToken);
        await Graph.ConnectAsync(
            start,
            relationship,
            end,
            cancellationToken: TestContext.Current.CancellationToken);

        var fetched = await Graph.Relationships<NullableCollectionRelationship>()
            .SingleAsync(TestContext.Current.CancellationToken);
        var queried = await Graph.Relationships<NullableCollectionRelationship>()
            .Where(candidate => candidate.Names.Contains(null))
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(relationship.Names, fetched.Names);
        Assert.Equal(relationship.Scores, fetched.Scores);
        Assert.Equal(relationship.Names, queried.Names);

        var replacementNames = new List<string?> { "updated", null, null };
        var replacementScores = new int?[] { null, null };
        var affected = await Graph.Relationships<NullableCollectionRelationship>()
            .UpdateAsync(
                setters => setters
                    .SetProperty(candidate => candidate.Names, replacementNames)
                    .SetProperty(candidate => candidate.Scores, replacementScores),
                TestContext.Current.CancellationToken);
        fetched = await Graph.Relationships<NullableCollectionRelationship>()
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, affected);
        Assert.Equal(replacementNames, fetched.Names);
        Assert.Equal(replacementScores, fetched.Scores);

        var deleted = await Graph.Relationships<NullableCollectionRelationship>()
            .DeleteAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, deleted);
        Assert.Null(await Graph.Relationships<NullableCollectionRelationship>()
            .SingleOrDefaultAsync(TestContext.Current.CancellationToken));
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
        Assert.Equal(expected.NullableIntegerList, actual.NullableIntegerList);
        Assert.Equal(expected.NullableNames, actual.NullableNames);
        Assert.Equal(expected.AllNullNames, actual.AllNullNames);
        Assert.Equal(expected.EmptyNullableIntegers, actual.EmptyNullableIntegers);
    }

    private static void AssertDynamicCollections(
        IReadOnlyDictionary<string, object?> properties,
        Guid firstId,
        Guid secondId)
    {
        List<string> expectedPropertyNames =
        [
            "__cvoya_sc:v1:n:c3RyaW5ncw",
            "__cvoya_sc:v1:scalar",
            "__cvoya_sc:v1:t:c3RyaW5ncw",
            "__cvoya_sc:v1:u:c3RyaW5ncw",
            "allNullIntegers",
            "emptyIntegers",
            "guids",
            "integers",
            "kinds",
            "markerValues",
            "nullableIntegers",
            "strings",
        ];
        Assert.Equal(
            expectedPropertyNames,
            properties.Keys.Order(StringComparer.Ordinal));
        AssertBasicDynamicCollections(properties, firstId, secondId);
        Assert.Equal(
            new int?[] { null, 4, null, null, 5, null },
            CollectionValues(properties["nullableIntegers"]).Select(value => value is null
                ? (int?)null
                : Convert.ToInt32(value, CultureInfo.InvariantCulture)));
        Assert.IsType<List<int?>>(properties["nullableIntegers"]);
        Assert.Equal(
            new int?[] { null, null },
            Assert.IsType<List<int?>>(properties["allNullIntegers"]));
        Assert.Empty(Assert.IsType<List<int?>>(properties["emptyIntegers"]));
        Assert.Equal(
            new string?[]
            {
                "__cvoya_sc:v1:n:value",
                null,
                "__cvoya_sc:v1:t:value",
                "__cvoya_sc:v1:u:value",
            },
            Assert.IsType<List<string>>(properties["markerValues"]));
        Assert.Equal(
            new string?[] { null, "null-index-name" },
            Assert.IsType<List<string>>(properties["__cvoya_sc:v1:n:c3RyaW5ncw"]));
        Assert.Equal(
            new string?[] { "safe", null },
            Assert.IsType<List<string>>(properties["__cvoya_sc:v1:t:c3RyaW5ncw"]));
        Assert.Equal(
            new string?[] { "user-name", null },
            Assert.IsType<List<string>>(properties["__cvoya_sc:v1:u:c3RyaW5ncw"]));
        Assert.Equal("scalar-safe", properties["__cvoya_sc:v1:scalar"]);
    }

    private static void AssertBasicDynamicCollections(
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
