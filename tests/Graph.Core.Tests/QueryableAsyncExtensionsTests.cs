// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Core.Tests;

using System.Collections;
using System.Linq.Expressions;

public sealed class QueryableAsyncExtensionsTests
{
    [Fact]
    public async Task AverageAsync_NonGraphSourceFallback_MatchesLinqOverloadMatrix()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        Assert.Equal(20.5, await Query(20, 21).AverageAsync(cancellationToken));
        Assert.Equal(100.5, await Query(100L, 101L).AverageAsync(cancellationToken));
        Assert.Equal(1.5f, await Query(1f, 2f).AverageAsync(cancellationToken));
        Assert.Equal(2.5, await Query(2d, 3d).AverageAsync(cancellationToken));
        Assert.Equal(3.5m, await Query(3m, 4m).AverageAsync(cancellationToken));

        Assert.Equal(15, await Query<int?>(10, null, 20).AverageAsync(cancellationToken));
        Assert.Equal(150, await Query<long?>(100, null, 200).AverageAsync(cancellationToken));
        Assert.Equal(2, await Query<float?>(1, null, 3).AverageAsync(cancellationToken));
        Assert.Equal(6, await Query<double?>(4, null, 8).AverageAsync(cancellationToken));
        Assert.Equal(8, await Query<decimal?>(7, null, 9).AverageAsync(cancellationToken));
        Assert.Null(await Query<int?>().AverageAsync(cancellationToken));
        Assert.Null(await Query<int?>(null, null).AverageAsync(cancellationToken));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Query<int>().AverageAsync(cancellationToken));
    }

    [Fact]
    public async Task AverageAsync_NonGraphSelectorFallback_MatchesLinqOverloadMatrix()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var rows = Query(
            new NumericRow(20, 100, 1, 2, 3, 10, 100, 1, 4, 7),
            new NumericRow(21, 101, 2, 3, 4, null, null, null, null, null),
            new NumericRow(22, 102, 3, 4, 5, 20, 200, 3, 8, 9));

        Assert.Equal(21, await rows.AverageAsync(row => row.Int, cancellationToken));
        Assert.Equal(101, await rows.AverageAsync(row => row.Long, cancellationToken));
        Assert.Equal(2, await rows.AverageAsync(row => row.Float, cancellationToken));
        Assert.Equal(3, await rows.AverageAsync(row => row.Double, cancellationToken));
        Assert.Equal(4, await rows.AverageAsync(row => row.Decimal, cancellationToken));
        Assert.Equal(15, await rows.AverageAsync(row => row.NullableInt, cancellationToken));
        Assert.Equal(150, await rows.AverageAsync(row => row.NullableLong, cancellationToken));
        Assert.Equal(2, await rows.AverageAsync(row => row.NullableFloat, cancellationToken));
        Assert.Equal(6, await rows.AverageAsync(row => row.NullableDouble, cancellationToken));
        Assert.Equal(8, await rows.AverageAsync(row => row.NullableDecimal, cancellationToken));

        var empty = Query<NumericRow>();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => empty.AverageAsync(row => row.Int, cancellationToken));
        Assert.Null(await empty.AverageAsync(row => row.NullableInt, cancellationToken));
    }

    private static EnumerableGraphQueryable<T> Query<T>(params T[] values) =>
        new EnumerableGraphQueryable<T>(values);

    private sealed record NumericRow(
        int Int,
        long Long,
        float Float,
        double Double,
        decimal Decimal,
        int? NullableInt,
        long? NullableLong,
        float? NullableFloat,
        double? NullableDouble,
        decimal? NullableDecimal);

    /// <summary>
    /// Supplies a normal LINQ-to-objects provider through <see cref="IQueryable.Provider"/> while
    /// retaining the graph-queryable marker needed to call the public async extensions.
    /// </summary>
    private sealed class EnumerableGraphQueryable<T>(IEnumerable<T> values) : IGraphQueryable<T>
    {
        private readonly IQueryable<T> queryable = values.AsQueryable();

        public Type ElementType => typeof(T);

        public Expression Expression => queryable.Expression;

        IGraphQueryProvider IGraphQueryable<T>.Provider =>
            throw new NotSupportedException("This fixture intentionally has no graph provider.");

        IQueryProvider IQueryable.Provider => queryable.Provider;

        public IGraph Graph =>
            throw new NotSupportedException("This fixture intentionally has no graph instance.");

        public IEnumerator<T> GetEnumerator() => queryable.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public async IAsyncEnumerator<T> GetAsyncEnumerator(
            CancellationToken cancellationToken = default)
        {
            foreach (var value in queryable)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return value;
                await Task.Yield();
            }
        }
    }
}
