// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests.Tests.Fakes;

/// <summary>
/// The base every fake-provider meta-test binds to, mirroring the shape a real provider's own
/// intermediate base class (e.g. <c>Neo4jTest</c>) would have.
/// </summary>
/// <param name="harness">The fake harness, injected by xUnit as a class fixture.</param>
public abstract class FakeProviderTest(FakeHarness harness) : CompatibilityTest(harness), IClassFixture<FakeHarness>;
