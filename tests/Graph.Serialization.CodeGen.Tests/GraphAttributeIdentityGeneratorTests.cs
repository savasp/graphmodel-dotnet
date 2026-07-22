// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Serialization.CodeGen.Tests;

public class GraphAttributeIdentityGeneratorTests
{
    [Fact]
    public void SourceDefinedGraphNamespaceLookalikes_DoNotAffectGeneratedMetadata()
    {
        const string source = """
            namespace Cvoya.Graph
            {
                public sealed class NodeAttribute(string label) : System.Attribute;
                public sealed class RelationshipAttribute(string label) : System.Attribute;

                public sealed class PropertyAttribute : System.Attribute
                {
                    public string? Label { get; set; }
                    public bool Ignore { get; set; }
                }
            }

            namespace Consumer
            {
                [Cvoya.Graph.Node("FakeNode")]
                public sealed record Subject : Cvoya.Graph.Node
                {
                    [Cvoya.Graph.Property(Label = "fake_name", Ignore = true)]
                    public string Name { get; set; } = string.Empty;
                }

                [Cvoya.Graph.Relationship("FAKE_EDGE")]
                public sealed record Edge : Cvoya.Graph.Relationship;
            }
            """;

        var generated = GeneratorTestHelpers.RunGenerator(source);

        Assert.Contains("simpleProperties[\"Name\"]", generated, StringComparison.Ordinal);
        Assert.Contains("Label: \"Subject\"", generated, StringComparison.Ordinal);
        Assert.Contains("Label: \"Edge\"", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void DerivedGraphAttributes_RetainGeneratedMetadataBehavior()
    {
        const string source = """
            public sealed class DerivedNodeAttribute(string label) : Cvoya.Graph.NodeAttribute(label);
            public sealed class DerivedRelationshipAttribute(string label) : Cvoya.Graph.RelationshipAttribute(label);
            public sealed class DerivedPropertyAttribute : Cvoya.Graph.PropertyAttribute;

            namespace Consumer
            {
                [DerivedNode("Person")]
                public sealed record Subject : Cvoya.Graph.Node
                {
                    [DerivedProperty(Label = "physical_name")]
                    public string Name { get; set; } = string.Empty;
                }

                [DerivedRelationship("KNOWS")]
                public sealed record Edge : Cvoya.Graph.Relationship;
            }
            """;

        var generated = GeneratorTestHelpers.RunGenerator(source);

        Assert.Contains("simpleProperties[\"physical_name\"]", generated, StringComparison.Ordinal);
        Assert.Contains("Label: \"Person\"", generated, StringComparison.Ordinal);
        Assert.Contains("Label: \"KNOWS\"", generated, StringComparison.Ordinal);
    }
}
