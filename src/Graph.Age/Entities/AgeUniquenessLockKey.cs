// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Entities;

using System.Buffers.Binary;
using System.Collections;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Derives the PostgreSQL advisory-lock identifiers that serialize AGE's application-level
/// uniqueness enforcement.
/// </summary>
/// <remarks>
/// AGE has no physical unique constraints, so a create/update proves uniqueness by probing for a
/// duplicate and then writing. Without a lock spanning both steps two concurrent transactions can
/// each observe no duplicate and each commit the same value. Every writer takes a transaction-scoped
/// advisory lock keyed by the constraint instance it is about to claim before probing, so conflicting
/// writers run one at a time and the loser's probe (under the READ COMMITTED default, which takes a
/// fresh snapshot per statement) sees the winner's committed row.
/// <para>
/// Keys must be stable across processes and cultures - two peers enforcing the same constraint have
/// to compute the same number - so the identity is hashed with SHA-256 rather than
/// <see cref="string.GetHashCode()"/>, which is randomized per process, and every component is
/// rendered invariantly.
/// </para>
/// </remarks>
internal static class AgeUniquenessLockKey
{
    /// <summary>Identifies a node-entity constraint in <see cref="Compute"/>'s entity-kind component.</summary>
    internal const string NodeEntityKind = "node";

    /// <summary>Identifies a relationship-entity constraint in <see cref="Compute"/>'s entity-kind component.</summary>
    internal const string RelationshipEntityKind = "relationship";

    /// <summary>Identifies the graph-wide root-node id claim in <see cref="Compute"/>'s constraint component.</summary>
    internal const string RootNodeIdConstraint = "root-node-id";

    /// <summary>
    /// Computes the advisory-lock identifier claiming <paramref name="nodeId"/> as a root-node id.
    /// </summary>
    /// <remarks>
    /// The label component is deliberately empty: the claim spans every label, so two writers
    /// creating the same id under different labels must contend on one key. An empty label cannot
    /// collide with a real one because <see cref="BuildConstraintIdentity"/> length-frames each
    /// component and a node label is never empty.
    /// </remarks>
    internal static long ComputeRootNodeId(string graphName, string nodeId) =>
        Compute(graphName, NodeEntityKind, label: string.Empty, RootNodeIdConstraint, [nodeId]);

    /// <summary>
    /// Computes the advisory-lock identifier for one constraint instance. Scoping by
    /// <paramref name="graphName"/> keeps graphs in the same PostgreSQL database (advisory locks are
    /// database-wide, not schema-scoped) from blocking each other, and scoping by
    /// <paramref name="entityKind"/> keeps a node label from colliding with an identically named
    /// relationship type.
    /// </summary>
    internal static long Compute(
        string graphName,
        string entityKind,
        string label,
        string constraint,
        IReadOnlyList<object?> values)
    {
        var identity = new StringBuilder();
        AppendText(identity, 'g', graphName);
        AppendText(identity, 'e', entityKind);
        identity.Append(BuildConstraintIdentity(label, constraint, values));

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity.ToString()));

        // pg_advisory_xact_lock's single-argument overload takes a signed bigint, so the first eight
        // hash bytes are read as one. Truncating 256 bits to 64 admits collisions, which cost only
        // spurious serialization between unrelated constraints - never a missed lock - because the
        // probe still decides correctness.
        return BinaryPrimitives.ReadInt64BigEndian(hash);
    }

    /// <summary>
    /// Builds the canonical identity shared by advisory locking and same-batch collision detection.
    /// Values are framed so embedded separator characters cannot forge component boundaries, maps
    /// are key-ordered, and numeric CLR types that AGE compares as the same number share an identity.
    /// </summary>
    internal static string BuildConstraintIdentity(
        string label,
        string constraint,
        IReadOnlyList<object?> values)
    {
        var identity = new StringBuilder();
        AppendText(identity, 'l', label);
        AppendText(identity, 'c', constraint);
        AppendCount(identity, 'v', values.Count);
        foreach (var value in values)
        {
            AppendValue(identity, value);
        }

        return identity.ToString();
    }

    private static void AppendValue(StringBuilder identity, object? value)
    {
        switch (value)
        {
            case null:
                identity.Append('n');
                return;
            case string text:
                AppendText(identity, 's', text);
                return;
            case char character:
                // System.Text.Json sends a char to AGE as a one-character string.
                AppendText(identity, 's', character.ToString());
                return;
            case bool boolean:
                identity.Append(boolean ? "b1" : "b0");
                return;
            case sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal:
                AppendText(identity, 'd', CanonicalizeNumber(value));
                return;
            case IDictionary dictionary:
                AppendDictionary(identity, dictionary);
                return;
            case IEnumerable sequence:
                var items = sequence.Cast<object?>().ToArray();
                AppendCount(identity, 'a', items.Length);
                foreach (var item in items)
                {
                    AppendValue(identity, item);
                }

                return;
            default:
                AppendText(identity, 's', Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
                return;
        }
    }

    private static void AppendDictionary(StringBuilder identity, IDictionary dictionary)
    {
        var entries = new List<(string Key, object? Value)>(dictionary.Count);
        foreach (DictionaryEntry entry in dictionary)
        {
            entries.Add((
                Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? string.Empty,
                entry.Value));
        }

        entries.Sort((left, right) => StringComparer.Ordinal.Compare(left.Key, right.Key));
        AppendCount(identity, 'm', entries.Count);
        foreach (var (key, entryValue) in entries)
        {
            AppendText(identity, 'k', key);
            AppendValue(identity, entryValue);
        }
    }

    private static string CanonicalizeNumber(object value)
    {
        var text = value switch
        {
            float single when float.IsNaN(single) => "NaN",
            float single when float.IsPositiveInfinity(single) => "Infinity",
            float single when float.IsNegativeInfinity(single) => "-Infinity",
            float single => single.ToString("R", CultureInfo.InvariantCulture),
            double number when double.IsNaN(number) => "NaN",
            double number when double.IsPositiveInfinity(number) => "Infinity",
            double number when double.IsNegativeInfinity(number) => "-Infinity",
            double number => number.ToString("R", CultureInfo.InvariantCulture),
            decimal number => number.ToString("G29", CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture)!,
        };

        if (text is "NaN" or "Infinity" or "-Infinity")
        {
            return text;
        }

        var negative = text[0] == '-';
        var unsigned = negative || text[0] == '+' ? text[1..] : text;
        var exponentIndex = unsigned.IndexOfAny(['e', 'E']);
        var exponent = exponentIndex < 0
            ? 0
            : int.Parse(unsigned[(exponentIndex + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        var significand = exponentIndex < 0 ? unsigned : unsigned[..exponentIndex];
        var decimalIndex = significand.IndexOf('.');
        if (decimalIndex >= 0)
        {
            exponent -= significand.Length - decimalIndex - 1;
            significand = significand.Remove(decimalIndex, 1);
        }

        significand = significand.TrimStart('0');
        if (significand.Length == 0)
        {
            return "0";
        }

        while (significand.EndsWith('0'))
        {
            significand = significand[..^1];
            exponent++;
        }

        return $"{(negative ? "-" : string.Empty)}{significand}e{exponent.ToString(CultureInfo.InvariantCulture)}";
    }

    private static void AppendText(StringBuilder identity, char tag, string value)
    {
        identity.Append(tag)
            .Append(value.Length.ToString(CultureInfo.InvariantCulture))
            .Append(':')
            .Append(value);
    }

    private static void AppendCount(StringBuilder identity, char tag, int count)
    {
        identity.Append(tag)
            .Append(count.ToString(CultureInfo.InvariantCulture))
            .Append(':');
    }
}
