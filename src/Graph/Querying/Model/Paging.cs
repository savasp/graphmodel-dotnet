// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying;

/// <summary>
/// Describes skip/take paging.
/// </summary>
public sealed record Paging
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Paging"/> record.
    /// </summary>
    /// <param name="skip">The number of elements to skip, or <see langword="null"/> when no skip is applied.</param>
    /// <param name="take">The number of elements to take, or <see langword="null"/> when no take is applied.</param>
    public Paging(int? skip, int? take)
    {
        if (skip < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(skip), "Skip must be non-negative.");
        }

        if (take < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(take), "Take must be non-negative.");
        }

        Skip = skip;
        Take = take;
    }

    /// <summary>
    /// Gets the number of elements to skip, or <see langword="null"/> when no skip is applied.
    /// </summary>
    public int? Skip { get; }

    /// <summary>
    /// Gets the number of elements to take, or <see langword="null"/> when no take is applied.
    /// </summary>
    public int? Take { get; }
}
