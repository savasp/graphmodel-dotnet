// Copyright CVOYA. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.InMemory.Tests;

using Cvoya.Graph.CompatibilityTests;

/// <summary>
/// The intermediate base every in-memory compatibility test binding derives from.
/// </summary>
public abstract class InMemoryTest(
    InMemoryHarness harness,
    StoreIsolation isolation = StoreIsolation.CleanSharedStore)
    : CompatibilityTest(harness, isolation), IClassFixture<InMemoryHarness>;
