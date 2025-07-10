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
/// Represents a point in 3D space with Longitude, Latitude, and Height coordinates.
/// It uses the WGS84 coordinate system, which is commonly used in GPS and mapping applications.
/// </summary>
public readonly record struct Point
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Point"/> struct at the origin (0,0,0).
    /// </summary>
    public Point() { }

    /// <summary>
    /// Creates a new point at the origin (0,0,0).
    /// </summary>
    public static Point Origin => new() { Longitude = 0.0, Latitude = 0.0, Height = 0.0 };

    /// <summary>
    /// Gets or inits the Longitude coordinate of the point.
    /// </summary>
    public required double Longitude { get; init; } = 0.0;

    /// <summary>
    /// Gets or inits the Latitude coordinate of the point.
    /// </summary>
    public required double Latitude { get; init; } = 0.0;

    /// <summary>
    /// Gets or inits the Height coordinate of the point.
    /// </summary>
    public required double Height { get; init; } = 0.0;
}
