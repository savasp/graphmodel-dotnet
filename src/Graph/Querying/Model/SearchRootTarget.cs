// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying;

/// <summary>
/// Identifies which graph element family a <see cref="SearchRoot"/> searches.
/// </summary>
public enum SearchRootTarget
{
    /// <summary>
    /// Search node full-text indexes.
    /// </summary>
    Nodes,

    /// <summary>
    /// Search relationship full-text indexes.
    /// </summary>
    Relationships,

    /// <summary>
    /// Search across node and relationship full-text indexes.
    /// </summary>
    Entities,
}
