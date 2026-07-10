// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Cypher.Tests;

using Cvoya.Graph.Cypher.Ast;
using Cvoya.Graph.Cypher.Ast.Expressions;
using Cvoya.Graph.Cypher.Validation;

public class CypherPassRunnerTests
{
    [Fact]
    public void Run_AppliesPassesInOrder()
    {
        var order = new List<string>();
        var statement = new CypherStatement(
        [
            new ReturnClause([new ReturnItem(new Literal(1), "value")], distinct: false)
        ], new Dictionary<string, object?>());

        var first = new RecordingPass("first", order, input => AddClause(input, new LimitClause(new Literal(10))));

        var second = new RecordingPass("second", order, input => AddClause(input, new SkipClause(new Literal(1))));

        var runner = new CypherPassRunner([first, second]);

        var result = runner.Run(statement);

        Assert.Equal(["first", "second"], order);
        Assert.IsType<LimitClause>(result.Clauses[1]);
        Assert.IsType<SkipClause>(result.Clauses[2]);
    }

    private static CypherStatement AddClause(CypherStatement input, ICypherClause clause)
    {
        return new CypherStatement([.. input.Clauses, clause], input.Parameters);
    }

    private sealed class RecordingPass(
        string name,
        List<string> order,
        Func<CypherStatement, CypherStatement> transform) : ICypherPass
    {
        public CypherStatement Run(CypherStatement input)
        {
            order.Add(name);

            return transform(input);
        }
    }
}
