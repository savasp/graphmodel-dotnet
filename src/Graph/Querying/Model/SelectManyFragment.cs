// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying;

using System.Linq.Expressions;

/// <summary>
/// Describes a provider-independent flattening projection over the current query scope.
/// </summary>
/// <remarks>
/// The model can represent the flattening so that recognition does not lose the collection or
/// result selectors; whether a provider can execute it is a separate planning concern.
/// </remarks>
public sealed record SelectManyFragment
{
    /// <summary>
    /// Initializes a new flattening-projection description.
    /// </summary>
    /// <param name="collectionSelector">The selector producing the sequence to flatten.</param>
    /// <param name="resultSelector">The optional result selector applied to each source/element pair.</param>
    public SelectManyFragment(LambdaExpression collectionSelector, LambdaExpression? resultSelector)
    {
        CollectionSelector = collectionSelector ?? throw new ArgumentNullException(nameof(collectionSelector));
        ResultSelector = resultSelector;
    }

    /// <summary>Gets the selector producing the sequence to flatten.</summary>
    public LambdaExpression CollectionSelector { get; }

    /// <summary>Gets the optional result selector applied to each source/element pair.</summary>
    public LambdaExpression? ResultSelector { get; }
}
