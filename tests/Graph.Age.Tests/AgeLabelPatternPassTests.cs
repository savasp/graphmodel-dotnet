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
    public void LowersNodeAndAliasedSingleHopRelationshipLabelsToLegacyGoldenOutput()
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

        Assert.Equal(
            """
            MATCH (src)-[knows]->(tgt)
            WHERE ('Person' IN coalesce(src.inheritance_labels, []) OR 'Manager' IN coalesce(src.inheritance_labels, [])) AND ('Knows' IN coalesce(knows.inheritance_labels, []) OR 'WorksWith' IN coalesce(knows.inheritance_labels, []))
            RETURN src
            """,
            rendered.Text);
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

        Assert.Equal(
            """
            MATCH (src)-[age_relationship_0]->(mid)-[age_relationship_1]->(tgt)
            WHERE ('Knows' IN coalesce(age_relationship_0.inheritance_labels, []) OR 'WorksWith' IN coalesce(age_relationship_0.inheritance_labels, [])) AND ('Likes' IN coalesce(age_relationship_1.inheritance_labels, []) OR 'Avoids' IN coalesce(age_relationship_1.inheritance_labels, []))
            RETURN tgt
            """,
            rendered.Text);
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

        Assert.Equal(
            """
            OPTIONAL MATCH (src)-[age_relationship_0*1..4]->(tgt)
            WHERE ('Knows' IN coalesce(age_relationship_0[toInteger(0)].inheritance_labels, []) OR 'WorksWith' IN coalesce(age_relationship_0[toInteger(0)].inheritance_labels, []))
            RETURN tgt
            """,
            rendered.Text);
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

        Assert.Equal(
            """
            MATCH (src)-[relationships*1..4]->(tgt)
            WHERE size([age_hop IN range(0, size(relationships) - 1) WHERE ('Knows' IN coalesce(relationships[toInteger(age_hop)].inheritance_labels, []) OR 'WorksWith' IN coalesce(relationships[toInteger(age_hop)].inheritance_labels, []))]) = size(relationships)
            RETURN tgt
            """,
            rendered.Text);
        var where = Assert.IsType<WhereClause>(lowered.Clauses[1]);
        var equality = Assert.IsType<BinaryExpression>(where.Predicate);
        var size = Assert.IsType<FunctionCall>(equality.Left);
        Assert.IsType<ListComprehensionExpression>(Assert.Single(size.Arguments));
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

        Assert.Equal(
            """
            MATCH (src)
            WHERE ('Person' IN coalesce(src.inheritance_labels, []) OR 'Manager' IN coalesce(src.inheritance_labels, [])) AND src.Active = true
            MATCH (other)
            WHERE ('Person' IN coalesce(other.inheritance_labels, []) OR 'Manager' IN coalesce(other.inheritance_labels, []))
            RETURN other
            """,
            rendered.Text);
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

        Assert.Equal(
            """
            MATCH (src)
            CALL {
              WITH src
              MATCH (src)-[knows]->(tgt)
              WHERE ('Person' IN coalesce(tgt.inheritance_labels, []) OR 'Manager' IN coalesce(tgt.inheritance_labels, [])) AND ('Knows' IN coalesce(knows.inheritance_labels, []) OR 'WorksWith' IN coalesce(knows.inheritance_labels, []))
              RETURN tgt
            }
            RETURN tgt
            """,
            rendered.Text);
    }

    [Fact]
    public void ReturnsSameStatementWhenNoLabelsOrTypesExist()
    {
        var statement = Statement(
            Match(
                optional: false,
                new NodePattern("src", []),
                Relationship("knows", []),
                new NodePattern("tgt", [])),
            Return("tgt"));

        Assert.Same(statement, pass.Run(statement));
    }

    private static CypherStatement Statement(params ICypherClause[] clauses) =>
        new(clauses, new Dictionary<string, object?>(StringComparer.Ordinal));

    private static MatchClause Match(bool optional, params PatternElement[] elements) =>
        new([new PathPattern(elements)], optional);

    private static RelationshipPattern Relationship(
        string? alias,
        IReadOnlyList<string> types,
        DepthRange? depth = null) =>
        new(alias, CypherDirection.Outgoing, depth, types);

    private static ReturnClause Return(string alias) =>
        new([new ReturnItem(new VariableRef(alias), null)], distinct: false);
}
