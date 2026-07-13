// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

public interface IGraphTest
{
    /// <summary>Gets the provider harness backing this compatibility-test binding.</summary>
    IGraphProviderTestHarness Harness { get; }

    /// <summary>Gets the graph acquired for the current compatibility test.</summary>
    IGraph Graph { get; }
}
