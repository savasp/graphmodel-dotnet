// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying;

using System.Linq.Expressions;

/// <summary>
/// Describes a predicate expression and the alias it applies to, if one is known.
/// </summary>
public sealed record PredicateFragment
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PredicateFragment"/> record.
    /// </summary>
    /// <param name="predicate">The normalized predicate lambda expression.</param>
    /// <param name="alias">The provider-neutral alias associated with the predicate, if known.</param>
    public PredicateFragment(LambdaExpression predicate, string? alias)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        QueryModelGuard.RequireNullOrNotWhiteSpace(alias, nameof(alias));

        if (predicate.ReturnType != typeof(bool))
        {
            throw new ArgumentException("A predicate lambda must return Boolean.", nameof(predicate));
        }

        Predicate = predicate;
        Alias = alias;
    }

    /// <summary>
    /// Gets the normalized predicate lambda expression.
    /// </summary>
    public LambdaExpression Predicate { get; }

    /// <summary>
    /// Gets the provider-neutral alias associated with the predicate, if known.
    /// </summary>
    public string? Alias { get; }
}
