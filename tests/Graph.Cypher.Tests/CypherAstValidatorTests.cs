// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Cypher.Tests;

using Cvoya.Graph;
using Cvoya.Graph.Cypher.Ast;
using Cvoya.Graph.Cypher.Ast.Expressions;
using Cvoya.Graph.Cypher.Validation;

public class CypherAstValidatorTests
{
    private readonly CypherAstValidator validator = new();
    private static readonly int[] UnwindValues = [1, 2, 3];

    [Fact]
    public void Run_ReturnsInput_WhenStatementIsValid()
    {
        var statement = new CypherStatement(
        [
            MatchNode("n"),
            new WhereClause(new BinaryExpression(
                CypherBinaryOperator.Equal,
                new PropertyAccess(new VariableRef("n"), "Name"),
                new QueryParameter("name"))),
            new ReturnClause([new ReturnItem(new VariableRef("n"), null)], distinct: false)
        ], new Dictionary<string, object?> { ["name"] = "Ada" });

        var result = validator.Run(statement);

        Assert.Same(statement, result);
    }

    [Fact]
    public void Run_ThrowsPreciseException_WhenVariableIsUnbound()
    {
        var statement = new CypherStatement(
        [
            new WhereClause(new VariableRef("n"))
        ], new Dictionary<string, object?>());

        var ex = Assert.Throws<GraphException>(() => validator.Run(statement));

        Assert.Contains("'n'", ex.Message, StringComparison.Ordinal);
        Assert.Contains("not bound", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Run_ThrowsPreciseException_WhenPropertyTargetIsUnbound()
    {
        var statement = new CypherStatement(
        [
            new ReturnClause([new ReturnItem(new PropertyAccess(new VariableRef("missing"), "Name"), null)], distinct: false)
        ], new Dictionary<string, object?>());

        var ex = Assert.Throws<GraphException>(() => validator.Run(statement));

        Assert.Contains("'missing'", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_ThrowsPreciseException_WhenParameterIsMissing()
    {
        var statement = new CypherStatement(
        [
            MatchNode("n"),
            new WhereClause(new BinaryExpression(
                CypherBinaryOperator.Equal,
                new VariableRef("n"),
                new QueryParameter("missing")))
        ], new Dictionary<string, object?>());

        var ex = Assert.Throws<GraphException>(() => validator.Run(statement));

        Assert.Contains("'missing'", ex.Message, StringComparison.Ordinal);
        Assert.Contains("parameter", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Run_AllowsRepeatedUseOfDefinedParameter()
    {
        var statement = new CypherStatement(
        [
            MatchNode("n"),
            new WhereClause(new BinaryExpression(
                CypherBinaryOperator.And,
                new BinaryExpression(CypherBinaryOperator.Equal, new VariableRef("n"), new QueryParameter("id")),
                new BinaryExpression(CypherBinaryOperator.NotEqual, new VariableRef("n"), new QueryParameter("id"))))
        ], new Dictionary<string, object?> { ["id"] = "node-1" });

        validator.Run(statement);
    }

    [Fact]
    public void Run_UsesWithProjectionAsNextScope()
    {
        var statement = new CypherStatement(
        [
            MatchNode("n"),
            new WithClause([new ReturnItem(new PropertyAccess(new VariableRef("n"), "Name"), "name")], distinct: false),
            new ReturnClause([new ReturnItem(new VariableRef("n"), null)], distinct: false)
        ], new Dictionary<string, object?>());

        var ex = Assert.Throws<GraphException>(() => validator.Run(statement));

        Assert.Contains("'n'", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_AllowsWithProjectionAliasInLaterClauses()
    {
        var statement = new CypherStatement(
        [
            MatchNode("n"),
            new WithClause([new ReturnItem(new PropertyAccess(new VariableRef("n"), "Name"), "name")], distinct: false),
            new ReturnClause([new ReturnItem(new VariableRef("name"), null)], distinct: false)
        ], new Dictionary<string, object?>());

        validator.Run(statement);
    }

    [Fact]
    public void Run_AllowsUnwindAliasInLaterClauses()
    {
        var statement = new CypherStatement(
        [
            new UnwindClause(new Literal(UnwindValues), "item"),
            new ReturnClause([new ReturnItem(new VariableRef("item"), null)], distinct: false)
        ], new Dictionary<string, object?>());

        validator.Run(statement);
    }

    [Fact]
    public void Run_AllowsCallYieldInLaterClauses()
    {
        var statement = new CypherStatement(
        [
            new CallClause("db.labels", [], ["label"]),
            new ReturnClause([new ReturnItem(new VariableRef("label"), null)], distinct: false)
        ], new Dictionary<string, object?>());

        validator.Run(statement);
    }

    [Fact]
    public void Run_AllowsScopedCallReturnAliasInOuterScope()
    {
        var statement = new CypherStatement(
        [
            MatchNode("n"),
            new CallSubqueryClause(
                ["n"],
                [new ReturnClause([new ReturnItem(new Literal(1), "value")], distinct: false)]),
            new ReturnClause([new ReturnItem(new VariableRef("value"), null)], distinct: false),
        ], new Dictionary<string, object?>());

        validator.Run(statement);
    }

    [Fact]
    public void Run_ThrowsWhenScopedCallDoesNotEndWithReturn()
    {
        var statement = new CypherStatement(
        [
            MatchNode("n"),
            new CallSubqueryClause(["n"], [new WithClause([new ReturnItem(new VariableRef("n"), null)], false)]),
        ], new Dictionary<string, object?>());

        var exception = Assert.Throws<GraphException>(() => validator.Run(statement));

        Assert.Contains("must end with a RETURN", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_ThrowsWhenScopedCallOutputConflictsWithOuterScope()
    {
        var statement = new CypherStatement(
        [
            MatchNode("n"),
            new CallSubqueryClause(
                ["n"],
                [new ReturnClause([new ReturnItem(new Literal(1), "n")], distinct: false)]),
        ], new Dictionary<string, object?>());

        var exception = Assert.Throws<GraphException>(() => validator.Run(statement));

        Assert.Contains("conflicts with an outer-scope variable", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_ThrowsPreciseException_WhenEntityProjectionAliasIsUnbound()
    {
        var statement = new CypherStatement(
        [
            MatchNode("n"),
            new EntityProjectionClause(
                EntityProjectionShape.Node,
                "missing",
                relationshipAlias: null,
                targetAlias: null,
                loadSourceProperties: false,
                loadTargetProperties: false)
        ], new Dictionary<string, object?>());

        var ex = Assert.Throws<GraphException>(() => validator.Run(statement));

        Assert.Contains("'missing'", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_ThrowsPreciseException_WhenEntityProjectionTargetAliasIsUnbound()
    {
        var statement = new CypherStatement(
        [
            new MatchClause([new PathPattern(
            [
                new NodePattern("src", ["Person"]),
                new RelationshipPattern("r", "KNOWS", CypherDirection.Outgoing, null),
                new NodePattern("tgt", ["Person"])
            ])], optional: false),
            new EntityProjectionClause(
                EntityProjectionShape.PathSegment,
                "src",
                relationshipAlias: "r",
                targetAlias: "elsewhere",
                loadSourceProperties: false,
                loadTargetProperties: false)
        ], new Dictionary<string, object?>());

        var ex = Assert.Throws<GraphException>(() => validator.Run(statement));

        Assert.Contains("'elsewhere'", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_DoesNotLeakPatternSubqueryAliasesIntoOuterScope()
    {
        var subquery = new PatternSubqueryExpression(
            PatternSubqueryKind.Exists,
            new PathPattern(
            [
                new NodePattern("n", []),
                new RelationshipPattern(null, "HAS", CypherDirection.Outgoing, null),
                new NodePattern("inner", ["Address"])
            ]));
        var statement = new CypherStatement(
        [
            MatchNode("n"),
            new WhereClause(subquery),
            new ReturnClause([new ReturnItem(new VariableRef("inner"), null)], distinct: false)
        ], new Dictionary<string, object?>());

        var ex = Assert.Throws<GraphException>(() => validator.Run(statement));

        Assert.Contains("'inner'", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_AcceptsNestedListComprehensionScopes()
    {
        var expression = new ListComprehensionExpression(
            new ListExpression([new ListExpression([new Literal(1), new Literal(2)])]),
            "outer",
            projection: new ListComprehensionExpression(
                new VariableRef("outer"),
                "inner",
                predicate: new BinaryExpression(
                    CypherBinaryOperator.GreaterThan,
                    new VariableRef("inner"),
                    new Literal(0)),
                projection: new BinaryExpression(
                    CypherBinaryOperator.Add,
                    new VariableRef("inner"),
                    new IndexExpression(new VariableRef("outer"), new Literal(0)))));

        validator.Run(ReturnExpression(expression));
    }

    [Fact]
    public void Run_AcceptsReduceBodyReferencesToAccumulatorAndIterator()
    {
        var expression = new ReduceExpression(
            "total",
            new Literal(0),
            "item",
            new ListExpression([new Literal(1), new Literal(2)]),
            new BinaryExpression(
                CypherBinaryOperator.Add,
                new VariableRef("total"),
                new VariableRef("item")));

        validator.Run(ReturnExpression(expression));
    }

    [Fact]
    public void Run_AcceptsAllIteratorInPredicateOverOuterProperty()
    {
        var expression = new AllExpression(
            "item",
            new PropertyAccess(new VariableRef("n"), "items"),
            new PropertyAccess(new VariableRef("item"), "active"));
        var statement = new CypherStatement(
        [
            MatchNode("n"),
            new ReturnClause([new ReturnItem(expression, null)], distinct: false),
        ], new Dictionary<string, object?>());

        validator.Run(statement);
    }

    [Fact]
    public void Run_DoesNotBindListIteratorInItsSourceExpression()
    {
        var expression = new ListComprehensionExpression(
            new VariableRef("item"),
            "item",
            projection: new VariableRef("item"));

        var ex = Assert.Throws<GraphException>(() => validator.Run(ReturnExpression(expression)));

        Assert.Contains("'item'", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_DoesNotBindReduceAccumulatorInItsSeedExpression()
    {
        var expression = new ReduceExpression(
            "total",
            new VariableRef("total"),
            "item",
            new ListExpression([new Literal(1)]),
            new BinaryExpression(
                CypherBinaryOperator.Add,
                new VariableRef("total"),
                new VariableRef("item")));

        var ex = Assert.Throws<GraphException>(() => validator.Run(ReturnExpression(expression)));

        Assert.Contains("'total'", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_DoesNotBindReduceIteratorInItsSourceExpression()
    {
        var expression = new ReduceExpression(
            "total",
            new Literal(0),
            "item",
            new VariableRef("item"),
            new BinaryExpression(
                CypherBinaryOperator.Add,
                new VariableRef("total"),
                new VariableRef("item")));

        var ex = Assert.Throws<GraphException>(() => validator.Run(ReturnExpression(expression)));

        Assert.Contains("'item'", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_DoesNotLeakListComprehensionAliasIntoSurroundingClause()
    {
        var expression = new ListComprehensionExpression(
            new ListExpression([new Literal(1)]),
            "item",
            projection: new VariableRef("item"));
        var statement = new CypherStatement(
        [
            new ReturnClause(
            [
                new ReturnItem(expression, "items"),
                new ReturnItem(new VariableRef("item"), "leaked"),
            ], distinct: false),
        ], new Dictionary<string, object?>());

        var ex = Assert.Throws<GraphException>(() => validator.Run(statement));

        Assert.Contains("'item'", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_WildcardWith_PreservesTheCurrentScope()
    {
        var statement = new CypherStatement(
        [
            MatchNode("n"),
            WithClause.All,
            new ReturnClause([new ReturnItem(new VariableRef("n"), null)], distinct: false)
        ], new Dictionary<string, object?>());

        validator.Run(statement);
    }

    private static MatchClause MatchNode(string alias)
    {
        return new MatchClause([new PathPattern([new NodePattern(alias, ["Person"])])], optional: false);
    }

    private static CypherStatement ReturnExpression(CypherExpression expression)
    {
        return new CypherStatement(
            [new ReturnClause([new ReturnItem(expression, null)], distinct: false)],
            new Dictionary<string, object?>());
    }
}
