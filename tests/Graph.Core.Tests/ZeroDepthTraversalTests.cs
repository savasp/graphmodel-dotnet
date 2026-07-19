// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Core.Tests;

using System.Linq.Expressions;
using Cvoya.Graph.Querying;

/// <summary>
/// A traversal is one or more relationship hops, so a depth bound of zero is not part of the
/// contract. These tests pin that zero is refused while the query is being constructed - before any
/// provider translation, execution, or materialization can observe it.
/// </summary>
[Trait("Area", "Traversal")]
public class ZeroDepthTraversalTests
{
    [Fact]
    public void Options_DepthZero_Throws()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new GraphTraversalOptions().Depth(0));

        Assert.Equal("maxDepth", exception.ParamName);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 1)]
    public void Options_DepthRangeWithZeroMinimum_Throws(int minDepth, int maxDepth)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new GraphTraversalOptions().Depth(minDepth, maxDepth));

        Assert.Equal("minDepth", exception.ParamName);
    }

    [Fact]
    public void Options_InvertedDepthRange_Throws()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new GraphTraversalOptions().Depth(3, 2));

        Assert.Equal("maxDepth", exception.ParamName);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 5)]
    [InlineData(2, 4)]
    public void Options_PositiveDepthRange_IsAccepted(int minDepth, int maxDepth)
    {
        var options = new GraphTraversalOptions().Depth(minDepth, maxDepth);

        Assert.Equal(minDepth, options.MinDepth);
        Assert.Equal(maxDepth, options.MaxDepth);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void Options_PositiveMaxDepth_LeavesMinimumUnspecified(int maxDepth)
    {
        var options = new GraphTraversalOptions().Depth(maxDepth);

        Assert.Null(options.MinDepth);
        Assert.Equal(maxDepth, options.MaxDepth);
    }

    [Fact]
    public void Traverse_MaxDepthZero_ThrowsAtQueryConstruction()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            Root<Person>().Traverse<Knows, Company>(0));

        Assert.Equal("maxDepth", exception.ParamName);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 1)]
    public void Traverse_MinDepthZero_ThrowsAtQueryConstruction(int minDepth, int maxDepth)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            Root<Person>().Traverse<Knows, Company>(minDepth, maxDepth));

        Assert.Equal("minDepth", exception.ParamName);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 1)]
    public void TraversePaths_MinDepthZero_ThrowsAtQueryConstruction(int minDepth, int maxDepth)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            Root<Person>().TraversePaths<Knows, Company>(minDepth, maxDepth));

        Assert.Equal("minDepth", exception.ParamName);
    }

    [Fact]
    public void TraverseWithOptions_DepthZero_ThrowsAtQueryConstruction()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Root<Person>().Traverse<Knows, Company>(options => options.Depth(0)));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 3)]
    public void Traverse_PositiveDepthRange_BuildsModelWithThatRange(int minDepth, int maxDepth)
    {
        var query = Root<Person>().Traverse<Knows, Company>(minDepth, maxDepth);

        var model = GraphQueryModelBuilder.Build(query.Expression);

        var step = Assert.Single(model.Traversal);
        Assert.Equal(minDepth, step.Depth.Min);
        Assert.Equal(maxDepth, step.Depth.Max);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 1)]
    [InlineData(-1, 2)]
    public void DepthRange_RejectsNonPositiveMinimum(int min, int max)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new DepthRange(min, max));

        Assert.Equal("min", exception.ParamName);
    }

    // The abstraction is intentional: callers compose the returned IGraphQueryable<T>.
#pragma warning disable CA1859
    private static IGraphQueryable<T> Root<T>() => new TestGraphQueryable<T>();
#pragma warning restore CA1859

    [Node("ZERO_DEPTH_PERSON")]
    private sealed record Person : Node
    {
        public string FirstName { get; init; } = string.Empty;
    }

    [Node("ZERO_DEPTH_COMPANY")]
    private sealed record Company : Node
    {
        public string Name { get; init; } = string.Empty;
    }

    [Relationship(Label = "ZERO_DEPTH_KNOWS")]
    private sealed record Knows(string Start, string End) : Relationship(Start, End);
}
