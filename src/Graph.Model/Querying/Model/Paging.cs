// Copyright 2025 Savas Parastatidis
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      https://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace Cvoya.Graph.Model.Querying;

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
