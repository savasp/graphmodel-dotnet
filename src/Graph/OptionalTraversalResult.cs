// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph;

/// <summary>Represents one source row and the nullable target of an optional traversal.</summary>
/// <typeparam name="TEnd">The traversal target node type.</typeparam>
/// <param name="Source">The source node preserved by the optional traversal.</param>
/// <param name="Target">The matched target node, or <see langword="null"/> when no hop matched.</param>
public sealed record OptionalTraversalResult<TEnd>(INode Source, TEnd? Target)
    where TEnd : class, INode;
