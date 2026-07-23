// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph;

/// <summary>
/// Marks a domain value as a graph node or relationship.
/// </summary>
/// <remarks>
/// This interface deliberately has no members. CVOYA Graph does not expose provider element
/// identity, a graph reference, relationship endpoints, or relationship direction through
/// <see cref="IEntity"/>. Domain keys are optional properties configured with
/// <see cref="PropertyAttribute.IsKey"/>.
/// </remarks>
public interface IEntity
{
}
