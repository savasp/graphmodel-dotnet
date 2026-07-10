// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Cypher.Ast.Expressions;

/// <summary>
/// Represents a literal Cypher value.
/// </summary>
/// <param name="Value">The literal value.</param>
public sealed record Literal(object? Value) : CypherExpression;
