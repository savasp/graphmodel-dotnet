// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Tests;

using Cvoya.Graph.Age.Querying.Cypher.Execution;

public sealed class AgeQueryRunnerTests
{
    [Fact]
    public void MaskQuotedContent_PreservesLengthAndMasksEscapedDelimiterContent()
    {
        const string cypher =
            "RETURN src.`net`` worth, (order by)`, 'literal\\' RETURN, (limit)' ORDER BY src.Id";

        var masked = AgeQueryRunner.MaskQuotedContent(cypher);

        Assert.Equal(cypher.Length, masked.Length);
        Assert.DoesNotContain("worth", masked, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("literal", masked, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(" ORDER BY src.Id", masked, StringComparison.Ordinal);
    }

    [Fact]
    public void NormalizeProjectionAliases_IgnoresKeywordsAndPunctuationInsideStringLiterals()
    {
        const string cypher =
            "MATCH (src:Person) RETURN 'value, (order by) RETURN limit', src.Name LIMIT 1";

        var normalized = AgeQueryRunner.NormalizeProjectionAliases(
            cypher,
            ["value, (order by) RETURN limit", "Name"]);

        Assert.Equal(
            "MATCH (src:Person) RETURN 'value, (order by) RETURN limit' AS age_column_0, " +
            "src.Name LIMIT 1",
            normalized.Cypher);
        Assert.Equal(["age_column_0", "Name"], normalized.Columns);
    }

    [Fact]
    public void NormalizeProjectionAliases_IgnoresKeywordsAndPunctuationInsideEscapedIdentifiers()
    {
        const string cypher =
            "MATCH (src:Person) RETURN src.`net, worth (order by)`, src.Name ORDER BY src.`net, worth (order by)`";

        var normalized = AgeQueryRunner.NormalizeProjectionAliases(
            cypher,
            ["net, worth (order by)", "Name"]);

        Assert.Equal(
            "MATCH (src:Person) RETURN src.`net, worth (order by)` AS age_column_0, src.Name " +
            "ORDER BY src.`net, worth (order by)`",
            normalized.Cypher);
        Assert.Equal(["age_column_0", "Name"], normalized.Columns);
    }

    [Fact]
    public void NormalizeProjectionAliases_IgnoresDoubledBacktickAndLimitInsideEscapedIdentifier()
    {
        const string cypher = "MATCH (src:Person) RETURN src.`note`` LIMIT, (x)` LIMIT 1";

        var normalized = AgeQueryRunner.NormalizeProjectionAliases(
            cypher,
            ["note` LIMIT, (x)"]);

        Assert.Equal(
            "MATCH (src:Person) RETURN src.`note`` LIMIT, (x)` AS age_column_0 LIMIT 1",
            normalized.Cypher);
        Assert.Equal(["age_column_0"], normalized.Columns);
    }

    [Fact]
    public void InferProjectionColumns_UsesOriginalEscapedIdentifiersAtMaskedBoundaries()
    {
        const string cypher =
            "MATCH (src:Person) RETURN src.`net, worth (order by)`, src.`note`` value` AS `output alias`";

        var columns = AgeQueryRunner.InferProjectionColumns(cypher);

        Assert.Equal(["net, worth (order by)", "output alias"], columns);
    }

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
