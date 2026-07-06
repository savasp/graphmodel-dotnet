// Copyright 2025 Savas Parastatidis
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      https://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace Cvoya.Graph.Model.Core.Tests;


[Trait("Area", "DynamicEntities")]
public class DynamicEntityTests
{
    [Fact]
    public void DynamicNode_ConstructorCopiesLabelsAndProperties()
    {
        var labels = new List<string> { "Person", "Employee" };
        var properties = new Dictionary<string, object?> { ["name"] = "Ada", ["age"] = 37 };

        var node = new DynamicNode("node-1", labels, properties);
        labels.Add("Mutated");
        properties["name"] = "Changed";

        Assert.Equal("node-1", node.Id);
        Assert.Equal(new[] { "Person", "Employee" }, node.Labels);
        Assert.Equal("Ada", node.Properties["name"]);
        Assert.Equal(37, node.Properties["age"]);
    }

    [Fact]
    public void DynamicNode_ConstructorProducesReadOnlyLabelsAndProperties()
    {
        var node = new DynamicNode(["Person"], new Dictionary<string, object?> { ["name"] = "Ada" });

        var labels = Assert.IsAssignableFrom<IList<string>>(node.Labels);
        var properties = Assert.IsAssignableFrom<IDictionary<string, object?>>(node.Properties);

        Assert.Throws<NotSupportedException>(() => labels.Add("Other"));
        Assert.Throws<NotSupportedException>(() => properties.Add("age", 37));
    }

    [Fact]
    public void DynamicRelationship_ConstructorCopiesPropertiesAndPreservesRelationshipShape()
    {
        var properties = new Dictionary<string, object?> { ["since"] = 2024 };

        var relationship = new DynamicRelationship(
            "source",
            "target",
            "KNOWS",
            properties,
            RelationshipDirection.Incoming);

        properties["since"] = 2026;

        Assert.Equal("source", relationship.StartNodeId);
        Assert.Equal("target", relationship.EndNodeId);
        Assert.Equal("KNOWS", relationship.Type);
        Assert.Equal(RelationshipDirection.Incoming, relationship.Direction);
        Assert.Equal(2024, relationship.Properties["since"]);
    }

    [Fact]
    public void DynamicRelationship_ConstructorProducesReadOnlyProperties()
    {
        var relationship = new DynamicRelationship(
            "source",
            "target",
            "KNOWS",
            new Dictionary<string, object?> { ["since"] = 2024 });

        var properties = Assert.IsAssignableFrom<IDictionary<string, object?>>(relationship.Properties);

        Assert.Throws<NotSupportedException>(() => properties.Add("weight", 10));
    }

    [Fact]
    public void ToDynamicNode_PreservesLabelsAndCustomProperties()
    {
        var address = new DynamicAddress("1 Main", "London");
        var tags = new List<string> { "founder", "engineer" };
        var node = new StrongNode
        {
            Id = "node-1",
            Labels = ["Person", "Employee"],
            Name = "Ada",
            Address = address,
            Tags = tags,
        };

        var dynamic = node.ToDynamicNode();

        Assert.Equal("node-1", dynamic.Id);
        Assert.Equal(new[] { "Person", "Employee" }, dynamic.Labels);
        Assert.Equal("Ada", dynamic.Properties[nameof(StrongNode.Name)]);
        Assert.Same(address, dynamic.Properties[nameof(StrongNode.Address)]);
        Assert.Same(tags, dynamic.Properties[nameof(StrongNode.Tags)]);
        Assert.DoesNotContain(nameof(INode.Labels), dynamic.Properties.Keys);
        Assert.DoesNotContain(nameof(IEntity.Id), dynamic.Properties.Keys);
    }

    [Fact]
    public void ToDynamicRelationship_PreservesShapeAndCustomProperties()
    {
        var relationship = new StrongRelationship("source", "target")
        {
            Id = "rel-1",
            Type = "KNOWS",
            Description = "met at work",
            Since = new DateOnly(2024, 1, 1),
        };

        var dynamic = relationship.ToDynamicRelationship();

        Assert.Equal("rel-1", dynamic.Id);
        Assert.Equal("source", dynamic.StartNodeId);
        Assert.Equal("target", dynamic.EndNodeId);
        Assert.Equal("KNOWS", dynamic.Type);
        Assert.Equal("met at work", dynamic.Properties[nameof(StrongRelationship.Description)]);
        Assert.Equal(new DateOnly(2024, 1, 1), dynamic.Properties[nameof(StrongRelationship.Since)]);
        Assert.DoesNotContain(nameof(IRelationship.StartNodeId), dynamic.Properties.Keys);
        Assert.DoesNotContain(nameof(IRelationship.EndNodeId), dynamic.Properties.Keys);
    }

    [Fact]
    public void ToDynamic_InterfaceOverloadsDispatchToNodeOrRelationship()
    {
        INode node = new StrongNode { Name = "Ada" };
        IRelationship relationship = new StrongRelationship("source", "target") { Description = "knows" };

        Assert.IsType<DynamicNode>(node.ToDynamic());
        Assert.IsType<DynamicRelationship>(relationship.ToDynamic());
    }

    [Fact]
    public void ToDynamicNode_ThrowsForNullNode()
    {
        StrongNode? node = null;

        Assert.Throws<ArgumentNullException>(() => node!.ToDynamicNode());
    }

    [Fact]
    public void ToDynamicRelationship_ThrowsForNullRelationship()
    {
        StrongRelationship? relationship = null;

        Assert.Throws<ArgumentNullException>(() => relationship!.ToDynamicRelationship());
    }

    private sealed record StrongNode : Node
    {
        public string Name { get; init; } = string.Empty;

        public DynamicAddress? Address { get; init; }

        public List<string> Tags { get; init; } = new();
    }

    private sealed record StrongRelationship(string Start, string End) : Relationship(Start, End)
    {
        public string Description { get; init; } = string.Empty;

        public DateOnly Since { get; init; }
    }

    private sealed record DynamicAddress(string Street, string City);
}
