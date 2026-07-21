// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying.Commands;

/// <summary>Internal provider SPI for exact-endpoint relationship creation.</summary>
internal interface IGraphRelationshipCommandExecutionContext : IGraphCommandExecutionContext
{
    Task CreateRelationshipAsync(
        GraphEndpointIntent source,
        IRelationship relationship,
        GraphEndpointIntent target,
        RelationshipDirection direction,
        CancellationToken cancellationToken);
}
