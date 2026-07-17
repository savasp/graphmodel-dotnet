// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Tests;

using Cvoya.Graph.CompatibilityTests;

/// <summary>
/// Neo4j schema-DDL coverage for #367: a typed property may carry an arbitrary storage name through
/// <c>[Property(Label = ...)]</c>, and <c>Neo4jSchemaManager</c> interpolates that name into
/// CREATE CONSTRAINT / CREATE INDEX statements. A label containing a space, punctuation, or an
/// embedded backtick must be rendered as one escaped identifier in every schema-object kind
/// (uniqueness, existence/required, range index, composite key), so schema initialization parses
/// and installs the objects, stays idempotent across re-discovery, and actually enforces them.
/// </summary>
[Node("HostileSchemaLabelNode")]
public record HostileSchemaLabelNode : Node
{
    // Composite key over two hostile labels (one containing the escape character itself).
    [Property(Label = "region key", IsKey = true)]
    public string Region { get; set; } = string.Empty;

    [Property(Label = "dept`key", IsKey = true)]
    public string Department { get; set; } = string.Empty;

    // Unique + required + indexed, all rendered off the same escaped label.
    [Property(Label = "e-mail (primary)", IsUnique = true, IsRequired = true, IsIndexed = true)]
    public string Email { get; set; } = string.Empty;

    // Plain range index over a spaced label.
    [Property(Label = "display name", IsIndexed = true)]
    public string DisplayName { get; set; } = string.Empty;
}

public sealed class TypedPropertyLabelSchemaEscapingTests(Neo4jHarness harness)
    : Neo4jTest(harness, StoreIsolation.FreshStore)
{
    [Fact]
    public async Task HostileSchemaPropertyLabels_InitializeEnforceAndRediscoverIdempotently()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        // The first create triggers schema initialization, which renders CREATE CONSTRAINT/INDEX
        // DDL for the composite-key, unique, required, and indexed properties whose storage labels
        // contain a space, punctuation, and a backtick. Unescaped, that DDL fails to parse and this
        // create throws.
        var first = new HostileSchemaLabelNode
        {
            Region = "NA",
            Department = "Eng",
            Email = "first@example.com",
            DisplayName = "Alice",
        };
        await Graph.CreateNodeAsync(first, null, cancellationToken);

        var roundTripped = await Graph.GetNodeAsync<HostileSchemaLabelNode>(first.Id, null, cancellationToken);
        Assert.Equal("first@example.com", roundTripped.Email);
        Assert.Equal("Eng", roundTripped.Department);

        // A second create re-runs schema discovery; the escaped constraint/index names must compare
        // equal to what is already installed, so discovery stays idempotent (no duplicate-object
        // error) and this distinct node is created normally.
        var second = new HostileSchemaLabelNode
        {
            Region = "EU",
            Department = "Sales",
            Email = "second@example.com",
            DisplayName = "Bob",
        };
        await Graph.CreateNodeAsync(second, null, cancellationToken);

        // The unique constraint on the escaped e-mail label is enforced, proving the DDL parsed and
        // the object was actually installed rather than silently skipped.
        var duplicate = new HostileSchemaLabelNode
        {
            Region = "APAC",
            Department = "Support",
            Email = "first@example.com",
            DisplayName = "Clone",
        };
        await Assert.ThrowsAnyAsync<Exception>(
            () => Graph.CreateNodeAsync(duplicate, null, cancellationToken));
    }
}
