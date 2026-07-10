// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying;

using System.Linq.Expressions;

/// <summary>
/// Describes the projection shape of a query.
/// </summary>
public sealed record ProjectionShape
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectionShape"/> record.
    /// </summary>
    /// <param name="kind">The projection kind.</param>
    /// <param name="selector">The normalized selector lambda expression, if the projection came from one.</param>
    public ProjectionShape(ProjectionKind kind, LambdaExpression? selector)
    {
        QueryModelGuard.RequireDefinedEnum(kind, nameof(kind));

        if (kind != ProjectionKind.Identity && selector is null)
        {
            throw new ArgumentNullException(nameof(selector), "Non-identity projections require a selector.");
        }

        if (selector?.ReturnType == typeof(void))
        {
            throw new ArgumentException("A projection selector must return a value.", nameof(selector));
        }

        Kind = kind;
        Selector = selector;
    }

    /// <summary>
    /// Gets the projection kind.
    /// </summary>
    public ProjectionKind Kind { get; }

    /// <summary>
    /// Gets the normalized selector lambda expression, if the projection came from one.
    /// </summary>
    public LambdaExpression? Selector { get; }
}
