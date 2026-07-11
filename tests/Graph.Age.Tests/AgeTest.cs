// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Tests;

using Cvoya.Graph.CompatibilityTests;

/// <summary>
/// The intermediate base every in-memory compatibility test binding derives from.
/// </summary>
public abstract class AgeTest(
    AgeHarness harness,
    StoreIsolation isolation = StoreIsolation.CleanSharedStore)
    : CompatibilityTest(harness, isolation), IClassFixture<AgeHarness>;
