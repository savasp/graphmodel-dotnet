// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Tests.GraphTests;

using Cvoya.Graph.CompatibilityTests;

public class GroupByTests(Neo4jHarness harness) :
    Neo4jTest(harness),
    IGroupByTests
{
}
