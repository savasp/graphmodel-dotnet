// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Entities;

/// <summary>A schema uniqueness probe that can run alone or as one command in an AGE batch.</summary>
internal sealed record AgeUniquenessCheck(
    string Cypher,
    IReadOnlyDictionary<string, object?> Parameters,
    string ErrorMessage,
    string ConstraintKey);
