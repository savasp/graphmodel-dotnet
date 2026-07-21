// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying.Commands;

/// <summary>An endpoint operand for internal native-bound relationship creation.</summary>
internal abstract record GraphCommandEndpoint;

/// <summary>An endpoint selected by provider-native element identity.</summary>
internal sealed record SelectedGraphCommandEndpoint(SelectedGraphElement Element) : GraphCommandEndpoint;

/// <summary>An endpoint that must be created as part of the relationship operation.</summary>
internal sealed record NewGraphCommandEndpoint(INode Node) : GraphCommandEndpoint;

/// <summary>Distinguishes ordinary two-operand creation from explicit all-new self-loop creation.</summary>
internal enum GraphRelationshipCreationMode
{
    /// <summary>Creates each new endpoint operand independently, even when their values are equal.</summary>
    Standard,

    /// <summary>Creates one new node and connects it to itself.</summary>
    SelfLoop,
}
