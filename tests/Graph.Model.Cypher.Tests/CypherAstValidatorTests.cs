// Copyright 2025 Savas Parastatidis
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      https://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace Cvoya.Graph.Model.Cypher.Tests;

using Cvoya.Graph.Model;
using Cvoya.Graph.Model.Cypher.Ast;
using Cvoya.Graph.Model.Cypher.Ast.Expressions;
using Cvoya.Graph.Model.Cypher.Validation;

public class CypherAstValidatorTests
{
    private readonly CypherAstValidator validator = new();

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
            new UnwindClause(new Literal(new[] { 1, 2, 3 }), "item"),
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

    private static MatchClause MatchNode(string alias)
    {
        return new MatchClause([new PathPattern([new NodePattern(alias, ["Person"])])], optional: false);
    }
}
