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

namespace Cvoya.Graph.Model.Neo4j.Querying.Cypher;

/// <summary>
/// Validates and escapes Cypher identifiers (labels, relationship types, property names, and
/// database names) that are not known at compile time.
/// </summary>
/// <remarks>
/// <para>
/// Cypher parameters (<c>$param</c>) can only ever be substituted for literal <em>values</em> — the
/// driver has no mechanism to parameterize identifiers such as labels, relationship types, property
/// names, or database names. Any such identifier that originates from a caller (for example,
/// <see cref="Model.DynamicNode.Labels"/> or <see cref="Model.DynamicRelationship.Type"/>) must
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
