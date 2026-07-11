// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Cypher;

/// <summary>
/// Marks a provider extension method as a dynamic-entity accessor understood by the shared Cypher planner.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class CypherDynamicEntityAccessorAttribute : Attribute
{
}
