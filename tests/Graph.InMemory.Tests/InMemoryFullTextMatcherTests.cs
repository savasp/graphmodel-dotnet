// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.InMemory.Tests;

using Cvoya.Graph.CompatibilityTests;
using Cvoya.Graph.InMemory.Querying;

/// <summary>
/// Focused unit tests for the in-memory full-text matcher — the executable spec of the #288
/// contract floor. <see cref="InMemoryFullTextMatcher.Matches"/> takes already-tokenized query
/// terms (lowercased, whole-word), so these tests drive the matching logic directly and cover the
/// value-side tokenization, AND-of-terms, case-insensitivity, whole-token, searchable-property, and
/// dynamic-entity rules independently of the executor and the shared tokenizer.
/// </summary>
public sealed class InMemoryFullTextMatcherTests : IAsyncLifetime
{
    private InMemoryGraphStore _store = null!;
    private InMemoryFullTextMatcher _matcher = null!;

    public async ValueTask InitializeAsync()
    {
        _store = new InMemoryGraphStore();

        // Persisting one node initializes the schema registry; the matcher then resolves any
        // graph entity type's schema on demand via the registry.
        await _store.Graph.CreateNodeAsync(
            new Person { FirstName = "seed" }, cancellationToken: TestContext.Current.CancellationToken);

        _matcher = new InMemoryFullTextMatcher(_store.Graph.SchemaRegistry);
    }

    public async ValueTask DisposeAsync() => await _store.DisposeAsync();

    [Fact]
    public void EmptyQuery_MatchesNothing()
    {
        var person = new Person { FirstName = "Alice", Bio = "cloud computing" };
        Assert.False(_matcher.Matches(person, []));
    }

    [Fact]
    public void SingleTerm_MatchesWholeTokenCaseInsensitively()
    {
        var person = new Person { FirstName = "CaseSensitive", Bio = "planning a long vacation" };

        Assert.True(_matcher.Matches(person, ["casesensitive"]));
        Assert.True(_matcher.Matches(person, ["vacation"]));
    }

    [Fact]
    public void SubToken_DoesNotMatch()
    {
        var person = new Person { FirstName = "Holiday", Bio = "planning a long vacation" };

        // "vaca" is a sub-token of "vacation": whole-word matching must reject it.
        Assert.False(_matcher.Matches(person, ["vaca"]));
    }

    [Fact]
    public void MultipleTerms_RequireAllToMatch()
    {
        var both = new Person { FirstName = "AndBoth", Bio = "cloud computing platforms" };
        var cloudOnly = new Person { FirstName = "AndCloud", Bio = "expertise in cloud systems" };

        // Terms may appear in any order and at any distance, but all must be present.
        Assert.True(_matcher.Matches(both, ["cloud", "computing"]));
        Assert.True(_matcher.Matches(both, ["computing", "cloud"]));
        Assert.False(_matcher.Matches(cloudOnly, ["cloud", "computing"]));
    }

    [Fact]
    public void NonSearchableStringProperty_IsExcluded()
    {
        var doc = new SearchDoc { Title = "visible heading", Secret = "hidden passphrase" };

        Assert.True(_matcher.Matches(doc, ["visible"]));
        // Secret is a string property flagged [Property(IncludeInFullTextSearch = false)].
        Assert.False(_matcher.Matches(doc, ["passphrase"]));
    }

    [Fact]
    public void NonStringProperty_IsNotTokenized()
    {
        var person = new Person { FirstName = "Bob", Age = 42 };

        // Age is an int; only string properties contribute searchable text.
        Assert.False(_matcher.Matches(person, ["42"]));
    }

    [Fact]
    public void DynamicNode_MatchesAnyStringPropertyValue()
    {
        var node = new DynamicNode(
            ["Person"],
            new Dictionary<string, object?> { ["FirstName"] = "Wonder", ["Age"] = 30 });

        Assert.True(_matcher.Matches(node, ["wonder"]));
        // The integer value is not a string, so it contributes no searchable tokens.
        Assert.False(_matcher.Matches(node, ["30"]));
    }
}

/// <summary>Fixture node with a searchable and a non-searchable string property.</summary>
public record SearchDoc : Node
{
    public string Title { get; set; } = string.Empty;

    [Property(IncludeInFullTextSearch = false)]
    public string Secret { get; set; } = string.Empty;
}
