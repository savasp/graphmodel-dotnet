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
/// Describes an inclusive traversal depth range.
/// </summary>
public sealed record DepthRange
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DepthRange"/> record.
    /// </summary>
    /// <param name="min">The minimum traversal depth. Must be non-negative.</param>
    /// <param name="max">The maximum traversal depth. Must be greater than or equal to <paramref name="min"/>.</param>
    public DepthRange(int min, int max)
    {
        if (min < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(min), "Minimum depth must be non-negative.");
        }

        if (max < min)
        {
            throw new ArgumentOutOfRangeException(nameof(max), "Maximum depth must be greater than or equal to minimum depth.");
        }

        Min = min;
        Max = max;
    }

    /// <summary>
    /// Gets the minimum traversal depth.
    /// </summary>
    public int Min { get; }

    /// <summary>
    /// Gets the maximum traversal depth.
    /// </summary>
    public int Max { get; }
}
