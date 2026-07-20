// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph;

/// <summary>
/// Represents the foundation for all entities in the graph model.
/// This is the base interface for both nodes and relationships.
/// </summary>
public interface IEntity
{
    /// <summary>
    /// Gets the identifier used by the transitional ID-based graph APIs.
    /// </summary>
    /// <remarks>
    /// This property is ordinary mapped data for schema purposes. It is not a domain key unless the
    /// implementing property explicitly declares <see cref="PropertyAttribute.IsKey"/>. Domain keys do not
    /// represent provider-native graph element identity and are not implicit mutation targets.
    /// </remarks>
    string Id { get; init; }
}
