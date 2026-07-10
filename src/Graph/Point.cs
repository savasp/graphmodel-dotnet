// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph;

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
    public double Longitude { get; init; } = 0.0;

    /// <summary>
    /// Gets or inits the Latitude coordinate of the point.
    /// </summary>
    public double Latitude { get; init; } = 0.0;

    /// <summary>
    /// Gets or inits the Height coordinate of the point.
    /// </summary>
    public double Height { get; init; } = 0.0;
}
