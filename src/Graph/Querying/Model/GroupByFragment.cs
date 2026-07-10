// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying;

using System.Linq.Expressions;

/// <summary>
/// Describes a provider-independent grouping of the current query scope by a key.
/// </summary>
/// <remarks>
/// The model can represent a grouping so that recognition does not lose the key, element, or
/// result selectors; whether a provider can execute it is a separate planning concern.
/// </remarks>
public sealed record GroupByFragment
{
    /// <summary>
    /// Initializes a new grouping description.
    /// </summary>
    /// <param name="keySelector">The grouping key selector.</param>
    /// <param name="elementSelector">The optional element selector applied to each group member.</param>
    /// <param name="resultSelector">The optional result selector applied to each key/group pair.</param>
    public GroupByFragment(
        LambdaExpression keySelector,
        LambdaExpression? elementSelector,
        LambdaExpression? resultSelector)
    {
        KeySelector = keySelector ?? throw new ArgumentNullException(nameof(keySelector));
        ElementSelector = elementSelector;
        ResultSelector = resultSelector;
    }

    /// <summary>Gets the grouping key selector.</summary>
    public LambdaExpression KeySelector { get; }

    /// <summary>Gets the optional element selector applied to each group member.</summary>
    public LambdaExpression? ElementSelector { get; }

    /// <summary>Gets the optional result selector applied to each key/group pair.</summary>
    public LambdaExpression? ResultSelector { get; }
}
