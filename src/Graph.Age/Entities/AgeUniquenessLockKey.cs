// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Entities;

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
    /// <summary>
    /// Separates identity components. ASCII unit separator, matching
    /// <see cref="AgeUniquenessCheck.BuildConstraintKey"/>, so no component boundary can be forged
    /// by a label or value that contains the separator in printable form.
    /// </summary>
    private const char ComponentSeparator = '\u001f';

    /// <summary>Identifies a node-entity constraint in <see cref="Compute"/>'s entity-kind component.</summary>
    internal const string NodeEntityKind = "node";

    /// <summary>Identifies a relationship-entity constraint in <see cref="Compute"/>'s entity-kind component.</summary>
    internal const string RelationshipEntityKind = "relationship";

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
        var identity = new StringBuilder()
            .Append(graphName).Append(ComponentSeparator)
            .Append(entityKind).Append(ComponentSeparator)
            .Append(label).Append(ComponentSeparator)
            .Append(constraint);

        foreach (var value in values)
        {
            identity.Append(ComponentSeparator).Append(Render(value));
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity.ToString()));

        // pg_advisory_xact_lock's single-argument overload takes a signed bigint, so the first eight
        // hash bytes are read as one. Truncating 256 bits to 64 admits collisions, which cost only
        // spurious serialization between unrelated constraints - never a missed lock - because the
        // probe still decides correctness.
        return BitConverter.ToInt64(hash, 0);
    }

    /// <summary>
    /// Renders one checked value so equal values always produce equal text regardless of the
    /// current culture. The type name is included so <c>1</c> and <c>"1"</c> hash differently, and
    /// a null value is distinguished from the empty string.
    /// </summary>
    private static string Render(object? value) => value switch
    {
        null => "null",
        string text => $"string{ComponentSeparator}{text}",
        IFormattable formattable =>
            $"{value.GetType().FullName}{ComponentSeparator}{formattable.ToString(null, CultureInfo.InvariantCulture)}",
        _ => $"{value.GetType().FullName}{ComponentSeparator}{value}",
    };
}
