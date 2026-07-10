// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.InMemory;

/// <summary>
/// The at-rest form of one node. <paramref name="Key"/> is the store-internal identity (complex
/// property edges link parent to child by key, never by the caller-visible <paramref name="Id"/>,
/// which is unique only per label). Property values are isolated snapshots.
/// </summary>
/// <param name="Key">Store-internal identity of this record.</param>
/// <param name="Id">The caller-visible opaque entity id.</param>
/// <param name="Label">The primary label.</param>
/// <param name="Labels">All labels stored for the node.</param>
/// <param name="ActualType">The concrete CLR type the node was serialized from.</param>
/// <param name="Properties">Snapshot of the node's simple properties keyed by storage name.</param>
/// <param name="IsComplexValue">True for a decomposed complex-property value node owned by
/// another node; such records are never counted as user-deletable roots.</param>
internal sealed record NodeRecord(
    Guid Key,
    string Id,
    string Label,
    IReadOnlyList<string> Labels,
    Type ActualType,
    IReadOnlyDictionary<string, StoredProperty> Properties,
    bool IsComplexValue);
