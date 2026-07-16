// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying;

/// <summary>Identifies a sequence operation combining two compatible graph queries.</summary>
public enum SetOperationKind
{
    /// <summary>Combines operands and removes duplicate results.</summary>
    Union,

    /// <summary>Appends the second operand while preserving duplicates.</summary>
    Concat,
}
