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

namespace Cvoya.Graph.Model.Neo4j.Querying.Linq.Helpers;

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