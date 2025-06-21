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

internal static class QueryableAsyncExtensionsMarkers
{
    // Internal marker methods - these are only used in expression trees, never called directly
    internal static List<T> ToListAsyncMarker<T>(IQueryable<T> source) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static T[] ToArrayAsyncMarker<T>(IQueryable<T> source) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static Dictionary<TKey, TElement> ToDictionaryAsyncMarker<TSource, TKey, TElement>(
        IQueryable<TSource> source, Expression<Func<TSource, TKey>> keySelector, Expression<Func<TSource, TElement>> elementSelector) where TKey : notnull =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static Dictionary<TKey, TSource> ToDictionaryAsyncMarker<TSource, TKey>(
        IQueryable<TSource> source, Expression<Func<TSource, TKey>> keySelector) where TKey : notnull =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static ILookup<TKey, TElement> ToLookupAsyncMarker<TSource, TKey, TElement>(
        IQueryable<TSource> source, Expression<Func<TSource, TKey>> keySelector, Expression<Func<TSource, TElement>> elementSelector) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static ILookup<TKey, TSource> ToLookupAsyncMarker<TSource, TKey>(
        IQueryable<TSource> source, Expression<Func<TSource, TKey>> keySelector) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static T FirstAsyncMarker<T>(IQueryable<T> source) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static T FirstAsyncMarker<T>(IQueryable<T> source, Expression<Func<T, bool>> predicate) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static T? FirstOrDefaultAsyncMarker<T>(IQueryable<T> source) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static T? FirstOrDefaultAsyncMarker<T>(IQueryable<T> source, Expression<Func<T, bool>> predicate) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static T LastAsyncMarker<T>(IQueryable<T> source) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static T LastAsyncMarker<T>(IQueryable<T> source, Expression<Func<T, bool>> predicate) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static T? LastOrDefaultAsyncMarker<T>(IQueryable<T> source) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static T? LastOrDefaultAsyncMarker<T>(IQueryable<T> source, Expression<Func<T, bool>> predicate) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static T SingleAsyncMarker<T>(IQueryable<T> source) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static T SingleAsyncMarker<T>(IQueryable<T> source, Expression<Func<T, bool>> predicate) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static T? SingleOrDefaultAsyncMarker<T>(IQueryable<T> source) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static T? SingleOrDefaultAsyncMarker<T>(IQueryable<T> source, Expression<Func<T, bool>> predicate) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static int CountAsyncMarker<T>(IQueryable<T> source) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static int CountAsyncMarker<T>(IQueryable<T> source, Expression<Func<T, bool>> predicate) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static long LongCountAsyncMarker<T>(IQueryable<T> source) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static long LongCountAsyncMarker<T>(IQueryable<T> source, Expression<Func<T, bool>> predicate) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static bool AnyAsyncMarker<T>(IQueryable<T> source) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static bool AnyAsyncMarker<T>(IQueryable<T> source, Expression<Func<T, bool>> predicate) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static bool AllAsyncMarker<T>(IQueryable<T> source, Expression<Func<T, bool>> predicate) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static bool ContainsAsyncMarker<T>(IQueryable<T> source, T item) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static T ElementAtAsyncMarker<T>(IQueryable<T> source, int index) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static T? ElementAtOrDefaultAsyncMarker<T>(IQueryable<T> source, int index) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    // Sum markers for all numeric types
    internal static int SumAsyncMarker(IQueryable<int> source) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static int? SumAsyncMarker(IQueryable<int?> source) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static long SumAsyncMarker(IQueryable<long> source) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static long? SumAsyncMarker(IQueryable<long?> source) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static float SumAsyncMarker(IQueryable<float> source) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static float? SumAsyncMarker(IQueryable<float?> source) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static double SumAsyncMarker(IQueryable<double> source) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static double? SumAsyncMarker(IQueryable<double?> source) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static decimal SumAsyncMarker(IQueryable<decimal> source) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static decimal? SumAsyncMarker(IQueryable<decimal?> source) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    // Sum with selector markers
    internal static int SumAsyncMarker<T>(IQueryable<T> source, Expression<Func<T, int>> selector) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static int? SumAsyncMarker<T>(IQueryable<T> source, Expression<Func<T, int?>> selector) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static long SumAsyncMarker<T>(IQueryable<T> source, Expression<Func<T, long>> selector) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static long? SumAsyncMarker<T>(IQueryable<T> source, Expression<Func<T, long?>> selector) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static float SumAsyncMarker<T>(IQueryable<T> source, Expression<Func<T, float>> selector) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static float? SumAsyncMarker<T>(IQueryable<T> source, Expression<Func<T, float?>> selector) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static double SumAsyncMarker<T>(IQueryable<T> source, Expression<Func<T, double>> selector) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static double? SumAsyncMarker<T>(IQueryable<T> source, Expression<Func<T, double?>> selector) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static decimal SumAsyncMarker<T>(IQueryable<T> source, Expression<Func<T, decimal>> selector) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static decimal? SumAsyncMarker<T>(IQueryable<T> source, Expression<Func<T, decimal?>> selector) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    // Average markers
    internal static double AverageAsyncMarker(IQueryable<int> source) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static double? AverageAsyncMarker(IQueryable<int?> source) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static double AverageAsyncMarker(IQueryable<long> source) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static double? AverageAsyncMarker(IQueryable<long?> source) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static float AverageAsyncMarker(IQueryable<float> source) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static float? AverageAsyncMarker(IQueryable<float?> source) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static double AverageAsyncMarker(IQueryable<double> source) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static double? AverageAsyncMarker(IQueryable<double?> source) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static decimal AverageAsyncMarker(IQueryable<decimal> source) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static decimal? AverageAsyncMarker(IQueryable<decimal?> source) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    // Average with selector markers
    internal static double AverageAsyncMarker<T>(IQueryable<T> source, Expression<Func<T, int>> selector) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static double? AverageAsyncMarker<T>(IQueryable<T> source, Expression<Func<T, int?>> selector) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static double AverageAsyncMarker<T>(IQueryable<T> source, Expression<Func<T, long>> selector) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static double? AverageAsyncMarker<T>(IQueryable<T> source, Expression<Func<T, long?>> selector) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static float AverageAsyncMarker<T>(IQueryable<T> source, Expression<Func<T, float>> selector) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static float? AverageAsyncMarker<T>(IQueryable<T> source, Expression<Func<T, float?>> selector) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static double AverageAsyncMarker<T>(IQueryable<T> source, Expression<Func<T, double>> selector) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static double? AverageAsyncMarker<T>(IQueryable<T> source, Expression<Func<T, double?>> selector) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static decimal AverageAsyncMarker<T>(IQueryable<T> source, Expression<Func<T, decimal>> selector) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static decimal? AverageAsyncMarker<T>(IQueryable<T> source, Expression<Func<T, decimal?>> selector) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    // Min/Max markers
    internal static T? MinAsyncMarker<T>(IQueryable<T> source) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static TResult? MinAsyncMarker<T, TResult>(IQueryable<T> source, Expression<Func<T, TResult>> selector) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static T? MaxAsyncMarker<T>(IQueryable<T> source) =>
        throw new NotImplementedException("This method should only be used in expression trees");

    internal static TResult? MaxAsyncMarker<T, TResult>(IQueryable<T> source, Expression<Func<T, TResult>> selector) =>
        throw new NotImplementedException("This method should only be used in expression trees");
}