// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Translation.Tests;

using System.Reflection;
using Cvoya.Graph.Neo4j.Entities;
using Cvoya.Graph.Neo4j.Serialization;

/// <summary>
/// Regression coverage for #135: <see cref="Neo4jNodeManager.DeleteNodeAsync"/> used to resolve
/// the root node's label(s) with a preparatory <c>CALL db.labels()</c> round-trip before it could
/// even build the label-scoped MATCH the delete queries share. These tests assert, at the
/// Cypher-generation level (no live Neo4j needed), that the label scoping is now baked into a
/// single compile-time MATCH template - so there is no per-delete query to resolve labels, only
/// the count/business-relationship-check/delete queries the operation inherently needs.
/// </summary>
public class Neo4jNodeManagerDeleteCypherTests
{
    [Fact]
    public void RootMatchPrelude_DoesNotDependOnDatabaseLabelsCatalog()
    {
        // The prelude must be usable to build every delete-path query without first calling out
        // to the database to discover what labels exist - it's a compile-time constant, not a
        // string built from a query result.
        var field = typeof(Neo4jNodeManager).GetField(
            "RootMatchPrelude",
            BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.GetField);

        Assert.NotNull(field);
        Assert.True(field.IsLiteral, "RootMatchPrelude must be a compile-time constant, not a value computed from a database round-trip.");

        var value = (string)field.GetRawConstantValue()!;

        Assert.DoesNotContain("db.labels", value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CALL", value, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RootMatchPrelude_ScopesByIdAndResolvesLabelFromTheMatchedNodeItself()
    {
        var prelude = Neo4jNodeManager.RootMatchPrelude;

        // Single MATCH on Id - not a per-label UNION built from a prior labels() query.
        Assert.Contains("MATCH (n {Id: $nodeId})", prelude, StringComparison.Ordinal);

        // Label scoping comes from labels(n) (a cheap per-node function over labels already
        // bound by the MATCH) cross-referenced against registered node labels resolved from
        // schema/serialization metadata - not a preparatory catalog query.
        Assert.Contains("labels(n)", prelude, StringComparison.Ordinal);
        Assert.Contains("$registeredNodeLabels", prelude, StringComparison.Ordinal);
        Assert.Contains($"n.{SerializationBridge.EntityKindPropertyName}", prelude, StringComparison.Ordinal);
        Assert.Contains("$nodeEntityKind", prelude, StringComparison.Ordinal);
    }

    [Fact]
    public void NoStringConstantOnNeo4jNodeManagerReferencesTheDatabaseLabelsCatalog()
    {
        // Belt-and-suspenders: scan every string constant declared on the type (not just
        // RootMatchPrelude) so the per-delete `CALL db.labels()` round trip can't quietly
        // reappear in some other query built on the class.
        var stringConstants = typeof(Neo4jNodeManager)
            .GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.GetField)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!);

        foreach (var constant in stringConstants)
        {
            Assert.DoesNotContain("db.labels", constant, StringComparison.OrdinalIgnoreCase);
        }
    }
}
