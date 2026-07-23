// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

using System.Reflection;

/// <summary>
/// Certifies provider boundaries that span the public entity model, native external data, and
/// backend provisioning behavior.
/// </summary>
public interface IProviderContractTests : IGraphTest
{
    /// <summary>Proves graph entities carry no universal identity or relationship endpoint state.</summary>
    [Fact]
    public void EntityInterfaces_ExposeOnlyDeclaredRuntimeShape()
    {
        Assert.Empty(typeof(IEntity).GetProperties(BindingFlags.Public | BindingFlags.Instance));
        Assert.Equal(
            [nameof(INode.Labels)],
            typeof(INode).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => property.Name));
        Assert.Equal(
            [nameof(IRelationship.Type)],
            typeof(IRelationship).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => property.Name));
    }

    /// <summary>
    /// Proves rows seeded through the provider's native API are available through typed, dynamic,
    /// relationship-root, and oriented path-segment queries without exposing physical metadata.
    /// </summary>
    [Fact]
    public async Task ExternalNativeRows_AreTypedDynamicAndTraversableWithoutPhysicalMetadata()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var marker = $"external-{Guid.NewGuid():N}";
        await Harness.SeedExternalGraphAsync(Graph, marker, cancellationToken);

        var typedNodes = await Graph.Nodes<ContractExternalNode>()
            .Where(node => node.Marker == marker)
            .OrderBy(node => node.Role)
            .ToListAsync(cancellationToken);
        var dynamicNodes = await Graph.DynamicNodes()
            .OfLabel(Labels.GetLabelFromType(typeof(ContractExternalNode)))
            .ToListAsync(cancellationToken);
        var typedRelationship = await Graph.Relationships<ContractExternalRelationship>()
            .Where(relationship => relationship.Marker == marker)
            .SingleAsync(cancellationToken);
        var dynamicRelationship = await Graph.DynamicRelationships()
            .Where(relationship => relationship.Type == Labels.GetLabelFromType(typeof(ContractExternalRelationship)))
            .SingleAsync(cancellationToken);
        var segment = await Graph.Nodes<ContractExternalNode>()
            .Where(node => node.Marker == marker && node.Role == "source")
            .PathSegments<ContractExternalNode, ContractExternalRelationship, ContractExternalNode>()
            .SingleAsync(cancellationToken);

        Assert.Equal(["source", "target"], typedNodes.Select(node => node.Role));
        Assert.Equal(2, dynamicNodes.Count);
        Assert.All(
            dynamicNodes,
            node =>
            {
                Assert.Equal(["ContractExternalNode"], node.Labels);
                Assert.Equal(
                    [nameof(ContractExternalNode.Marker), nameof(ContractExternalNode.Role)],
                    node.Properties.Keys.Order(StringComparer.Ordinal));
                Assert.Equal(marker, node.Properties[nameof(ContractExternalNode.Marker)]);
            });
        Assert.Equal(marker, typedRelationship.Marker);
        Assert.Equal("CONTRACT_EXTERNAL_RELATIONSHIP", dynamicRelationship.Type);
        Assert.Equal(
            [nameof(ContractExternalRelationship.Marker)],
            dynamicRelationship.Properties.Keys);
        Assert.Equal(marker, dynamicRelationship.Properties[nameof(ContractExternalRelationship.Marker)]);
        Assert.Equal("source", segment.StartNode.Role);
        Assert.Equal("target", segment.EndNode.Role);
        Assert.Equal(marker, segment.Relationship.Marker);
        Assert.Equal(RelationshipDirection.Outgoing, segment.Direction);
    }

    /// <summary>Proves an ordinary read does not create backend infrastructure.</summary>
    [Fact]
    public async Task ReadOnlyQuery_DoesNotCreateStoreArtifacts()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var before = (await Harness.GetStoreArtifactsAsync(Graph, cancellationToken))
            .Order(StringComparer.Ordinal)
            .ToArray();

        var results = await Graph.Nodes<ContractExternalNode>()
            .Where(node => node.Marker == "missing")
            .ToListAsync(cancellationToken);

        var after = (await Harness.GetStoreArtifactsAsync(Graph, cancellationToken))
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Empty(results);
        Assert.Equal(before, after);
    }
}
