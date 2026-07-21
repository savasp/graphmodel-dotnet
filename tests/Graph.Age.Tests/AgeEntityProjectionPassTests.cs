// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Tests;

using Cvoya.Graph.Age.Querying.Cypher;
using Cvoya.Graph.Age.Querying.Cypher.Lowering;
using Cvoya.Graph.Cypher;
using Cvoya.Graph.Cypher.Ast;
using Cvoya.Graph.Cypher.Ast.Expressions;

public sealed class AgeEntityProjectionPassTests
{
    private readonly AgeEntityProjectionPass pass = new();
    private readonly CypherRenderer renderer = new(AgeDialect.Instance);

    [Fact]
    public void ExpandsNodeHydrationToStructuredLegacyGoldenOutput()
    {
        var statement = Statement(
            MatchNode("src"),
            new EntityProjectionClause(
                EntityProjectionShape.Node,
                "src",
                relationshipAlias: null,
                targetAlias: null,
                loadSourceProperties: true,
                loadTargetProperties: false));

        var lowered = pass.Run(statement);
        var rendered = renderer.Render(lowered);

        Assert.Equal(
            """
            MATCH (src)
            OPTIONAL MATCH (src)-[rels*1..5]->(prop)
            WHERE coalesce(rels[toInteger(0)].__graphModelComplexProperty, false) = true
            WITH src, CASE WHEN rels IS NULL THEN [] ELSE [i IN range(0, size(rels) - 1) | { ParentNode: CASE WHEN i = 0 THEN src ELSE startNode(rels[toInteger(i)]) END, Relationship: rels[toInteger(i)], SequenceNumber: rels[toInteger(i)].SequenceNumber, Property: endNode(rels[toInteger(i)]) }] END AS src_property_path
            WITH src, collect(src_property_path) AS src_properties
            RETURN { Node: src, ComplexProperties: src_properties } AS Node
            """,
            rendered.Text);
        Assert.DoesNotContain(lowered.Clauses, clause => clause is EntityProjectionClause);
        Assert.DoesNotContain("ALL(", rendered.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("reduce(", rendered.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void NodeHydrationCarriesInputRowIdentityThroughAggregation()
    {
        var statement = Statement(
            new MatchClause(
            [
                new PathPattern(
                [
                    new NodePattern("src", []),
                    new RelationshipPattern("r", null, CypherDirection.Outgoing, depth: null),
                    new NodePattern("tgt", []),
                ]),
            ], optional: false),
            new EntityProjectionClause(
                EntityProjectionShape.Node,
                "tgt",
                relationshipAlias: null,
                targetAlias: null,
                loadSourceProperties: true,
                loadTargetProperties: false,
                includePathCoordinates: false,
                ordering: [],
                rowIdentityAliases: ["src", "r"]));

        var rendered = renderer.Render(pass.Run(statement));

        Assert.Contains("WITH tgt, src, r, CASE", rendered.Text, StringComparison.Ordinal);
        Assert.Contains("WITH tgt, src, r, collect", rendered.Text, StringComparison.Ordinal);
        Assert.Equal(["Node"], rendered.ProjectionColumns);
    }

    [Fact]
    public void ExpandsRelationshipProjectionWithoutEndpointVariables()
    {
        var statement = Statement(
            new MatchClause(
            [
                new PathPattern(
                [
                    new NodePattern("src", []),
                    new RelationshipPattern("r", null, CypherDirection.Outgoing, depth: null),
                    new NodePattern("tgt", []),
                ]),
            ], optional: false),
            new EntityProjectionClause(
                EntityProjectionShape.Relationship,
                "r",
                relationshipAlias: null,
                targetAlias: null,
                loadSourceProperties: false,
                loadTargetProperties: false));

        var lowered = pass.Run(statement);
        var rendered = renderer.Render(lowered);

        Assert.EndsWith("RETURN r AS Relationship", rendered.Text, StringComparison.Ordinal);
        Assert.Equal(["Relationship"], rendered.ProjectionColumns);
        Assert.DoesNotContain(lowered.Clauses, clause => clause is EntityProjectionClause);
    }

    [Fact]
    public void LowersNamedOptionalPathAllAndReduceNodesDirectly()
    {
        var pathIndex = new VariableRef("i");
        var pathNodes = Function("nodes", new VariableRef("src_path"));
        var statement = Statement(
            new MatchClause(
            [
                new PathPattern(
                [
                    new NodePattern("src", []),
                    new RelationshipPattern(
                        "rels",
                        CypherDirection.Outgoing,
                        new DepthRange(1, 5),
                        types: []),
                    new NodePattern("prop", []),
                ], "src_path"),
            ], optional: true),
            new WhereClause(new AllExpression(
                "rel",
                new VariableRef("rels"),
                new BinaryExpression(
                    CypherBinaryOperator.Equal,
                    new PropertyAccess(new VariableRef("rel"), "marker"),
                    new Literal(true)))),
            new WithClause(
            [
                new ReturnItem(
                    new CaseExpression(
                        new UnaryExpression(CypherUnaryOperator.IsNull, new VariableRef("src_path")),
                        new ListExpression([]),
                        new ListComprehensionExpression(
                            Function("range", new Literal(0), new Literal(1)),
                            "i",
                            projection: new MapExpression(
                            [
                                new MapEntry("Parent", new IndexExpression(pathNodes, pathIndex)),
                                new MapEntry(
                                    "Property",
                                    new IndexExpression(
                                        pathNodes,
                                        new BinaryExpression(
                                            CypherBinaryOperator.Add,
                                            pathIndex,
                                            new Literal(1)))),
                            ]))),
                    "property_path"),
            ], distinct: false),
            new WithClause(
            [
                new ReturnItem(
                    new ReduceExpression(
                        "flat",
                        new ListExpression([]),
                        "path",
                        Function("collect", new VariableRef("property_path")),
                        new BinaryExpression(
                            CypherBinaryOperator.Add,
                            new VariableRef("flat"),
                            new VariableRef("path"))),
                    "properties"),
            ], distinct: false),
            Return("properties"));

        var rendered = renderer.Render(pass.Run(statement));

        Assert.Equal(
            """
            OPTIONAL MATCH (src)-[rels*1..5]->(prop)
            WHERE coalesce(rels[toInteger(0)].marker, false) = true
            WITH CASE WHEN rels IS NULL THEN [] ELSE [i IN range(0, 1) | { Parent: startNode(rels[toInteger(i)]), Property: endNode(rels[toInteger(i)]) }] END AS property_path
            WITH collect(property_path) AS properties
            RETURN properties
            """,
            rendered.Text);
    }

    [Theory]
    [InlineData("year", 0, 4)]
    [InlineData("month", 5, 2)]
    [InlineData("day", 8, 2)]
    [InlineData("hour", 11, 2)]
    [InlineData("minute", 14, 2)]
    [InlineData("second", 17, 2)]
    public void LowersTemporalMembersToSubstringExtraction(string member, int offset, int length)
    {
        var statement = Statement(
            MatchNode("src"),
            new ReturnClause(
            [
                new ReturnItem(
                    new PropertyAccess(
                        new PropertyAccess(new VariableRef("src"), "Created"),
                        member),
                    "value"),
            ], distinct: false));

        var rendered = renderer.Render(pass.Run(statement));

        Assert.Equal(
            $"MATCH (src){Environment.NewLine}RETURN toInteger(substring(src.Created, {offset}, {length})) AS value",
            rendered.Text);
    }

    [Fact]
    public void LowersStringContainsToParenthesizedRegexGoldenOutput()
    {
        var statement = Statement(
            [
                MatchNode("src"),
                new ReturnClause(
                [
                    new ReturnItem(
                        new BinaryExpression(
                            CypherBinaryOperator.Contains,
                            new PropertyAccess(new VariableRef("src"), "Name"),
                            new QueryParameter("p0")),
                        "matches"),
                ], distinct: false),
            ],
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["p0"] = "voy" });

        var rendered = renderer.Render(pass.Run(statement));

        Assert.Equal(
            $"MATCH (src){Environment.NewLine}RETURN src.Name =~ ('.*' + $p0 + '.*') AS matches",
            rendered.Text);
    }

    [Fact]
    public void ConvertsOnlyLegacyPathIndexesToIntegers()
    {
        var statement = Statement(
            new MatchClause(
            [
                new PathPattern(
                [
                    new NodePattern("src", []),
                    new RelationshipPattern(
                        "rels",
                        CypherDirection.Outgoing,
                        new DepthRange(1, 5),
                        types: []),
                    new NodePattern("tgt", []),
                ]),
            ], optional: false),
            new UnwindClause(new ListExpression([new Literal(0)]), "i"),
            new ReturnClause(
            [
                new ReturnItem(new IndexExpression(new VariableRef("rels"), new VariableRef("i")), "current"),
                new ReturnItem(
                    new IndexExpression(
                        new VariableRef("rels"),
                        new BinaryExpression(
                            CypherBinaryOperator.Add,
                            new VariableRef("i"),
                            new Literal(1))),
                    "next"),
                new ReturnItem(new IndexExpression(new VariableRef("rels"), new Literal(2)), "fixed"),
            ], distinct: false));

        var rendered = renderer.Render(pass.Run(statement));

        Assert.Equal(
            """
            MATCH (src)-[rels*1..5]->(tgt)
            UNWIND [0] AS i
            RETURN rels[toInteger(i)] AS current, rels[toInteger(i + 1)] AS next, rels[2] AS fixed
            """,
            rendered.Text);
    }

    [Fact]
    public void EscapesReservedAliasesWithoutChangingProjectionColumnNames()
    {
        var statement = Statement(new ReturnClause(
        [
            new ReturnItem(new Literal(true), "exists"),
            new ReturnItem(new Literal(false), "contains"),
        ], distinct: false));

        var rendered = renderer.Render(pass.Run(statement));
        var columns = rendered.ProjectionColumns
            .Select(AgeEntityProjectionPass.NormalizeProjectionColumn)
            .ToArray();

        Assert.Equal("RETURN true AS `exists`, false AS `contains`", rendered.Text);
        Assert.Equal(["exists", "contains"], columns);
    }

    [Fact]
    public void CoalescesEmptySumToLegacyGoldenOutput()
    {
        var statement = Statement(
            MatchNode("src"),
            new ReturnClause(
            [
                new ReturnItem(Function("sum", new PropertyAccess(new VariableRef("src"), "Amount")), null),
            ], distinct: false));

        var rendered = renderer.Render(pass.Run(statement));

        Assert.Equal(
            $"MATCH (src){Environment.NewLine}RETURN coalesce(sum(src.Amount), 0)",
            rendered.Text);
    }

    [Fact]
    public void PreservesProjectionOrderingAndPathCoordinates()
    {
        var statement = Statement(
            new MatchClause(
            [
                new PathPattern(
                [
                    new NodePattern("src", []),
                    new RelationshipPattern("r", null, CypherDirection.Outgoing, depth: null),
                    new NodePattern("tgt", []),
                ]),
            ], optional: false),
            new WithClause(
            [
                Variable("src"),
                Variable("r"),
                Variable("tgt"),
                new ReturnItem(new Literal(0), "pathIndex"),
                new ReturnItem(new Literal(0), "hopIndex"),
            ], distinct: false),
            new EntityProjectionClause(
                EntityProjectionShape.PathSegment,
                "src",
                "r",
                "tgt",
                loadSourceProperties: false,
                loadTargetProperties: false,
                includePathCoordinates: true,
                ordering:
                [
                    new OrderByItem(new PropertyAccess(new VariableRef("src"), "Name"), descending: true),
                ]));

        var rendered = renderer.Render(pass.Run(statement));

        Assert.Equal(
            """
            MATCH (src)-[r]->(tgt)
            WITH src, r, tgt, 0 AS pathIndex, 0 AS hopIndex
            WITH src, r, tgt, pathIndex, hopIndex, src.Name AS __projectionOrder0
            WITH pathIndex, hopIndex, { StartNode: { Node: src, ComplexProperties: [] }, Relationship: r, EndNode: { Node: tgt, ComplexProperties: [] } } AS PathSegment, __projectionOrder0
            ORDER BY __projectionOrder0 DESC
            RETURN pathIndex, hopIndex, PathSegment
            """,
            rendered.Text);
        Assert.Equal(["pathIndex", "hopIndex", "PathSegment"], rendered.ProjectionColumns);
    }

    [Fact]
    public void ExpandsPathSegmentPropertyLoadsToStructuredLegacyGoldenOutput()
    {
        var statement = Statement(
            new MatchClause(
            [
                new PathPattern(
                [
                    new NodePattern("src", []),
                    new RelationshipPattern("r", null, CypherDirection.Outgoing, depth: null),
                    new NodePattern("tgt", []),
                ]),
            ], optional: false),
            new EntityProjectionClause(
                EntityProjectionShape.PathSegment,
                "src",
                "r",
                "tgt",
                loadSourceProperties: true,
                loadTargetProperties: true));

        var lowered = pass.Run(statement);
        var rendered = renderer.Render(lowered);

        Assert.Equal(
            """
            MATCH (src)-[r]->(tgt)
            OPTIONAL MATCH (src)-[rels*1..5]->(prop)
            WHERE coalesce(rels[toInteger(0)].__graphModelComplexProperty, false) = true
            WITH src, r, tgt, CASE WHEN rels IS NULL THEN [] ELSE [i IN range(0, size(rels) - 1) | { ParentNode: CASE WHEN i = 0 THEN src ELSE startNode(rels[toInteger(i)]) END, Relationship: rels[toInteger(i)], SequenceNumber: rels[toInteger(i)].SequenceNumber, Property: endNode(rels[toInteger(i)]) }] END AS src_property_path
            WITH src, r, tgt, collect(src_property_path) AS src_properties
            OPTIONAL MATCH (tgt)-[trels*1..5]->(tprop)
            WHERE coalesce(trels[toInteger(0)].__graphModelComplexProperty, false) = true
            WITH src, r, tgt, src_properties, CASE WHEN trels IS NULL THEN [] ELSE [i IN range(0, size(trels) - 1) | { ParentNode: CASE WHEN i = 0 THEN tgt ELSE startNode(trels[toInteger(i)]) END, Relationship: trels[toInteger(i)], SequenceNumber: trels[toInteger(i)].SequenceNumber, Property: endNode(trels[toInteger(i)]) }] END AS tgt_property_path
            WITH src, r, tgt, src_properties, collect(tgt_property_path) AS tgt_properties
            RETURN { StartNode: { Node: src, ComplexProperties: src_properties }, Relationship: r, EndNode: { Node: tgt, ComplexProperties: tgt_properties } } AS PathSegment
            """,
            rendered.Text);
        Assert.DoesNotContain(lowered.Clauses, clause => clause is EntityProjectionClause);
        Assert.DoesNotContain("ALL(", rendered.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("reduce(", rendered.Text, StringComparison.Ordinal);
        Assert.Equal(["PathSegment"], rendered.ProjectionColumns);
    }

    [Fact]
    public void LowersEntityProjectionInsideCallSubquery()
    {
        var statement = Statement(
            MatchNode("src"),
            new CallSubqueryClause(
                ["src"],
                [
                    new EntityProjectionClause(
                        EntityProjectionShape.Node,
                        "src",
                        relationshipAlias: null,
                        targetAlias: null,
                        loadSourceProperties: false,
                        loadTargetProperties: false),
                ]),
            Return("Node"));

        var rendered = renderer.Render(pass.Run(statement));

        Assert.Equal(
            """
            MATCH (src)
            CALL {
              WITH src
              RETURN { Node: src, ComplexProperties: [] } AS Node
            }
            RETURN Node
            """,
            rendered.Text);
    }

    [Fact]
    public void ReturnsSameStatementWhenNoProjectionCompatibilityShapeExists()
    {
        var statement = Statement(Return("src"));

        Assert.Same(statement, pass.Run(statement));
    }

    private static CypherStatement Statement(params ICypherClause[] clauses) =>
        Statement(clauses, new Dictionary<string, object?>(StringComparer.Ordinal));

    private static CypherStatement Statement(
        ICypherClause clause,
        IReadOnlyDictionary<string, object?> parameters) =>
        Statement([clause], parameters);

    private static CypherStatement Statement(
        IReadOnlyList<ICypherClause> clauses,
        IReadOnlyDictionary<string, object?> parameters) =>
        new(clauses, parameters);

    private static ReturnClause Return(string alias) =>
        new([new ReturnItem(new VariableRef(alias), null)], distinct: false);

    private static MatchClause MatchNode(string alias) =>
        new([new PathPattern([new NodePattern(alias, [])])], optional: false);

    private static ReturnItem Variable(string alias) =>
        new(new VariableRef(alias), null);

    private static FunctionCall Function(string name, params CypherExpression[] arguments) =>
        new(name, arguments);
}
