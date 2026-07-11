// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Tests;

using Cvoya.Graph.Age.Querying.Cypher.Execution;

public sealed class AgeQueryRunnerTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FoldsTemporalParameterArithmetic(bool useString)
    {
        var timestamp = new DateTime(2026, 7, 11, 8, 0, 0, DateTimeKind.Utc);
        var parameters = new Dictionary<string, object?>
        {
            ["p0"] = useString ? timestamp.ToString("O") : timestamp,
            ["p1"] = -2,
        };

        var result = AgeQueryRunner.NormalizeTemporalParameterArithmetic(
            "RETURN datetime($p0 + duration({ days: $p1 })) AS value",
            parameters);

        Assert.Equal("RETURN $age_temporal_0 AS value", result.Cypher);
        Assert.Equal(timestamp.AddDays(-2), ((DateTimeOffset)result.Parameters["age_temporal_0"]!).UtcDateTime);
    }
}
