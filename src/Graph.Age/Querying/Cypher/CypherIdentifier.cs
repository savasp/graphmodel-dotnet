// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Querying.Cypher;

/// <summary>
/// Validates and escapes Cypher identifiers (labels, relationship types, property names, and
/// database names) that are not known at compile time.
/// </summary>
/// <remarks>
/// <para>
/// Cypher parameters (<c>$param</c>) can only ever be substituted for literal <em>values</em> — the
/// driver has no mechanism to parameterize identifiers such as labels, relationship types, property
/// names, or database names. Any such identifier that originates from a caller (for example,
/// <see cref="Graph.DynamicNode.Labels"/> or <see cref="Graph.DynamicRelationship.Type"/>) must
/// therefore be validated and escaped before it is interpolated into Cypher text.
/// </para>
/// <para>
/// Escaping uses Cypher's backtick-quoting (<c>`identifier`</c>), which permits any character except
/// a backtick, and a backtick is escaped by doubling it (<c>`</c> becomes <c>``</c>) — the same rule
/// Cypher itself uses when un-escaping. Naively wrapping a value in backticks without doubling
/// embedded backticks is not a fix: an attacker-supplied value such as <c>``` MATCH (n) DETACH DELETE n //</c>
/// would still break out of the identifier position.
/// </para>
/// <para>
/// Validation is applied before escaping and rejects values that are empty, whitespace-only, or that
/// contain control characters (including newlines and NUL) — these are never legitimate in a label,
/// type, property name, or database name, and their presence in the input is a strong signal that the
/// value was never intended to be an identifier. Where the caller can reasonably reject bad input
/// instead of merely escaping it (dynamic-entity schema validation, for instance), rejection is
/// preferred: escaping is defense-in-depth, not a license to accept arbitrary values.
/// </para>
/// </remarks>
internal static class CypherIdentifier
{
    private static readonly HashSet<string> ReservedKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "ADD", "ALL", "AND", "ANY", "AS", "ASC", "ASCENDING", "ASSERT", "BY", "CALL", "CASE", "COMMIT",
        "CONSTRAINT", "CONTAINS", "CREATE", "CSV", "CURRENT", "DELETE", "DESC", "DESCENDING",
        "DETACH", "DISTINCT", "DO", "DROP", "EACH", "ELSE", "END", "ENDS", "EXISTS", "EXPLAIN", "FALSE",
        "FOREACH", "FOR", "GRANT", "GRAPH", "HEADERS", "HOME", "IN", "INDEX", "IS", "JOIN",
        "KEY", "LABEL", "LIMIT", "LOAD", "LOOKUP", "MANAGEMENT", "MATCH", "MERGE", "NODE",
        "MANDATORY", "NOT", "NULL", "OF", "ON", "ONLY", "OPTIONAL", "OPTIONS", "OR", "ORDER", "PASSWORD",
        "PATH", "PATHS", "PERIODIC", "PRIMARY", "PRIVILEGES", "READ", "REMOVE", "RENAME",
        "REQUIRE", "RETURN", "REVOKE", "ROLE", "ROLES", "ROW", "ROWS", "SCALAR", "SCAN", "SECONDARY",
        "SEEK", "SET", "SHOW", "SKIP", "START", "STARTS", "STATUS", "STOP", "SUPPORTED", "TERMINATE",
        "THEN", "TO", "TRANSACTION", "TRANSACTIONS", "TRAVERSE", "TRIM", "TRUE", "UNION",
        "UNIQUE", "UNWIND", "USE", "USER", "USERS", "USING", "WAIT", "WHEN", "WHERE", "WITH",
        "WITHOUT", "WRITE", "XOR", "YIELD",
    };

    /// <summary>
    /// Validates <paramref name="identifier"/> and returns it wrapped in backticks, with any embedded
    /// backtick doubled, suitable for direct interpolation into Cypher text as a label, relationship
    /// type, property name, or database name.
    /// </summary>
    /// <param name="identifier">The identifier to validate and escape.</param>
    /// <param name="kind">
    /// A short description of what <paramref name="identifier"/> represents (for example "node label"),
    /// used only to produce a precise error message.
    /// </param>
    /// <returns>The identifier, backtick-quoted and safe to interpolate into Cypher text.</returns>
    /// <exception cref="GraphException">
    /// <paramref name="identifier"/> is null, empty, whitespace-only, or contains a control character.
    /// </exception>
    public static string Escape(string? identifier, string kind = "identifier")
    {
        Validate(identifier, kind);
        return $"`{identifier!.Replace("`", "``")}`";
    }

    /// <summary>
    /// Validates <paramref name="identifier"/> and returns it backtick-quoted only when it is not a
    /// plain, non-reserved Cypher symbolic name (an ASCII letter or underscore followed by ASCII
    /// letters, digits, or underscores). Use this on read-path rendering, where ordinary identifiers
    /// must appear unquoted so generated Cypher stays byte-stable.
    /// </summary>
    /// <param name="identifier">The identifier to validate and, when needed, escape.</param>
    /// <param name="kind">
    /// A short description of what <paramref name="identifier"/> represents (for example "node label"),
    /// used only to produce a precise error message.
    /// </param>
    /// <returns>The identifier, backtick-quoted when needed, safe to interpolate into Cypher text.</returns>
    /// <exception cref="GraphException">
    /// <paramref name="identifier"/> is null, empty, whitespace-only, or contains a control character.
    /// </exception>
    public static string EscapeIfNeeded(string? identifier, string kind = "identifier")
    {
        Validate(identifier, kind);
        return IsPlainSymbolicName(identifier!) && !ReservedKeywords.Contains(identifier!)
            ? identifier!
            : $"`{identifier!.Replace("`", "``")}`";
    }

    private static bool IsPlainSymbolicName(string identifier)
    {
        if (!char.IsAsciiLetter(identifier[0]) && identifier[0] != '_')
        {
            return false;
        }

        for (var i = 1; i < identifier.Length; i++)
        {
            if (!char.IsAsciiLetterOrDigit(identifier[i]) && identifier[i] != '_')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Validates that <paramref name="identifier"/> is an acceptable Cypher identifier without
    /// escaping it. Use this when the identifier will be embedded as one of several colon-joined
    /// labels (for example <c>n:`A`:`B`</c>) rather than a single backtick-quoted token.
    /// </summary>
    /// <param name="identifier">The identifier to validate.</param>
    /// <param name="kind">
    /// A short description of what <paramref name="identifier"/> represents (for example "node label"),
    /// used only to produce a precise error message.
    /// </param>
    /// <exception cref="GraphException">
    /// <paramref name="identifier"/> is null, empty, whitespace-only, or contains a control character.
    /// </exception>
    public static void Validate(string? identifier, string kind = "identifier")
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new GraphException($"A Cypher {kind} cannot be null, empty, or whitespace.");
        }

        foreach (var c in identifier.Where(char.IsControl))
        {
            throw new GraphException(
                $"The Cypher {kind} '{identifier}' contains a control character (U+{(int)c:X4}), which is not allowed.");
        }
    }

    /// <summary>
    /// Validates and escapes each of <paramref name="identifiers"/>, joining them with <c>:</c> so the
    /// result can be used directly as a colon-separated label list (for example <c>`A`:`B`</c>).
    /// </summary>
    /// <param name="identifiers">The labels to validate and escape.</param>
    /// <param name="kind">
    /// A short description of what the identifiers represent (for example "node label"), used only to
    /// produce a precise error message.
    /// </param>
    /// <returns>The colon-joined, backtick-quoted labels.</returns>
    /// <exception cref="GraphException">
    /// Any entry in <paramref name="identifiers"/> is null, empty, whitespace-only, or contains a
    /// control character.
    /// </exception>
    public static string EscapeLabels(IEnumerable<string> identifiers, string kind = "node label")
    {
        return string.Join(":", identifiers.Select(label => Escape(label, kind)));
    }
}
