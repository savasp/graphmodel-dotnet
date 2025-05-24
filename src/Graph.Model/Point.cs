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

namespace Cvoya.Graph.Model;

/// <summary>
/// Represents a point in 3D space with X, Y, and Z coordinates.
/// </summary>
/// <param name="x">The X coordinate of the point.</param>
/// <param name="y">The Y coordinate of the point.</param>
/// <param name="z">The Z coordinate of the point.</param>
public readonly record struct Point(double X, double Y, double Z)
{
    /// <summary>
    /// Creates a new point at the origin (0,0,0).
    /// </summary>
    public static Point Origin => new(0, 0, 0);

    /// <summary>
    /// Calculates the Euclidean distance between this point and another point.
    /// </summary>
    /// <param name="other">The other point to calculate distance to.</param>
    /// <returns>The Euclidean distance between the points.</returns>
    public double DistanceTo(Point other)
    {
        double dx = X - other.X;
        double dy = Y - other.Y;
        double dz = Z - other.Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }
}
