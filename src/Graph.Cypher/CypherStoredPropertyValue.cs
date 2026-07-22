// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Cypher;

/// <summary>Represents one physical property assignment produced from a logical graph property.</summary>
/// <param name="StorageName">The physical property name.</param>
/// <param name="Value">The provider parameter value.</param>
public sealed record CypherStoredPropertyValue(string StorageName, object? Value);
