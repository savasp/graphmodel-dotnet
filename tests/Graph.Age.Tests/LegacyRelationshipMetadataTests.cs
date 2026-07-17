// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Tests;

using Cvoya.Graph.Age;
using Cvoya.Graph.Age.Core;
using Cvoya.Graph.Age.Querying.Cypher.Execution;
using Cvoya.Graph.CompatibilityTests;

public sealed class LegacyRelationshipMetadataTests(AgeHarness harness) : AgeTest(harness)
{
    [Fact]
    public async Task MetadataFreeLegacyRelationships_SameIdentityUpdateBackfillsClrMetadata()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var source = new Person { FirstName = "Legacy source" };
        var target = new Person { FirstName = "Legacy target" };
        await Graph.CreateNodeAsync(source, null, cancellationToken);
        await Graph.CreateNodeAsync(target, null, cancellationToken);

        var typed = new Knows(source, target) { Since = DateTime.UtcNow.AddDays(-3) };
        var dynamic = new DynamicRelationship(
            source.Id,
            target.Id,
            "LEGACY_DYNAMIC_RELATIONSHIP",
            new Dictionary<string, object?> { ["status"] = "before" });
        var customType = new Knows(source, target)
        {
            Type = "LEGACY_CUSTOM_KNOWS",
            Since = DateTime.UtcNow.AddDays(-2)
        };

        await Graph.CreateRelationshipAsync(typed, null, cancellationToken);
        await Graph.CreateRelationshipAsync(dynamic, null, cancellationToken);
        await Graph.CreateRelationshipAsync(customType, null, cancellationToken);

        var expected = new[]
        {
            new ExpectedIdentity(typed.Id, "KNOWS", "KNOWS", typeof(Knows)),
            new ExpectedIdentity(dynamic.Id, dynamic.Type, nameof(DynamicRelationship), typeof(DynamicRelationship)),
            new ExpectedIdentity(customType.Id, customType.Type, "KNOWS", typeof(Knows))
        };
        await RemoveMetadataAndAssertLegacyShapeAsync(expected, cancellationToken);

        typed.Since = DateTime.UtcNow.AddDays(-1);
        await Graph.UpdateRelationshipAsync(typed, null, cancellationToken);

        var dynamicUpdate = new DynamicRelationship(
            source.Id,
            target.Id,
            dynamic.Type,
            new Dictionary<string, object?> { ["status"] = "after" })
        {
            Id = dynamic.Id
        };
        await Graph.UpdateRelationshipAsync(dynamicUpdate, null, cancellationToken);

        customType.Since = DateTime.UtcNow;
        await Graph.UpdateRelationshipAsync(customType, null, cancellationToken);

        var fetchedTyped = await Graph.GetRelationshipAsync<Knows>(typed.Id, null, cancellationToken);
        Assert.Equal(typed.Since, fetchedTyped.Since);
        var fetchedDynamic = await Graph.GetDynamicRelationshipAsync(dynamic.Id, null, cancellationToken);
        Assert.Equal("after", fetchedDynamic.GetProperty<string>("status"));

        await AssertMetadataBackfilledAsync(expected, cancellationToken);
    }

    [Fact]
    public async Task MetadataFreeLegacyRelationship_MismatchedCanonicalLabelReportsStoredLabel()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var source = new Person { FirstName = "Legacy source" };
        var target = new Person { FirstName = "Legacy target" };
        await Graph.CreateNodeAsync(source, null, cancellationToken);
        await Graph.CreateNodeAsync(target, null, cancellationToken);

        var relationship = new Knows(source, target) { Since = DateTime.UtcNow.AddDays(-1) };
        await Graph.CreateRelationshipAsync(relationship, null, cancellationToken);

        await using (var transaction = await Graph.GetTransactionAsync(cancellationToken))
        {
            var runner = ((AgeGraphTransaction)transaction).Runner;
            await using var result = await runner.RunAsync(
                """
                MATCH ()-[r:CvoyaRelationship]->()
                WHERE r.Id = $id
                REMOVE r.__metadata__
                SET r.inheritance_labels = ['WRONG_LEGACY_LABEL']
                RETURN true AS updated
                """,
                new { id = relationship.Id },
                cancellationToken);
            _ = await result.SingleAsync(cancellationToken);
            await transaction.CommitAsync();
        }

        relationship.Since = DateTime.UtcNow;
        var exception = await Assert.ThrowsAsync<GraphException>(() =>
            Graph.UpdateRelationshipAsync(relationship, null, cancellationToken));

        Assert.Contains(
            "legacy CLR label is 'WRONG_LEGACY_LABEL'",
            exception.Message,
            StringComparison.Ordinal);
    }

    private async Task RemoveMetadataAndAssertLegacyShapeAsync(
        IReadOnlyCollection<ExpectedIdentity> expected,
        CancellationToken cancellationToken)
    {
        await using var transaction = await Graph.GetTransactionAsync(cancellationToken);
        var runner = ((AgeGraphTransaction)transaction).Runner;
        await using var result = await runner.RunAsync(
            """
            MATCH ()-[r:CvoyaRelationship]->()
            WHERE r.Id IN $ids
            WITH r, r.__metadata__ AS originalMetadata
            REMOVE r.__metadata__
            RETURN r.Id AS id,
                   originalMetadata AS originalMetadata,
                   head(r.inheritance_labels) AS canonicalLabel,
                   r.Type AS logicalType,
                   r.__metadata__ IS NULL AS metadataRemoved
            """,
            new { ids = expected.Select(item => item.Id).ToArray() },
            cancellationToken);
        var records = await result.ToListAsync(cancellationToken);
        await transaction.CommitAsync();

        Assert.Equal(expected.Count, records.Count);
        var recordsById = records.ToDictionary(record => record["id"].As<string>());
        foreach (var identity in expected)
        {
            var record = recordsById[identity.Id];
            Assert.Equal(identity.ClrType, Type.GetType(record["originalMetadata"].As<string>()));
            Assert.Equal(identity.CanonicalLabel, record["canonicalLabel"].As<string>());
            Assert.Equal(identity.StorageType, record["logicalType"].As<string>());
            Assert.True(record["metadataRemoved"].As<bool>());
        }
    }

    private async Task AssertMetadataBackfilledAsync(
        IReadOnlyCollection<ExpectedIdentity> expected,
        CancellationToken cancellationToken)
    {
        await using var transaction = await Graph.GetTransactionAsync(cancellationToken);
        var runner = ((AgeGraphTransaction)transaction).Runner;
        await using var result = await runner.RunAsync(
            """
            MATCH ()-[r:CvoyaRelationship]->()
            WHERE r.Id IN $ids
            RETURN r.Id AS id, r.__metadata__ AS metadata
            """,
            new { ids = expected.Select(item => item.Id).ToArray() },
            cancellationToken);
        var records = await result.ToListAsync(cancellationToken);
        await transaction.CommitAsync();

        Assert.Equal(expected.Count, records.Count);
        var recordsById = records.ToDictionary(record => record["id"].As<string>());
        foreach (var identity in expected)
        {
            var metadata = recordsById[identity.Id]["metadata"].As<string>();
            Assert.DoesNotContain(", Version=", metadata, StringComparison.Ordinal);
            Assert.Equal(identity.ClrType, Type.GetType(metadata));
        }
    }

    private sealed record ExpectedIdentity(string Id, string StorageType, string CanonicalLabel, Type ClrType);
}
