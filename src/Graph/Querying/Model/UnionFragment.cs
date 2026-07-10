// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying;

/// <summary>
/// Describes a provider-independent set union between a graph query and a second graph query.
/// </summary>
/// <remarks>
/// The model can represent the union so that recognition does not lose the second source; whether
/// a provider can execute it is a separate planning concern.
/// </remarks>
public sealed record UnionFragment
{
    /// <summary>
    /// Initializes a new union description.
    /// </summary>
    /// <param name="second">The second query model whose results are unioned with the current query.</param>
    public UnionFragment(GraphQueryModel second)
    {
        Second = second ?? throw new ArgumentNullException(nameof(second));
    }

    /// <summary>Gets the second query model whose results are unioned with the current query.</summary>
    public GraphQueryModel Second { get; }
}
