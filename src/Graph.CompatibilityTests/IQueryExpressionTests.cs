// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

/// <summary>
/// Provider-neutral semantic coverage for composed predicates, projections, sequence operators,
/// aggregates, and terminals over the common public query-expression contract.
/// </summary>
public interface IQueryExpressionTests : IGraphTest
{
    [Fact]
    public async Task BooleanComparisonNullAndCapturedPredicates_SelectOnlyMatchingRows()
    {
        var scope = await SeedAsync();
        var minimum = 10;
        var maximum = 40;
        var optionalPrefix = "n";
        var excludedCategory = "C";
        var blockedScore = 30;

        var markers = await Graph.Nodes<QueryExpressionCoverageNode>()
            .Where(node =>
                node.Scope == scope &&
                node.Score >= minimum &&
                node.Score <= maximum &&
                (node.OptionalText == null || node.OptionalText.StartsWith(optionalPrefix)) &&
                !(node.Category == excludedCategory || node.Score == blockedScore))
            .OrderBy(node => node.Marker)
            .Select(node => node.Marker)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["alpha-null", "amber-note"], markers);
    }

    [Fact]
    [RequiresCapability(GraphCapability.NullElementsInSimpleCollections)]
    public async Task CapturedAndPropertyCollectionMembership_HandleDuplicatesNullAndEmptyResults()
    {
        var scope = await SeedAsync();
        var allowedCategories = new[] { "A", "C" };

        var allowedBlueMarkers = await Graph.Nodes<QueryExpressionCoverageNode>()
            .Where(node =>
                node.Scope == scope &&
                allowedCategories.Contains(node.Category) &&
                node.Tags.Contains("blue"))
            .OrderBy(node => node.Marker)
            .Select(node => node.Marker)
            .ToListAsync(TestContext.Current.CancellationToken);

        var nullTagMarkers = await Graph.Nodes<QueryExpressionCoverageNode>()
            .Where(node => node.Scope == scope && node.Tags.Contains(null))
            .OrderBy(node => node.Marker)
            .Select(node => node.Marker)
            .ToListAsync(TestContext.Current.CancellationToken);

        var missingTagMarkers = await Graph.Nodes<QueryExpressionCoverageNode>()
            .Where(node => node.Scope == scope && node.Tags.Contains("missing"))
            .Select(node => node.Marker)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["alpha-null", "amber-note"], allowedBlueMarkers);
        Assert.Equal(["alpha-null", "beta-null"], nullTagMarkers);
        Assert.Empty(missingTagMarkers);
    }

    [Fact]
    public async Task StringNumericAndTemporalExpressions_ComposeWithoutClientEvaluation()
    {
        var scope = await SeedAsync();
        var targetScore = 15;
        var tolerance = 6;
        var year = 2024;
        var cutoff = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var normalizedPrefix = "a";

#pragma warning disable CA1862 // Exercise the provider-translated normalization pipeline.
        var markers = await Graph.Nodes<QueryExpressionCoverageNode>()
            .Where(node =>
                node.Scope == scope &&
                node.Name.Trim().ToLowerInvariant().StartsWith(normalizedPrefix) &&
                Math.Abs(node.Score - targetScore) <= tolerance &&
                node.OccurredAt.Year == year &&
                node.OccurredAt.AddDays(1) < cutoff)
            .OrderBy(node => node.Marker)
            .Select(node => node.Marker)
            .ToListAsync(TestContext.Current.CancellationToken);
#pragma warning restore CA1862

        Assert.Equal(["alpha-null", "amber-note"], markers);
    }

    [Fact]
    public async Task ScalarAnonymousAndConstructorProjections_PreserveNullsAndComputedValues()
    {
        var scope = await SeedAsync();
        var bonus = 3;
        var fallback = "(missing)";

        var nullableScalars = await Graph.Nodes<QueryExpressionCoverageNode>()
            .Where(node => node.Scope == scope && node.Category == "A")
            .OrderBy(node => node.Score)
            .Select(node => node.OptionalText)
            .ToListAsync(TestContext.Current.CancellationToken);

        var anonymousRows = await Graph.Nodes<QueryExpressionCoverageNode>()
            .Where(node => node.Scope == scope && node.Category == "A")
            .OrderBy(node => node.Score)
            .Select(node => new
            {
                node.Marker,
                AdjustedScore = node.Score + bonus,
                Display = node.OptionalText == null ? fallback : node.OptionalText.ToUpperInvariant(),
            })
            .ToListAsync(TestContext.Current.CancellationToken);

        var constructorRows = await Graph.Nodes<QueryExpressionCoverageNode>()
            .Where(node => node.Scope == scope && node.Category == "A")
            .OrderBy(node => node.Score)
            .Select(node => new QueryExpressionCoverageProjection(
                node.Name,
                node.OptionalText,
                node.Score + bonus,
                node.OptionalText == null ? fallback : node.OptionalText.ToUpperInvariant()))
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new string?[] { null, "note" }, nullableScalars);
        Assert.Collection(
            anonymousRows,
            row =>
            {
                Assert.Equal("alpha-null", row.Marker);
                Assert.Equal(13, row.AdjustedScore);
                Assert.Equal(fallback, row.Display);
            },
            row =>
            {
                Assert.Equal("amber-note", row.Marker);
                Assert.Equal(23, row.AdjustedScore);
                Assert.Equal("NOTE", row.Display);
            });
        Assert.Collection(
            constructorRows,
            row =>
            {
                Assert.Equal(" Alpha ", row.Name);
                Assert.Null(row.OptionalText);
                Assert.Equal(13, row.AdjustedScore);
                Assert.Equal(fallback, row.Display);
            },
            row =>
            {
                Assert.Equal("amber", row.Name);
                Assert.Equal("note", row.OptionalText);
                Assert.Equal(23, row.AdjustedScore);
                Assert.Equal("NOTE", row.Display);
            });
    }

    [Fact]
    public async Task ChainedFiltersOrderingProjectionAndPaging_PreserveOperatorOrder()
    {
        var scope = await SeedAsync();
        var minimum = 10;
        var bonus = 1;

        var adjustedScores = await Graph.Nodes<QueryExpressionCoverageNode>()
            .Where(node => node.Scope == scope)
            .Where(node => node.Score >= minimum)
            .OrderBy(node => node.Category)
            .ThenByDescending(node => node.Score)
            .Select(node => node.Score + bonus)
            .Skip(1)
            .Take(3)
            .ToListAsync(TestContext.Current.CancellationToken);

        var windowCount = await Graph.Nodes<QueryExpressionCoverageNode>()
            .Where(node => node.Scope == scope)
            .OrderBy(node => node.Score)
            .Skip(2)
            .Take(3)
            .CountAsync(TestContext.Current.CancellationToken);

        var enabledInFirstFour = await Graph.Nodes<QueryExpressionCoverageNode>()
            .Where(node => node.Scope == scope)
            .OrderBy(node => node.Score)
            .ThenBy(node => node.Marker)
            .Take(4)
            .CountAsync(node => node.Enabled, TestContext.Current.CancellationToken);

        Assert.Equal([11, 41, 31], adjustedScores);
        Assert.Equal(3, windowCount);
        Assert.Equal(2, enabledInFirstFour);
    }

    [Fact]
    public async Task DuplicateScalarValues_DistinctPageAndAggregateCorrectly()
    {
        var scope = await SeedAsync();

        var distinctScorePage = await Graph.Nodes<QueryExpressionCoverageNode>()
            .Where(node => node.Scope == scope)
            .Select(node => node.Score)
            .Distinct()
            .OrderBy(score => score)
            .Skip(1)
            .Take(3)
            .ToListAsync(TestContext.Current.CancellationToken);

        var aggregateSource = Graph.Nodes<QueryExpressionCoverageNode>()
            .Where(node => node.Scope == scope && node.Category != "D");
        var total = await aggregateSource.SumAsync(
            node => node.Amount,
            TestContext.Current.CancellationToken);
        var average = await aggregateSource.AverageAsync(
            node => node.Amount,
            TestContext.Current.CancellationToken);

        Assert.Equal([20, 30, 40], distinctScorePage);
        Assert.Equal(26.5m, total);
        Assert.Equal(5.3m, average);
    }

    [Fact]
    public async Task EmptyAllMatchNoMatchAndMixedSets_DriveTerminalSemantics()
    {
        var scope = await SeedAsync();
        var empty = Graph.Nodes<QueryExpressionCoverageNode>()
            .Where(node => node.Scope == scope && node.Category == "missing");
        var scoped = Graph.Nodes<QueryExpressionCoverageNode>()
            .Where(node => node.Scope == scope);

        var emptyRows = await empty.ToListAsync(TestContext.Current.CancellationToken);
        var emptyAny = await empty.AnyAsync(TestContext.Current.CancellationToken);
        var emptyCount = await empty.CountAsync(TestContext.Current.CancellationToken);
        var emptyFirst = await empty.FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        var allScoresPositive = await scoped.AllAsync(
            node => node.Score >= 10,
            TestContext.Current.CancellationToken);
        var allScoresBelowForty = await scoped.AllAsync(
            node => node.Score < 40,
            TestContext.Current.CancellationToken);
        var anyNullHighScore = await scoped.AnyAsync(
            node => node.OptionalText == null && node.Score >= 30,
            TestContext.Current.CancellationToken);
        var enabledAtLeastTwenty = await scoped.CountAsync(
            node => node.Enabled && node.Score >= 20,
            TestContext.Current.CancellationToken);
        var single = await scoped.SingleAsync(
            node => node.Marker == "beta-present",
            TestContext.Current.CancellationToken);

        Assert.Empty(emptyRows);
        Assert.False(emptyAny);
        Assert.Equal(0, emptyCount);
        Assert.Null(emptyFirst);
        Assert.True(allScoresPositive);
        Assert.False(allScoresBelowForty);
        Assert.True(anyNullHighScore);
        Assert.Equal(2, enabledAtLeastTwenty);
        Assert.Equal("beta-present", single.Marker);
    }

    [Fact]
    public async Task UnsupportedIndexedPredicateAndProjection_FailBeforeProviderExecution()
    {
        var indexedPredicate = await Assert.ThrowsAsync<GraphQueryTranslationException>(async () =>
            await Graph.Nodes<QueryExpressionCoverageNode>()
                .AsQueryable()
                .Where((node, index) => node.Score > index)
                .ToListAsync(TestContext.Current.CancellationToken));

        var indexedProjection = await Assert.ThrowsAsync<GraphQueryTranslationException>(async () =>
            await Graph.Nodes<QueryExpressionCoverageNode>()
                .AsQueryable()
                .Select((node, index) => node.Score + index)
                .ToListAsync(TestContext.Current.CancellationToken));

        Assert.Contains("indexed Where overload is not supported", indexedPredicate.Message, StringComparison.Ordinal);
        Assert.Contains("no positional element index", indexedPredicate.Message, StringComparison.Ordinal);
        Assert.Contains("indexed Select overload is not supported", indexedProjection.Message, StringComparison.Ordinal);
        Assert.Contains("no positional element index", indexedProjection.Message, StringComparison.Ordinal);
    }

    private async Task<string> SeedAsync()
    {
        var scope = $"query-expression-{Guid.NewGuid():N}";
        QueryExpressionCoverageNode[] nodes =
        [
            new()
            {
                Scope = scope,
                Marker = "alpha-null",
                Category = "A",
                Name = " Alpha ",
                OptionalText = null,
                Score = 10,
                Amount = 2.5m,
                OccurredAt = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                Enabled = true,
                Tags = ["blue", null, "blue"],
            },
            new()
            {
                Scope = scope,
                Marker = "amber-note",
                Category = "A",
                Name = "amber",
                OptionalText = "note",
                Score = 20,
                Amount = 4m,
                OccurredAt = new DateTime(2024, 6, 15, 0, 0, 0, DateTimeKind.Utc),
                Enabled = true,
                Tags = ["green", "blue"],
            },
            new()
            {
                Scope = scope,
                Marker = "beta-null",
                Category = "B",
                Name = " Beta",
                OptionalText = null,
                Score = 30,
                Amount = 3m,
                OccurredAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                Enabled = false,
                Tags = [null],
            },
            new()
            {
                Scope = scope,
                Marker = "beta-present",
                Category = "B",
                Name = "bravo",
                OptionalText = "memo",
                Score = 40,
                Amount = 8m,
                OccurredAt = new DateTime(2024, 12, 31, 0, 0, 0, DateTimeKind.Utc),
                Enabled = true,
                Tags = [],
            },
            new()
            {
                Scope = scope,
                Marker = "gamma-present",
                Category = "C",
                Name = "Gamma",
                OptionalText = "other",
                Score = 20,
                Amount = 9m,
                OccurredAt = new DateTime(2023, 3, 10, 0, 0, 0, DateTimeKind.Utc),
                Enabled = false,
                Tags = ["green"],
            },
            new()
            {
                Scope = scope,
                Marker = "delta-empty",
                Category = "D",
                Name = "delta",
                OptionalText = string.Empty,
                Score = 50,
                Amount = 0m,
                OccurredAt = new DateTime(2024, 2, 29, 0, 0, 0, DateTimeKind.Utc),
                Enabled = false,
                Tags = [],
            },
        ];

        foreach (var node in nodes)
        {
            await Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken);
        }

        return scope;
    }
}
