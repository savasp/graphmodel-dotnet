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

public class GeneratedWellFormedStatementTests
{
    [Fact]
    public void ValidatorAcceptsDeterministicallyGeneratedWellFormedStatements()
    {
        var validator = new CypherAstValidator();

        for (var seed = 0; seed < 100; seed++)
        {
            var statement = BuildStatement(seed);

            validator.Run(statement);
        }
    }

    private static CypherStatement BuildStatement(int seed)
    {
        var generator = new Generator(seed + 1);
        var startAlias = $"n{seed}";
        var endAlias = $"m{seed}";
        var relationshipAlias = $"r{seed}";
        var parameterName = $"p{seed}";
        var clauses = new List<ICypherClause>
        {
            new MatchClause(
            [
                new PathPattern(
                [
                    new NodePattern(startAlias, LabelSet(generator)),
                    new RelationshipPattern(
                        relationshipAlias,
                        generator.NextBool() ? "KNOWS" : null,
                        Direction(generator),
                        generator.NextBool() ? new DepthRange(0, generator.Next(4)) : null),
                    new NodePattern(endAlias, LabelSet(generator))
                ])
            ], optional: generator.NextBool())
        };

        if (generator.NextBool())
        {
            clauses.Add(new WhereClause(new BinaryExpression(
                Comparison(generator),
                new PropertyAccess(new VariableRef(startAlias), "Name"),
                new QueryParameter(parameterName))));
        }

        if (generator.NextBool())
        {
            var projectedAlias = $"projected{seed}";
            clauses.Add(new WithClause(
            [
                new ReturnItem(new VariableRef(startAlias), null),
                new ReturnItem(new PropertyAccess(new VariableRef(endAlias), "Name"), projectedAlias)
            ], distinct: generator.NextBool()));
            clauses.Add(new ReturnClause(
            [
                new ReturnItem(new VariableRef(startAlias), null),
                new ReturnItem(new VariableRef(projectedAlias), null)
            ], distinct: generator.NextBool()));
        }
        else
        {
            clauses.Add(new ReturnClause(
            [
                new ReturnItem(new VariableRef(startAlias), null),
                new ReturnItem(new VariableRef(endAlias), null),
                new ReturnItem(new VariableRef(relationshipAlias), null)
            ], distinct: generator.NextBool()));
        }

        if (generator.NextBool())
        {
            clauses.Add(new LimitClause(new Literal(generator.Next(50) + 1)));
        }

        var parameters = clauses
            .SelectMany(FindParameterNames)
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(name => name, name => (object?)$"value-{name}", StringComparer.Ordinal);

        return new CypherStatement(clauses, parameters);
    }

    private static IReadOnlyList<string> LabelSet(Generator generator)
    {
        return generator.Next(3) switch
        {
            0 => [],
            1 => ["Person"],
            _ => ["Person", "Employee"]
        };
    }

    private static CypherDirection Direction(Generator generator)
    {
        return generator.Next(3) switch
        {
            0 => CypherDirection.Outgoing,
            1 => CypherDirection.Incoming,
            _ => CypherDirection.Both
        };
    }

    private static CypherBinaryOperator Comparison(Generator generator)
    {
        return generator.Next(4) switch
        {
            0 => CypherBinaryOperator.Equal,
            1 => CypherBinaryOperator.NotEqual,
            2 => CypherBinaryOperator.StartsWith,
            _ => CypherBinaryOperator.Contains
        };
    }

    private static IEnumerable<string> FindParameterNames(ICypherClause clause)
    {
        return clause switch
        {
            WhereClause where => FindParameterNames(where.Predicate),
            WithClause with => with.Items.SelectMany(item => FindParameterNames(item.Expression)),
            UnwindClause unwind => FindParameterNames(unwind.Source),
            CallClause call => call.Arguments.SelectMany(FindParameterNames),
            ReturnClause @return => @return.Items.SelectMany(item => FindParameterNames(item.Expression)),
            OrderByClause orderBy => orderBy.Items.SelectMany(item => FindParameterNames(item.Expression)),
            SkipClause skip => FindParameterNames(skip.Count),
            LimitClause limit => FindParameterNames(limit.Count),
            _ => []
        };
    }

    private static IEnumerable<string> FindParameterNames(CypherExpression expression)
    {
        return expression switch
        {
            QueryParameter parameter => [parameter.Name],
            PropertyAccess property => FindParameterNames(property.Target),
            FunctionCall function => function.Arguments.SelectMany(FindParameterNames),
            BinaryExpression binary => FindParameterNames(binary.Left).Concat(FindParameterNames(binary.Right)),
            UnaryExpression unary => FindParameterNames(unary.Operand),
            LabelTest label => FindParameterNames(label.Target),
            _ => []
        };
    }

    private sealed class Generator(int seed)
    {
        private uint state = (uint)seed;

        public int Next(int exclusiveMaximum)
        {
            state = (state * 1_664_525) + 1_013_904_223;

            return (int)(state % (uint)exclusiveMaximum);
        }

        public bool NextBool()
        {
            return Next(2) == 0;
        }
    }
}
