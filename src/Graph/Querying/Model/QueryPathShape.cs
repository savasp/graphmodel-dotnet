// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying;

/// <summary>
/// Carries the CLR types required to materialize a decomposed graph path result.
/// </summary>
/// <param name="SourceType">The path source node type.</param>
/// <param name="RelationshipType">The path relationship type.</param>
/// <param name="TargetType">The path target node type.</param>
public sealed record QueryPathShape(Type SourceType, Type RelationshipType, Type TargetType);
