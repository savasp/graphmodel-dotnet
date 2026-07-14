// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph.Cypher.Ast.Expressions;

namespace Cvoya.Graph.Cypher;

/// <summary>
/// The subset of <see cref="CypherRenderer"/> services a dialect needs when it renders a clause
/// whose syntax it owns (for example, a full-text search clause via
/// <see cref="ICypherDialect.RenderFullTextSearch"/>). The surface is deliberately minimal;
/// members are added only as a dialect-owned render path requires them.
/// </summary>
public interface ICypherRenderContext
{
    /// <summary>Renders a Cypher expression to this dialect's text.</summary>
    /// <param name="expression">The expression to render.</param>
    string RenderExpression(CypherExpression expression);

    /// <summary>Renders a CLR value as an inline Cypher literal.</summary>
    /// <param name="value">The value to render.</param>
    string RenderLiteral(object? value);
}
