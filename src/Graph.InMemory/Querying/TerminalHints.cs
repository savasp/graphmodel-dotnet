// Copyright CVOYA. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.InMemory.Querying;

using System.Linq.Expressions;

/// <summary>
/// Execution details the shared query model deliberately does not carry: whether the terminal
/// was the <c>...OrDefault</c> form. The model folds both forms into one
/// <c>TerminalOperation</c>; the outermost method call in the original expression still knows.
/// </summary>
/// <param name="OrDefault">Whether the outermost terminal is an <c>...OrDefault</c> form.</param>
internal sealed record TerminalHints(bool OrDefault)
{
    /// <summary>Reads the hints off the outermost call of the query expression.</summary>
    public static TerminalHints From(Expression expression) =>
        new(expression is MethodCallExpression call &&
            call.Method.Name.Contains("OrDefault", StringComparison.Ordinal));
}
