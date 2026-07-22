// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.InMemory;

/// <summary>The collision-free in-memory storage metadata for one complex collection.</summary>
internal sealed record StoredComplexCollection(
    string RelationshipType,
    Type ElementType,
    int Length,
    IReadOnlyList<int> NullIndexes);
