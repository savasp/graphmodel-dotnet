// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph;

using System.Linq.Expressions;

/// <summary>
/// Marker methods that stand in for LINQ terminal (query-executing) operators inside expression
/// trees. <see cref="QueryableAsyncExtensions"/> builds a <see cref="MethodCallExpression"/>
/// against one of these methods and hands it to <see cref="IGraphQueryProvider.ExecuteAsync{TResult}"/>
/// - the marker method itself is never invoked (every body throws).
/// </summary>
/// <remarks>
/// This type is internal, not public API: providers recognize terminal operators by
/// <see cref="System.Reflection.MethodInfo"/> identity (comparing
/// <c>MethodCallExpression.Method</c>, or its generic method definition, against these members),
/// never by matching the method name as a string. <c>Cvoya.Graph.Neo4j</c> (and any future
/// in-tree provider) is granted access via <c>InternalsVisibleTo</c> so it can reference these
/// members directly to build its dispatch table, instead of re-deriving them via reflection or
/// string matching.
///
/// The generic numeric overloads for <c>Sum</c>/<c>Average</c> are collapsed to a single
/// unconstrained generic definition per shape (see <see cref="SumAsyncMarker{TResult}(IQueryable{TResult})"/>
/// and friends): since these bodies are never executed, there is no need for one overload per
/// numeric type (<c>int</c>, <c>long</c>, <c>float</c>, <c>double</c>, <c>decimal</c>, and their
/// nullable forms) - <see cref="QueryableAsyncExtensions"/> closes the generic definition over the
/// numeric type it operates on. For <c>Average</c> that type parameter is the input type; the LINQ
/// result type (for example <c>double</c> for an <c>int</c> average) is carried separately by the
/// <c>ExecuteAsync&lt;TResult&gt;</c> call, not by the marker.
/// </remarks>
internal static class QueryTerminals
{
    // Internal marker methods - these are only used in expression trees, never called directly
    internal static List<T> ToListAsyncMarker<T>(IQueryable<T> source) =>
        throw new InvalidOperationException("This method should only be used in expression trees");

    internal static T[] ToArrayAsyncMarker<T>(IQueryable<T> source) =>
        throw new InvalidOperationException("This method should only be used in expression trees");

    internal static Dictionary<TKey, TElement> ToDictionaryAsyncMarker<TSource, TKey, TElement>(
        IQueryable<TSource> source, Expression<Func<TSource, TKey>> keySelector, Expression<Func<TSource, TElement>> elementSelector) where TKey : notnull =>
        throw new InvalidOperationException("This method should only be used in expression trees");

    internal static Dictionary<TKey, TSource> ToDictionaryAsyncMarker<TSource, TKey>(
        IQueryable<TSource> source, Expression<Func<TSource, TKey>> keySelector) where TKey : notnull =>
        throw new InvalidOperationException("This method should only be used in expression trees");

    internal static ILookup<TKey, TElement> ToLookupAsyncMarker<TSource, TKey, TElement>(
        IQueryable<TSource> source, Expression<Func<TSource, TKey>> keySelector, Expression<Func<TSource, TElement>> elementSelector) =>
        throw new InvalidOperationException("This method should only be used in expression trees");

    internal static ILookup<TKey, TSource> ToLookupAsyncMarker<TSource, TKey>(
        IQueryable<TSource> source, Expression<Func<TSource, TKey>> keySelector) =>
        throw new InvalidOperationException("This method should only be used in expression trees");

    internal static T FirstAsyncMarker<T>(IQueryable<T> source) =>
        throw new InvalidOperationException("This method should only be used in expression trees");

    internal static T FirstAsyncMarker<T>(IQueryable<T> source, Expression<Func<T, bool>> predicate) =>
        throw new InvalidOperationException("This method should only be used in expression trees");

    internal static T? FirstOrDefaultAsyncMarker<T>(IQueryable<T> source) =>
        throw new InvalidOperationException("This method should only be used in expression trees");

    internal static T? FirstOrDefaultAsyncMarker<T>(IQueryable<T> source, Expression<Func<T, bool>> predicate) =>
        throw new InvalidOperationException("This method should only be used in expression trees");

    internal static T LastAsyncMarker<T>(IQueryable<T> source) =>
        throw new InvalidOperationException("This method should only be used in expression trees");

    internal static T LastAsyncMarker<T>(IQueryable<T> source, Expression<Func<T, bool>> predicate) =>
        throw new InvalidOperationException("This method should only be used in expression trees");

    internal static T? LastOrDefaultAsyncMarker<T>(IQueryable<T> source) =>
        throw new InvalidOperationException("This method should only be used in expression trees");

    internal static T? LastOrDefaultAsyncMarker<T>(IQueryable<T> source, Expression<Func<T, bool>> predicate) =>
        throw new InvalidOperationException("This method should only be used in expression trees");

    internal static T SingleAsyncMarker<T>(IQueryable<T> source) =>
        throw new InvalidOperationException("This method should only be used in expression trees");

    internal static T SingleAsyncMarker<T>(IQueryable<T> source, Expression<Func<T, bool>> predicate) =>
        throw new InvalidOperationException("This method should only be used in expression trees");

    internal static T? SingleOrDefaultAsyncMarker<T>(IQueryable<T> source) =>
        throw new InvalidOperationException("This method should only be used in expression trees");

    internal static T? SingleOrDefaultAsyncMarker<T>(IQueryable<T> source, Expression<Func<T, bool>> predicate) =>
        throw new InvalidOperationException("This method should only be used in expression trees");

    internal static int CountAsyncMarker<T>(IQueryable<T> source) =>
        throw new InvalidOperationException("This method should only be used in expression trees");

    internal static int CountAsyncMarker<T>(IQueryable<T> source, Expression<Func<T, bool>> predicate) =>
        throw new InvalidOperationException("This method should only be used in expression trees");

    internal static long LongCountAsyncMarker<T>(IQueryable<T> source) =>
        throw new InvalidOperationException("This method should only be used in expression trees");

    internal static long LongCountAsyncMarker<T>(IQueryable<T> source, Expression<Func<T, bool>> predicate) =>
        throw new InvalidOperationException("This method should only be used in expression trees");

    internal static bool AnyAsyncMarker<T>(IQueryable<T> source) =>
        throw new InvalidOperationException("This method should only be used in expression trees");

    internal static bool AnyAsyncMarker<T>(IQueryable<T> source, Expression<Func<T, bool>> predicate) =>
        throw new InvalidOperationException("This method should only be used in expression trees");

    internal static bool AllAsyncMarker<T>(IQueryable<T> source, Expression<Func<T, bool>> predicate) =>
        throw new InvalidOperationException("This method should only be used in expression trees");

    internal static bool ContainsAsyncMarker<T>(IQueryable<T> source, T item) =>
        throw new InvalidOperationException("This method should only be used in expression trees");

    internal static T ElementAtAsyncMarker<T>(IQueryable<T> source, int index) =>
        throw new InvalidOperationException("This method should only be used in expression trees");

    internal static T? ElementAtOrDefaultAsyncMarker<T>(IQueryable<T> source, int index) =>
        throw new InvalidOperationException("This method should only be used in expression trees");

    // Sum/Average markers - a single unconstrained generic definition per shape. These bodies are
    // never executed (the whole point of a marker method), so there's no need for one overload
    // per numeric type: the provider dispatch table matches on the open generic method definition
    // and the model records only the terminal operation plus the input (selector/element) type.
    //
    // Sum's result type equals its input type, so its type parameter serves both roles. Average's
    // result type differs from its input (int/long average to double), so its generic parameter is
    // the numeric *input* type; QueryableAsyncExtensions.AverageAsync supplies the LINQ result type
    // separately through the ExecuteAsync<TResult> call that materializes the aggregate.
    internal static TResult SumAsyncMarker<TResult>(IQueryable<TResult> source) =>
        throw new InvalidOperationException("This method should only be used in expression trees");

    internal static TResult SumAsyncMarker<TSource, TResult>(IQueryable<TSource> source, Expression<Func<TSource, TResult>> selector) =>
        throw new InvalidOperationException("This method should only be used in expression trees");

    internal static TInput AverageAsyncMarker<TSource, TInput>(IQueryable<TSource> source) =>
        throw new InvalidOperationException("This method should only be used in expression trees");

    internal static TInput AverageAsyncMarker<TSource, TInput>(IQueryable<TSource> source, Expression<Func<TSource, TInput>> selector) =>
        throw new InvalidOperationException("This method should only be used in expression trees");

    // Min/Max markers
    internal static T? MinAsyncMarker<T>(IQueryable<T> source) =>
        throw new InvalidOperationException("This method should only be used in expression trees");

    internal static TResult? MinAsyncMarker<T, TResult>(IQueryable<T> source, Expression<Func<T, TResult>> selector) =>
        throw new InvalidOperationException("This method should only be used in expression trees");

    internal static T? MaxAsyncMarker<T>(IQueryable<T> source) =>
        throw new InvalidOperationException("This method should only be used in expression trees");

    internal static TResult? MaxAsyncMarker<T, TResult>(IQueryable<T> source, Expression<Func<T, TResult>> selector) =>
        throw new InvalidOperationException("This method should only be used in expression trees");
}
