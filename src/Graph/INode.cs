// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph;

/// <summary>
/// Defines the contract for node entities in the graph model.
/// Nodes represent primary data entities that can be connected via relationships.
/// This interface serves as a marker interface that extends IEntity, 
/// signifying that implementing classes represent nodes rather than relationships.
/// </summary>
/// <remarks>
/// Implement this interface on classes that represent domain entities in your graph model.
/// Typically used with the <see cref="NodeAttribute"/> to define node metadata.
/// </remarks>
public interface INode : IEntity
{
    /// <summary>
    /// Gets the caller-visible graph labels for this node.
    /// Provider-owned infrastructure labels are not included.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This property is automatically populated by the graph provider when the node is
    /// retrieved from or saved to the database. The labels are derived from the <see cref="NodeAttribute"/>
    /// on the implementing type, or the type name if no attribute is present.
    /// </para>
    /// <para>
    /// This property enables polymorphic queries and filtering by label at runtime,
    /// complementing the compile-time type system.
    /// </para>
    /// <para>
    /// Do not set this property manually - it is managed by the graph provider.
    /// </para>
    /// </remarks>
    IReadOnlyList<string> Labels { get; }
}
