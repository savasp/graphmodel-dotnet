// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph;

/// <summary>
/// Represents a sorted graph queryable that supports additional ordering operations
/// </summary>
/// <typeparam name="T">The type of elements in the graph queryable</typeparam>
public interface IOrderedGraphQueryable<out T> : IGraphQueryable<T>, IOrderedQueryable<T>
{
}
