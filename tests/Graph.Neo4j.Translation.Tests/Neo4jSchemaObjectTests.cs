// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Translation.Tests;

using Cvoya.Graph.Neo4j.Schema;
using global::Neo4j.Driver;

public sealed class Neo4jSchemaObjectTests
{
    [Fact]
    public void ErrorClassification_UsesNeo4jCodeInsteadOfMessageText()
    {
        var deadlock = new Neo4jException(
            "Neo.TransientError.Transaction.DeadlockDetected",
            "Message deliberately contains no retry hint.");
        var misleadingClientError = new Neo4jException(
            "Neo.ClientError.Schema.IndexWithNameAlreadyExists",
            "deadlock transient retry");

        Assert.True(Neo4jSchemaErrorClassifier.IsRetryable(deadlock));
        Assert.False(Neo4jSchemaErrorClassifier.IsRetryable(misleadingClientError));
    }

    [Theory]
    [InlineData("Neo.ClientError.Schema.ConstraintAlreadyExists")]
    [InlineData("Neo.ClientError.Schema.ConstraintWithNameAlreadyExists")]
    [InlineData("Neo.ClientError.Schema.EquivalentSchemaRuleAlreadyExists")]
    [InlineData("Neo.ClientError.Schema.IndexAlreadyExists")]
    [InlineData("Neo.ClientError.Schema.IndexWithNameAlreadyExists")]
    public void PotentialEquivalentConflict_RecognizesOnlySchemaConflictCodes(string code)
    {
        var exception = new Neo4jException(code, "unrelated message");

        Assert.True(Neo4jSchemaErrorClassifier.IsPotentialEquivalentConflict(exception));
    }

    [Fact]
    public void PotentialEquivalentConflict_DoesNotTreatTransientFailureAsEquivalent()
    {
        var exception = new Neo4jException(
            "Neo.TransientError.Transaction.DeadlockDetected",
            "EquivalentSchemaRuleAlreadyExists");

        Assert.False(Neo4jSchemaErrorClassifier.IsPotentialEquivalentConflict(exception));
    }

    [Fact]
    public void DescriptorEquivalence_IgnoresMetadataListOrdering()
    {
        var requested = Descriptor(
            "node_fulltext_index",
            Neo4jSchemaObjectKind.FullTextIndex,
            Neo4jSchemaEntityType.Node,
            ["Person", "Project"],
            ["Name", "Summary"]);
        var installed = Descriptor(
            "node_fulltext_index",
            Neo4jSchemaObjectKind.FullTextIndex,
            Neo4jSchemaEntityType.Node,
            ["Project", "Person"],
            ["Summary", "Name"]);

        Assert.True(requested.IsEquivalentTo(installed));
    }

    [Fact]
    public void DescriptorEquivalence_RejectsEveryIncompatibleDefinitionPart()
    {
        var requested = Descriptor(
            "idx_person_name",
            Neo4jSchemaObjectKind.RangeIndex,
            Neo4jSchemaEntityType.Node,
            ["Person"],
            ["Name"]);
        Neo4jSchemaObjectDescriptor[] incompatible =
        [
            requested with { Name = "idx_person_other" },
            requested with { Kind = Neo4jSchemaObjectKind.FullTextIndex },
            requested with { EntityType = Neo4jSchemaEntityType.Relationship },
            requested with { LabelsOrTypes = ["Project"] },
            requested with { Properties = ["Summary"] }
        ];

        Assert.All(incompatible, installed => Assert.False(requested.IsEquivalentTo(installed)));
    }

    [Theory]
    [InlineData("UNIQUENESS", nameof(Neo4jSchemaObjectKind.NodeUniquenessConstraint))]
    [InlineData("NODE_PROPERTY_UNIQUENESS", nameof(Neo4jSchemaObjectKind.NodeUniquenessConstraint))]
    [InlineData("RELATIONSHIP_UNIQUENESS", nameof(Neo4jSchemaObjectKind.RelationshipUniquenessConstraint))]
    [InlineData("RELATIONSHIP_PROPERTY_UNIQUENESS", nameof(Neo4jSchemaObjectKind.RelationshipUniquenessConstraint))]
    public void ConstraintKind_NormalizesCypher5AndCypher25Names(
        string metadataType,
        string expectedName)
    {
        Assert.Equal(expectedName, Neo4jSchemaMetadata.GetConstraintKind(metadataType).ToString());
    }

    [Theory]
    [InlineData("NODE", nameof(Neo4jSchemaEntityType.Node))]
    [InlineData("RELATIONSHIP", nameof(Neo4jSchemaEntityType.Relationship))]
    [InlineData("SOMETHING_ELSE", null)]
    [InlineData(null, null)]
    public void EntityType_MapsKnownValuesAndRejectsUnknownOnes(string? metadataEntityType, string? expectedName)
    {
        Assert.Equal(expectedName, Neo4jSchemaMetadata.GetEntityType(metadataEntityType)?.ToString());
    }

    private static Neo4jSchemaObjectDescriptor Descriptor(
        string name,
        Neo4jSchemaObjectKind kind,
        Neo4jSchemaEntityType entityType,
        IReadOnlyList<string> labelsOrTypes,
        IReadOnlyList<string> properties)
    {
        return new Neo4jSchemaObjectDescriptor(name, kind, entityType, labelsOrTypes, properties);
    }
}
