// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Translation.Tests;

using Cvoya.Graph.Neo4j.Entities;
using Cvoya.Graph.Serialization;

[Trait("Area", "GraphCommands")]
public sealed class ComplexPropertyMutationFragmentTests
{
    [Fact]
    public void BuildElementBoundMutationFragment_TargetsOnlyMarkerOwnedSelectedProperties()
    {
        var addressProperty = typeof(ComplexMutationOwner).GetProperty(nameof(ComplexMutationOwner.Address))!;
        var address = new EntityInfo(
            typeof(ComplexMutationAddressValue),
            nameof(ComplexMutationAddressValue),
            [],
            new Dictionary<string, Property>
            {
                [nameof(ComplexMutationAddressValue.City)] = new(
                    typeof(ComplexMutationAddressValue).GetProperty(nameof(ComplexMutationAddressValue.City))!,
                    nameof(ComplexMutationAddressValue.City),
                    false,
                    new SimpleValue("Seattle", typeof(string))),
            },
            new Dictionary<string, Property>());
        var replacement = new EntityInfo(
            typeof(ComplexMutationOwner),
            nameof(ComplexMutationOwner),
            [],
            new Dictionary<string, Property>(),
            new Dictionary<string, Property>
            {
                [nameof(ComplexMutationOwner.Address)] = new(
                    addressProperty,
                    nameof(ComplexMutationOwner.Address),
                    false,
                    address,
                    "HOME ADDRESS"),
            });

        var fragment = ComplexPropertyManager.BuildElementBoundMutationFragment(
            "target",
            replacement,
            ["HOME ADDRESS"],
            ["old scalar"]);

        Assert.Contains(
            $"__complexOwnerRelationship.{ComplexPropertyStorage.RelationshipMarkerProperty} = true",
            fragment.Cypher,
            StringComparison.Ordinal);
        Assert.Contains(
            "type(__complexOwnerRelationship) IN $__complexRelationshipTypes",
            fragment.Cypher,
            StringComparison.Ordinal);
        Assert.Contains("DETACH DELETE __complexPropertyNode", fragment.Cypher, StringComparison.Ordinal);
        Assert.Contains("REMOVE target.`old scalar`", fragment.Cypher, StringComparison.Ordinal);
        Assert.Contains("CREATE (target)-[__complexRelationship0:`HOME ADDRESS`]", fragment.Cypher, StringComparison.Ordinal);
        Assert.Equal(
            ["HOME ADDRESS"],
            Assert.IsAssignableFrom<IReadOnlyList<string>>(fragment.Parameters["__complexRelationshipTypes"]));

        var nodeProperties = Assert.IsType<Dictionary<string, object?>>(fragment.Parameters["__complexNodeProperties0"]);
        var relationshipProperties = Assert.IsType<Dictionary<string, object>>(fragment.Parameters["__complexRelationshipProperties0"]);
        Assert.DoesNotContain(nameof(IEntity.Id), nodeProperties.Keys);
        Assert.DoesNotContain(nameof(IEntity.Id), relationshipProperties.Keys);
        Assert.True((bool)relationshipProperties[ComplexPropertyStorage.RelationshipMarkerProperty]);
        Assert.Equal(0, relationshipProperties["SequenceNumber"]);
    }
}

internal sealed record ComplexMutationOwner : Node
{
    public ComplexMutationAddressValue? Address { get; init; }
}

internal sealed record ComplexMutationAddressValue
{
    public string City { get; init; } = string.Empty;
}
