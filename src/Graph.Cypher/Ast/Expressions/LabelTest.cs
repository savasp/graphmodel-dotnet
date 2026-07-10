// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph.Cypher.Internal;

namespace Cvoya.Graph.Cypher.Ast.Expressions;

/// <summary>
/// Represents a label predicate against a target expression.
/// </summary>
public sealed record LabelTest : CypherExpression
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LabelTest"/> class.
    /// </summary>
    /// <param name="target">The expression to test.</param>
    /// <param name="labels">The labels that must match.</param>
    public LabelTest(CypherExpression target, IReadOnlyList<string> labels)
    {
        ArgumentNullException.ThrowIfNull(target);

        Target = target;
        Labels = ArgumentValidation.RequiredStringList(labels, nameof(labels));
    }

    /// <summary>
    /// Gets the expression to test.
    /// </summary>
    public CypherExpression Target { get; }

    /// <summary>
    /// Gets the labels that must match.
    /// </summary>
    public IReadOnlyList<string> Labels { get; }
}
