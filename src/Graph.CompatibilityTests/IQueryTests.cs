// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

public interface IQueryTests : IGraphTest
{
    [Fact]
    public async Task CanQueryNodesByProperty()
    {
        var p1 = new Person { FirstName = "Alice", LastName = "Smith" };
        var p2 = new Person { FirstName = "Bob", LastName = "Smith" };
        var p3 = new Person { FirstName = "Charlie", LastName = "Jones" };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p3, null, TestContext.Current.CancellationToken);

        var smiths = await this.Graph.Nodes<Person>()
            .Where(p => p.LastName == "Smith")
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Contains(smiths, p => p.FirstName == "Alice");
        Assert.Contains(smiths, p => p.FirstName == "Bob");
        Assert.DoesNotContain(smiths, p => p.FirstName == "Charlie");
    }

    [Fact]
    public async Task CanQueryNodeWithEscapableLabel()
    {
        var venue = new SpacedLabelVenue { Name = "Union Hall" };
        await this.Graph.CreateNodeAsync(venue, null, TestContext.Current.CancellationToken);

        var found = await this.Graph.Nodes<SpacedLabelVenue>()
            .Where(v => v.Name == "Union Hall")
            .ToListAsync(TestContext.Current.CancellationToken);

        var roundTripped = Assert.Single(found);
        Assert.Equal(venue.Id, roundTripped.Id);
        Assert.Equal("Union Hall", roundTripped.Name);
    }

    /// <summary>
    /// Pins the #221 decision: complex-property navigation is null-propagating, so a leaf
    /// null-comparison matches BOTH owners with no complex-property node at all and owners whose
    /// leaf value is literally null (mirroring Cypher's null semantics and C#'s ?. intuition).
    /// Recorded on #221; a future revisit is tracked in #233.
    /// </summary>
    [Fact]
    public async Task NavigationEquality_NullMatchesMissingComplexProperty()
    {
        var missing = new PersonWithOptionalProfile { FirstName = "NoProfile", Profile = null };
        var nullLeaf = new PersonWithOptionalProfile { FirstName = "NullMotto", Profile = new OptionalProfileValue { Motto = null } };
        var populated = new PersonWithOptionalProfile { FirstName = "HasMotto", Profile = new OptionalProfileValue { Motto = "carpe diem" } };
        await this.Graph.CreateNodeAsync(missing, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(nullLeaf, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(populated, null, TestContext.Current.CancellationToken);

        var matches = await this.Graph.Nodes<PersonWithOptionalProfile>()
            .Where(p => p.Profile!.Motto == null)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, matches.Count);
        Assert.Contains(matches, p => p.FirstName == "NoProfile");
        Assert.Contains(matches, p => p.FirstName == "NullMotto");
        Assert.DoesNotContain(matches, p => p.FirstName == "HasMotto");
    }

    /// <summary>
    /// The projection counterpart of the #221 decision: projecting a leaf through a missing
    /// complex property yields a null value for that row instead of dropping the row.
    /// </summary>
    [Fact]
    public async Task NavigationProjection_MissingComplexPropertyYieldsNull()
    {
        var missing = new PersonWithOptionalProfile { FirstName = "NoProfile", Profile = null };
        var populated = new PersonWithOptionalProfile { FirstName = "HasMotto", Profile = new OptionalProfileValue { Motto = "carpe diem" } };
        await this.Graph.CreateNodeAsync(missing, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(populated, null, TestContext.Current.CancellationToken);

        var rows = await this.Graph.Nodes<PersonWithOptionalProfile>()
            .Select(p => new { p.FirstName, p.Profile!.Motto })
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.FirstName == "NoProfile" && r.Motto == null);
        Assert.Contains(rows, r => r.FirstName == "HasMotto" && r.Motto == "carpe diem");
    }

    [Fact]
    public async Task CanQueryNodesByMultipleProperties()
    {
        var p1 = new Person { FirstName = "Alice", LastName = "Smith" };
        var p2 = new Person { FirstName = "Bob", LastName = "Smith" };
        var p3 = new Person { FirstName = "Charlie", LastName = "Jones" };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p3, null, TestContext.Current.CancellationToken);

        var smiths = await this.Graph.Nodes<Person>()
            .Where(p => p.LastName == "Smith" && p.FirstName.StartsWith("A"))
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Single(smiths);
        Assert.Equal("Alice", smiths[0].FirstName);
    }

    [Fact]
    public async Task CanQueryNodesWithComplexPropertyInWhere()
    {
        var p1 = new PersonWithComplexProperties { FirstName = "Alice", LastName = "Smith", Age = 30, Address = new AddressValue { City = "New York", Street = "123 Main St" } };
        var p2 = new PersonWithComplexProperties { FirstName = "Bob", LastName = "Smith", Age = 25, Address = new AddressValue { City = "Los Angeles", Street = "456 Elm St" } };
        var p3 = new PersonWithComplexProperties { FirstName = "Charlie", LastName = "Jones", Age = 35, Address = new AddressValue { City = "Chicago", Street = "789 Oak St" } };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p3, null, TestContext.Current.CancellationToken);

        var youngSmiths = await this.Graph.Nodes<PersonWithComplexProperties>()
            .Where(p => p.LastName == "Smith" && p.Age < 30 && p.Address.City == "Los Angeles")
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(youngSmiths);
        Assert.Equal("Bob", youngSmiths[0].FirstName);
    }

    [Fact]
    public async Task CanQueryNodesWithComplexPropertyInSelect()
    {
        var p1 = new PersonWithComplexProperties { FirstName = "Alice", LastName = "Smith", Age = 30, Address = new AddressValue { City = "New York", Street = "123 Main St" } };
        var p2 = new PersonWithComplexProperties { FirstName = "Bob", LastName = "Smith", Age = 25, Address = new AddressValue { City = "Los Angeles", Street = "456 Elm St" } };
        var p3 = new PersonWithComplexProperties { FirstName = "Charlie", LastName = "Jones", Age = 35, Address = new AddressValue { City = "Chicago", Street = "789 Oak St" } };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p3, null, TestContext.Current.CancellationToken);

        var peopleAndCities = await this.Graph.Nodes<PersonWithComplexProperties>()
            .Select(p => new { p.FirstName, p.Address.City })
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(3, peopleAndCities.Count);
        Assert.Contains(peopleAndCities, pc => pc.FirstName == "Alice" && pc.City == "New York");
        Assert.Contains(peopleAndCities, pc => pc.FirstName == "Bob" && pc.City == "Los Angeles");
        Assert.Contains(peopleAndCities, pc => pc.FirstName == "Charlie" && pc.City == "Chicago");
    }

    [Fact]
    public async Task CanQueryNodesByMultipleComplexPropertiesInSequence()
    {
        var c1 = new Class1
        {
            Property1 = "Value1",
            Property2 = "Value2",
            A = new ComplexClassA
            {
                Property1 = "A1",
                Property2 = "A2",
                B = new ComplexClassB { Property1 = "B1" }
            },
            B = new ComplexClassB { Property1 = "B2" }
        };

        var c2 = new Class2
        {
            Property1 = "Value3",
            Property2 = "Value4",
            A = new List<ComplexClassA>
            {
                new ComplexClassA { Property1 = "A3", Property2 = "A4" }
            },
            B = new List<ComplexClassB>
            {
                new ComplexClassB { Property1 = "B3" }
            }
        };

        await this.Graph.CreateNodeAsync(c1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(c2, null, TestContext.Current.CancellationToken);

        var results = await this.Graph.Nodes<Class1>()
            .Where(c => c.A!.B!.Property1 == "B1" || c.B!.Property1 == "B2")
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(results);
        Assert.Equal("Value1", results[0].Property1);
        Assert.Equal("B2", results[0].B!.Property1);
    }

    [Fact]
    public async Task CanQueryAllNodes()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);

        var all = await this.Graph.Nodes<Person>().ToListAsync(TestContext.Current.CancellationToken);
        Assert.True(all.Count >= 2);
        Assert.Contains(all, p => p.FirstName == "A");
        Assert.Contains(all, p => p.FirstName == "B");
    }

    [Fact]
    public async Task CanQueryWithWhereAndOrderBy()
    {
        var p1 = new Person { FirstName = "Charlie", LastName = "Smith" };
        var p2 = new Person { FirstName = "Alice", LastName = "Smith" };
        var p3 = new Person { FirstName = "Bob", LastName = "Jones" };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p3, null, TestContext.Current.CancellationToken);

        var smithsOrdered = await this.Graph.Nodes<Person>()
            .Where(p => p.LastName == "Smith")
            .OrderBy(p => p.FirstName)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, smithsOrdered.Count);
        Assert.Equal("Alice", smithsOrdered[0].FirstName);
        Assert.Equal("Charlie", smithsOrdered[1].FirstName);
    }

    [Fact]
    public async Task CanQueryWithTakeAndSkip()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        var p3 = new Person { FirstName = "C" };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p3, null, TestContext.Current.CancellationToken);

        var taken = await this.Graph.Nodes<Person>().OrderBy(p => p.FirstName).Take(2).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, taken.Count);
        Assert.Equal("A", taken[0].FirstName);
        Assert.Equal("B", taken[1].FirstName);

        var skipped = await this.Graph.Nodes<Person>().OrderBy(p => p.FirstName).Skip(1).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Contains(skipped, p => p.FirstName == "B");
        Assert.Contains(skipped, p => p.FirstName == "C");
    }

    [Theory]
    [InlineData(0, "")]
    [InlineData(1, "A")]
    [InlineData(10, "A,B,C")]
    public async Task CanQueryWithTakeEdgeCases(int take, string expectedCsv)
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        var p3 = new Person { FirstName = "C" };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p3, null, TestContext.Current.CancellationToken);

        var taken = await this.Graph.Nodes<Person>()
            .OrderBy(p => p.FirstName)
            .Take(take)
            .ToListAsync(TestContext.Current.CancellationToken);

        var expected = string.IsNullOrEmpty(expectedCsv) ? Array.Empty<string>() : expectedCsv.Split(',');
        Assert.Equal(expected, taken.Select(p => p.FirstName).ToArray());
    }

    [Fact]
    public async Task CanQueryWithTakeAndWhere()
    {
        var p1 = new Person { FirstName = "Alice", LastName = "Smith" };
        var p2 = new Person { FirstName = "Bob", LastName = "Smith" };
        var p3 = new Person { FirstName = "Charlie", LastName = "Jones" };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p3, null, TestContext.Current.CancellationToken);

        var taken = await this.Graph.Nodes<Person>()
            .Where(p => p.LastName == "Smith")
            .OrderBy(p => p.FirstName)
            .Take(1)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(taken);
        Assert.Equal("Alice", taken[0].FirstName);
    }

    [Fact]
    public async Task CanQueryWithTakeAndSelect()
    {
        var p1 = new Person { FirstName = "Alice", LastName = "Smith" };
        var p2 = new Person { FirstName = "Bob", LastName = "Smith" };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);

        var taken = await this.Graph.Nodes<Person>()
            .OrderBy(p => p.FirstName)
            .Select(p => p.FirstName)
            .Take(1)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(taken);
        Assert.Equal("Alice", taken[0]);
    }

    [Fact]
    public async Task CanQueryWithTakeAndDistinct()
    {
        var p1 = new Person { FirstName = "Alice", LastName = "Smith" };
        var p2 = new Person { FirstName = "Alice", LastName = "Jones" };
        var p3 = new Person { FirstName = "Bob", LastName = "Smith" };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p3, null, TestContext.Current.CancellationToken);

        var taken = await this.Graph.Nodes<Person>()
            .Select(p => p.FirstName)
            .Distinct()
            .OrderBy(name => name)
            .Take(2)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, taken.Count);
        Assert.Equal("Alice", taken[0]);
        Assert.Equal("Bob", taken[1]);
    }

    [Fact]
    public async Task CanQueryWithFirstAndSingle()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);

        var first = await this.Graph.Nodes<Person>().OrderBy(p => p.FirstName).FirstAsync(TestContext.Current.CancellationToken);
        Assert.Equal("A", first.FirstName);

        var single = await this.Graph.Nodes<Person>().SingleAsync(p => p.FirstName == "A", TestContext.Current.CancellationToken);
        Assert.Equal("A", single.FirstName);
    }

    [Fact]
    public async Task FirstAsync_EmptySource_Throws()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var missingGroup = $"FirstAsync-empty-{Guid.NewGuid():N}";

        var query = this.Graph.Nodes<Person>().Where(p => p.LastName == missingGroup);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => query.FirstAsync(cancellationToken));
    }

    [Fact]
    public async Task SingleAsync_EmptySource_Throws()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var missingGroup = $"SingleAsync-empty-{Guid.NewGuid():N}";

        var query = this.Graph.Nodes<Person>().Where(p => p.LastName == missingGroup);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => query.SingleAsync(cancellationToken));
    }

    [Fact]
    public async Task SingleAsync_MultipleElements_Throws()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var group = $"SingleAsync-many-{Guid.NewGuid():N}";
        await this.Graph.CreateNodeAsync(new Person { FirstName = "A", LastName = group }, null, cancellationToken);
        await this.Graph.CreateNodeAsync(new Person { FirstName = "B", LastName = group }, null, cancellationToken);

        var query = this.Graph.Nodes<Person>().Where(p => p.LastName == group);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => query.SingleAsync(cancellationToken));
    }

    [Fact]
    public async Task SingleOrDefaultAsync_MultipleElements_Throws()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var group = $"SingleOrDefaultAsync-many-{Guid.NewGuid():N}";
        await this.Graph.CreateNodeAsync(new Person { FirstName = "A", LastName = group }, null, cancellationToken);
        await this.Graph.CreateNodeAsync(new Person { FirstName = "B", LastName = group }, null, cancellationToken);

        var query = this.Graph.Nodes<Person>().Where(p => p.LastName == group);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => query.SingleOrDefaultAsync(cancellationToken));
    }

    [Fact]
    public async Task FirstOrDefaultAsync_EmptySource_ReturnsDefault()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var missingGroup = $"FirstOrDefaultAsync-empty-{Guid.NewGuid():N}";

        var result = await this.Graph.Nodes<Person>()
            .Where(p => p.LastName == missingGroup)
            .FirstOrDefaultAsync(cancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task SingleOrDefaultAsync_EmptySource_ReturnsDefault()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var missingGroup = $"SingleOrDefaultAsync-empty-{Guid.NewGuid():N}";

        var result = await this.Graph.Nodes<Person>()
            .Where(p => p.LastName == missingGroup)
            .SingleOrDefaultAsync(cancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task SingleAsync_SingleElement_ReturnsElement()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var group = $"SingleAsync-one-{Guid.NewGuid():N}";
        var person = new Person { FirstName = "Only", LastName = group };
        await this.Graph.CreateNodeAsync(person, null, cancellationToken);

        var result = await this.Graph.Nodes<Person>()
            .Where(p => p.LastName == group)
            .SingleAsync(cancellationToken);

        Assert.Equal(person.Id, result.Id);
    }

    [Fact]
    public async Task SingleOrDefaultAsync_SingleElement_ReturnsElement()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var group = $"SingleOrDefaultAsync-one-{Guid.NewGuid():N}";
        var person = new Person { FirstName = "Only", LastName = group };
        await this.Graph.CreateNodeAsync(person, null, cancellationToken);

        var result = await this.Graph.Nodes<Person>()
            .Where(p => p.LastName == group)
            .SingleOrDefaultAsync(cancellationToken);

        Assert.NotNull(result);
        Assert.Equal(person.Id, result.Id);
    }

    [Fact]
    public async Task FirstAsync_DefaultValueProjection_ReturnsDefaultValue()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var group = $"FirstAsync-default-{Guid.NewGuid():N}";
        await this.Graph.CreateNodeAsync(new Person { FirstName = "Default", LastName = group, Age = 0 }, null, cancellationToken);

        var result = await this.Graph.Nodes<Person>()
            .Where(p => p.LastName == group)
            .Select(p => p.Age)
            .FirstAsync(cancellationToken);

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task FirstAsync_NullScalarProjectionToNonNullableValue_ThrowsClearMaterializationError()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var group = $"FirstAsync-null-scalar-{Guid.NewGuid():N}";
        var node = new DynamicNode(
            [nameof(Person)],
            new Dictionary<string, object?>
            {
                [nameof(Person.FirstName)] = "NullScalar",
                [nameof(Person.LastName)] = group
            });

        await this.Graph.CreateNodeAsync(node, null, cancellationToken);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            this.Graph.Nodes<Person>()
                .Where(p => p.LastName == group)
                .Select(p => p.Age)
                .FirstAsync(cancellationToken));

        Assert.Contains("Cannot materialize null into non-nullable type", exception.Message);
        Assert.Contains(typeof(int).FullName!, exception.Message);
        Assert.DoesNotContain("Sequence contains no elements", exception.Message);
    }

    [Fact]
    public async Task SingleAsync_DefaultValueProjection_ReturnsDefaultValue()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var group = $"SingleAsync-default-{Guid.NewGuid():N}";
        await this.Graph.CreateNodeAsync(new Person { FirstName = "Default", LastName = group, Age = 0 }, null, cancellationToken);

        var result = await this.Graph.Nodes<Person>()
            .Where(p => p.LastName == group)
            .Select(p => p.Age)
            .SingleAsync(cancellationToken);

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task CanQueryWithAnyAndCount()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);

        var anyA = await this.Graph.Nodes<Person>().AnyAsync(p => p.FirstName == "A", TestContext.Current.CancellationToken);
        Assert.True(anyA);

        var count = await this.Graph.Nodes<Person>().CountAsync(TestContext.Current.CancellationToken);
        Assert.True(count >= 2);
    }

    [Fact]
    public async Task Issue74AsyncTerminals_WorkAndPreserveEmptySourceSemantics()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var group = $"Issue74-{Guid.NewGuid():N}";
        var missingGroup = $"{group}-missing";
        var people = new[]
        {
            new Person { FirstName = $"{group}-Alice", LastName = group, Age = 21 },
            new Person { FirstName = $"{group}-Bob", LastName = group, Age = 35 },
            new Person { FirstName = $"{group}-Charlie", LastName = group, Age = 42 }
        };

        foreach (var person in people)
        {
            await this.Graph.CreateNodeAsync(person, null, cancellationToken);
        }

        var ordered = this.Graph.Nodes<Person>()
            .Where(p => p.LastName == group)
            .OrderBy(p => p.Age);

        var last = await ordered.LastAsync(cancellationToken);
        Assert.Equal(42, last.Age);

        var lastOrDefault = await ordered.LastOrDefaultAsync(cancellationToken);
        Assert.NotNull(lastOrDefault);
        Assert.Equal(42, lastOrDefault.Age);

        var olderCount = await this.Graph.Nodes<Person>()
            .CountAsync(p => p.LastName == group && p.Age >= 35, cancellationToken);
        Assert.Equal(2, olderCount);

        var anyYoung = await this.Graph.Nodes<Person>()
            .AnyAsync(p => p.LastName == group && p.Age == 21, cancellationToken);
        Assert.True(anyYoung);

        var maxAge = await this.Graph.Nodes<Person>()
            .Where(p => p.LastName == group)
            .MaxAsync(p => p.Age, cancellationToken);
        Assert.Equal(42, maxAge);

        var minAge = await this.Graph.Nodes<Person>()
            .Where(p => p.LastName == group)
            .MinAsync(p => p.Age, cancellationToken);
        Assert.Equal(21, minAge);

        var emptyOrdered = this.Graph.Nodes<Person>()
            .Where(p => p.LastName == missingGroup)
            .OrderBy(p => p.Age);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => emptyOrdered.LastAsync(cancellationToken));
        Assert.Null(await emptyOrdered.LastOrDefaultAsync(cancellationToken));

        var emptyCount = await this.Graph.Nodes<Person>()
            .CountAsync(p => p.LastName == missingGroup, cancellationToken);
        Assert.Equal(0, emptyCount);

        var emptyAny = await this.Graph.Nodes<Person>()
            .AnyAsync(p => p.LastName == missingGroup, cancellationToken);
        Assert.False(emptyAny);

        var empty = this.Graph.Nodes<Person>()
            .Where(p => p.LastName == missingGroup);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => empty.MaxAsync(p => p.Age, cancellationToken));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => empty.MinAsync(p => p.Age, cancellationToken));
    }

    [Fact]
    public async Task CanQueryWithContainsOnScalarProjection()
    {
        var p1 = new Person { FirstName = "Contains-A", Bio = "alpha" };
        var p2 = new Person { FirstName = "Contains-B", Bio = null! };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);

        var names = this.Graph.Nodes<Person>()
            .Where(p => p.FirstName.StartsWith("Contains-"))
            .Select(p => p.FirstName);

        Assert.True(await names.ContainsAsync("Contains-A", TestContext.Current.CancellationToken));
        Assert.False(await names.ContainsAsync("Contains-Z", TestContext.Current.CancellationToken));

        var bios = this.Graph.Nodes<Person>()
            .Where(p => p.FirstName.StartsWith("Contains-"))
            .Select(p => p.Bio);

        Assert.True(await bios.ContainsAsync(null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CanQueryWithLocalScopeVariableCapture()
    {
        var p1 = new Person { FirstName = "A" };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);

        var localName = "A";

        var a = await this.Graph.Nodes<Person>().Where(p => p.FirstName == localName).FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(a);
        Assert.Equal(localName, a.FirstName);
    }

    [Fact]
    public async Task CanQueryWithLocalScopeObjectCapture()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);

        var a = await this.Graph.Nodes<Person>().Where(p => p.FirstName == p1.FirstName).FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(a);
        Assert.Equal(p1.FirstName, a.FirstName);
    }

    [Fact]
    public async Task CanQueryWithDateTimeComparison()
    {
        var now = DateTime.UtcNow;
        var memory1 = new Memory
        {
            Text = "Old memory",
            CreatedAt = now.AddDays(-7),
            UpdatedAt = now.AddDays(-7),
            CapturedBy = new MemorySource { Name = "Test", Description = "Test", Version = "1.0", Device = "Test" },
            Location = new Point { Longitude = 0, Latitude = 0, Height = 0 },
            Deleted = false
        };
        var memory2 = new Memory
        {
            Text = "Recent memory",
            CreatedAt = now.AddDays(-1),
            UpdatedAt = now.AddDays(-1),
            CapturedBy = new MemorySource { Name = "Test", Description = "Test", Version = "1.0", Device = "Test" },
            Location = new Point { Longitude = 0, Latitude = 0, Height = 0 },
            Deleted = false
        };
        var memory3 = new Memory
        {
            Text = "Very recent memory",
            CreatedAt = now.AddHours(-1),
            UpdatedAt = now.AddHours(-1),
            CapturedBy = new MemorySource { Name = "Test", Description = "Test", Version = "1.0", Device = "Test" },
            Location = new Point { Longitude = 0, Latitude = 0, Height = 0 },
            Deleted = false
        };

        await this.Graph.CreateNodeAsync(memory1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(memory2, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(memory3, null, TestContext.Current.CancellationToken);

        // Test DateTime parameter comparison - this should generate datetime($param) in Cypher
        var recentMemories = await this.Graph.Nodes<Memory>()
            .Where(m => m.CreatedAt >= now.AddDays(-2))
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, recentMemories.Count);
        Assert.Contains(recentMemories, m => m.Text == "Recent memory");
        Assert.Contains(recentMemories, m => m.Text == "Very recent memory");
        Assert.DoesNotContain(recentMemories, m => m.Text == "Old memory");
    }

    [Fact]
    public async Task CanQueryWithDateTimeLessThanComparison()
    {
        var now = DateTime.UtcNow;
        var memory1 = new Memory
        {
            Text = "Old memory",
            CreatedAt = now.AddDays(-7),
            UpdatedAt = now.AddDays(-7),
            CapturedBy = new MemorySource { Name = "Test", Description = "Test", Version = "1.0", Device = "Test" },
            Location = new Point { Longitude = 0, Latitude = 0, Height = 0 },
            Deleted = false
        };
        var memory2 = new Memory
        {
            Text = "Recent memory",
            CreatedAt = now.AddDays(-1),
            UpdatedAt = now.AddDays(-1),
            CapturedBy = new MemorySource { Name = "Test", Description = "Test", Version = "1.0", Device = "Test" },
            Location = new Point { Longitude = 0, Latitude = 0, Height = 0 },
            Deleted = false
        };
        var memory3 = new Memory
        {
            Text = "Very recent memory",
            CreatedAt = now.AddHours(-1),
            UpdatedAt = now.AddHours(-1),
            CapturedBy = new MemorySource { Name = "Test", Description = "Test", Version = "1.0", Device = "Test" },
            Location = new Point { Longitude = 0, Latitude = 0, Height = 0 },
            Deleted = false
        };

        await this.Graph.CreateNodeAsync(memory1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(memory2, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(memory3, null, TestContext.Current.CancellationToken);

        // Test DateTime parameter comparison with less than
        var oldMemories = await this.Graph.Nodes<Memory>()
            .Where(m => m.CreatedAt < now.AddDays(-2))
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(oldMemories);
        Assert.Equal("Old memory", oldMemories[0].Text);
    }

    [Fact]
    public async Task CanQueryWithDateTimeEqualsComparison()
    {
        var specificTime = DateTime.UtcNow.AddDays(-3);
        var memory1 = new Memory
        {
            Text = "Exact time memory",
            CreatedAt = specificTime,
            UpdatedAt = specificTime,
            CapturedBy = new MemorySource { Name = "Test", Description = "Test", Version = "1.0", Device = "Test" },
            Location = new Point { Longitude = 0, Latitude = 0, Height = 0 },
            Deleted = false
        };
        var memory2 = new Memory
        {
            Text = "Different time memory",
            CreatedAt = specificTime.AddHours(1),
            UpdatedAt = specificTime.AddHours(1),
            CapturedBy = new MemorySource { Name = "Test", Description = "Test", Version = "1.0", Device = "Test" },
            Location = new Point { Longitude = 0, Latitude = 0, Height = 0 },
            Deleted = false
        };

        await this.Graph.CreateNodeAsync(memory1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(memory2, null, TestContext.Current.CancellationToken);

        // Test DateTime parameter comparison with equals
        var exactTimeMemories = await this.Graph.Nodes<Memory>()
            .Where(m => m.CreatedAt == specificTime)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(exactTimeMemories);
        Assert.Equal("Exact time memory", exactTimeMemories[0].Text);
    }

    [Fact]
    public async Task CanQueryWithDateTimeMemberAccess()
    {
        var p1 = new Person { FirstName = "Temporal-A", DateOfBirth = new DateTime(1990, 5, 15, 0, 0, 0, DateTimeKind.Utc) };
        var p2 = new Person { FirstName = "Temporal-B", DateOfBirth = new DateTime(1990, 6, 15, 0, 0, 0, DateTimeKind.Utc) };
        var p3 = new Person { FirstName = "Temporal-C", DateOfBirth = new DateTime(1991, 5, 15, 0, 0, 0, DateTimeKind.Utc) };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p3, null, TestContext.Current.CancellationToken);

        var bornInMay1990 = await this.Graph.Nodes<Person>()
            .Where(p => p.FirstName.StartsWith("Temporal-"))
            .Where(p => p.DateOfBirth.Year == 1990 && p.DateOfBirth.Month == 5)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(bornInMay1990);
        Assert.Equal("Temporal-A", bornInMay1990[0].FirstName);

        var bornOnFifteenth = await this.Graph.Nodes<Person>()
            .Where(p => p.FirstName.StartsWith("Temporal-"))
            .Where(p => p.DateOfBirth.Day == 15)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(3, bornOnFifteenth.Count);
    }

    [Fact]
    public async Task CanQueryWithTraverseAndPathSegmentsAndWhereClauseWithoutComplexProperties()
    {
        // Create test data similar to the user's scenario
        var user = new User { Id = "user123", Name = "Test User", Email = "test@example.com", GoogleId = "google123" };
        var memory1 = new MemoryWithoutSourceProperty
        {
            Text = "Memory 1",
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow.AddDays(-1),
            Location = new Point { Longitude = 0, Latitude = 0, Height = 0 },
            Deleted = false,
        };
        var memory2 = new MemoryWithoutSourceProperty
        {
            Text = "Memory 2",
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            UpdatedAt = DateTime.UtcNow.AddDays(-2),
            Location = new Point { Longitude = 0, Latitude = 0, Height = 0 },
            Deleted = false,
        };

        var memorySourceNode = new MemorySourceNode
        {
            Name = "TestApp",
            Description = "A test application",
            Version = "1.0",
            Device = "TestDevice"
        };

        await this.Graph.CreateNodeAsync(user, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(memory1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(memory2, null, TestContext.Current.CancellationToken);

        await this.Graph.CreateNodeAsync(memorySourceNode, null, TestContext.Current.CancellationToken);

        // Create relationships
        var userMemory1 = new UserMemory(user.Id, memory1.Id);
        var userMemory2 = new UserMemory(user.Id, memory2.Id);
        var memory1ToSource = new MemoryToMemorySourceNode(memory1.Id, memorySourceNode.Id);
        var memory2ToSource = new MemoryToMemorySourceNode(memory2.Id, memorySourceNode.Id);
        await this.Graph.CreateRelationshipAsync(userMemory1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(userMemory2, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(memory1ToSource, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(memory2ToSource, null, TestContext.Current.CancellationToken);

        // Test the problematic query pattern: Traverse + PathSegments + WHERE + Select
        var from = DateTime.UtcNow.AddDays(-3);
        var to = DateTime.UtcNow;
        var limit = 1;

        var memories = await this.Graph.Nodes<User>()
            .Where(u => u.Id == user.Id)
            .Traverse<UserMemory, MemoryWithoutSourceProperty>()
            .Where(m => m.CreatedAt >= from)
            .Where(m => m.CreatedAt <= to)
            .OrderByDescending(m => m.CreatedAt)
            .Take(limit)
            .PathSegments<MemoryWithoutSourceProperty, MemoryToMemorySourceNode, MemorySourceNode>()
            .Select(ps => new { ps.StartNode, ps.EndNode })
            .ToListAsync(TestContext.Current.CancellationToken);

        // Verify the query executed successfully
        Assert.NotNull(memories);
        Assert.Single(memories);
    }

    [Fact]
    public async Task CanQueryWithTraverseAndPathSegmentsAndWhereClauseWithComplexProperties()
    {
        // Create test data similar to the user's scenario
        var user = new User { Id = "user123", Name = "Test User", Email = "test@example.com", GoogleId = "google123" };
        var memory1 = new Memory
        {
            Text = "Memory 1",
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow.AddDays(-1),
            Location = new Point { Longitude = 0, Latitude = 0, Height = 0 },
            Deleted = false,
            CapturedBy = new MemorySource { Name = "Test", Description = "Test", Version = "1.0", Device = "Test" }
        };
        var memory2 = new Memory
        {
            Text = "Memory 2",
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            UpdatedAt = DateTime.UtcNow.AddDays(-2),
            Location = new Point { Longitude = 0, Latitude = 0, Height = 0 },
            Deleted = false,
            CapturedBy = new MemorySource { Name = "Test", Description = "Test", Version = "1.0", Device = "Test" }
        };

        var memorySourceNode = new MemorySourceNode
        {
            Name = "TestApp",
            Description = "A test application",
            Version = "1.0",
            Device = "TestDevice"
        };

        await this.Graph.CreateNodeAsync(user, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(memory1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(memory2, null, TestContext.Current.CancellationToken);

        await this.Graph.CreateNodeAsync(memorySourceNode, null, TestContext.Current.CancellationToken);

        // Create relationships
        var userMemory1 = new UserMemory(user.Id, memory1.Id);
        var userMemory2 = new UserMemory(user.Id, memory2.Id);
        var memory1ToSource = new MemoryToMemorySourceNode(memory1.Id, memorySourceNode.Id);
        var memory2ToSource = new MemoryToMemorySourceNode(memory2.Id, memorySourceNode.Id);
        await this.Graph.CreateRelationshipAsync(userMemory1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(userMemory2, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(memory1ToSource, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(memory2ToSource, null, TestContext.Current.CancellationToken);

        // Test the problematic query pattern: Traverse + PathSegments + WHERE + Select
        var from = DateTime.UtcNow.AddDays(-3);
        var to = DateTime.UtcNow;
        var limit = 1;

        var memories = await this.Graph.Nodes<User>()
            .Where(u => u.Id == user.Id)
            .Traverse<UserMemory, Memory>()
            .Where(m => m.CreatedAt >= from)
            .Where(m => m.CreatedAt <= to)
            .OrderByDescending(m => m.CreatedAt)
            .Take(limit)
            .PathSegments<Memory, MemoryToMemorySourceNode, MemorySourceNode>()
            .Select(ps => new { ps.StartNode, ps.EndNode })
            .ToListAsync(TestContext.Current.CancellationToken);

        // Verify the query executed successfully
        Assert.NotNull(memories);
        Assert.Single(memories);
    }
}
