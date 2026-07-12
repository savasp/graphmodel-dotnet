// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying.Linq;

using System.Linq.Expressions;


/// <summary>
/// Extension methods for async operations on graph queryables.
/// These are used by the expression tree to represent async operations.
/// </summary>
internal static class GraphQueryableAsyncExtensions
{
    public static Task<T> FirstAsync<T>(IQueryable<T> source, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("This method should not be called directly. It's for expression tree representation only.");
    }

    public static Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> source, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("This method should not be called directly. It's for expression tree representation only.");
    }

    public static Task<T> SingleAsync<T>(IQueryable<T> source, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("This method should not be called directly. It's for expression tree representation only.");
    }

    public static Task<T?> SingleOrDefaultAsync<T>(IQueryable<T> source, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("This method should not be called directly. It's for expression tree representation only.");
    }

    public static Task<int> CountAsync<T>(IQueryable<T> source, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("This method should not be called directly. It's for expression tree representation only.");
    }

    public static Task<long> LongCountAsync<T>(IQueryable<T> source, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("This method should not be called directly. It's for expression tree representation only.");
    }

    public static Task<bool> AnyAsync<T>(IQueryable<T> source, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("This method should not be called directly. It's for expression tree representation only.");
    }

    public static Task<bool> AllAsync<T>(IQueryable<T> source, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("This method should not be called directly. It's for expression tree representation only.");
    }

    public static Task<T> MinAsync<T>(IQueryable<T> source, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("This method should not be called directly. It's for expression tree representation only.");
    }

    public static Task<T> MaxAsync<T>(IQueryable<T> source, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("This method should not be called directly. It's for expression tree representation only.");
    }

    public static Task<List<T>> ToListAsync<T>(IQueryable<T> source, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("This method should not be called directly. It's for expression tree representation only.");
    }

    public static Task<T[]> ToArrayAsync<T>(IQueryable<T> source, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("This method should not be called directly. It's for expression tree representation only.");
    }

    public static Task<Dictionary<TKey, T>> ToDictionaryAsync<TKey, T>(
        IQueryable<T> source,
        Expression<Func<T, TKey>> keySelector,
        CancellationToken cancellationToken = default) where TKey : notnull
    {
        throw new InvalidOperationException("This method should not be called directly. It's for expression tree representation only.");
    }

    public static Task<HashSet<T>> ToHashSetAsync<T>(IQueryable<T> source, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("This method should not be called directly. It's for expression tree representation only.");
    }

    public static Task<bool> ContainsAsync<T>(IQueryable<T> source, T item, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("This method should not be called directly. It's for expression tree representation only.");
    }
}
