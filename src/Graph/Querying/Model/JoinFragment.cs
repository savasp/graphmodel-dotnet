// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying;

using System.Linq.Expressions;

/// <summary>
/// Describes a provider-independent equijoin between a graph query and another graph root.
/// </summary>
public sealed record JoinFragment
{
    /// <summary>
    /// Initializes a new join description.
    /// </summary>
    /// <param name="innerRoot">The joined query root.</param>
    /// <param name="outerKeySelector">The outer key selector.</param>
    /// <param name="innerKeySelector">The inner key selector.</param>
    /// <param name="resultSelector">The result selector.</param>
    public JoinFragment(
        QueryRoot innerRoot,
        LambdaExpression outerKeySelector,
        LambdaExpression innerKeySelector,
        LambdaExpression resultSelector)
    {
        InnerRoot = innerRoot ?? throw new ArgumentNullException(nameof(innerRoot));
        OuterKeySelector = outerKeySelector ?? throw new ArgumentNullException(nameof(outerKeySelector));
        InnerKeySelector = innerKeySelector ?? throw new ArgumentNullException(nameof(innerKeySelector));
        ResultSelector = resultSelector ?? throw new ArgumentNullException(nameof(resultSelector));
    }

    /// <summary>Gets the joined query root.</summary>
    public QueryRoot InnerRoot { get; }

    /// <summary>Gets the outer key selector.</summary>
    public LambdaExpression OuterKeySelector { get; }

    /// <summary>Gets the inner key selector.</summary>
    public LambdaExpression InnerKeySelector { get; }

    /// <summary>Gets the result selector.</summary>
    public LambdaExpression ResultSelector { get; }
}
