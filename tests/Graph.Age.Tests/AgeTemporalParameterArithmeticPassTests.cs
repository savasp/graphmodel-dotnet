// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Tests;

using Cvoya.Graph.Age.Querying.Cypher;
using Cvoya.Graph.Age.Querying.Cypher.Lowering;
using Cvoya.Graph.Cypher;
using Cvoya.Graph.Cypher.Ast;
using Cvoya.Graph.Cypher.Ast.Expressions;

public sealed class AgeTemporalParameterArithmeticPassTests
{
    private readonly AgeTemporalParameterArithmeticPass pass = new();
    private readonly CypherRenderer renderer = new(AgeDialect.Instance);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FoldsTemporalParameterArithmeticToLegacyGoldenOutput(bool useString)
    {
        var timestamp = new DateTime(2026, 7, 11, 8, 0, 0, DateTimeKind.Utc);
        var statement = Statement(
            TemporalAdd("days"),
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["p0"] = useString ? timestamp.ToString("O") : timestamp,
                ["p1"] = -2,
            });

        var lowered = pass.Run(statement);
        var rendered = renderer.Render(lowered);

        Assert.Equal("RETURN $age_temporal_0 AS value", rendered.Text);
        Assert.Equal(
            timestamp.AddDays(-2),
            Assert.IsType<DateTimeOffset>(lowered.Parameters["age_temporal_0"]).UtcDateTime);
    }

    [Theory]
    [InlineData("days", 2)]
    [InlineData("hours", 3)]
    [InlineData("minutes", 4)]
    [InlineData("seconds", 5)]
    [InlineData("milliseconds", 6)]
    [InlineData("months", 7)]
    [InlineData("years", 8)]
    public void FoldsEverySupportedDurationUnit(string unit, int amount)
    {
        var timestamp = new DateTimeOffset(2020, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var statement = Statement(
            TemporalAdd(unit),
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["p0"] = timestamp,
                ["p1"] = amount,
            });

        var lowered = pass.Run(statement);

        var expected = unit switch
        {
            "days" => timestamp.AddDays(amount),
            "hours" => timestamp.AddHours(amount),
            "minutes" => timestamp.AddMinutes(amount),
            "seconds" => timestamp.AddSeconds(amount),
            "milliseconds" => timestamp.AddMilliseconds(amount),
            "months" => timestamp.AddMonths(amount),
            "years" => timestamp.AddYears(amount),
            _ => throw new InvalidOperationException(),
        };
        Assert.Equal(expected, lowered.Parameters["age_temporal_0"]);
    }

    [Fact]
    public void EvaluatesParameterFreeTemporalFunctionsAndUnwrapsTemporalMembers()
    {
        var statement = new CypherStatement(
        [
            new MatchClause(
                [new PathPattern([new NodePattern("src", [])])],
                optional: false),
            new ReturnClause(
            [
                new ReturnItem(Function("temporal.datetime"), "now"),
                new ReturnItem(
                    new PropertyAccess(
                        Function("temporal.datetime", new PropertyAccess(new VariableRef("src"), "Created")),
                        "year"),
                    "year"),
            ], distinct: false),
        ], new Dictionary<string, object?>(StringComparer.Ordinal));

        var lowered = pass.Run(statement);
        var rendered = renderer.Render(lowered);

        Assert.Equal(
            """
            MATCH (src)
            RETURN $age_temporal_0 AS now, src.Created.year AS year
            """,
            rendered.Text);
        Assert.Equal(DateTimeKind.Utc, Assert.IsType<DateTime>(lowered.Parameters["age_temporal_0"]).Kind);
    }

    [Fact]
    public void LeavesUnparseableArithmeticStructuredAfterRemovingWrappers()
    {
        var statement = Statement(
            TemporalAdd("days"),
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["p0"] = "not-a-date",
                ["p1"] = 2,
            });

        var lowered = pass.Run(statement);
        var rendered = renderer.Render(lowered);

        Assert.Equal("RETURN $p0 + { days: $p1 } AS value", rendered.Text);
        Assert.DoesNotContain("age_temporal_0", lowered.Parameters);
    }

    private static CypherStatement Statement(
        CypherExpression expression,
        IReadOnlyDictionary<string, object?> parameters) => new(
        [new ReturnClause([new ReturnItem(expression, "value")], distinct: false)],
        parameters);

    private static FunctionCall TemporalAdd(string unit) => Function(
        "temporal.datetime",
        new BinaryExpression(
            CypherBinaryOperator.Add,
            new QueryParameter("p0"),
            Function(
                "temporal.duration",
                new MapExpression([new MapEntry(unit, new QueryParameter("p1"))]))));

    private static FunctionCall Function(string name, params CypherExpression[] arguments) =>
        new(name, arguments);
}
