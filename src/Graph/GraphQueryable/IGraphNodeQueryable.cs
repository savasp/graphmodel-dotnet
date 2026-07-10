// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph;

/// <summary>
/// Non-generic base interface for node queryables.
/// </summary>
/// <remarks>
/// Obsolete: node/relationship queryables are unified as <see cref="IGraphQueryable{T}"/>;
/// graph operators are gated by generic constraints instead of a receiver interface hierarchy.
/// This alias is kept for one release to ease migration and will be removed afterwards.
/// </remarks>
[Obsolete("Use IGraphQueryable<T> with a 'where T : INode' constraint instead. This alias will be removed in a future release.")]
public interface IGraphNodeQueryable : IGraphQueryable
{
}