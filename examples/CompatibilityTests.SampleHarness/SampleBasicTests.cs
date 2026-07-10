// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests.SampleHarness;

using Cvoya.Graph.CompatibilityTests;

/// <summary>
/// A single example binding: implementing one <c>I*Tests</c> interface is a one-line class. A
/// real provider's test project has one of these per suite interface it wants to run (typically
/// all of them) - see <c>tests/Graph.Model.Neo4j.Tests/GraphModelTests</c> for the full set.
/// </summary>
public sealed class SampleBasicTests(SampleHarness harness) : SampleProviderTest(harness), IBasicTests;
