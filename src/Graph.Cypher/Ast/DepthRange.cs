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

namespace Cvoya.Graph.Cypher.Ast;

/// <summary>
/// Represents an inclusive relationship traversal depth range.
/// </summary>
public sealed record DepthRange
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DepthRange"/> class.
    /// </summary>
    /// <param name="min">The minimum traversal depth.</param>
    /// <param name="max">The maximum traversal depth.</param>
    /// <exception cref="ArgumentException">Thrown when the range is negative or inverted.</exception>
    public DepthRange(int min, int max)
    {
        if (min < 0)
        {
            throw new ArgumentException("The minimum depth cannot be negative.", nameof(min));
        }

        if (max < min)
        {
            throw new ArgumentException("The maximum depth cannot be less than the minimum depth.", nameof(max));
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
