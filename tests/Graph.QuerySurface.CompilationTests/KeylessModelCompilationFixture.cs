// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.QuerySurface.CompilationTests;

using Cvoya.Graph;

/// <summary>
/// Normal consumer-compilation fixture proving that a keyless graph model remains diagnostic-free
/// when the shipped analyzer runs as part of a project build.
/// </summary>
internal sealed record KeylessModelCompilationFixture : Node
{
    public string Name { get; init; } = string.Empty;
}
