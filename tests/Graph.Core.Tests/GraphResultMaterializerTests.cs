// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Core.Tests;

using System.Reflection;
using Cvoya.Graph.Serialization;
using Cvoya.Graph.Serialization.Results;


/// <summary>
/// Constructor-parameter materialization must reject stored nulls that the declared parameter cannot
/// hold, rather than substituting <c>default(T)</c>. Scalar, nested scalar-collection, projected-map
/// collection, missing-member, and top-level collection paths are asserted separately.
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
    public async Task NullScalar_IntoNonNullableReferenceParameter_Throws()
    {
        var exception = await Assert.ThrowsAsync<GraphException>(() => MaterializeAsync<ScalarProjection>(
            ("Name", GraphValue.Scalar(null)),
            ("Age", GraphValue.Scalar(30L))));

        Assert.Equal(
            $"Cannot materialize null into non-nullable type '{typeof(string).FullName}' for 'Name'.",
            exception.Message);
    }

    [Fact]
    public async Task NullScalar_IntoNullableReferenceParameter_PreservesNull()
    {
        var result = await MaterializeAsync<NullableReferenceScalarProjection>(
            ("Name", GraphValue.Scalar(null)),
            ("Age", GraphValue.Scalar(30L)));

        Assert.Null(result.Name);
        Assert.Equal(30, result.Age);
    }

    [Fact]
    public async Task NullScalar_IntoNullableObliviousReferenceParameter_PreservesNull()
    {
        var result = await MaterializeAsync<ObliviousScalarProjection>(
            ("Name", GraphValue.Scalar(null)));

        Assert.Null(result.Name);
    }

    [Fact]
    public async Task NestedNullElement_IntoNonNullableValueElementParameter_Throws()
    {
        var exception = await Assert.ThrowsAsync<GraphException>(() => MaterializeAsync<NestedIntegerCollectionProjection>(
            ("Matrix", GraphValue.List([
                GraphValue.List([GraphValue.Scalar(1L), GraphValue.Scalar(null)])
            ]))));

        Assert.Equal(
            $"Constructor parameter 'Matrix' contains a null element at index 1, " +
            $"but its target element type '{typeof(int)}' is non-nullable.",
            exception.Message);
    }

    [Fact]
    public async Task NestedNullElement_IntoNullableValueElementParameter_PreservesNull()
    {
        var result = await MaterializeAsync<NestedNullableIntegerCollectionProjection>(
            ("Matrix", GraphValue.List([
                GraphValue.List([GraphValue.Scalar(1L), GraphValue.Scalar(null)])
            ])));

        Assert.Equal([1, null], Assert.Single(result.Matrix));
    }

    [Fact]
    public async Task MissingRequiredParameter_ThrowsInsteadOfInventingDefault()
    {
        var exception = await Assert.ThrowsAsync<GraphException>(() => MaterializeAsync<ScalarProjection>(
            ("Name", GraphValue.Scalar("Ada"))));

        Assert.Equal(
            $"Cannot materialize null into non-nullable type '{typeof(int).FullName}' for 'Age'.",
            exception.Message);
    }

    [Fact]
    public async Task MissingOptionalParameter_UsesDeclaredDefault()
    {
        var result = await MaterializeAsync<OptionalScalarProjection>(
            ("Name", GraphValue.Scalar("Ada")));

        Assert.Equal(42, result.Age);
    }

    [Fact]
    public async Task MissingNullableParameter_PreservesNull()
    {
        var result = await MaterializeAsync<NullableScalarProjection>(
            ("Name", GraphValue.Scalar("Ada")));

        Assert.Null(result.Age);
    }

    [Fact]
    public async Task MultipleScalarColumns_IntoScalarCollectionElement_ThrowsShapeError()
    {
        var exception = await Assert.ThrowsAsync<GraphException>(() => MaterializeAsync<IntegerCollectionProjection>(
            ("Scores", GraphValue.List([
                GraphValue.Map(new Dictionary<string, GraphValue>
                {
                    ["Left"] = GraphValue.Scalar(null),
                    ["Right"] = GraphValue.Scalar(null),
                })
            ]))));

        Assert.Contains("projection with 2 simple and 0 complex values", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NullProjectedMapElement_IntoNonNullableReferenceCollection_Throws()
    {
        var exception = await Assert.ThrowsAsync<GraphException>(() => MaterializeAsync<NameCollectionProjection>(
            ("Names", GraphValue.List([
                GraphValue.Map(new Dictionary<string, GraphValue>
                {
                    ["Value"] = GraphValue.Scalar(null),
                })
            ]))));

        Assert.Contains("contains a null element at index 0", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NullProjectedMapElement_IntoNullableReferenceCollection_PreservesNull()
    {
        var result = await MaterializeAsync<NullableNameCollectionProjection>(
            ("Names", GraphValue.List([
                GraphValue.Map(new Dictionary<string, GraphValue>
                {
                    ["Value"] = GraphValue.Scalar(null),
                })
            ])));

        Assert.Null(Assert.Single(result.Names));
    }

    [Fact]
    public async Task TopLevelReferenceScalarCollection_PreservesNullRowAndOrder()
    {
        var result = await MaterializeRecordsAsync<List<string?>>(
            ScalarRecord("Ada"),
            ScalarRecord(null),
            ScalarRecord("Grace"));

        Assert.Equal(["Ada", null, "Grace"], result);
    }

    [Fact]
    public async Task TopLevelNonNullableValueScalarCollection_RejectsNullRow()
    {
        var exception = await Assert.ThrowsAsync<GraphException>(() => MaterializeRecordsAsync<List<int>>(
            ScalarRecord(1L),
            ScalarRecord(null),
            ScalarRecord(3L)));

        Assert.Contains("Cannot materialize null into non-nullable type", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnconstrainedGenericElement_FailsClosed()
    {
        var exception = await Assert.ThrowsAsync<GraphException>(() => MaterializeAsync<GenericProjection<string>>(
            ("Items", GraphValue.List([GraphValue.Scalar("Ada"), GraphValue.Scalar(null)]))));

        Assert.Contains("contains a null element at index 1", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnconstrainedNullableGenericElement_FailsClosedBecauseRuntimeMetadataIsIndistinguishable()
    {
        var exception = await Assert.ThrowsAsync<GraphException>(() => MaterializeAsync<NullableGenericProjection<string>>(
            ("Items", GraphValue.List([GraphValue.Scalar("Ada"), GraphValue.Scalar(null)]))));

        Assert.Contains("contains a null element at index 1", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReferenceConstrainedGenericElement_UsesDeclaredNullability()
    {
        await Assert.ThrowsAsync<GraphException>(() => MaterializeAsync<ReferenceGenericProjection<string>>(
            ("Items", GraphValue.List([GraphValue.Scalar("Ada"), GraphValue.Scalar(null)]))));

        var nullable = await MaterializeAsync<NullableReferenceGenericProjection<string>>(
            ("Items", GraphValue.List([GraphValue.Scalar("Ada"), GraphValue.Scalar(null)])));

        Assert.Equal(["Ada", null], nullable.Items);
    }

    [Fact]
    public void GenericPropertySchema_FailsClosedUnlessConstraintPreservesNullableMetadata()
    {
        Assert.False(ElementNullability<GenericPropertyProjection<string>>());
        Assert.False(ElementNullability<NullableGenericPropertyProjection<string>>());
        Assert.False(ElementNullability<ReferenceGenericPropertyProjection<string>>());
        Assert.True(ElementNullability<NullableReferenceGenericPropertyProjection<string>>());
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

    [Fact]
    public async Task NullScalar_IntoAnonymousTypeReferenceParameter_PreservesNull()
    {
        var result = await MaterializeShapeAsync(
            new { Name = string.Empty },
            ("Name", GraphValue.Scalar(null)));

        Assert.Null(result.Name);
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

    [Fact]
    public void ComplexCollectionParameter_RejectsNullRegardlessOfDeclaredElementNullability()
    {
        var collection = new EntityCollection(typeof(MaterializedAddress), [null!]);

        var exception = Assert.Throws<GraphException>(() =>
            InvokeComplexCollectionMaterialization<NullableComplexCollectionProjection>(
                collection,
                "stored{addresses}"));

        Assert.Equal(
            "Complex collection property 'stored{addresses}' contains a null element at index 0, " +
            $"but its target element type '{typeof(MaterializedAddress)}' does not allow null elements.",
            exception.Message);
    }

    [Fact]
    public void ComplexCollectionParameter_RejectsMistypedElementAtOriginalIndex()
    {
        var collection = new EntityCollection(
            typeof(MaterializedAddress),
            [
                ComplexEntity(typeof(HomeAddress)),
                ComplexEntity(typeof(OtherComplexValue)),
            ]);

        var exception = Assert.Throws<GraphException>(() =>
            InvokeComplexCollectionMaterialization<ComplexCollectionProjection>(
                collection,
                "stored{addresses}"));

        Assert.Equal(
            $"Complex collection property 'stored{{addresses}}' contains an element of type '{typeof(OtherComplexValue)}' at index 1, " +
            $"which is not assignable to its target element type '{typeof(MaterializedAddress)}'.",
            exception.Message);
    }

    [Fact]
    public void ComplexCollectionParameter_PreservesPolymorphicCountAndOrder()
    {
        var collection = new EntityCollection(
            typeof(MaterializedAddress),
            [
                ComplexEntity(typeof(HomeAddress)),
                ComplexEntity(typeof(WorkAddress)),
            ]);

        var result = Assert.IsType<List<MaterializedAddress>>(
            InvokeComplexCollectionMaterialization<ComplexCollectionProjection>(
                collection,
                "stored{addresses}"));

        Assert.Collection(
            result,
            item => Assert.IsType<HomeAddress>(item),
            item => Assert.IsType<WorkAddress>(item));
    }

    [Fact]
    public void ComplexCollectionSchema_DoesNotAdvertiseNullableElements()
    {
        var property = typeof(NullableComplexCollectionProjection)
            .GetProperty(nameof(NullableComplexCollectionProjection.Addresses))!;
        var schema = new PropertySchema(
            property,
            "stored{addresses}",
            PropertyType.ComplexCollection,
            typeof(MaterializedAddress));

        Assert.False(schema.IsElementNullable);
    }

    // Anonymous types cannot be named as a type argument, so the shape instance infers it.
    private Task<T> MaterializeShapeAsync<T>(T shape, params (string Key, GraphValue Value)[] fields) =>
        MaterializeAsync<T>(fields);

    private async Task<T> MaterializeAsync<T>(params (string Key, GraphValue Value)[] fields)
    {
        var record = new GraphRecord(fields.ToDictionary(field => field.Key, field => field.Value));
        return await MaterializeRecordsAsync<T>(record);
    }

    private async Task<T> MaterializeRecordsAsync<T>(params GraphRecord[] records)
    {
        var materializer = new GraphResultMaterializer(factory, loggerFactory: null);
        var result = await materializer.MaterializeAsync<T>(
            records,
            cancellationToken: TestContext.Current.CancellationToken);

        return Assert.IsType<T>(result);
    }

    private static GraphRecord ScalarRecord(object? value) => new(
        new Dictionary<string, GraphValue>
        {
            ["Value"] = GraphValue.Scalar(value),
        });

    private object InvokeComplexCollectionMaterialization<T>(
        EntityCollection collection,
        string propertyLabel)
    {
        var materializer = new GraphResultMaterializer(factory, loggerFactory: null);
        var parameter = typeof(T).GetConstructors().Single().GetParameters().Single();
        var method = typeof(GraphResultMaterializer).GetMethod(
            "MaterializeEntityCollection",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        try
        {
            return method.Invoke(materializer, [collection, parameter, propertyLabel])!;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw exception.InnerException;
        }
    }

    private static EntityInfo ComplexEntity(Type actualType) => new(
        actualType,
        actualType.Name,
        [],
        new Dictionary<string, Property>(),
        new Dictionary<string, Property>());

    private static bool ElementNullability<T>()
    {
        var property = typeof(T).GetProperty("Items")!;
        return new PropertySchema(
            property,
            property.Name,
            PropertyType.SimpleCollection,
            GraphResultTypeHelpersForTest(property.PropertyType)).IsElementNullable;
    }

    private static Type GraphResultTypeHelpersForTest(Type collectionType) =>
        collectionType.GetInterfaces()
            .First(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            .GetGenericArguments()[0];

    private sealed record ScalarProjection(string Name, int Age);

    private sealed record NullableScalarProjection(string Name, int? Age);

    private sealed record NullableReferenceScalarProjection(string? Name, int Age);

    private sealed record OptionalScalarProjection(string Name, int Age = 42);

    private sealed record IntegerCollectionProjection(List<int> Scores);

    private sealed record NullableIntegerCollectionProjection(List<int?> Scores);

    private sealed record NestedIntegerCollectionProjection(List<List<int>> Matrix);

    private sealed record NestedNullableIntegerCollectionProjection(List<List<int?>> Matrix);

    private sealed record NameCollectionProjection(List<string> Names);

    private sealed record NullableNameCollectionProjection(List<string?> Names);

    private sealed record ArrayProjection(int[] Scores);

    private sealed record ComplexCollectionProjection(List<MaterializedAddress> Addresses);

    private sealed record NullableComplexCollectionProjection(List<MaterializedAddress?> Addresses);

    private sealed record BlobProjection(byte[] Data);

    private class MaterializedAddress
    {
        public MaterializedAddress() { }
    }

    private sealed class HomeAddress : MaterializedAddress
    {
        public HomeAddress() { }
    }

    private sealed class WorkAddress : MaterializedAddress
    {
        public WorkAddress() { }
    }

    private sealed class OtherComplexValue
    {
        public OtherComplexValue() { }
    }

    private sealed record GenericProjection<T>(List<T> Items);

    private sealed record NullableGenericProjection<T>(List<T?> Items);

    private sealed record ReferenceGenericProjection<T>(List<T> Items) where T : class;

    private sealed record NullableReferenceGenericProjection<T>(List<T?> Items) where T : class;

    private sealed class GenericPropertyProjection<T>
    {
        public List<T> Items { get; init; } = [];
    }

    private sealed class NullableGenericPropertyProjection<T>
    {
        public List<T?> Items { get; init; } = [];
    }

    private sealed class ReferenceGenericPropertyProjection<T> where T : class
    {
        public List<T> Items { get; init; } = [];
    }

    private sealed class NullableReferenceGenericPropertyProjection<T> where T : class
    {
        public List<T?> Items { get; init; } = [];
    }

#nullable disable
    private sealed record ObliviousCollectionProjection(List<string> Names);

    private sealed record ObliviousScalarProjection(string Name);
#nullable restore
}
