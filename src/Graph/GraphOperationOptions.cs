// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph;

/// <summary>
/// Options that tune how a graph write operation is performed.
/// </summary>
public sealed record GraphOperationOptions
{
    /// <summary>
    /// Gets a value indicating how the endpoint nodes of a subgraph create are handled.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When <see langword="true"/>, each endpoint node is merged by <see cref="IEntity.Id"/>. A node
    /// with that id that already exists is reused <em>entirely as-is</em>: only its id is used to
    /// match, and both its simple properties and its existing complex-property subtrees are left
    /// untouched — the passed-in endpoint object's properties are ignored for a matched endpoint. An
    /// endpoint that does not yet exist is created with its full simple properties and its
    /// complex-property subtree. The connecting edge is always created.
    /// </para>
    /// <para>
    /// When <see langword="false"/> (the default), both endpoint nodes are created, and the whole
    /// operation fails atomically if an endpoint id already exists. This matches the create-only
    /// semantics of <see cref="IGraph.CreateNodeAsync{TNode}"/>.
    /// </para>
    /// </remarks>
    public bool CreateMissingEndpoints { get; init; }
}
