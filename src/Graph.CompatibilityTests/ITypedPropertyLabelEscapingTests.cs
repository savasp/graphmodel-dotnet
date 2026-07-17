// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

/// <summary>
/// Contract coverage for #367: a typed property may use an arbitrary storage name through
/// <c>[Property(Label = ...)]</c>, and that name ends up in Cypher identifier (property-access)
/// position on the read path. A label containing a space, punctuation, or an embedded backtick
/// must round-trip through create/get/update and be queryable through predicates, projections, and
/// ordering, rendered as one escaped identifier - it must never break out of property position or
/// alter any other data. Unlike the dynamic-identifier contract in
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

        // Punctuation (a hyphen) that is likewise not a plain symbolic name, exercised through
        // projection and ordering. (Labels that embed Cypher keywords such as "order by" or
        // parentheses additionally trip AGE's regex-based projection post-processing; that is a
        // separate provider limitation tracked outside this escaping contract.)
        [Property(Label = "annual-bonus")]
        public int Rank { get; set; }

        // The escape character itself plus a Cypher-looking break-out payload: escaping must double
        // the embedded backtick so the whole thing stays a single inert identifier through
        // create/update/get.
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

        var node = new NodeWithHostilePropertyLabels { LastName = "Smith", Rank = 7, Note = "initial" };
        await Graph.CreateNodeAsync(node, null, cancellationToken);

        var created = await Graph.GetNodeAsync<NodeWithHostilePropertyLabels>(node.Id, null, cancellationToken);
        Assert.Equal("Smith", created.LastName);
        Assert.Equal(7, created.Rank);
        Assert.Equal("initial", created.Note);

        node.Note = "updated`value";
        node.Rank = 9;
        await Graph.UpdateNodeAsync(node, null, cancellationToken);

        var updated = await Graph.GetNodeAsync<NodeWithHostilePropertyLabels>(node.Id, null, cancellationToken);
        Assert.Equal("updated`value", updated.Note);
        Assert.Equal(9, updated.Rank);

        var sentinelStillPresent = await Graph.Nodes<Person>()
            .Where(p => p.Id == sentinel.Id)
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
            new NodeWithHostilePropertyLabels { LastName = "Anderson", Rank = 3, Note = "a" },
            new NodeWithHostilePropertyLabels { LastName = "Anderson", Rank = 1, Note = "b" },
            new NodeWithHostilePropertyLabels { LastName = "Baker", Rank = 2, Note = "c" }
        };

        foreach (var person in people)
        {
            await Graph.CreateNodeAsync(person, null, cancellationToken);
        }

        // Predicate on the space-labeled property, ordering and projection on the punctuation-labeled one.
        var ranks = await Graph.Nodes<NodeWithHostilePropertyLabels>()
            .Where(n => n.LastName == "Anderson")
            .OrderBy(n => n.Rank)
            .Select(n => n.Rank)
            .ToListAsync(cancellationToken);

        Assert.Equal([1, 3], ranks);
    }
}
