// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Tests.GraphTests;

using Cvoya.Graph.CompatibilityTests;

public class FullTextSearchTests(AgeHarness harness) : AgeTest(harness, StoreIsolation.FreshStore), IFullTextSearchTests { }
