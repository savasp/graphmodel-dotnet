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

using Cvoya.Graph.Model.Cypher.Ast;
using Cvoya.Graph.Model.Cypher.Ast.Expressions;
using Cvoya.Graph.Model.Cypher.Validation;

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
