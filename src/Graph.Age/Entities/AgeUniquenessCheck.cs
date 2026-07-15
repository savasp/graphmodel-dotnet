// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Entities;

/// <summary>A schema uniqueness probe that can run alone or as one command in an AGE batch.</summary>
internal sealed record AgeUniquenessCheck(
    string Cypher,
    IReadOnlyDictionary<string, object?> Parameters,
    string ErrorMessage,
    string ConstraintKey)
{
    /// <summary>
    /// Builds the key that identifies one constraint instance (label/type, constraint, and checked
    /// values). Two entities in the same subgraph create with equal keys would violate uniqueness
    /// against each other, which the database probes cannot see because both run before either
    /// entity is written.
    /// </summary>
    internal static string BuildConstraintKey(string label, string constraint, IReadOnlyList<object?> values) =>
        $"{label}\u001f{constraint}\u001f{System.Text.Json.JsonSerializer.Serialize(values)}";
}
