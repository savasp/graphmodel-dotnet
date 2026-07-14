// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph.Cypher.Ast;
using Cvoya.Graph.Cypher.Ast.Expressions;

namespace Cvoya.Graph.Cypher.Tests;

public class CypherRendererTests
{
    private readonly CypherRenderer renderer = new(TestCypherDialect.Full);

    [Fact]
    public void Render_ReturnAliasesBecomeExactProjectionSchema()
    {
        var statement = new CypherStatement(
            [
                new MatchClause([new PathPattern([new NodePattern("n", ["Person"])])], optional: false),
                new ReturnClause(
                [
                    new ReturnItem(new PropertyAccess(new VariableRef("n"), "Name"), "DisplayName"),
                    new ReturnItem(new VariableRef("n"), "NodeValue"),
                ],
                distinct: false),
            ],
            new Dictionary<string, object?>());

        var result = renderer.Render(statement);

        Assert.Equal(["DisplayName", "NodeValue"], result.ProjectionColumns);
    }

    [Fact]
    public void Render_DecomposedPathProjectionReportsCoordinatesInRenderedOrder()
    {
        var statement = new CypherStatement(
            [
                new MatchClause(
                    [new PathPattern([
                        new NodePattern("src", []),
                        new RelationshipPattern("r", CypherDirection.Outgoing, depth: null, types: ["KNOWS"]),
                        new NodePattern("tgt", []),
                    ])],
                    optional: false),
                new EntityProjectionClause(
                    EntityProjectionShape.PathSegment,
                    "src",
                    "r",
                    "tgt",
                    loadSourceProperties: false,
                    loadTargetProperties: false,
                    includePathCoordinates: true),
            ],
            new Dictionary<string, object?>());

        var result = renderer.Render(statement);

        Assert.Equal(["pathIndex", "hopIndex", "PathSegment"], result.ProjectionColumns);
    }

    [Fact]
    public void Render_ListComprehensionWithFilterOnly_UsesCanonicalSyntax()
    {
        var expression = new ListComprehensionExpression(
            IntegerList(),
            "item",
            predicate: new BinaryExpression(
                CypherBinaryOperator.GreaterThan,
                new VariableRef("item"),
                new Literal(1)));

        var result = RenderExpression(expression);

        Assert.Equal("[item IN [1, 2] WHERE item > 1]", result);
    }

    [Fact]
    public void Render_ListComprehensionWithProjectionOnly_UsesCanonicalSyntax()
    {
        var expression = new ListComprehensionExpression(
            IntegerList(),
            "item",
            projection: new BinaryExpression(
                CypherBinaryOperator.Multiply,
                new VariableRef("item"),
                new Literal(2)));

        var result = RenderExpression(expression);

        Assert.Equal("[item IN [1, 2] | item * 2]", result);
    }

    [Fact]
    public void Render_ListComprehensionWithFilterAndProjection_UsesCanonicalSyntax()
    {
        var expression = new ListComprehensionExpression(
            IntegerList(),
            "item",
            new BinaryExpression(
                CypherBinaryOperator.GreaterThan,
                new VariableRef("item"),
                new Literal(1)),
            new BinaryExpression(
                CypherBinaryOperator.Multiply,
                new VariableRef("item"),
                new Literal(2)));

        var result = RenderExpression(expression);

        Assert.Equal("[item IN [1, 2] WHERE item > 1 | item * 2]", result);
    }

    [Fact]
    public void Render_NestedListComprehensions_PreserveTheirLocalAliases()
    {
        var expression = new ListComprehensionExpression(
            new ListExpression([IntegerList()]),
            "outer",
            projection: new ListComprehensionExpression(
                new VariableRef("outer"),
                "inner",
                new BinaryExpression(
                    CypherBinaryOperator.GreaterThan,
                    new VariableRef("inner"),
                    new Literal(1)),
                new BinaryExpression(
                    CypherBinaryOperator.Multiply,
                    new VariableRef("inner"),
                    new Literal(2))));

        var result = RenderExpression(expression);

        Assert.Equal("[outer IN [[1, 2]] | [inner IN outer WHERE inner > 1 | inner * 2]]", result);
    }

    [Fact]
    public void Render_Reduce_UsesAccumulatorAndIteratorSyntax()
    {
        var expression = new ReduceExpression(
            "total",
            new Literal(0),
            "item",
            IntegerList(),
            new BinaryExpression(
                CypherBinaryOperator.Add,
                new VariableRef("total"),
                new VariableRef("item")));

        var result = RenderExpression(expression);

        Assert.Equal("reduce(total = 0, item IN [1, 2] | total + item)", result);
    }

    [Fact]
    public void Render_AllOverPropertyExpression_UsesCanonicalSyntax()
    {
        var expression = new AllExpression(
            "item",
            new PropertyAccess(new VariableRef("n"), "items"),
            new BinaryExpression(
                CypherBinaryOperator.Equal,
                new PropertyAccess(new VariableRef("item"), "active"),
                new Literal(true)));

        var result = RenderExpression(expression);

        Assert.Equal("ALL(item IN n.items WHERE item.active = true)", result);
    }

    [Fact]
    public void Render_FullTextClause_AgainstDialectWithoutSupport_ThrowsNamedTranslationException()
    {
        // TestCypherDialect declares no RenderFullTextSearch override, so it inherits the throwing
        // interface default — the dialect-owned seam's backstop for a dialect that lacks full-text.
        var statement = new CypherStatement(
            [new FullTextSearchClause(Cvoya.Graph.Querying.SearchRootTarget.Nodes, new QueryParameter("p0"), "n")],
            new Dictionary<string, object?> { ["p0"] = "Ada" });

        var exception = Assert.Throws<GraphQueryTranslationException>(() => renderer.Render(statement));

        Assert.Contains(nameof(GraphCapability.FullTextSearch), exception.Message, StringComparison.Ordinal);
        Assert.Contains("TestCypher", exception.Message, StringComparison.Ordinal);
    }

    private string RenderExpression(CypherExpression expression) =>
        ((ICypherRenderContext)renderer).RenderExpression(expression);

    private static ListExpression IntegerList() => new([new Literal(1), new Literal(2)]);
}
