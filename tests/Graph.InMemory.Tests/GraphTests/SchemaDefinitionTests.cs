// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.InMemory.Tests.GraphTests;

using Cvoya.Graph.CompatibilityTests;

public class SchemaDefinitionTests(InMemoryHarness harness) : InMemoryTest(harness, StoreIsolation.FreshStore), ISchemaDefinitionTests { }
