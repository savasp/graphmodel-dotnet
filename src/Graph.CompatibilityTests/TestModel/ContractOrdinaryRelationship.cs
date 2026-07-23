// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

/// <summary>
/// A relationship whose ordinary domain properties deliberately reuse former structural names.
/// </summary>
[Relationship("CONTRACT_ORDINARY_RELATIONSHIP")]
public sealed record ContractOrdinaryRelationship : Relationship
{
    /// <summary>Gets or sets an ordinary, non-unique domain identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets ordinary domain data independent of physical path orientation.</summary>
    public RelationshipDirection Direction { get; set; }

    /// <summary>Gets or sets the per-test marker used to isolate contract rows.</summary>
    public string Marker { get; set; } = string.Empty;
}
