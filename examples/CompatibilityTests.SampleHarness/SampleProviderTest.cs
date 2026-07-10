// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests.SampleHarness;

using Cvoya.Graph.CompatibilityTests;
using Xunit;

/// <summary>
/// The intermediate base a real provider's test project declares once, then never touches again -
/// every <c>I*Tests</c> binding class derives from it. Mirrors <c>Neo4jTest</c> in
/// <c>tests/Graph.Neo4j.Tests</c>, the in-tree reference implementation.
/// </summary>
/// <param name="harness">The sample harness, injected by xUnit as a class fixture.</param>
public abstract class SampleProviderTest(SampleHarness harness)
    : CompatibilityTest(harness), IClassFixture<SampleHarness>;
