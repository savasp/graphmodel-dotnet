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

namespace Cvoya.Graph.Model.Age.Querying.Cypher.Execution;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using Npgsql.Age.Types;

/// <summary>
/// Materializes scalar (non-entity) results from AGE Cypher queries.
/// Handles type conversion from Agtype to CLR types for aggregation results
/// like count, sum, average, min, max, etc.
/// </summary>
internal static class ScalarResultMaterializer
{
    /// <summary>
    /// Type-dispatch dictionary mapping target CLR types to Agtype converters.
    /// Each converter attempts to extract a native Agtype value with a fallback.
    /// </summary>
    private static readonly Dictionary<Type, Func<Agtype, object?>> AgtypeConverters = new()
    {
        [typeof(string)] = ag => { try { return ag.GetString(); } catch { return ag.ToString(); } },
        [typeof(long)] = ag => { try { return ag.GetInt64(); } catch { return null; } },
        [typeof(long?)] = ag => { try { return ag.GetInt64(); } catch { return null; } },
        [typeof(int)] = ag =>
        {
            // count(*) in AGE returns bigint (Int64). Try Int32 first, then fall back to Int64 and cast.
            try { return ag.GetInt32(); }
            catch { try { return (int)ag.GetInt64(); } catch { return null; } }
        },
        [typeof(int?)] = ag =>
        {
            try { return ag.GetInt32(); }
            catch { try { return (int)ag.GetInt64(); } catch { return null; } }
        },
        [typeof(short)] = ag => { try { return ag.GetInt16(); } catch { return null; } },
        [typeof(short?)] = ag => { try { return ag.GetInt16(); } catch { return null; } },
        [typeof(double)] = ag => { try { return ag.GetDouble(); } catch { return null; } },
        [typeof(double?)] = ag => { try { return ag.GetDouble(); } catch { return null; } },
        [typeof(float)] = ag => { try { return ag.GetFloat(); } catch { return null; } },
        [typeof(float?)] = ag => { try { return ag.GetFloat(); } catch { return null; } },
        [typeof(decimal)] = ag => { try { return ag.GetDecimal(); } catch { return null; } },
        [typeof(decimal?)] = ag => { try { return ag.GetDecimal(); } catch { return null; } },
        [typeof(bool)] = ag => { try { return ag.GetBoolean(); } catch { return null; } },
        [typeof(bool?)] = ag => { try { return ag.GetBoolean(); } catch { return null; } },
        [typeof(byte)] = ag => { try { return ag.GetByte(); } catch { return null; } },

        // DateTime: AGE returns date/time as string; parse it via the Agtype string representation
        [typeof(DateTime)] = ag =>
        {
            try
            {
                var strVal = ag.ToString()?.Trim('"', ' ', '\'');
                if (DateTime.TryParse(strVal, out var dtVal))
                    return dtVal;
                return null;
            }
            catch { return null; }
        },
        [typeof(DateTime?)] = ag =>
        {
            try
            {
                var strVal = ag.ToString()?.Trim('"', ' ', '\'');
                if (DateTime.TryParse(strVal, out var dtVal))
                    return dtVal;
                return null;
            }
            catch { return null; }
        },
    };

    /// <summary>
    /// Reads scalar results from a query and materializes them as <typeparamref name="T"/>.
    /// </summary>
    public static async Task<T?> MaterializeAsync<T>(
        NpgsqlDataReader reader,
        Type elementType,
        CancellationToken cancellationToken,
        string? aggregationType = null)
    {
        var results = new List<object?>();

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            // Handle DBNull (e.g., SUM/AVG of empty set returns a single NULL row)
            if (await reader.IsDBNullAsync(0, cancellationToken).ConfigureAwait(false))
            {
                // Average of empty set should throw InvalidOperationException
                if (string.Equals(aggregationType, "Average", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Sequence contains no elements");
                }

                // Sum/Min/Max of empty set returns null, which defaults to 0/default
                results.Add(null);
                continue;
            }

            var agVal = reader.GetFieldValue<Agtype>(0);
            object? rawValue = null;

            // Look up the type converter from the dispatch dictionary (replaces 15-line if/else chain).
            if (AgtypeConverters.TryGetValue(elementType, out var converter))
            {
                rawValue = converter(agVal);
            }
            else
            {
                // Fallback: can't add null-forgiving, and try is necessary here
                try { rawValue = agVal.GetString() ?? agVal.ToString(); } catch { rawValue = agVal.ToString(); }
            }

            if (rawValue is null)
                rawValue = agVal.ToString();

            results.Add(rawValue);
        }

        return CollectionHelper.ToListOrSingle<T>(results, elementType);
    }
}
