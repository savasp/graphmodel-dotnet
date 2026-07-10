// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph;

/// <summary>
/// Non-generic base interface for graph queryables.
/// Allows type checking and common operations without knowing the element type.
/// </summary>
public interface IGraphQueryable : IQueryable
{
}