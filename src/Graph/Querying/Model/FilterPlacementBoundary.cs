// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying;

/// <summary>Identifies the sequence boundary crossed by a later scope filter.</summary>
internal enum FilterPlacementBoundary
{
    Select,
    Traverse,
    Skip,
    Take,
}
