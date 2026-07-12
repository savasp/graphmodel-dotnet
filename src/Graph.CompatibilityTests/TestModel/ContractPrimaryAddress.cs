// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

/// <summary>A domain relationship whose type collides with the contract complex-address edge.</summary>
/// <param name="StartNodeId">The relationship start-node identifier.</param>
/// <param name="EndNodeId">The relationship end-node identifier.</param>
[Relationship("CONTRACT_PRIMARY_ADDRESS")]
public sealed record ContractPrimaryAddress(string StartNodeId, string EndNodeId)
    : Relationship(StartNodeId, EndNodeId);
