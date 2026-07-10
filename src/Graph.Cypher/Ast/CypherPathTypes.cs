// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Cypher.Ast;

/// <summary>Carries the CLR types needed to materialize a decomposed graph path.</summary>
/// <param name="Source">The source node type.</param>
/// <param name="Relationship">The relationship type.</param>
/// <param name="Target">The target node type.</param>
public sealed record CypherPathTypes(Type Source, Type Relationship, Type Target);
