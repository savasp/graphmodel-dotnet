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

namespace Cvoya.Graph.Model;

using System.Linq.Expressions;


/// <summary>
/// Provides aggregation methods for numeric types in graph queryables.
/// </summary>
/// <typeparam name="T">The numeric type of elements in the queryable</typeparam>
public interface IGraphQueryableAggregation<T> where T : struct, IComparable<T>
{
    /// <summary>
    /// Asynchronously computes the sum of the values in the sequence.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the sum of the values.</returns>
    Task<T> SumAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously computes the average of the values in the sequence.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the average of the values.</returns>
    Task<double> AverageAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Extension methods that add numeric aggregation capabilities to IGraphQueryable&lt;T&gt;.
/// </summary>
public static class GraphQueryableAggregationExtensions
{
    /// <summary>
    /// Asynchronously computes the sum of the values obtained by invoking a transform function on each element.
    /// </summary>
    /// <typeparam name="TSource">The type of the elements in the source sequence.</typeparam>
    /// <typeparam name="TResult">The type of the values to sum.</typeparam>
    /// <param name="source">The graph queryable to compute the sum over.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the sum of the projected values.</returns>
    public static async Task<TResult> SumAsync<TSource, TResult>(
        this IGraphQueryable<TSource> source,
        Expression<Func<TSource, TResult>> selector,
        CancellationToken cancellationToken = default)
        where TResult : struct, IComparable<TResult>
    {
        var projected = source.Select(selector);
        if (projected is IGraphQueryableAggregation<TResult> aggregation)
        {
            return await aggregation.SumAsync(cancellationToken);
        }

        // Fallback to materializing and computing locally
        var selectedQuery = source.Select(selector);
        var values = await ((IGraphQueryable<TResult>)selectedQuery).ToListAsync(cancellationToken);
        return values.Aggregate((a, b) => (dynamic)a + b);
    }

    /// <summary>
    /// Asynchronously computes the average of the values obtained by invoking a transform function on each element.
    /// </summary>
    /// <typeparam name="TSource">The type of the elements in the source sequence.</typeparam>
    /// <typeparam name="TResult">The type of the values to average.</typeparam>
    /// <param name="source">The graph queryable to compute the average over.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the average of the projected values.</returns>
    public static async Task<double> AverageAsync<TSource, TResult>(
        this IGraphQueryable<TSource> source,
        Expression<Func<TSource, TResult>> selector,
        CancellationToken cancellationToken = default)
        where TResult : struct, IComparable<TResult>
    {
        var projected = source.Select(selector);
        if (projected is IGraphQueryableAggregation<TResult> aggregation)
        {
            return await aggregation.AverageAsync(cancellationToken);
        }

        // Fallback to materializing and computing locally
        var selectedQuery = source.Select(selector);
        var values = await ((IGraphQueryable<TResult>)selectedQuery).ToListAsync(cancellationToken);
        if (values.Count == 0) throw new InvalidOperationException("Sequence contains no elements");

        return values.Select(v => Convert.ToDouble(v)).Average();
    }

    /// <summary>
    /// Asynchronously computes the sum of a sequence of int values.
    /// </summary>
    /// <param name="source">A sequence of int values to calculate the sum of.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the sum of the values in the sequence.</returns>
    public static async Task<int> SumAsync(
        this IGraphQueryable<int> source,
        CancellationToken cancellationToken = default)
    {
        if (source is IGraphQueryableAggregation<int> aggregation)
        {
            return await aggregation.SumAsync(cancellationToken);
        }

        var values = await source.ToListAsync(cancellationToken);
        return values.Sum();
    }

    /// <summary>
    /// Asynchronously computes the average of a sequence of int values.
    /// </summary>
    /// <param name="source">A sequence of int values to calculate the average of.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the average of the values in the sequence.</returns>
    public static async Task<double> AverageAsync(
        this IGraphQueryable<int> source,
        CancellationToken cancellationToken = default)
    {
        if (source is IGraphQueryableAggregation<int> aggregation)
        {
            return await aggregation.AverageAsync(cancellationToken);
        }

        var values = await source.ToListAsync(cancellationToken);
        return values.Average();
    }

    /// <summary>
    /// Asynchronously computes the sum of a sequence of long values.
    /// </summary>
    /// <param name="source">A sequence of long values to calculate the sum of.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the sum of the values in the sequence.</returns>
    public static async Task<long> SumAsync(
        this IGraphQueryable<long> source,
        CancellationToken cancellationToken = default)
    {
        if (source is IGraphQueryableAggregation<long> aggregation)
        {
            return await aggregation.SumAsync(cancellationToken);
        }

        var values = await source.ToListAsync(cancellationToken);
        return values.Sum();
    }

    /// <summary>
    /// Asynchronously computes the average of a sequence of long values.
    /// </summary>
    /// <param name="source">A sequence of long values to calculate the average of.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the average of the values in the sequence.</returns>
    public static async Task<double> AverageAsync(
        this IGraphQueryable<long> source,
        CancellationToken cancellationToken = default)
    {
        if (source is IGraphQueryableAggregation<long> aggregation)
        {
            return await aggregation.AverageAsync(cancellationToken);
        }

        var values = await source.ToListAsync(cancellationToken);
        return values.Average();
    }

    /// <summary>
    /// Asynchronously computes the sum of a sequence of decimal values.
    /// </summary>
    /// <param name="source">A sequence of decimal values to calculate the sum of.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the sum of the values in the sequence.</returns>
    public static async Task<decimal> SumAsync(
        this IGraphQueryable<decimal> source,
        CancellationToken cancellationToken = default)
    {
        if (source is IGraphQueryableAggregation<decimal> aggregation)
        {
            return await aggregation.SumAsync(cancellationToken);
        }

        var values = await source.ToListAsync(cancellationToken);
        return values.Sum();
    }

    /// <summary>
    /// Asynchronously computes the average of a sequence of decimal values.
    /// </summary>
    /// <param name="source">A sequence of decimal values to calculate the average of.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the average of the values in the sequence.</returns>
    public static async Task<decimal> AverageAsync(
        this IGraphQueryable<decimal> source,
        CancellationToken cancellationToken = default)
    {
        if (source is IGraphQueryableAggregation<decimal> aggregation)
        {
            return (decimal)await aggregation.AverageAsync(cancellationToken);
        }

        var values = await source.ToListAsync(cancellationToken);
        return (decimal)values.Average();
    }

    /// <summary>
    /// Asynchronously computes the sum of a sequence of double values.
    /// </summary>
    /// <param name="source">A sequence of double values to calculate the sum of.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the sum of the values in the sequence.</returns>
    public static async Task<double> SumAsync(
        this IGraphQueryable<double> source,
        CancellationToken cancellationToken = default)
    {
        if (source is IGraphQueryableAggregation<double> aggregation)
        {
            return await aggregation.SumAsync(cancellationToken);
        }

        var values = await source.ToListAsync(cancellationToken);
        return values.Sum();
    }

    /// <summary>
    /// Asynchronously computes the average of a sequence of double values.
    /// </summary>
    /// <param name="source">A sequence of double values to calculate the average of.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the average of the values in the sequence.</returns>
    public static async Task<double> AverageAsync(
        this IGraphQueryable<double> source,
        CancellationToken cancellationToken = default)
    {
        if (source is IGraphQueryableAggregation<double> aggregation)
        {
            return await aggregation.AverageAsync(cancellationToken);
        }

        var values = await source.ToListAsync(cancellationToken);
        return values.Average();
    }

    /// <summary>
    /// Asynchronously computes the sum of a sequence of float values.
    /// </summary>
    /// <param name="source">A sequence of float values to calculate the sum of.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the sum of the values in the sequence.</returns>
    public static async Task<float> SumAsync(
        this IGraphQueryable<float> source,
        CancellationToken cancellationToken = default)
    {
        if (source is IGraphQueryableAggregation<float> aggregation)
        {
            return await aggregation.SumAsync(cancellationToken);
        }

        var values = await source.ToListAsync(cancellationToken);
        return values.Sum();
    }

    /// <summary>
    /// Asynchronously computes the average of a sequence of float values.
    /// </summary>
    /// <param name="source">A sequence of float values to calculate the average of.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the average of the values in the sequence.</returns>
    public static async Task<float> AverageAsync(
        this IGraphQueryable<float> source,
        CancellationToken cancellationToken = default)
    {
        if (source is IGraphQueryableAggregation<float> aggregation)
        {
            return (float)await aggregation.AverageAsync(cancellationToken);
        }

        var values = await source.ToListAsync(cancellationToken);
        return (float)values.Average();
    }
}
