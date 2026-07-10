// Copyright CVOYA. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.InMemory;

/// <summary>
/// The at-rest form of one relationship: either a user relationship (linking nodes by their
/// caller-visible ids) or an internal complex-property edge (linking a parent record to an owned
/// value node by store key, marked with <paramref name="IsComplexProperty"/> for cascade cleanup).
/// </summary>
/// <param name="Id">The caller-visible opaque relationship id.</param>
/// <param name="Type">The relationship type (for complex-property edges, the property name or
/// its <c>[ComplexProperty]</c> override).</param>
/// <param name="StartNodeId">The logical start node id.</param>
/// <param name="EndNodeId">The logical end node id.</param>
/// <param name="Direction">The at-rest direction: <see cref="RelationshipDirection.Outgoing"/>
/// means the physical edge points start-to-end, <see cref="RelationshipDirection.Incoming"/>
/// end-to-start. Immutable after create.</param>
/// <param name="ActualType">The concrete CLR type for user relationships; null for
/// complex-property edges, which have no CLR relationship type.</param>
/// <param name="Properties">Snapshot of the relationship's simple properties.</param>
/// <param name="IsComplexProperty">True for an internal complex-property edge.</param>
/// <param name="SequenceNumber">Collection index for complex-property edges (0 for single
/// values); orders collection items on read.</param>
/// <param name="StartKey">Store key of the parent record for complex-property edges.</param>
/// <param name="EndKey">Store key of the owned value node for complex-property edges.</param>
internal sealed record RelationshipRecord(
    string Id,
    string Type,
    string StartNodeId,
    string EndNodeId,
    RelationshipDirection Direction,
    Type? ActualType,
    IReadOnlyDictionary<string, StoredProperty> Properties,
    bool IsComplexProperty,
    int SequenceNumber,
    Guid? StartKey = null,
    Guid? EndKey = null)
{
    /// <summary>
    /// Gets the id of the node the physical edge leaves from, per <see cref="Direction"/>.
    /// </summary>
    public string PhysicalSourceId => Direction == RelationshipDirection.Outgoing ? StartNodeId : EndNodeId;

    /// <summary>
    /// Gets the id of the node the physical edge arrives at, per <see cref="Direction"/>.
    /// </summary>
    public string PhysicalTargetId => Direction == RelationshipDirection.Outgoing ? EndNodeId : StartNodeId;
}
