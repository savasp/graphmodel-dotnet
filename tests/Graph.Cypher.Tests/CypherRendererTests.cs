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
}
