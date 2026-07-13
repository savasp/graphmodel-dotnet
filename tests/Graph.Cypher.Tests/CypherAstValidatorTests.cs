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
}
