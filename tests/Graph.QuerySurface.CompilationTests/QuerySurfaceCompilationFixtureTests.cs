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

namespace Cvoya.Graph.QuerySurface.CompilationTests;

/// <summary>
/// Compilation-fixture tests (issue #94 testing requirement 2): chains that MUST compile against
/// the new query surface - including the #93 §B4 regression
/// (<c>.Where(...).OrderBy(...).Traverse&lt;...&gt;()</c> keeping the graph-typed chain, which
/// broke under the old per-kind interface hierarchy because <c>OrderBy</c> returned
/// <c>IOrderedGraphQueryable&lt;TSource&gt;</c>, not an <c>IGraphNodeQueryable&lt;TSource&gt;</c>)
/// and mixed composability chains (traversal -&gt; where -&gt; order -&gt; take -&gt; traversal
/// -&gt; projection, per the 2026-07-03 addendum) - and misuse that MUST NOT compile (graph
/// traversal on a non-node source; the free-floating <c>WithDepth</c>/<c>Direction</c> modifiers,
/// which are <c>[Obsolete]</c>, becoming hard errors under <c>WarningsAsErrors</c> - matching the
/// repository's own build setting).
/// </summary>
public class QuerySurfaceCompilationFixtureTests
{
    private const string DomainModel = """
        using System;
        using System.Collections.Generic;
        using System.Threading.Tasks;
        using Cvoya.Graph;

        [Node("Person")]
        public record Person : Node
        {
            public string FirstName { get; set; } = string.Empty;
            public string LastName { get; set; } = string.Empty;
            public int Age { get; set; }
        }

        [Node("Company")]
        public record Company : Node
        {
            public string Name { get; set; } = string.Empty;
        }

        [Relationship("KNOWS")]
        public record Knows(string StartNodeId, string EndNodeId) : Relationship(StartNodeId, EndNodeId)
        {
            public int Since { get; set; }
        }
        """;

    private static string WithDomainModel(string body) => DomainModel + Environment.NewLine + body;

    // ---- Must compile ----

    [Fact]
    public void WhereThenOrderByThenTraverse_Compiles()
    {
        // The #93 §B4 regression: OrderBy must keep the chain graph-typed so Traverse remains
        // callable afterward, without needing to reshuffle the query or re-cast.
        var source = WithDomainModel("""
            public static class Queries
            {
                public static IGraphQueryable<Person> Run(IGraphQueryable<Person> people) =>
                    people
                        .Where(p => p.Age > 18)
                        .OrderBy(p => p.LastName)
                        .Traverse<Knows, Person>();
            }
            """);

        var result = CompilationFixture.Compile(source);

        Assert.False(result.HasErrors, result.DescribeErrors());
    }

    [Fact]
    public void DistinctThenSkipThenTakeThenTraverse_Compiles()
    {
        // Distinct/Skip/Take must also preserve the graph-typed chain, for the same reason as
        // OrderBy above (they used to shadow to IGraphQueryable<T> too, but the old node-only
        // shadow layer (GraphNodeQueryableExtensions) only re-declared Where/Select).
        var source = WithDomainModel("""
            public static class Queries
            {
                public static IGraphQueryable<Person> Run(IGraphQueryable<Person> people) =>
                    people
                        .Distinct()
                        .Skip(1)
                        .Take(10)
                        .Traverse<Knows, Person>();
            }
            """);

        var result = CompilationFixture.Compile(source);

        Assert.False(result.HasErrors, result.DescribeErrors());
    }

    [Fact]
    public void MixedCompositabilityChain_TraverseWhereOrderTakeTraverseProjection_Compiles()
    {
        // The 2026-07-03 addendum's composability requirement: traversal -> where -> order ->
        // take -> traversal -> projection, not just single-operator chains.
        var source = WithDomainModel("""
            public static class Queries
            {
                public static IGraphQueryable<string> Run(IGraphQueryable<Person> people) =>
                    people
                        .Traverse<Knows, Person>()
                        .Where(p => p.Age > 21)
                        .OrderBy(p => p.LastName)
                        .Take(5)
                        .Traverse<Knows, Company>()
                        .Select(c => c.Name);
            }
            """);

        var result = CompilationFixture.Compile(source);

        Assert.False(result.HasErrors, result.DescribeErrors());
    }

    [Fact]
    public void TwoArgTraverse_OnCovariantNodeQueryableReceiver_Compiles()
    {
        // Issue #94 "Option C": Traverse<TRel, TEnd> is declared on IGraphQueryable<INode> - any
        // IGraphQueryable<Person> (Person : INode) must convert by covariance without an explicit
        // cast, and TStart must not need to be spelled out.
        var source = WithDomainModel("""
            public static class Queries
            {
                public static IGraphQueryable<Person> Run(IGraphQueryable<Person> people) =>
                    people.Traverse<Knows, Person>();
            }
            """);

        var result = CompilationFixture.Compile(source);

        Assert.False(result.HasErrors, result.DescribeErrors());
    }

    [Fact]
    public void ObsoleteThreeArgTraverse_StillCompiles_ButWarns()
    {
        // The three-arg shim must remain callable for one release (generic arity disambiguates it
        // from the two-arg form) - functional, but obsolete (CS0618), not a hard error, unless the
        // consuming project opts into WarningsAsErrors (see the WithDepth/Direction fixtures below
        // for that case applied to the free-floating modifiers).
        var source = WithDomainModel("""
            public static class Queries
            {
            #pragma warning disable CS0618
                public static IGraphQueryable<Person> Run(IGraphQueryable<Person> people) =>
                    people.Traverse<Person, Knows, Person>();
            #pragma warning restore CS0618
            }
            """);

        var result = CompilationFixture.Compile(source);

        Assert.False(result.HasErrors, result.DescribeErrors());
    }

    [Fact]
    public void PathSegments_KeepsThreeTypeArguments_Compiles()
    {
        // The stated principle (issue #94 "Option C"): PathSegments' result type
        // (IGraphPathSegment<TStart,TRel,TEnd>) names the start type, so it keeps all three type
        // arguments even though Traverse/TraverseRelationships/TraversePaths dropped to two.
        var source = WithDomainModel("""
            public static class Queries
            {
                public static IGraphQueryable<IGraphPathSegment<Person, Knows, Person>> Run(IGraphQueryable<Person> people) =>
                    people.PathSegments<Person, Knows, Person>();
            }
            """);

        var result = CompilationFixture.Compile(source);

        Assert.False(result.HasErrors, result.DescribeErrors());
    }

    [Fact]
    public void PathSegmentsThenWhereOnEndNode_Compiles()
    {
        var source = WithDomainModel("""
            public static class Queries
            {
                public static IGraphQueryable<IGraphPathSegment<Person, Knows, Person>> Run(IGraphQueryable<Person> people) =>
                    people
                        .PathSegments<Person, Knows, Person>()
                        .Where(ps => ps.EndNode.Age > 21);
            }
            """);

        var result = CompilationFixture.Compile(source);

        Assert.False(result.HasErrors, result.DescribeErrors());
    }

    [Fact]
    public void TraversePathsThenToListAsync_Compiles()
    {
        var source = WithDomainModel("""
            public static class Queries
            {
                public static Task<List<IGraphPath>> Run(IGraphQueryable<Person> people) =>
                    people.TraversePaths<Knows, Person>(1, 3).ToListAsync();
            }
            """);

        var result = CompilationFixture.Compile(source);

        Assert.False(result.HasErrors, result.DescribeErrors());
    }

    [Fact]
    public void AwaitForeachOverGraphQueryable_Compiles()
    {
        var source = WithDomainModel("""
            public static class Queries
            {
                public static async Task Run(IGraphQueryable<Person> people)
                {
                    await foreach (var person in people.Where(p => p.Age > 18))
                    {
                        _ = person.FirstName;
                    }
                }
            }
            """);

        var result = CompilationFixture.Compile(source);

        Assert.False(result.HasErrors, result.DescribeErrors());
    }

    [Fact]
    public void SyncGraphRootsDoNotRequireAwait_Compiles()
    {
        // Sync roots (issue #94 scope item 4): IGraph.Nodes<N>/Relationships<R> must not return
        // Task<...> - building the queryable must not require an await.
        var source = WithDomainModel("""
            public static class Queries
            {
                public static IGraphQueryable<Person> Run(IGraph graph) => graph.Nodes<Person>();
            }
            """);

        var result = CompilationFixture.Compile(source);

        Assert.False(result.HasErrors, result.DescribeErrors());
    }

    // ---- Must NOT compile ----

    [Fact]
    public void TraverseOnNonNodeSource_DoesNotCompile()
    {
        // Traverse<TRel, TEnd> is declared on IGraphQueryable<INode> (issue #94 "Option C") -
        // IGraphQueryable<Knows> does not convert to IGraphQueryable<INode> by covariance (Knows
        // is an IRelationship, not an INode), so calling Traverse on a relationship source must
        // fail to compile, not silently degrade.
        var source = WithDomainModel("""
            public static class Queries
            {
                public static IGraphQueryable<Person> Run(IGraphQueryable<Knows> knows) =>
                    knows.Traverse<Knows, Person>();
            }
            """);

        var result = CompilationFixture.Compile(source);

        Assert.True(result.HasErrors, "Expected Traverse<TRel, TEnd> to fail to compile when the receiver's element type is not an INode.");
    }

    [Fact]
    public void ObsoleteThreeArgTraverseOnNonNodeSource_DoesNotCompile()
    {
        // Same "must not compile" case as above, but through the obsolete three-arg shim: its
        // 'where TStartNode : class, INode' constraint must still reject a relationship source.
        var source = WithDomainModel("""
            public static class Queries
            {
            #pragma warning disable CS0618
                public static IGraphQueryable<Person> Run(IGraphQueryable<Knows> knows) =>
                    knows.Traverse<Knows, Knows, Person>();
            #pragma warning restore CS0618
            }
            """);

        var result = CompilationFixture.Compile(source);

        Assert.True(result.HasErrors, "Expected the obsolete three-arg Traverse<TStartNode,...> to fail to compile when TStartNode does not satisfy 'where TStartNode : class, INode'.");
    }

    [Fact]
    public void PathSegmentsOnNonNodeSource_ControlCase_Compiles()
    {
        // Control alongside the genuine non-node-source failure above: Company : INode, so this
        // legitimately compiles - proving the constraint is actually exercised (i.e. it isn't that
        // everything with this shape fails to compile for unrelated reasons).
        var source = WithDomainModel("""
            public static class Queries
            {
                public static IGraphQueryable<IGraphPathSegment<Company, Knows, Person>> Run(IGraphQueryable<Company> companies) =>
                    companies.PathSegments<Company, Knows, Person>();
            }
            """);

        var result = CompilationFixture.Compile(source);

        Assert.False(result.HasErrors, result.DescribeErrors());
    }

    [Fact]
    public void StructImplementingINode_AtQueryRoot_DoesNotCompile()
    {
        // The reference-type constraint sweep (issue #94, @savasp directive): every 'where T :
        // INode'/'where T : IRelationship' constraint across the query surface gained 'class'. A
        // struct implementing INode must therefore fail to compile at a generic entity type
        // parameter such as IGraph.Nodes<N>() - this is also the documented struct-entity caveat
        // for the two-arg traversal operators (a struct can never satisfy the covariant receiver
        // conversion to IGraphQueryable<INode> either, since variance requires a reference type).
        var source = WithDomainModel("""
            public struct StructNode : INode
            {
                public string Id { get; set; }
                public IReadOnlyList<string> Labels { get; set; }
            }

            public static class Queries
            {
                public static IGraphQueryable<StructNode> Run(IGraph graph) => graph.Nodes<StructNode>();
            }
            """);

        var result = CompilationFixture.Compile(source);

        Assert.True(result.HasErrors, "Expected a struct implementing INode to fail to compile at a 'where T : class, INode' generic entity type parameter such as IGraph.Nodes<N>().");
    }

    [Fact]
    public void FreeFloatingWithDepth_UnderWarningsAsErrors_DoesNotCompile()
    {
        // WithDepth/Direction as free-floating postfix modifiers are [Obsolete] (CS0618, a
        // warning by default). Under WarningsAsErrors (the repository's own build setting), that
        // warning becomes a hard compile failure - which is the point: new code should not be
        // able to silently keep using the deprecated free-floating form.
        var source = WithDomainModel("""
            public static class Queries
            {
                public static IGraphQueryable<Person> Run(IGraphQueryable<Person> people) =>
                    people.WithDepth(3);
            }
            """);

        var result = CompilationFixture.Compile(source, warningsAsErrors: true);

        Assert.True(result.HasErrors, "Expected the obsolete free-floating WithDepth to fail to compile under WarningsAsErrors.");
        Assert.Contains(result.Errors, e => e.Id == "CS0618");
    }

    [Fact]
    public void FreeFloatingDirection_UnderWarningsAsErrors_DoesNotCompile()
    {
        var source = WithDomainModel("""
            public static class Queries
            {
                public static IGraphQueryable<Person> Run(IGraphQueryable<Person> people) =>
                    people.Direction(GraphTraversalDirection.Incoming);
            }
            """);

        var result = CompilationFixture.Compile(source, warningsAsErrors: true);

        Assert.True(result.HasErrors, "Expected the obsolete free-floating Direction to fail to compile under WarningsAsErrors.");
        Assert.Contains(result.Errors, e => e.Id == "CS0618");
    }

    [Fact]
    public void FreeFloatingWithDepth_WithoutWarningsAsErrors_StillCompilesButWarns()
    {
        // Control case: without WarningsAsErrors, the obsolete call is still just a warning (this
        // PR keeps it functional for one release), not an error - proving the previous test's
        // failure is specifically attributable to WarningsAsErrors, not something else about the
        // free-floating form.
        var source = WithDomainModel("""
            public static class Queries
            {
                public static IGraphQueryable<Person> Run(IGraphQueryable<Person> people) =>
                    people.WithDepth(3);
            }
            """);

        var result = CompilationFixture.Compile(source, warningsAsErrors: false);

        Assert.False(result.HasErrors, result.DescribeErrors());
    }

    [Fact]
    public void IGraphNodeQueryableUsage_UnderWarningsAsErrors_DoesNotCompile()
    {
        // IGraphNodeQueryable<T>/IGraphRelationshipQueryable<T> are [Obsolete] aliases; under
        // WarningsAsErrors, declaring a variable/parameter of that type must fail to compile
        // unless explicitly suppressed.
        var suppressedSource = WithDomainModel("""
            public static class Queries
            {
            #pragma warning disable CS0618
                public static IGraphNodeQueryable<Person> Run(IGraphNodeQueryable<Person> people) => people;
            #pragma warning restore CS0618
            }
            """);

        // Control: with the pragma suppressing CS0618 locally, this compiles even under
        // WarningsAsErrors - proving the next assertion's failure is attributable to the obsolete
        // usage itself once the suppression is removed.
        var suppressedResult = CompilationFixture.Compile(suppressedSource, warningsAsErrors: true);
        Assert.False(suppressedResult.HasErrors, suppressedResult.DescribeErrors());

        var unsuppressedSource = WithDomainModel("""
            public static class Queries
            {
                public static IGraphNodeQueryable<Person> Run(IGraphNodeQueryable<Person> people) => people;
            }
            """);

        var result = CompilationFixture.Compile(unsuppressedSource, warningsAsErrors: true);

        Assert.True(result.HasErrors, "Expected IGraphNodeQueryable<T> usage to fail to compile under WarningsAsErrors.");
        Assert.Contains(result.Errors, e => e.Id == "CS0618");
    }
}
