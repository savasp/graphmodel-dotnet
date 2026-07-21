// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.QuerySurface.CompilationTests;

/// <summary>
/// Compilation-fixture tests (issue #94 testing requirement 2): chains that MUST compile against
/// the new query surface - including the #93 §B4 regression
/// (<c>.Where(...).OrderBy(...).Traverse&lt;...&gt;()</c> keeping the graph-typed chain, which
/// broke under the old per-kind interface hierarchy because <c>OrderBy</c> returned
/// <c>IOrderedGraphQueryable&lt;TSource&gt;</c>, not an <c>IGraphNodeQueryable&lt;TSource&gt;</c>)
/// and mixed composability chains (traversal -&gt; where -&gt; order -&gt; take -&gt; traversal
/// -&gt; projection, per the 2026-07-03 addendum) - and misuse that MUST NOT compile (graph
/// traversal on a non-node source, removed three-type-argument traversal overloads, removed
/// free-floating traversal modifiers, and removed per-kind queryable aliases).
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
            public long Salary { get; set; }
            public float Weight { get; set; }
            public double Height { get; set; }
            public decimal NetWorth { get; set; }
            public int? Score { get; set; }
            public long? NullableSalary { get; set; }
            public float? NullableWeight { get; set; }
            public double? NullableHeight { get; set; }
            public decimal? NullableNetWorth { get; set; }
            public byte ByteValue { get; set; }
        }

        [Node("Company")]
        public record Company : Node
        {
            public string Name { get; set; } = string.Empty;
        }

        [Relationship("KNOWS")]
        public record Knows : Relationship
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
    public void RemovedThreeArgTraverse_DoesNotCompile()
    {
        var source = WithDomainModel("""
            public static class Queries
            {
                public static IGraphQueryable<Person> Run(IGraphQueryable<Person> people) =>
                    people.Traverse<Person, Knows, Person>();
            }
            """);

        var result = CompilationFixture.Compile(source);

        Assert.True(result.HasErrors, "Expected the removed three-argument Traverse overload to fail to compile.");
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

    [Fact]
    public void AverageAsync_SelectorOverloadsExposeLinqResultTypeMatrix_Compiles()
    {
        var source = WithDomainModel("""
            public static class Queries
            {
                public static void Run(IGraphQueryable<Person> people)
                {
                    Task<double> intAverage = people.AverageAsync(person => person.Age);
                    Task<double> longAverage = people.AverageAsync(person => person.Salary);
                    Task<float> floatAverage = people.AverageAsync(person => person.Weight);
                    Task<double> doubleAverage = people.AverageAsync(person => person.Height);
                    Task<decimal> decimalAverage = people.AverageAsync(person => person.NetWorth);
                    Task<double?> nullableIntAverage = people.AverageAsync(person => person.Score);
                    Task<double?> nullableLongAverage = people.AverageAsync(person => person.NullableSalary);
                    Task<float?> nullableFloatAverage = people.AverageAsync(person => person.NullableWeight);
                    Task<double?> nullableDoubleAverage = people.AverageAsync(person => person.NullableHeight);
                    Task<decimal?> nullableDecimalAverage = people.AverageAsync(person => person.NullableNetWorth);
                }
            }
            """);

        var result = CompilationFixture.Compile(source);

        Assert.False(result.HasErrors, result.DescribeErrors());
    }

    [Fact]
    public void AverageAsync_SourceOverloadsExposeLinqResultTypeMatrix_Compiles()
    {
        var source = WithDomainModel("""
            public static class Queries
            {
                public static void Run(
                    IGraphQueryable<int> ints,
                    IGraphQueryable<long> longs,
                    IGraphQueryable<float> floats,
                    IGraphQueryable<double> doubles,
                    IGraphQueryable<decimal> decimals,
                    IGraphQueryable<int?> nullableInts,
                    IGraphQueryable<long?> nullableLongs,
                    IGraphQueryable<float?> nullableFloats,
                    IGraphQueryable<double?> nullableDoubles,
                    IGraphQueryable<decimal?> nullableDecimals)
                {
                    Task<double> intAverage = ints.AverageAsync();
                    Task<double> longAverage = longs.AverageAsync();
                    Task<float> floatAverage = floats.AverageAsync();
                    Task<double> doubleAverage = doubles.AverageAsync();
                    Task<decimal> decimalAverage = decimals.AverageAsync();
                    Task<double?> nullableIntAverage = nullableInts.AverageAsync();
                    Task<double?> nullableLongAverage = nullableLongs.AverageAsync();
                    Task<float?> nullableFloatAverage = nullableFloats.AverageAsync();
                    Task<double?> nullableDoubleAverage = nullableDoubles.AverageAsync();
                    Task<decimal?> nullableDecimalAverage = nullableDecimals.AverageAsync();
                }
            }
            """);

        var result = CompilationFixture.Compile(source);

        Assert.False(result.HasErrors, result.DescribeErrors());
    }

    [Fact]
    public void RecreateManagedIndexesAsync_Compiles()
    {
        var source = WithDomainModel("""
            public static class SchemaMaintenance
            {
                public static Task Run(IGraph graph) => graph.RecreateManagedIndexesAsync();
            }
            """);

        var result = CompilationFixture.Compile(source);

        Assert.False(result.HasErrors, result.DescribeErrors());
    }

    [Fact]
    public void IdentityFreeCommandSurface_Compiles()
    {
        var source = WithDomainModel("""
            public static class Commands
            {
                public static void Run(IGraph graph, IGraphTransaction transaction, System.Threading.CancellationToken cancellationToken)
                {
                    var people = graph.Nodes<Person>().Where(person => person.FirstName == "Ada");
                    var companies = graph.Nodes<Company>().Where(company => company.Name == "CVOYA");
                    var relationships = graph.Relationships<Knows>().Where(relationship => relationship.Since < 2020);

                    Task<int> update = people.UpdateAsync(
                        setters => setters.SetProperty(person => person.Age, 42),
                        cancellationToken);
                    Task<int> deleteNodes = people.DeleteAsync(
                        cascadeDelete: true,
                        cancellationToken: cancellationToken);
                    Task<int> deleteRelationships = relationships.DeleteAsync(cancellationToken);

                    Task selectedSelected = graph.CreateRelationshipAsync(
                        people, new Knows(), companies, RelationshipDirection.Incoming, cancellationToken);
                    Task selectedNew = graph.CreateAsync(
                        people, new Knows(), new Company(), RelationshipDirection.Incoming, cancellationToken);
                    Task newSelected = graph.CreateAsync(
                        new Person(), new Knows(), companies, RelationshipDirection.Incoming, cancellationToken);
                    Task newNew = graph.CreateAsync(
                        new Person(), new Knows(), new Company(), RelationshipDirection.Incoming, transaction, cancellationToken);
                    Task selfLoop = graph.CreateSelfLoopAsync(
                        new Person(), new Knows(), transaction, cancellationToken);
                }
            }
            """);

        var result = CompilationFixture.Compile(source);

        Assert.False(result.HasErrors, result.DescribeErrors());
    }

    // ---- Must NOT compile ----

    [Fact]
    public void RemovedRecreateIndexesAsync_DoesNotCompile()
    {
        var source = WithDomainModel("""
            public static class SchemaMaintenance
            {
                public static Task Run(IGraph graph) => graph.RecreateIndexesAsync();
            }
            """);

        var result = CompilationFixture.Compile(source);

        Assert.True(result.HasErrors, "Expected the removed RecreateIndexesAsync method to fail to compile.");
    }

    [Fact]
    public void RemovedEntityIdentityAndRelationshipEndpoints_DoNotCompile()
    {
        var source = WithDomainModel("""
            public static class RemovedIdentity
            {
                public static void Run(IEntity entity)
                {
                    _ = entity.Id;
                    _ = new Knows("start", "end");
                }
            }
            """);

        var result = CompilationFixture.Compile(source);

        Assert.True(result.HasErrors, "Expected the removed entity identity and relationship endpoint constructor surfaces to fail to compile.");
        Assert.True(result.Errors.Count >= 2, result.DescribeErrors());
    }

    [Fact]
    public void ExecutePrefixedMutationAliases_DoNotCompile()
    {
        var source = WithDomainModel("""
            public static class RemovedAliases
            {
                public static void Run(IGraphQueryable<Person> people)
                {
                    _ = people.ExecuteUpdateAsync(setters => setters.SetProperty(person => person.Age, 42));
                    _ = people.ExecuteDeleteAsync();
                }
            }
            """);

        var result = CompilationFixture.Compile(source);

        Assert.True(result.HasErrors, "Expected Execute-prefixed graph mutation aliases to remain unavailable.");
        Assert.True(result.Errors.Count >= 2, result.DescribeErrors());
    }

    [Fact]
    public void CommandSurfaceRejectsWrongEntityKinds_DoesNotCompile()
    {
        var source = WithDomainModel("""
            public static class InvalidCommands
            {
                public static void Run(
                    IGraph graph,
                    IGraphQueryable<string> values,
                    IGraphQueryable<Knows> relationships,
                    IGraphQueryable<Person> people)
                {
                    _ = values.UpdateAsync(setters => setters.SetProperty(value => value.Length, 1));
                    _ = graph.CreateRelationshipAsync(relationships, new Knows(), people);
                    _ = graph.CreateAsync(people, new Knows(), new Knows());
                }
            }
            """);

        var result = CompilationFixture.Compile(source);

        Assert.True(result.HasErrors, "Expected graph commands to reject non-entity and non-node operands.");
        Assert.True(result.Errors.Count >= 3, result.DescribeErrors());
    }

    [Fact]
    public void AverageAsync_UnsupportedNumericType_DoesNotCompile()
    {
        var source = WithDomainModel("""
            public static class Queries
            {
                public static Task<byte> Run(IGraphQueryable<Person> people) =>
                    people.AverageAsync(person => person.ByteValue);
            }
            """);

        var result = CompilationFixture.Compile(source);

        Assert.True(result.HasErrors, "Expected AverageAsync over byte to fail to compile.");
    }

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
    public void RemovedThreeArgTraverseWithDepth_DoesNotCompile()
    {
        var source = WithDomainModel("""
            public static class Queries
            {
                public static IGraphQueryable<Person> Run(IGraphQueryable<Person> people) =>
                    people.Traverse<Person, Knows, Person>(3);
            }
            """);

        var result = CompilationFixture.Compile(source);

        Assert.True(result.HasErrors, "Expected the removed three-argument Traverse depth overload to fail to compile.");
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
    public void RemovedFreeFloatingWithDepth_DoesNotCompile()
    {
        var source = WithDomainModel("""
            public static class Queries
            {
                public static IGraphQueryable<Person> Run(IGraphQueryable<Person> people) =>
                    people.WithDepth(3);
            }
            """);

        var result = CompilationFixture.Compile(source);

        Assert.True(result.HasErrors, "Expected the removed free-floating WithDepth method to fail to compile.");
    }

    [Fact]
    public void RemovedFreeFloatingDirection_DoesNotCompile()
    {
        var source = WithDomainModel("""
            public static class Queries
            {
                public static IGraphQueryable<Person> Run(IGraphQueryable<Person> people) =>
                    people.Direction(GraphTraversalDirection.Incoming);
            }
            """);

        var result = CompilationFixture.Compile(source);

        Assert.True(result.HasErrors, "Expected the removed free-floating Direction method to fail to compile.");
    }

    [Fact]
    public void RemovedQueryableAliases_DoNotCompile()
    {
        var source = WithDomainModel("""
            public static class Queries
            {
                public static void Run(
                    IGraphNodeQueryable node,
                    IGraphNodeQueryable<Person> nodes,
                    IOrderedGraphNodeQueryable<Person> orderedNodes,
                    IGraphRelationshipQueryable relationship,
                    IGraphRelationshipQueryable<Knows> relationships,
                    IOrderedGraphRelationshipQueryable<Knows> orderedRelationships)
                {
                }
            }
            """);

        var result = CompilationFixture.Compile(source);

        Assert.True(result.HasErrors, "Expected every removed per-kind queryable alias to fail to compile.");
        Assert.True(result.Errors.Count >= 6, result.DescribeErrors());
    }
}
