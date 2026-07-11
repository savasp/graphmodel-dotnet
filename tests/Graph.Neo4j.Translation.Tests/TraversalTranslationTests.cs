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

    // ---- Operators composed after TraversePaths stay at one row per captured path until a
    // path-valued result is decomposed for materialization. The shared builder's whitelist is the
    // choke point: the supported shapes below translate over p, while OrderBy remains the negative
    // control proving an operator without a path lowering still throws instead of binding to a hop.

    [Fact]
    public Task WhereAfterTraversePaths_FiltersByEndPropertyAndDepth()
    {
        var query = Root.Nodes<Person>()
            .TraversePaths<Knows, Person>(1, 3)
            .Where(p => p.Start.Id != "" && ((Person)p.End).Age > 21 && p.Segments.Count > 1);
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

    /// <summary>
    /// The query model applies path predicates before projection and pagination, so a Where that
    /// follows Select/Take/Skip would silently filter a different row set than LINQ semantics
    /// require. The builder rejects the composition instead of mistranslating (before this guard,
    /// this query lowered the node predicate against the path variable itself - WHERE p.Age - and
    /// silently returned wrong results).
    /// </summary>
    [Fact]
    public Task SelectThenWhereAfterTraversePaths_ThrowsOrderingGuard()
    {
        var query = Root.Nodes<Person>()
            .TraversePaths<Knows, Person>(1, 3)
            .Select(p => p.End)
            .Where(n => ((Person)n).Age > 21);
        return VerifyTranslationThrows(query);
    }

    /// <summary>
    /// Select(p => p) is a projection no-op: the translation must be identical to the bare
    /// TraversePaths decomposition, not a bare RETURN p that no materializer consumes.
    /// </summary>
    [Fact]
    public void SelectIdentityAfterTraversePaths_TranslatesLikeBarePaths()
    {
        var bare = Root.Nodes<Person>().TraversePaths<Knows, Person>(1, 3);
        var identity = Root.Nodes<Person>().TraversePaths<Knows, Person>(1, 3).Select(p => p);

        Assert.Equal(CypherTranslator.Translate(bare), CypherTranslator.Translate(identity));
    }

    /// <summary>
    /// Take(2).Skip(1) in LINQ bounds the window to two paths and then consumes one of them:
    /// SKIP 1 LIMIT 1, not SKIP 1 LIMIT 2.
    /// </summary>
    [Fact]
    public Task TakeThenSkipAfterTraversePaths_FoldsPagingWindow()
    {
        var query = Root.Nodes<Person>()
            .TraversePaths<Knows, Person>(1, 3)
            .Take(2)
            .Skip(1);
        return VerifyTranslation(query);
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

    [Fact]
    public Task CountAfterTraversePaths_CountsPaths()
    {
        var source = Root.Nodes<Person>().TraversePaths<Knows, Person>(1, 3);
        var expression = MarkerExpressions.Call<IGraphPath>("CountAsyncMarker", source.Expression);
        return VerifyTranslation(typeof(Person), expression);
    }

    [Fact]
    public Task TakeThenCountAfterTraversePaths_PaginatesPathsBeforeAggregate()
    {
        var source = Root.Nodes<Person>()
            .TraversePaths<Knows, Person>(1, 3)
            .Take(5);
        var expression = MarkerExpressions.Call<IGraphPath>("CountAsyncMarker", source.Expression);
        return VerifyTranslation(typeof(Person), expression);
    }

    [Fact]
    public Task AnyAfterTraversePaths_ChecksPathExistence()
    {
        var source = Root.Nodes<Person>().TraversePaths<Knows, Person>(1, 3);
        var expression = MarkerExpressions.Call<IGraphPath>("AnyAsyncMarker", source.Expression);
        return VerifyTranslation(typeof(Person), expression);
    }

    /// <summary>
    /// At exactly one hop the pattern label-constrains both hop columns, so complex-property
    /// loading follows each endpoint's static type: Person loads, Company (no complex
    /// properties) must not pay for the loading subquery.
    /// </summary>
    [Fact]
    public Task TraversePathsSingleHop_LoadsComplexPropertiesPerEndpointType()
    {
        var query = Root.Nodes<Person>().TraversePaths<WorksAt, Company>(1, 1);
        return VerifyTranslation(query);
    }

    /// <summary>
    /// With more than one hop possible, intermediate nodes are label-unconstrained, so both hop
    /// columns must load complex properties even when the declared endpoint types have none -
    /// an intermediate of any type may appear in either column.
    /// </summary>
    [Fact]
    public Task TraversePathsMultiHop_LoadsComplexPropertiesForIntermediates()
    {
        var query = Root.Nodes<Person>().TraversePaths<WorksAt, Company>(1, 2);
        return VerifyTranslation(query);
    }

    /// <summary>
    /// Select(p => p.End) resolves the entity type from the path shape's endpoint (Company, no
    /// complex properties), not the declared INode member type, which would conservatively force
    /// the complex-property loading pipeline onto every projection.
    /// </summary>
    [Fact]
    public Task SelectEndAfterTraversePaths_SkipsComplexLoadingForSimpleEndpoint()
    {
        var query = Root.Nodes<Person>()
            .TraversePaths<WorksAt, Company>(1, 3)
            .Select(p => p.End);
        return VerifyTranslation(query);
    }

    /// <summary>
    /// Control case: a bare <c>TraversePaths</c> query still translates and decomposes only after
    /// all path-level composition is complete.
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
