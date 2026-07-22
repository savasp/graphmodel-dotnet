// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

/// <summary>
/// Contract coverage for #367: a typed property may use an arbitrary storage name through
/// <c>[Property(Label = ...)]</c>, and that name ends up in Cypher identifier (property-access)
/// position on the read path. A label containing a space, punctuation, a Cypher keyword, structural
/// punctuation, or an embedded backtick must round-trip through create/get/update and be queryable
/// through predicates, projections, ordering, relationships, and complex-property navigation,
/// rendered as one escaped identifier - it must never break out of property position or alter any
/// other data. Unlike the dynamic-identifier contract in
/// <see cref="IHostileDynamicIdentifierTests"/>, these are valid typed-model labels and must be
/// accepted, not rejected.
/// </summary>
public interface ITypedPropertyLabelEscapingTests : IGraphTest
{
    [Node("TypedHostilePropertyLabelNode")]
    public record NodeWithHostilePropertyLabels : Node
    {
        // A space is not a plain Cypher symbolic name, so property access must backtick-escape it.
        [Property(Label = "family name")]
        public string LastName { get; set; } = string.Empty;

        // Punctuation plus a clause keyword and grouping punctuation: AGE projection metadata must
        // treat all of it as identifier content rather than RETURN syntax (#411).
        [Property(Label = "annual, bonus (order by)")]
        public int Rank { get; set; }

        // A plain Cypher keyword exercises the contextual identifier position without punctuation.
        [Property(Label = "match")]
        public string ReservedWord { get; set; } = string.Empty;

        // The escape character itself plus a Cypher-looking break-out payload: escaping must double
        // the embedded backtick so the whole thing stays a single inert identifier through
        // create/update/get.
        [Property(Label = "note`; MATCH (m) DETACH DELETE m //")]
        public string Note { get; set; } = string.Empty;

        public HostilePropertyDetails Details { get; set; } = new();
    }

    public record HostilePropertyDetails
    {
        [Property(Label = "postal code")]
        public string PostalCode { get; set; } = string.Empty;
    }

    [Relationship("TypedHostilePropertyLabelRelationship")]
    public record RelationshipWithHostilePropertyLabels : Relationship
    {
        [Property(Label = "relationship, rank (order by)")]
        public int Rank { get; set; }

        [Property(Label = "match")]
        public string ReservedWord { get; set; } = string.Empty;

        [Property(Label = "note`; MATCH (m) DETACH DELETE m //")]
        public string Note { get; set; } = string.Empty;
    }

    /// <summary>
    /// A node whose properties use hostile custom labels round-trips through create, get, and
    /// update with every value preserved verbatim, and a sentinel node created first is untouched
    /// (the backtick label cannot have broken out to execute a second clause).
    /// </summary>
    [Fact]
    public async Task HostilePropertyLabels_RoundTripThroughCreateGetUpdate()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var sentinel = new Person { FirstName = "Sentinel", LastName = "Untouched" };
        await Graph.CreateNodeAsync(sentinel, null, cancellationToken);

        var node = new NodeWithHostilePropertyLabels
        {
            LastName = "Smith",
            Rank = 7,
            ReservedWord = "reserved",
            Note = "initial",
            Details = new HostilePropertyDetails { PostalCode = "98101" },
        };
        await Graph.CreateNodeAsync(node, null, cancellationToken);

        var selectedNode = Graph.Nodes<NodeWithHostilePropertyLabels>()
            .Where(candidate => candidate.LastName == node.LastName && candidate.ReservedWord == node.ReservedWord);
        var created = await selectedNode.SingleAsync(cancellationToken);
        Assert.Equal("Smith", created.LastName);
        Assert.Equal(7, created.Rank);
        Assert.Equal("reserved", created.ReservedWord);
        Assert.Equal("initial", created.Note);
        Assert.Equal("98101", created.Details.PostalCode);

        await selectedNode.UpdateAsync(
            setters => setters
                .SetProperty(candidate => candidate.Note, "updated`value")
                .SetProperty(candidate => candidate.Rank, 9),
            cancellationToken);

        var updated = await selectedNode.SingleAsync(cancellationToken);
        Assert.Equal("updated`value", updated.Note);
        Assert.Equal(9, updated.Rank);

        var sentinelStillPresent = await Graph.Nodes<Person>()
            .Where(p => p.TestKey == sentinel.TestKey)
            .CountAsync(cancellationToken);
        Assert.Equal(1, sentinelStillPresent);
    }

    /// <summary>
    /// Hostile custom property labels are queryable through a predicate, a scalar projection, and
    /// an ordering, each of which renders the label as an escaped identifier.
    /// </summary>
    [Fact]
    public async Task HostilePropertyLabels_QueryablePredicateProjectionOrdering()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var people = new[]
        {
            new NodeWithHostilePropertyLabels
            {
                LastName = "Anderson", Rank = 3, ReservedWord = "include", Note = "a",
                Details = new HostilePropertyDetails { PostalCode = "98101" },
            },
            new NodeWithHostilePropertyLabels
            {
                LastName = "Anderson", Rank = 1, ReservedWord = "include", Note = "b",
                Details = new HostilePropertyDetails { PostalCode = "98102" },
            },
            new NodeWithHostilePropertyLabels
            {
                LastName = "Baker", Rank = 2, ReservedWord = "exclude", Note = "c",
                Details = new HostilePropertyDetails { PostalCode = "98103" },
            }
        };

        foreach (var person in people)
        {
            await Graph.CreateNodeAsync(person, null, cancellationToken);
        }

        // Predicate on the space-labeled property, ordering and projection on the punctuation-labeled one.
        var ranks = await Graph.Nodes<NodeWithHostilePropertyLabels>()
            .Where(n => n.LastName == "Anderson")
            .Where(n => n.ReservedWord == "include" && n.Note != "missing")
            .OrderBy(n => n.Rank)
            .Select(n => n.Rank)
            .ToListAsync(cancellationToken);

        Assert.Equal([1, 3], ranks);

        var postalCodes = await Graph.Nodes<NodeWithHostilePropertyLabels>()
            .Where(n => n.Details.PostalCode == "98102")
            .Select(n => n.Details.PostalCode)
            .ToListAsync(cancellationToken);

        Assert.Equal(["98102"], postalCodes);
    }

    /// <summary>
    /// Hostile custom labels use the same safe rendering on relationship CRUD and query paths.
    /// </summary>
    [Fact]
    public async Task HostileRelationshipPropertyLabels_RoundTripAndQuery()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var start = new Person { FirstName = "Relationship", LastName = "Start" };
        var end = new Person { FirstName = "Relationship", LastName = "End" };
        await Graph.CreateNodeAsync(start, null, cancellationToken);
        await Graph.CreateNodeAsync(end, null, cancellationToken);

        var first = new RelationshipWithHostilePropertyLabels
        {
            Rank = 2,
            ReservedWord = "include",
            Note = "first",
        };
        var second = new RelationshipWithHostilePropertyLabels
        {
            Rank = 1,
            ReservedWord = "include",
            Note = "second",
        };
        await Graph.ConnectAsync(start, first, end, cancellationToken: cancellationToken);
        await Graph.ConnectAsync(start, second, end, cancellationToken: cancellationToken);

        var selectedRelationship = Graph.Relationships<RelationshipWithHostilePropertyLabels>()
            .Where(relationship => relationship.Note == "first");
        await selectedRelationship.UpdateAsync(
            setters => setters.SetProperty(relationship => relationship.Note, "updated"),
            cancellationToken);
        var updated = await Graph.Relationships<RelationshipWithHostilePropertyLabels>()
            .Where(relationship => relationship.Note == "updated")
            .SingleAsync(cancellationToken);
        Assert.Equal("updated", updated.Note);

        var ranks = await Graph.Relationships<RelationshipWithHostilePropertyLabels>()
            .Where(relationship => relationship.ReservedWord == "include" && relationship.Note != "missing")
            .OrderBy(relationship => relationship.Rank)
            .Select(relationship => relationship.Rank)
            .ToListAsync(cancellationToken);

        Assert.Equal([1, 2], ranks);
    }
}
