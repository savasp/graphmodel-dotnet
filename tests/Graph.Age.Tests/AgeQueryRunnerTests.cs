// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Tests;

using Cvoya.Graph.Age.Querying.Cypher.Execution;

public sealed class AgeQueryRunnerTests
{
    [Fact]
    public async Task StreamingCursorFetchesOnDemandAndDisposesAbandonedSource()
    {
        var source = new RecordingAgeRecordSource(
            Record(("value", 1L)),
            Record(("value", 2L)));
        var cursor = new AgeResultCursor(source, distinct: false);
        using var cts = new CancellationTokenSource();

        Assert.Equal(0, source.ReadCount);
        Assert.True(await cursor.FetchAsync(cts.Token));
        Assert.Equal(1L, cursor.Current["value"]);
        Assert.Equal(1, source.ReadCount);
        Assert.Equal(cts.Token, source.LastCancellationToken);

        await cursor.DisposeAsync();

        Assert.Equal(1, source.DisposeCount);
        Assert.Equal(1, source.ReadCount);
    }

    [Fact]
    public async Task StreamingCursorAppliesDistinctWithoutBufferingAllRecords()
    {
        var source = new RecordingAgeRecordSource(
            Record(("value", 1L)),
            Record(("value", 1L)),
            Record(("value", 2L)));
        await using var cursor = new AgeResultCursor(source, distinct: true);

        Assert.True(await cursor.FetchAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1L, cursor.Current["value"]);
        Assert.Equal(1, source.ReadCount);

        Assert.True(await cursor.FetchAsync(TestContext.Current.CancellationToken));
        Assert.Equal(2L, cursor.Current["value"]);
        Assert.Equal(3, source.ReadCount);
    }

    [Fact]
    public async Task StreamingCursorRejectsPreCancelledEnumeration()
    {
        var source = new RecordingAgeRecordSource(Record(("value", 1L)));
        await using var cursor = new AgeResultCursor(source, distinct: false);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await cursor.FetchAsync(cts.Token));

        Assert.Equal(0, source.ReadCount);
    }

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

    private static AgeRecord Record(params (string Key, object? Value)[] values) =>
        new(values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));

    private sealed class RecordingAgeRecordSource(params AgeRecord[] records) : IAgeRecordSource
    {
        private int index = -1;

        public AgeRecord Current { get; private set; } = null!;

        public int ReadCount { get; private set; }

        public int DisposeCount { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public ValueTask<bool> ReadAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastCancellationToken = cancellationToken;
            ReadCount++;
            index++;
            if (index >= records.Length)
            {
                return ValueTask.FromResult(false);
            }

            Current = records[index];
            return ValueTask.FromResult(true);
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }
}
