// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Tests;

using Cvoya.Graph.Age.Querying.Cypher;
using Cvoya.Graph.Age.Querying.Cypher.Lowering;
using Cvoya.Graph.Cypher;
using Cvoya.Graph.Cypher.Ast;
using Cvoya.Graph.Cypher.Ast.Expressions;
using Cvoya.Graph.Cypher.Validation;

public sealed class AgeInlineComplexPropertyProjectionPassTests
{
    private readonly AgeInlineComplexPropertyProjectionPass pass = new();
    private readonly AgeEntityProjectionPass entityPass = new();
    private readonly CypherRenderer renderer = new(AgeDialect.Instance);

    [Fact]
    public void ExpandsLoadedEntityIntoStructuredLegacyGoldenOutput()
    {
        var statement = Statement(
            MatchPath(),
            new ReturnClause(
            [
                new ReturnItem(new EntityProjectionExpression("src", loadComplexProperties: true), "Owner"),
            ], distinct: false));

        var expanded = pass.Run(statement);
        var lowered = entityPass.Run(expanded);
        new CypherAstValidator().Run(lowered);
        var rendered = renderer.Render(lowered);

        Assert.Equal(
            """
            MATCH (src)-[r]->(tgt)
            OPTIONAL MATCH (src)-[src_inline_relationships*1..5]->(src_inline_property)
            WHERE coalesce(src_inline_relationships[toInteger(0)].__graphModelComplexProperty, false) = true
            WITH src, r, tgt, CASE WHEN src_inline_relationships IS NULL THEN [] ELSE [i IN range(0, size(src_inline_relationships) - 1) | { ParentNode: startNode(src_inline_relationships[toInteger(i)]), Relationship: src_inline_relationships[toInteger(i)], SequenceNumber: src_inline_relationships[toInteger(i)].SequenceNumber, Property: endNode(src_inline_relationships[toInteger(i)]) }] END AS src_inline_path
            WITH src, r, tgt, collect(src_inline_path) AS src_inline_properties
            RETURN { Node: src, ComplexProperties: src_inline_properties } AS Owner
            """,
            rendered.Text);
        Assert.IsType<AllExpression>(Assert.IsType<WhereClause>(expanded.Clauses[2]).Predicate);
        Assert.IsType<CaseExpression>(
            Assert.IsType<WithClause>(expanded.Clauses[3]).Items[^1].Expression);
        Assert.IsType<MapExpression>(
            Assert.IsType<ReturnClause>(expanded.Clauses[^1]).Items[0].Expression);
        Assert.DoesNotContain("ALL(", rendered.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("reduce(", rendered.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void CarriesPathCoordinatesAndHydratesMultipleAliasesInEncounterOrder()
    {
        var statement = Statement(
            MatchPath(),
            new WithClause(
            [
                new ReturnItem(new Literal(0), "pathIndex"),
                new ReturnItem(new Literal(0), "hopIndex"),
                Variable("src"),
                Variable("r"),
                Variable("tgt"),
            ], distinct: false),
            new ReturnClause(
            [
                new ReturnItem(
                    new MapExpression(
                    [
                        new MapEntry(
                            "StartNode",
                            new EntityProjectionExpression("src", loadComplexProperties: true)),
                        new MapEntry("Relationship", new VariableRef("r")),
                        new MapEntry(
                            "EndNode",
                            new EntityProjectionExpression("tgt", loadComplexProperties: true)),
                    ]),
                    "PathSegment"),
            ], distinct: false));

        var lowered = entityPass.Run(pass.Run(statement));
        new CypherAstValidator().Run(lowered);
        var rendered = renderer.Render(lowered);

        Assert.Contains(
            "WITH pathIndex, hopIndex, src, r, tgt, CASE WHEN src_inline_relationships IS NULL",
            rendered.Text,
            StringComparison.Ordinal);
        Assert.Contains(
            "WITH pathIndex, hopIndex, src, r, tgt, src_inline_properties, CASE WHEN tgt_inline_relationships IS NULL",
            rendered.Text,
            StringComparison.Ordinal);
        Assert.Contains(
            "WITH pathIndex, hopIndex, src, r, tgt, src_inline_properties, collect(tgt_inline_path) AS tgt_inline_properties",
            rendered.Text,
            StringComparison.Ordinal);
        Assert.EndsWith(
            "RETURN { StartNode: { Node: src, ComplexProperties: src_inline_properties }, Relationship: r, EndNode: { Node: tgt, ComplexProperties: tgt_inline_properties } } AS PathSegment",
            rendered.Text,
            StringComparison.Ordinal);
        Assert.Equal(2, rendered.Text.Split("OPTIONAL MATCH", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public void HydratesRepeatedAliasOnlyOnce()
    {
        var statement = Statement(
            MatchNode("src"),
            new ReturnClause(
            [
                new ReturnItem(
                    new MapExpression(
                    [
                        new MapEntry(
                            "First",
                            new EntityProjectionExpression("src", loadComplexProperties: true)),
                        new MapEntry(
                            "Second",
                            new EntityProjectionExpression("src", loadComplexProperties: true)),
                    ]),
                    "Pair"),
            ], distinct: false));

        var rendered = renderer.Render(entityPass.Run(pass.Run(statement)));

        Assert.Equal(1, rendered.Text.Split("OPTIONAL MATCH", StringSplitOptions.None).Length - 1);
        Assert.Contains(
            "RETURN { First: { Node: src, ComplexProperties: src_inline_properties }, Second: { Node: src, ComplexProperties: src_inline_properties } } AS Pair",
            rendered.Text,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RewritesUnloadedEntityWithoutAddingHydrationClauses()
    {
        var statement = Statement(
            MatchNode("src"),
            new ReturnClause(
            [
                new ReturnItem(new EntityProjectionExpression("src", loadComplexProperties: false), "Node"),
            ], distinct: false));

        var lowered = pass.Run(statement);
        var rendered = renderer.Render(lowered);

        Assert.NotSame(statement, lowered);
        Assert.Equal(
            $"MATCH (src){Environment.NewLine}RETURN {{ Node: src, ComplexProperties: [] }} AS Node",
            rendered.Text);
        Assert.DoesNotContain(lowered.Clauses, clause => clause is MatchClause { Optional: true });
    }

    [Fact]
    public void InsertsHydrationBeforeDistinctWithProjection()
    {
        var statement = Statement(
            MatchNode("src"),
            new WithClause(
            [
                new ReturnItem(new EntityProjectionExpression("src", loadComplexProperties: true), "projected"),
            ], distinct: true),
            new ReturnClause([Variable("projected")], distinct: false));

        var lowered = entityPass.Run(pass.Run(statement));
        var rendered = renderer.Render(lowered);

        var hydrationIndex = rendered.Text.IndexOf("OPTIONAL MATCH", StringComparison.Ordinal);
        var distinctIndex = rendered.Text.IndexOf("WITH DISTINCT", StringComparison.Ordinal);
        Assert.True(hydrationIndex >= 0 && hydrationIndex < distinctIndex);
        Assert.Contains(
            "WITH DISTINCT { Node: src, ComplexProperties: src_inline_properties } AS projected",
            rendered.Text,
            StringComparison.Ordinal);
    }

    [Fact]
    public void LowersInlineProjectionInsideCallSubquery()
    {
        var statement = Statement(
            MatchNode("src"),
            new CallSubqueryClause(
                ["src"],
                [
                    new ReturnClause(
                    [
                        new ReturnItem(
                            new EntityProjectionExpression("src", loadComplexProperties: true),
                            "Owner"),
                    ], distinct: false),
                ]),
            new ReturnClause([Variable("Owner")], distinct: false));

        var rendered = renderer.Render(entityPass.Run(pass.Run(statement)));

        Assert.Contains(
            "CALL {" + Environment.NewLine +
            "  WITH src" + Environment.NewLine +
            "  OPTIONAL MATCH (src)-[src_inline_relationships*1..5]->(src_inline_property)",
            rendered.Text,
            StringComparison.Ordinal);
        Assert.Contains(
            "  RETURN { Node: src, ComplexProperties: src_inline_properties } AS Owner",
            rendered.Text,
            StringComparison.Ordinal);
    }

    [Fact]
    public void CarriesOnlyAliasesInScopeAfterNarrowingWith()
    {
        var statement = Statement(
            MatchPath(),
            new WithClause([Variable("src")], distinct: false),
            new ReturnClause(
            [
                new ReturnItem(new EntityProjectionExpression("src", loadComplexProperties: true), "Owner"),
            ], distinct: false));

        var lowered = entityPass.Run(pass.Run(statement));
        new CypherAstValidator().Run(lowered);
        var rendered = renderer.Render(lowered);

        Assert.Contains(
            "WITH src, CASE WHEN src_inline_relationships IS NULL",
            rendered.Text,
            StringComparison.Ordinal);
        Assert.Contains(
            "WITH src, collect(src_inline_path) AS src_inline_properties",
            rendered.Text,
            StringComparison.Ordinal);
    }

    [Fact]
    public void CarriesOuterAliasReferencedInsidePatternComprehension()
    {
        var statement = Statement(
            new MatchClause(
            [
                new PathPattern(
                [
                    new NodePattern("a", []),
                    new RelationshipPattern("b", null, CypherDirection.Outgoing, depth: null),
                    new NodePattern("c", []),
                ]),
            ], optional: false),
            new ReturnClause(
            [
                new ReturnItem(
                    new MapExpression(
                    [
                        new MapEntry(
                            "Entity",
                            new EntityProjectionExpression("a", loadComplexProperties: true)),
                        new MapEntry(
                            "Others",
                            new PatternComprehensionExpression(
                                new PathPattern(
                                [
                                    new NodePattern("c", []),
                                    new RelationshipPattern("d", null, CypherDirection.Outgoing, depth: null),
                                    new NodePattern("e", []),
                                ]),
                                new VariableRef("e"),
                                predicate: null)),
                    ]),
                    "Row"),
            ], distinct: false));

        var lowered = entityPass.Run(pass.Run(statement));
        new CypherAstValidator().Run(lowered);
        var rendered = renderer.Render(lowered);

        Assert.Contains(
            "WITH a, c, CASE WHEN a_inline_relationships IS NULL",
            rendered.Text,
            StringComparison.Ordinal);
    }

    [Fact]
    public void CarriesAliasReferencedByOrderByAfterReturn()
    {
        var statement = Statement(
            new MatchClause(
            [
                new PathPattern(
                [
                    new NodePattern("a", []),
                    new RelationshipPattern("b", null, CypherDirection.Outgoing, depth: null),
                    new NodePattern("c", []),
                ]),
            ], optional: false),
            new ReturnClause(
            [
                new ReturnItem(new EntityProjectionExpression("a", loadComplexProperties: true), "Owner"),
            ], distinct: false),
            new OrderByClause([new OrderByItem(new PropertyAccess(new VariableRef("c"), "Name"), descending: false)]));

        var lowered = entityPass.Run(pass.Run(statement));
        new CypherAstValidator().Run(lowered);
        var rendered = renderer.Render(lowered);

        Assert.Contains(
            "WITH a, c, CASE WHEN a_inline_relationships IS NULL",
            rendered.Text,
            StringComparison.Ordinal);
        Assert.EndsWith("ORDER BY c.Name", rendered.Text.TrimEnd(), StringComparison.Ordinal);
    }

    [Fact]
    public void ReturnsSameStatementWhenNoInlineEntityProjectionExists()
    {
        var statement = Statement(
            MatchNode("src"),
            new ReturnClause([Variable("src")], distinct: false));

        Assert.Same(statement, pass.Run(statement));
    }

    private static CypherStatement Statement(params ICypherClause[] clauses) =>
        new(clauses, new Dictionary<string, object?>(StringComparer.Ordinal));

    private static MatchClause MatchNode(string alias) =>
        new([new PathPattern([new NodePattern(alias, [])])], optional: false);

    private static MatchClause MatchPath() => new(
    [
        new PathPattern(
        [
            new NodePattern("src", []),
            new RelationshipPattern("r", null, CypherDirection.Outgoing, depth: null),
            new NodePattern("tgt", []),
        ]),
    ], optional: false);

    private static ReturnItem Variable(string alias) => new(new VariableRef(alias), null);
}
