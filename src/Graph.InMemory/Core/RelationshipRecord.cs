// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.InMemory;

/// <summary>
/// The at-rest form of one relationship. <paramref name="Key"/> is the only relationship identity,
/// and every endpoint is represented by a non-null node record key. The optional
/// <paramref name="CompatibilityId"/> exists solely for transitional direct-ID APIs.
/// </summary>
/// <param name="Key">Store-internal identity of this relationship record.</param>
/// <param name="CompatibilityId">The transitional caller-visible relationship id, when present.</param>
/// <param name="Type">The relationship type (for complex-property edges, the property name or
/// its <c>[ComplexProperty]</c> override).</param>
/// <param name="StartKey">The first logical endpoint's node record key.</param>
/// <param name="EndKey">The second logical endpoint's node record key.</param>
/// <param name="Direction">The at-rest direction: <see cref="RelationshipDirection.Outgoing"/>
/// means the physical edge points start-to-end, <see cref="RelationshipDirection.Incoming"/>
/// end-to-start. Immutable after create.</param>
/// <param name="ActualType">The concrete CLR type for user relationships; null for
/// complex-property edges, which have no CLR relationship type.</param>
/// <param name="Properties">Snapshot of the relationship's simple properties.</param>
/// <param name="IsComplexProperty">True for an internal complex-property edge.</param>
/// <param name="SequenceNumber">Collection index for complex-property edges (0 for single
/// values); orders collection items on read.</param>
internal sealed record RelationshipRecord(
    Guid Key,
    string? CompatibilityId,
    string Type,
    Guid StartKey,
    Guid EndKey,
    RelationshipDirection Direction,
    Type? ActualType,
    IReadOnlyDictionary<string, StoredProperty> Properties,
    bool IsComplexProperty,
    int SequenceNumber)
{
    /// <summary>
    /// Gets the key of the node the physical edge leaves from, per <see cref="Direction"/>.
    /// </summary>
    public Guid PhysicalSourceKey => Direction == RelationshipDirection.Outgoing ? StartKey : EndKey;

    /// <summary>
    /// Gets the key of the node the physical edge arrives at, per <see cref="Direction"/>.
    /// </summary>
    public Guid PhysicalTargetKey => Direction == RelationshipDirection.Outgoing ? EndKey : StartKey;
}
