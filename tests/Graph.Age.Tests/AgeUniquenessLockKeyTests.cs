// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Tests;

using System.Globalization;
using Cvoya.Graph.Age.Entities;

/// <summary>
/// Derivation properties of the uniqueness advisory-lock keys (#365). These need no database: the
/// point is that two peers enforcing the same constraint compute the same number, and that peers
/// enforcing different constraints do not serialize against each other.
/// </summary>
public sealed class AgeUniquenessLockKeyTests
{
    private const string Graph = "cvoya_graph";
    private const string Node = AgeUniquenessLockKey.NodeEntityKind;
    private const string Relationship = AgeUniquenessLockKey.RelationshipEntityKind;

    [Fact]
    public void Compute_IsStableAcrossProcessesAndArchitectures()
    {
        // Pin the complete derivation, including byte order. Comparing two calls in one process would
        // also pass for string.GetHashCode(), despite that method being randomized between processes.
        Assert.Equal(
            3247420127699772120L,
            AgeUniquenessLockKey.Compute(Graph, Node, "Person", "unique property 'Email'", ["a@b.c"]));
    }

    [Fact]
    public void Compute_IsCultureIndependent()
    {
        // A culture that renders numbers with a different decimal separator must not change the key,
        // or two peers with different locales would fail to serialize against each other.
        var invariant = ComputeUnder(CultureInfo.InvariantCulture, 1234.5m);
        var german = ComputeUnder(CultureInfo.GetCultureInfo("de-DE"), 1234.5m);

        Assert.Equal(invariant, german);

        static long ComputeUnder(CultureInfo culture, decimal value)
        {
            var previous = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = culture;
            try
            {
                return AgeUniquenessLockKey.Compute(Graph, Node, "Order", "composite key", [value]);
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }
    }

    [Fact]
    public void Compute_ScopesKeysByGraph()
    {
        // Advisory locks are database-wide, so two AGE graphs in one database would otherwise block
        // each other on identical values.
        Assert.NotEqual(
            AgeUniquenessLockKey.Compute("graph_a", Node, "Person", "composite key", ["x"]),
            AgeUniquenessLockKey.Compute("graph_b", Node, "Person", "composite key", ["x"]));
    }

    [Fact]
    public void Compute_ScopesKeysByEntityKind()
    {
        // A node label and a relationship type may share a name and are separate constraints.
        Assert.NotEqual(
            AgeUniquenessLockKey.Compute(Graph, Node, "OWNS", "composite key", ["x"]),
            AgeUniquenessLockKey.Compute(Graph, Relationship, "OWNS", "composite key", ["x"]));
    }

    [Fact]
    public void Compute_SeparatesLabelsConstraintsAndValues()
    {
        var baseline = AgeUniquenessLockKey.Compute(Graph, Node, "Person", "composite key", ["x"]);

        Assert.NotEqual(baseline, AgeUniquenessLockKey.Compute(Graph, Node, "Company", "composite key", ["x"]));
        Assert.NotEqual(baseline, AgeUniquenessLockKey.Compute(Graph, Node, "Person", "unique property 'Name'", ["x"]));
        Assert.NotEqual(baseline, AgeUniquenessLockKey.Compute(Graph, Node, "Person", "composite key", ["y"]));
    }

    [Fact]
    public void Compute_DistinguishesValuesThatShareATextRendering()
    {
        // ("a", "b") and ("ab") must not collide, nor 1 with "1", nor null with the empty
        // string: each is a different constraint instance that must be free to proceed in parallel.
        var pair = AgeUniquenessLockKey.Compute(Graph, Node, "Person", "composite key", ["a", "b"]);
        var joined = AgeUniquenessLockKey.Compute(Graph, Node, "Person", "composite key", ["ab"]);
        Assert.NotEqual(pair, joined);

        Assert.NotEqual(
            AgeUniquenessLockKey.Compute(Graph, Node, "Person", "composite key", [1]),
            AgeUniquenessLockKey.Compute(Graph, Node, "Person", "composite key", ["1"]));

        Assert.NotEqual(
            AgeUniquenessLockKey.Compute(Graph, Node, "Person", "composite key", [null]),
            AgeUniquenessLockKey.Compute(Graph, Node, "Person", "composite key", [string.Empty]));
    }

    [Fact]
    public void Compute_CanonicalizesValuesThatAgeComparesAsTheSameNumber()
    {
        var integer = AgeUniquenessLockKey.Compute(Graph, Node, "Person", "composite key", [1]);

        Assert.Equal(integer, AgeUniquenessLockKey.Compute(Graph, Node, "Person", "composite key", [1L]));
        Assert.Equal(integer, AgeUniquenessLockKey.Compute(Graph, Node, "Person", "composite key", [1.0f]));
        Assert.Equal(integer, AgeUniquenessLockKey.Compute(Graph, Node, "Person", "composite key", [1.0d]));
        Assert.Equal(integer, AgeUniquenessLockKey.Compute(Graph, Node, "Person", "composite key", [1.00m]));
    }

    [Fact]
    public void Compute_CanonicalizesNestedMapOrderAndNumericTypes()
    {
        var first = new Dictionary<string, object?>
        {
            ["name"] = "Ada",
            ["score"] = 1,
        };
        var second = new Dictionary<string, object?>
        {
            ["score"] = 1L,
            ["name"] = "Ada",
        };

        Assert.Equal(
            AgeUniquenessLockKey.Compute(Graph, Node, "Person", "composite key", [first]),
            AgeUniquenessLockKey.Compute(Graph, Node, "Person", "composite key", [second]));
    }

    [Fact]
    public void Compute_LengthFramesEmbeddedSeparatorCharacters()
    {
        Assert.NotEqual(
            AgeUniquenessLockKey.Compute(
                Graph,
                Node,
                "Person",
                "composite key",
                ["a\u001fstring", "b"]),
            AgeUniquenessLockKey.Compute(
                Graph,
                Node,
                "Person",
                "composite key",
                ["a", "string\u001fb"]));
    }

    [Fact]
    public void Compute_IsOrderSensitiveAcrossCompositeKeyValues()
    {
        // Composite key (Tenant, Account) = ("acme", "1") is a different row from ("1", "acme").
        Assert.NotEqual(
            AgeUniquenessLockKey.Compute(Graph, Node, "Account", "composite key", ["acme", "1"]),
            AgeUniquenessLockKey.Compute(Graph, Node, "Account", "composite key", ["1", "acme"]));
    }
}
