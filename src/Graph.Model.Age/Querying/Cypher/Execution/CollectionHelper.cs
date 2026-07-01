// Copyright 2025 Savas Parastatidis

namespace Cvoya.Graph.Model.Age.Querying.Cypher.Execution;

using Cvoya.Graph.Model.Serialization;

/// <summary>
/// Helper for converting lists of scalar results to the expected return type.
/// </summary>
internal static class CollectionHelper
{
    /// <summary>
    /// Converts a list of scalar values to either a single value or a collection,
    /// depending on the target type T.
    /// </summary>
    /// <typeparam name="T">
    /// The expected return type. Can be either a single scalar type (e.g., int, long)
    /// used by CountAsync/SumAsync, or a collection type (e.g., List&lt;int&gt;, int[])
    /// used by ToListAsync. The dual role is driven by whether T implements ICollection.
    /// </typeparam>
    /// <param name="items">The list of raw scalar values from the query result.</param>
    /// <param name="elementType">The CLR type of each individual element in the list.</param>
    public static T? ToListOrSingle<T>(List<object?> items, Type elementType)
    {
        if (CollectionTypeHelper.IsCollectionType(typeof(T)))
        {
            // Return as collection
            return CollectionTypeHelper.ConvertToCollectionType<T>(items!, elementType);
        }

        // Return single value (e.g., for CountAsync, AnyAsync, etc.)
        if (items.Count == 0)
            return default;

        var val = items[0];
        if (val is null) return default;

        // Convert to target type if needed
        if (typeof(T).IsAssignableFrom(val.GetType()))
            return (T)val;

        // Try ChangeType
        try
        {
            return (T)Convert.ChangeType(val, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
        }
        catch
        {
            return (T)val;
        }
    }
}
