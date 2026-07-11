// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Translation.Tests;

using Cvoya.Graph.Neo4j.Translation.Tests.Harness;
using Cvoya.Graph.Neo4j.Translation.Tests.Model;

public class TraversalTranslationTests : TranslationTestBase
{
    [Fact]
    public Task PathSegments_Basic()
    {
        var query = Root.Nodes<Person>().PathSegments<Person, Knows, Person>();
        return VerifyTranslation(query);
    }

    [Fact]
    public Task Traverse_NoDepthOrDirection()
    {
        // Two-arg surface (issue #94, "Option C"): TStart is recovered from the source expression
        // chain's element type at translation time, not spelled as a type argument. The generated
        // Cypher must be identical to the pre-reshape three-arg call (see
        // Traverse_NoDepthOrDirection_ObsoleteThreeArgShim_ProducesSameCypher below) - the reshape
        // only changes the C# call-site type-argument count, not the translated query.
        var query = Root.Nodes<Person>().Traverse<Knows, Person>();
        return VerifyTranslation(query);
    }

    [Fact]
    public Task Traverse_WidenedToINodeBeforeWhere_UsesINodeStartLabels()
    {
        var query = ((IGraphQueryable<INode>)Root.Nodes<Person>())
            .Where(n => n.Id != "")
            .Traverse<Knows, Person>();

        return VerifyTranslation(query);
    }

    [Fact]
    public Task Traverse_WithMaxDepth()
    {
        var query = Root.Nodes<Person>().Traverse<Knows, Person>(3);
        return VerifyTranslation(query);
    }

    [Fact]
    public Task Traverse_WithMinAndMaxDepth()
    {
        var query = Root.Nodes<Person>().Traverse<Knows, Person>(1, 3);
        return VerifyTranslation(query);
    }

    [Fact]
    public Task Traverse_WithDirection()
    {
        var query = Root.Nodes<Person>().Traverse<Knows, Person>(GraphTraversalDirection.Incoming);
        return VerifyTranslation(query);
    }

    [Fact]
    public Task Traverse_ToDifferentNodeType()
    {
        var query = Root.Nodes<Person>().Traverse<WorksAt, Company>();
        return VerifyTranslation(query);
    }

    /// <summary>
    /// NOTE (characterization): <c>ReverseTraverse&lt;TRel, TEnd&gt;</c> is a client-side extension
    /// method that eagerly composes
    /// <c>PathSegments().Direction(Incoming).Select(ps => ps.EndNode)</c> and calls
    /// <c>source.Provider.CreateQuery&lt;T&gt;</c> immediately rather than deferring - so the
    /// literal method name "ReverseTraverse" never appears in an expression tree passed to
    /// <c>CypherQueryVisitor</c>. The "ReverseTraverse" case in
    /// <c>CypherQueryVisitor.HandleLinqMethod</c> is therefore dead code; this test snapshots the
    /// resulting Cypher, which is identical in shape to <c>PathSegments().Direction(Incoming)</c>.
    /// </summary>
    [Fact]
    public Task ReverseTraverse_ProducesPathSegmentsDirectionSelectShape()
    {
        var query = Root.Nodes<Person>().ReverseTraverse<Knows, Person>();
        return VerifyTranslation(query);
    }

    [Fact]
    public Task TraverseRelationships_ReturnsRelationshipsNotNodes()
    {
        var query = Root.Nodes<Person>().TraverseRelationships<Knows, Person>();
        return VerifyTranslation(query);
    }

#pragma warning disable CS0618 // These tests deliberately snapshot the obsolete free-floating traversal modifiers.
    [Fact]
    public Task Direction_Outgoing_OnPathSegments()
    {
        var query = Root.Nodes<Person>()
            .PathSegments<Person, Knows, Person>()
            .Direction(GraphTraversalDirection.Outgoing);
        return VerifyTranslation(query);
    }

    [Fact]
    public Task Direction_Both_OnPathSegments()
    {
        var query = Root.Nodes<Person>()
            .PathSegments<Person, Knows, Person>()
            .Direction(GraphTraversalDirection.Both);
        return VerifyTranslation(query);
    }

    [Fact]
    public Task WithDepth_MaxOnly_OnPathSegments()
    {
        var query = Root.Nodes<Person>()
            .PathSegments<Person, Knows, Person>()
            .WithDepth(4);
        return VerifyTranslation(query);
    }

    [Fact]
    public Task WithDepth_MinAndMax_OnPathSegments()
    {
        var query = Root.Nodes<Person>()
            .PathSegments<Person, Knows, Person>()
            .WithDepth(2, 4);
        return VerifyTranslation(query);
    }
#pragma warning restore CS0618

    [Fact]
    public Task PathSegments_WithWhereOnEndNode()
    {
        var query = Root.Nodes<Person>()
            .PathSegments<Person, Knows, Person>()
            .Where(ps => ps.EndNode.Age > 21);
        return VerifyTranslation(query);
    }

    [Fact]
    public Task TraversePaths_MinAndMaxDepth()
    {
        // Two-arg surface: TStart recovered from the source chain (HandleTraversePaths resolves it
        // via TypeHelpers.GetElementType(node.Arguments[0].Type) rather than a third generic arg).
        var query = Root.Nodes<Person>().TraversePaths<Knows, Person>(1, 3);
        return VerifyTranslation(query);
    }

    // ---- The known gap, generalized (issue #94 testing requirement 5 / the coordinator's
    // follow-up): TraversePaths builds its own per-hop RETURN shape rather than a single row per
    // IGraphPath, so no operator this visitor doesn't already know how to keep supporting
    // (ToListOrArray, Direction, WithDepth - see CypherQueryVisitor.SupportedAfterTraversePaths)
    // has a well-defined row/alias to translate against. A single choke point
    // (CypherQueryVisitor.ThrowIfUnsupportedAfterTraversePaths, called from HandleLinqMethod right
    // after the source is visited) rejects EVERY other operator chained after TraversePaths with a
    // NotSupportedException naming the operator - not just Where - so a new operator added later
    // can't silently regress into mistranslating against the wrong row shape (the #100
    // throw-over-silent-wrong precedent). Callers should materialize the paths first and continue
    // client-side (see the contract-suite equivalent,
    // IQueryTraversalTests.CanFilterTargetNodesByPropertyWithoutTraverseTwoHops).

    [Fact]
    public Task WhereAfterTraversePaths_FiltersByEndPropertyAndDepth()
    {
        var query = Root.Nodes<Person>()
            .TraversePaths<Knows, Person>(1, 3)
            .Where(p => ((Person)p.End).Age > 21 && p.Segments.Count > 1);
        return VerifyTranslation(query);
    }

    [Fact]
    public Task SelectScalarAfterTraversePaths_ProjectsDepth()
    {
        var query = Root.Nodes<Person>()
            .TraversePaths<Knows, Person>(1, 3)
            .Select(p => p.Segments.Count);
        return VerifyTranslation(query);
    }

    [Fact]
    public Task SelectStartAfterTraversePaths_ProjectsNode()
    {
        var query = Root.Nodes<Person>()
            .TraversePaths<Knows, Person>(1, 3)
            .Select(p => p.Start);
        return VerifyTranslation(query);
    }

    [Fact]
    public Task SelectEndAfterTraversePaths_ProjectsNode()
    {
        var query = Root.Nodes<Person>()
            .TraversePaths<Knows, Person>(1, 3)
            .Select(p => p.End);
        return VerifyTranslation(query);
    }

    [Fact]
    public Task OrderByAfterTraversePaths_ThrowsNotSupported()
    {
        var query = Root.Nodes<Person>()
            .TraversePaths<Knows, Person>(1, 3)
            .OrderBy(p => p.Segments.Count);
        return VerifyTranslationThrows(query);
    }

    [Fact]
    public Task TakeAfterTraversePaths_LimitsPathsBeforeDecomposition()
    {
        var query = Root.Nodes<Person>()
            .TraversePaths<Knows, Person>(1, 3)
            .Take(5);
        return VerifyTranslation(query);
    }

    [Fact]
    public Task SkipAfterTraversePaths_SkipsPathsBeforeDecomposition()
    {
        var query = Root.Nodes<Person>()
            .TraversePaths<Knows, Person>(1, 3)
            .Skip(2);
        return VerifyTranslation(query);
    }

    /// <summary>
    /// Control case alongside the four "must throw" tests above: the bare <c>TraversePaths</c>
    /// query (no operator chained after it) must still translate successfully - proving the choke
    /// point (<see cref="CypherQueryVisitor.ThrowIfUnsupportedAfterTraversePaths"/>) only rejects
    /// operators chained <em>after</em> <c>TraversePaths</c>, not <c>TraversePaths</c> itself. This
    /// is exactly <see cref="TraversePaths_MinAndMaxDepth"/> above, referenced here (rather than
    /// duplicated) so the two snapshot files stay a single source of truth.
    /// </summary>
    [Fact]
    public Task BareTraversePaths_StillTranslatesSuccessfully_SeeTraversePathsMinAndMaxDepth() =>
        TraversePaths_MinAndMaxDepth();

    [Fact]
#pragma warning disable CS0618 // Direction is the obsolete-but-still-functional free-floating modifier; exercising it directly is the point of this test.
    public Task DirectionAfterTraversePaths_StillTranslatesSuccessfully()
    {
        // Direction/WithDepth are the sanctioned wrappers the TraversePaths(configure) options-
        // lambda overload itself builds (see GraphTraversalExtensions.TraversePaths); they mutate
        // builder state describing the traversal that produces the paths, not the shape of a
        // result row, so the choke point allows them through unlike Where/Select/OrderBy/Take
        // above. This is the translated equivalent of TraversePaths_OptionsLambda_AppliesDepthAndDirection's
        // shape (see ExpressionShapeTests), snapshotted here for the actual generated Cypher.
        var query = Root.Nodes<Person>()
            .TraversePaths<Knows, Person>(1, 3)
            .Direction(GraphTraversalDirection.Incoming);
        return VerifyTranslation(query);
    }
#pragma warning restore CS0618

    // ---- Obsolete three-arg shims: prove the generated Cypher is unchanged by the reshape ----

    [Fact]
#pragma warning disable CS0618 // exercising the obsolete three-arg shim directly is the point of this test.
    public Task Traverse_NoDepthOrDirection_ObsoleteThreeArgShim_ProducesSameCypher()
    {
        var query = Root.Nodes<Person>().Traverse<Person, Knows, Person>();
        return VerifyTranslation(query);
    }

    [Fact]
    public Task TraversePaths_MinAndMaxDepth_ObsoleteThreeArgShim_ProducesSameCypher()
    {
        var query = Root.Nodes<Person>().TraversePaths<Person, Knows, Person>(1, 3);
        return VerifyTranslation(query);
    }
#pragma warning restore CS0618
}
