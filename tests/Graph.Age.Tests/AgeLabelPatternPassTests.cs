// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Tests;

using Cvoya.Graph.Age.Querying.Cypher;
using Cvoya.Graph.Age.Querying.Cypher.Lowering;
using Cvoya.Graph.Cypher;
using Cvoya.Graph.Cypher.Ast;
using Cvoya.Graph.Cypher.Ast.Expressions;

public sealed class AgeLabelPatternPassTests
{
    private readonly AgeLabelPatternPass pass = new();
    private readonly CypherRenderer renderer = new(AgeDialect.Instance);

    [Fact]
    public void LowersNodeAndAliasedSingleHopRelationshipLabelsToNativeOrHierarchyPredicates()
    {
        var statement = Statement(
            Match(
                optional: false,
                new NodePattern("src", ["Person", "Manager"]),
                Relationship("knows", ["Knows", "WorksWith"]),
                new NodePattern("tgt", [])),
            Return("src"));

        var lowered = pass.Run(statement);
        var rendered = renderer.Render(lowered);

        Assert.Contains("'Person' IN labels(src)", rendered.Text, StringComparison.Ordinal);
        Assert.Contains("'Person' IN coalesce(src.inheritance_labels, [])", rendered.Text, StringComparison.Ordinal);
        Assert.Contains("type(knows) = 'Knows'", rendered.Text, StringComparison.Ordinal);
        Assert.Contains("'Knows' IN coalesce(knows.inheritance_labels, [])", rendered.Text, StringComparison.Ordinal);
        Assert.Contains("knows.__graphModelComplexProperty", rendered.Text, StringComparison.Ordinal);
        var match = Assert.IsType<MatchClause>(lowered.Clauses[0]);
        Assert.All(match.Patterns[0].Elements.OfType<NodePattern>(), node => Assert.Empty(node.Labels));
        Assert.All(match.Patterns[0].Elements.OfType<RelationshipPattern>(), relationship => Assert.Empty(relationship.Types));
    }

    [Fact]
    public void GeneratesRelationshipAliasesInPatternOrder()
    {
        var statement = Statement(
            Match(
                optional: false,
                new NodePattern("src", []),
                Relationship(alias: null, ["Knows", "WorksWith"]),
                new NodePattern("mid", []),
                Relationship(alias: null, ["Likes", "Avoids"]),
                new NodePattern("tgt", [])),
            Return("tgt"));

        var lowered = pass.Run(statement);
        var rendered = renderer.Render(lowered);

        Assert.Contains("MATCH (src)-[age_relationship_0]->(mid)-[age_relationship_1]->(tgt)", rendered.Text, StringComparison.Ordinal);
        Assert.Contains("type(age_relationship_0) = 'Knows'", rendered.Text, StringComparison.Ordinal);
        Assert.Contains("type(age_relationship_1) = 'Likes'", rendered.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void ChecksOnlyFirstHopForOptionalVariableLengthRelationships()
    {
        var statement = Statement(
            Match(
                optional: true,
                new NodePattern("src", []),
                Relationship(alias: null, ["Knows", "WorksWith"], new DepthRange(1, 4)),
                new NodePattern("tgt", [])),
            Return("tgt"));

        var rendered = renderer.Render(pass.Run(statement));

        Assert.Contains("type(age_relationship_0[toInteger(0)]) = 'Knows'", rendered.Text, StringComparison.Ordinal);
        Assert.Contains("age_relationship_0[toInteger(0)].inheritance_labels", rendered.Text, StringComparison.Ordinal);
        Assert.Contains("age_relationship_0[toInteger(0)].__graphModelComplexProperty", rendered.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void ChecksEveryHopForRequiredVariableLengthRelationships()
    {
        var statement = Statement(
            Match(
                optional: false,
                new NodePattern("src", []),
                Relationship("relationships", ["Knows", "WorksWith"], new DepthRange(1, 4)),
                new NodePattern("tgt", [])),
            Return("tgt"));

        var lowered = pass.Run(statement);
        var rendered = renderer.Render(lowered);

        Assert.Contains("type(relationships[toInteger(age_hop)]) = 'Knows'", rendered.Text, StringComparison.Ordinal);
        Assert.Contains("relationships[toInteger(age_hop)].inheritance_labels", rendered.Text, StringComparison.Ordinal);
        Assert.Contains("relationships[toInteger(age_hop)].__graphModelComplexProperty", rendered.Text, StringComparison.Ordinal);
        _ = Assert.IsType<WhereClause>(lowered.Clauses[1]);
        Assert.Contains("size([age_hop IN range", rendered.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void MergesExistingPredicatesAndKeepsConsecutiveMatchClausesScoped()
    {
        var statement = Statement(
            Match(
                optional: false,
                new NodePattern("src", ["Person", "Manager"])),
            new WhereClause(new BinaryExpression(
                CypherBinaryOperator.Equal,
                new PropertyAccess(new VariableRef("src"), "Active"),
                new Literal(true))),
            Match(
                optional: false,
                new NodePattern("other", ["Person", "Manager"])),
            Return("other"));

        var rendered = renderer.Render(pass.Run(statement));

        Assert.Contains("'Person' IN labels(src)", rendered.Text, StringComparison.Ordinal);
        Assert.Contains("src.Active = true", rendered.Text, StringComparison.Ordinal);
        Assert.Contains("MATCH (other)", rendered.Text, StringComparison.Ordinal);
        Assert.Contains("'Manager' IN coalesce(other.inheritance_labels, [])", rendered.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void LowersMatchClausesInsideCallSubqueries()
    {
        var statement = Statement(
            Match(
                optional: false,
                new NodePattern("src", [])),
            new CallSubqueryClause(
                ["src"],
                [
                    Match(
                        optional: false,
                        new NodePattern("src", []),
                        Relationship("knows", ["Knows", "WorksWith"]),
                        new NodePattern("tgt", ["Person", "Manager"])),
                    Return("tgt"),
                ]),
            Return("tgt"));

        var rendered = renderer.Render(pass.Run(statement));

        Assert.Contains("CALL {", rendered.Text, StringComparison.Ordinal);
        Assert.Contains("'Person' IN labels(tgt)", rendered.Text, StringComparison.Ordinal);
        Assert.Contains("'Manager' IN coalesce(tgt.inheritance_labels, [])", rendered.Text, StringComparison.Ordinal);
        Assert.Contains("type(knows) = 'Knows'", rendered.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void LowersSingleLabelToNativeOrHierarchyPredicate()
    {
        var statement = Statement(
            Match(
                optional: false,
                new NodePattern("src", ["Person"])),
            Return("src"));

        var rendered = renderer.Render(pass.Run(statement));

        Assert.Contains("'Person' IN labels(src)", rendered.Text, StringComparison.Ordinal);
        Assert.Contains("'Person' IN coalesce(src.inheritance_labels, [])", rendered.Text, StringComparison.Ordinal);
        Assert.Contains("__graphModelComplexProperty", rendered.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void CollectsPredicatesAcrossCommaSeparatedPatternsInOneMatch()
    {
        var statement = Statement(
            new MatchClause(
                [
                    new PathPattern([new NodePattern("a", ["Person"])]),
                    new PathPattern([new NodePattern("b", ["Company"])]),
                ],
                optional: false),
            Return("a"));

        var rendered = renderer.Render(pass.Run(statement));

        Assert.Contains("'Person' IN labels(a)", rendered.Text, StringComparison.Ordinal);
        Assert.Contains("'Company' IN labels(b)", rendered.Text, StringComparison.Ordinal);
        Assert.Contains("'Person' IN coalesce(a.inheritance_labels, [])", rendered.Text, StringComparison.Ordinal);
        Assert.Contains("'Company' IN coalesce(b.inheritance_labels, [])", rendered.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void ExcludesInternalComplexElementsWhenNoLabelsOrTypesExist()
    {
        var statement = Statement(
            Match(
                optional: false,
                new NodePattern("src", []),
                Relationship("knows", []),
                new NodePattern("tgt", [])),
            Return("tgt"));

        var rendered = renderer.Render(pass.Run(statement));

        Assert.DoesNotContain("age_owner_relationship_0.__graphModelComplexProperty", rendered.Text, StringComparison.Ordinal);
        Assert.Contains("knows.__graphModelComplexProperty", rendered.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void ComplexPropertyNavigation_AcceptsOwnedAndCollidingDomainRelationships()
    {
        var statement = Statement(
            Match(
                optional: false,
                new NodePattern("src", []),
                Relationship(
                    "primary",
                    ["ContractPrimaryAddress"],
                    isComplexProperty: true),
                new NodePattern("address", ["ContractAddressValue"])),
            Return("src"));

        var rendered = renderer.Render(pass.Run(statement));

        Assert.Contains("type(primary) = 'ContractPrimaryAddress'", rendered.Text, StringComparison.Ordinal);
        Assert.Contains("'ContractPrimaryAddress' IN coalesce(primary.inheritance_labels, [])", rendered.Text, StringComparison.Ordinal);
        Assert.Equal(
            1,
            rendered.Text.Split(
                "primary.__graphModelComplexProperty",
                StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public void ComplexPropertyNavigationWithoutLogicalType_StaysMarkerIsolated()
    {
        var statement = Statement(
            Match(
                optional: false,
                new NodePattern("src", []),
                Relationship(
                    "primary",
                    [],
                    isComplexProperty: true),
                new NodePattern("address", [])),
            Return("src"));

        var rendered = renderer.Render(pass.Run(statement));

        Assert.Contains(
            "coalesce(primary.__graphModelComplexProperty, false) = true",
            rendered.Text,
            StringComparison.Ordinal);
        Assert.DoesNotContain("type(primary) <> 'CvoyaRelationship'", rendered.Text, StringComparison.Ordinal);
    }

    private static CypherStatement Statement(params ICypherClause[] clauses) =>
        new(clauses, new Dictionary<string, object?>(StringComparer.Ordinal));

    private static MatchClause Match(bool optional, params PatternElement[] elements) =>
        new([new PathPattern(elements)], optional);

    private static RelationshipPattern Relationship(
        string? alias,
        IReadOnlyList<string> types,
        DepthRange? depth = null,
        bool isComplexProperty = false) =>
        new(alias, CypherDirection.Outgoing, depth, types, isComplexProperty);

    private static ReturnClause Return(string alias) =>
        new([new ReturnItem(new VariableRef(alias), null)], distinct: false);
}
