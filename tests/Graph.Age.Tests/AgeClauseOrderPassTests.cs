// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Tests;

using Cvoya.Graph.Age.Querying.Cypher;
using Cvoya.Graph.Age.Querying.Cypher.Lowering;
using Cvoya.Graph.Cypher;
using Cvoya.Graph.Cypher.Ast;
using Cvoya.Graph.Cypher.Ast.Expressions;

public sealed class AgeClauseOrderPassTests
{
    private readonly AgeClauseOrderPass pass = new();
    private readonly CypherRenderer renderer = new(AgeDialect.Instance);

    [Fact]
    public void PreservesPrimaryPagingAndMovesContinuationOrdering()
    {
        var statement = Statement(
            Match("src"),
            WithClause.All,
            OrderBy(Property("src", "Age")),
            new LimitClause(new Literal(3)),
            WithClause.All,
            new WhereClause(new BinaryExpression(
                CypherBinaryOperator.GreaterThanOrEqual,
                Property("src", "Age"),
                new Literal(2))),
            WithClause.All,
            OrderBy(Property("src", "Age"), descending: true),
            Return("src"));

        var rendered = renderer.Render(pass.Run(statement));

        Assert.Equal(
            """
            MATCH (src)
            WITH *
            ORDER BY src.Age
            LIMIT 3
            WITH *
            WHERE src.Age >= 2
            WITH *
            RETURN src
            ORDER BY src.Age DESC
            """,
            rendered.Text);
    }

    [Fact]
    public void PreservesCarriedProjectionOrderingPipeline()
    {
        var statement = Statement(
            Match("src"),
            WithClause.All,
            OrderBy(Property("src", "Age")),
            new LimitClause(new Literal(3)),
            WithClause.All,
            OrderBy(Property("src", "Age"), descending: true),
            new EntityProjectionClause(
                EntityProjectionShape.Node,
                "src",
                relationshipAlias: null,
                targetAlias: null,
                loadSourceProperties: false,
                loadTargetProperties: false,
                includePathCoordinates: false,
                ordering: [new OrderByItem(Property("src", "Age"), descending: true)]));

        var lowered = pass.Run(statement);

        Assert.Same(statement, lowered);
    }

    [Fact]
    public void MovesOnlyUnscopedOrderingAndPaging()
    {
        var statement = Statement(
            Match("src"),
            OrderBy(Property("src", "Age")),
            new LimitClause(new Literal(3)),
            Return("src"));

        var rendered = renderer.Render(pass.Run(statement));

        Assert.Equal(
            """
            MATCH (src)
            RETURN src
            ORDER BY src.Age
            LIMIT 3
            """,
            rendered.Text);
    }

    [Fact]
    public void RewritesEntityOrderingToPublicId()
    {
        var statement = Statement(
            Match("src"),
            OrderBy(new VariableRef("src"), descending: true),
            Return("src"));

        var rendered = renderer.Render(pass.Run(statement));

        Assert.Equal(
            """
            MATCH (src)
            RETURN src
            ORDER BY src.Id DESC
            """,
            rendered.Text);
    }

    [Fact]
    public void KeepsPathPagingAheadOfDecomposition()
    {
        var statement = Statement(
            new MatchClause(
            [
                new PathPattern(
                [
                    new NodePattern("src", []),
                    new RelationshipPattern("r", null, CypherDirection.Outgoing, new DepthRange(1, 3)),
                    new NodePattern("tgt", []),
                ], "p"),
            ], optional: false),
            new SkipClause(new Literal(2)),
            new LimitClause(new Literal(4)),
            new WithClause(
                [new ReturnItem(Function("collect", new VariableRef("p")), "__paths")],
                distinct: false),
            new ReturnClause(
                [new ReturnItem(new VariableRef("__paths"), null)],
                distinct: false));

        var rendered = renderer.Render(pass.Run(statement));

        Assert.Equal(
            """
            MATCH p = (src)-[r*1..3]->(tgt)
            WITH p
            SKIP 2
            LIMIT 4
            WITH collect(p) AS __paths
            RETURN __paths
            """,
            rendered.Text);
    }

    [Fact]
    public void PlacesPagingBeforeAggregateReturn()
    {
        var statement = Statement(
            Match("src"),
            OrderBy(Property("src", "Age")),
            new LimitClause(new Literal(3)),
            new WithClause(
                [new ReturnItem(new VariableRef("src"), null)],
                distinct: false),
            new ReturnClause(
                [new ReturnItem(Function("count", new VariableRef("src")), null)],
                distinct: false));

        var rendered = renderer.Render(pass.Run(statement));

        Assert.Equal(
            """
            MATCH (src)
            WITH src
            ORDER BY src.Age
            LIMIT 3
            RETURN count(src)
            """,
            rendered.Text);
    }

    private static CypherStatement Statement(params ICypherClause[] clauses) =>
        new(clauses, new Dictionary<string, object?>(StringComparer.Ordinal));

    private static MatchClause Match(string alias) => new(
        [new PathPattern([new NodePattern(alias, [])])],
        optional: false);

    private static ReturnClause Return(string alias) => new(
        [new ReturnItem(new VariableRef(alias), null)],
        distinct: false);

    private static OrderByClause OrderBy(CypherExpression expression, bool descending = false) =>
        new([new OrderByItem(expression, descending)]);

    private static PropertyAccess Property(string alias, string property) =>
        new(new VariableRef(alias), property);

    private static FunctionCall Function(string name, params CypherExpression[] arguments) =>
        new(name, arguments);
}
