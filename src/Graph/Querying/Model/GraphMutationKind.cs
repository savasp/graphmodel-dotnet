// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying;

/// <summary>Identifies a provider-neutral set-based graph mutation.</summary>
public enum GraphMutationKind
{
    /// <summary>Updates mapped properties on the selected graph elements.</summary>
    Update,

    /// <summary>Deletes the selected graph elements.</summary>
    Delete,
}
