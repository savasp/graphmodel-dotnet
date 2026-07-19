// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Core.Tests;

using Cvoya.Graph.Serialization;
using Cvoya.Graph.Serialization.Results;


/// <summary>
/// Constructor-parameter materialization must reject stored nulls that the declared parameter cannot
/// hold, rather than substituting <c>default(T)</c>. The scalar, wire-list, and simple-collection
/// paths are asserted separately because each reaches the target parameter differently.
/// </summary>
[Trait("Area", "ResultMaterialization")]
public class GraphResultMaterializerTests
{
    private readonly EntityFactory factory = new();

    [Fact]
    public async Task NullScalar_IntoNonNullableValueParameter_ThrowsWithoutInventingDefault()
    {
        var exception = await Assert.ThrowsAsync<GraphException>(() => MaterializeAsync<ScalarProjection>(
            ("Name", GraphValue.Scalar("Ada")),
            ("Age", GraphValue.Scalar(null))));

        Assert.Equal(
            $"Cannot materialize null into non-nullable type '{typeof(int).FullName}' for 'Age'.",
            exception.Message);
    }

    [Fact]
    public async Task NullScalar_IntoNullableValueParameter_PreservesNull()
    {
        var result = await MaterializeAsync<NullableScalarProjection>(
            ("Name", GraphValue.Scalar("Ada")),
            ("Age", GraphValue.Scalar(null)));

        Assert.Equal("Ada", result.Name);
        Assert.Null(result.Age);
    }

    [Fact]
    public async Task NullElement_IntoNonNullableValueElementParameter_ThrowsWithFailingIndex()
    {
        var exception = await Assert.ThrowsAsync<GraphException>(() => MaterializeAsync<IntegerCollectionProjection>(
            ("Scores", GraphValue.List([GraphValue.Scalar(1L), GraphValue.Scalar(null), GraphValue.Scalar(3L)]))));

        Assert.Equal(
            $"Constructor parameter 'Scores' contains a null element at index 1, " +
            $"but its target element type '{typeof(int)}' is non-nullable.",
            exception.Message);
    }

    [Fact]
    public async Task NullElement_IntoNullableValueElementParameter_PreservesNullAndOrder()
    {
        var result = await MaterializeAsync<NullableIntegerCollectionProjection>(
            ("Scores", GraphValue.List([GraphValue.Scalar(1L), GraphValue.Scalar(null), GraphValue.Scalar(3L)])));

        Assert.Equal([1, null, 3], result.Scores);
    }

    [Fact]
    public async Task NullElement_IntoNonNullableReferenceElementParameter_ThrowsWithFailingIndex()
    {
        var exception = await Assert.ThrowsAsync<GraphException>(() => MaterializeAsync<NameCollectionProjection>(
            ("Names", GraphValue.List([GraphValue.Scalar("Ada"), GraphValue.Scalar(null)]))));

        Assert.Equal(
            $"Constructor parameter 'Names' contains a null element at index 1, " +
            $"but its target element type '{typeof(string)}' is non-nullable.",
            exception.Message);
    }

    [Fact]
    public async Task NullElement_IntoNullableReferenceElementParameter_PreservesNullAndOrder()
    {
        var result = await MaterializeAsync<NullableNameCollectionProjection>(
            ("Names", GraphValue.List([GraphValue.Scalar("Ada"), GraphValue.Scalar(null), GraphValue.Scalar("Grace")])));

        Assert.Equal(["Ada", null, "Grace"], result.Names);
    }

    [Fact]
    public async Task NullElement_IntoNonNullableArrayElementParameter_ThrowsWithFailingIndex()
    {
        var exception = await Assert.ThrowsAsync<GraphException>(() => MaterializeAsync<ArrayProjection>(
            ("Scores", GraphValue.List([GraphValue.Scalar(1L), GraphValue.Scalar(2L), GraphValue.Scalar(null)]))));

        Assert.Equal(
            $"Constructor parameter 'Scores' contains a null element at index 2, " +
            $"but its target element type '{typeof(int)}' is non-nullable.",
            exception.Message);
    }

    [Fact]
    public async Task NullScalar_IntoReferenceParameter_PreservesNull()
    {
        var result = await MaterializeAsync<ScalarProjection>(
            ("Name", GraphValue.Scalar(null)),
            ("Age", GraphValue.Scalar(30L)));

        Assert.Null(result.Name);
        Assert.Equal(30, result.Age);
    }

    /// <summary>
    /// The C# compiler emits no nullable-reference metadata for anonymous-type constructor
    /// parameters, so their reference elements read as nullable-oblivious and fail closed — the same
    /// reading <see cref="PropertySchema.IsElementNullable"/> gives an oblivious declared property.
    /// </summary>
    [Fact]
    public async Task NullElement_IntoNullableObliviousElementParameter_FailsClosed()
    {
        var exception = await Assert.ThrowsAsync<GraphException>(() => MaterializeAsync<ObliviousCollectionProjection>(
            ("Names", GraphValue.List([GraphValue.Scalar("Ada"), GraphValue.Scalar(null)]))));

        Assert.Contains("contains a null element at index 1", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Pins the anonymous-type reading the provider implementers guide now promises: the projection
    /// shape callers reach for most has no annotation to read, so it takes the oblivious path above.
    /// </summary>
    [Fact]
    public async Task NullElement_IntoAnonymousTypeElementParameter_FailsClosed()
    {
        var exception = await Assert.ThrowsAsync<GraphException>(() => MaterializeShapeAsync(
            new { Names = new List<string>() },
            ("Names", GraphValue.List([GraphValue.Scalar("Ada"), GraphValue.Scalar(null)]))));

        Assert.Contains("contains a null element at index 1", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NullScalar_IntoAnonymousTypeValueParameter_Throws()
    {
        var exception = await Assert.ThrowsAsync<GraphException>(() => MaterializeShapeAsync(
            new { Age = 0 },
            ("Age", GraphValue.Scalar(null))));

        Assert.Equal(
            $"Cannot materialize null into non-nullable type '{typeof(int).FullName}' for 'Age'.",
            exception.Message);
    }

    /// <summary>
    /// A blob parameter skips the null-element scan, since a byte[] cannot hold one. This asserts the
    /// skip still materializes the blob rather than declining to convert it.
    /// </summary>
    [Fact]
    public async Task ByteArrayParameter_MaterializesThroughTheScanSkip()
    {
        var result = await MaterializeAsync<BlobProjection>(
            ("Data", GraphValue.Scalar(new byte[] { 1, 2, 3 })));

        Assert.Equal([1, 2, 3], result.Data);
    }

    [Fact]
    public async Task NonNullCollection_MaterializesWithoutRejection()
    {
        var result = await MaterializeAsync<NameCollectionProjection>(
            ("Names", GraphValue.List([GraphValue.Scalar("Ada"), GraphValue.Scalar("Grace")])));

        Assert.Equal(["Ada", "Grace"], result.Names);
    }

    // Anonymous types cannot be named as a type argument, so the shape instance infers it.
    private Task<T> MaterializeShapeAsync<T>(T shape, params (string Key, GraphValue Value)[] fields) =>
        MaterializeAsync<T>(fields);

    private async Task<T> MaterializeAsync<T>(params (string Key, GraphValue Value)[] fields)
    {
        var record = new GraphRecord(fields.ToDictionary(field => field.Key, field => field.Value));
        var materializer = new GraphResultMaterializer(factory, loggerFactory: null);
        var result = await materializer.MaterializeAsync<T>(
            [record],
            cancellationToken: TestContext.Current.CancellationToken);

        return Assert.IsType<T>(result);
    }

    private sealed record ScalarProjection(string Name, int Age);

    private sealed record NullableScalarProjection(string Name, int? Age);

    private sealed record IntegerCollectionProjection(List<int> Scores);

    private sealed record NullableIntegerCollectionProjection(List<int?> Scores);

    private sealed record NameCollectionProjection(List<string> Names);

    private sealed record NullableNameCollectionProjection(List<string?> Names);

    private sealed record ArrayProjection(int[] Scores);

    private sealed record BlobProjection(byte[] Data);

#nullable disable
    private sealed record ObliviousCollectionProjection(List<string> Names);
#nullable restore
}
