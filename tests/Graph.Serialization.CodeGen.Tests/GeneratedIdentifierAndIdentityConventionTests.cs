// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Serialization.CodeGen.Tests;

using System.Globalization;
using Cvoya.Graph.Serialization;


/// <summary>
/// Covers consumer-authored C# identifiers in generated source and verifies that names such as
/// <c>Id</c> and <c>Direction</c> do not select framework-specific deserialization defaults.
/// </summary>
public class GeneratedIdentifierAndIdentityConventionTests
{
    private const string IdentifierSource = """
        using System.Collections.Generic;
        using Cvoya.Graph;

        namespace KeywordIdentifiers;

        public enum BusinessRelationshipDirection
        {
            Unknown,
            Incoming,
            Outgoing,
        }

        [Node("physical_keyword_nodes")]
        public sealed record KeywordNode(
            string Id,
            BusinessRelationshipDirection Direction,
            string @class,
            string @event,
            string café,
            string entity,
            string result,
            string index,
            string simpleValue,
            string guidValue) : INode
        {
            public IReadOnlyList<string> Labels { get; init; } = [];
            public string @namespace { get; set; } = string.Empty;
            public string @required { get; init; } = string.Empty;
            public string @field { get; set; } = string.Empty;
            public string URL { get; init; } = string.Empty;
            public string Url { get; init; } = string.Empty;
        }

        [Relationship("physical_keyword_links")]
        public sealed record KeywordLink(
            string Id,
            RelationshipDirection Direction,
            string @class,
            string result) : IRelationship
        {
            public string Type { get; init; } = string.Empty;
            public string @event { get; set; } = string.Empty;
            public string naïve { get; set; } = string.Empty;
        }
        """;

    private const string IdentityConventionSource = """
        using System.Collections.Generic;
        using Cvoya.Graph;

        namespace IdentityConventions;

        public enum BusinessRelationshipDirection
        {
            Unknown,
            Incoming,
            Outgoing,
        }

        [Node("Id")]
        public sealed class AssignedNode : INode
        {
            [Property(Label = "domain_id")]
            public string Id { get; init; } = "initializer-id";

            public IReadOnlyList<string> Labels { get; set; } = [];

            [Property(Label = "domain_direction")]
            public BusinessRelationshipDirection Direction { get; set; } = BusinessRelationshipDirection.Outgoing;

            [Property(Label = "Id")]
            public string Description { get; set; } = "initializer-description";

            [Property(Label = "Direction")]
            public BusinessRelationshipDirection Orientation { get; set; } = BusinessRelationshipDirection.Outgoing;
        }

        [Node("Direction")]
        public sealed record BoundNode(
            [property: Property(Label = "domain_id")] string Id,
            [property: Property(Label = "domain_direction")] BusinessRelationshipDirection Direction,
            [property: Property(Label = "Id")] string Description,
            [property: Property(Label = "Direction")] BusinessRelationshipDirection Orientation) : INode
        {
            public IReadOnlyList<string> Labels { get; init; } = [];
        }

        [Relationship("physical_assigned_link")]
        public sealed class AssignedLink : IRelationship
        {
            public string Id { get; init; } = "initializer-id";
            public string Type { get; set; } = string.Empty;
            public RelationshipDirection Direction { get; init; } = RelationshipDirection.Incoming;
        }

        [Relationship("physical_bound_link")]
        public sealed record BoundLink(
            string Id,
            RelationshipDirection Direction) : IRelationship
        {
            public string Type { get; init; } = string.Empty;
        }
        """;

    [Fact]
    public Task KeywordUnicodeAndCollidingIdentifiers()
    {
        var generated = GeneratorTestHelpers.RunGenerator(IdentifierSource);

        Assert.DoesNotContain("Guid.NewGuid", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("RelationshipDirection.Outgoing", generated, StringComparison.Ordinal);

        return Verifier.Verify(RenderIdentifierContract(generated));
    }

    [Theory]
    [InlineData("class", "@class")]
    [InlineData("event", "@event")]
    [InlineData("required", "@required")]
    [InlineData("field", "@field")]
    [InlineData("café", "café")]
    [InlineData("ordinary", "ordinary")]
    public void EscapeIdentifier_UsesRoslynKeywordClassification(string identifier, string expected)
    {
        Assert.Equal(expected, Utils.EscapeIdentifier(identifier));
    }

    [Fact]
    public void KeywordUnicodeAndCollidingIdentifiers_CompileAndRoundTrip()
    {
        var assembly = GeneratorTestHelpers.CompileAndLoadGeneratedAssembly(IdentifierSource);
        var nodeType = assembly.GetType("KeywordIdentifiers.KeywordNode", throwOnError: true)!;
        var linkType = assembly.GetType("KeywordIdentifiers.KeywordLink", throwOnError: true)!;
        var businessDirectionType = assembly.GetType(
            "KeywordIdentifiers.BusinessRelationshipDirection",
            throwOnError: true)!;
        var nodeSerializer = CreateSerializer(assembly, "KeywordIdentifiers.Generated.KeywordNodeSerializer");
        var linkSerializer = CreateSerializer(assembly, "KeywordIdentifiers.Generated.KeywordLinkSerializer");

        var node = Activator.CreateInstance(
            nodeType,
            ["node-id", Enum.ToObject(businessDirectionType, 2), "class", "event", "café", "entity", "result", "index", "simple", "guid"])!;
        Set(nodeType, node, "namespace", "namespace");
        Set(nodeType, node, "required", "required");
        Set(nodeType, node, "field", "field");
        Set(nodeType, node, "URL", "upper");
        Set(nodeType, node, "Url", "mixed");

        var roundTrippedNode = nodeSerializer.Deserialize(nodeSerializer.Serialize(node));

        Assert.Equal("class", Get(nodeType, roundTrippedNode, "class"));
        Assert.Equal("event", Get(nodeType, roundTrippedNode, "event"));
        Assert.Equal("café", Get(nodeType, roundTrippedNode, "café"));
        Assert.Equal("namespace", Get(nodeType, roundTrippedNode, "namespace"));
        Assert.Equal("required", Get(nodeType, roundTrippedNode, "required"));
        Assert.Equal("field", Get(nodeType, roundTrippedNode, "field"));
        Assert.Equal("upper", Get(nodeType, roundTrippedNode, "URL"));
        Assert.Equal("mixed", Get(nodeType, roundTrippedNode, "Url"));

        var link = Activator.CreateInstance(
            linkType,
            ["link-id", RelationshipDirection.Incoming, "class", "result"])!;
        Set(linkType, link, "event", "event");
        Set(linkType, link, "naïve", "unicode");

        var roundTrippedLink = linkSerializer.Deserialize(linkSerializer.Serialize(link));

        Assert.Equal("class", Get(linkType, roundTrippedLink, "class"));
        Assert.Equal("event", Get(linkType, roundTrippedLink, "event"));
        Assert.Equal("unicode", Get(linkType, roundTrippedLink, "naïve"));
    }

    [Fact]
    public void MissingIdentityLikeNames_UseOnlyDeclaredTypeDefaults()
    {
        var assembly = GeneratorTestHelpers.CompileAndLoadGeneratedAssembly(IdentityConventionSource);

        AssertMissingAssignedNodeDefaults(assembly);
        AssertMissingBoundNodeDefaults(assembly);
        AssertMissingRelationshipDefaults(assembly, "AssignedLink");
        AssertMissingRelationshipDefaults(assembly, "BoundLink");
    }

    [Fact]
    public void PresentIdentityLikeNames_RoundTripAsDomainProperties()
    {
        var assembly = GeneratorTestHelpers.CompileAndLoadGeneratedAssembly(IdentityConventionSource);
        var nodeType = assembly.GetType("IdentityConventions.AssignedNode", throwOnError: true)!;
        var serializer = CreateSerializer(assembly, "IdentityConventions.Generated.AssignedNodeSerializer");
        var node = Activator.CreateInstance(nodeType)!;

        Set(nodeType, node, "Id", "domain-id");
        Set(nodeType, node, "Direction", 2);
        Set(nodeType, node, "Description", "description");
        Set(nodeType, node, "Orientation", 1);

        var serialized = serializer.Serialize(node);
        var roundTripped = serializer.Deserialize(serialized);

        Assert.Equal("Id", serialized.Label);
        Assert.Contains("Direction", serialized.SimpleProperties.Keys, StringComparer.Ordinal);
        Assert.Contains("Id", serialized.SimpleProperties.Keys, StringComparer.Ordinal);
        Assert.Contains("domain_direction", serialized.SimpleProperties.Keys, StringComparer.Ordinal);
        Assert.Contains("domain_id", serialized.SimpleProperties.Keys, StringComparer.Ordinal);
        Assert.Equal("domain-id", Get(nodeType, roundTripped, "Id"));
        Assert.Equal(2, Convert.ToInt32(Get(nodeType, roundTripped, "Direction"), CultureInfo.InvariantCulture));
        Assert.Equal("description", Get(nodeType, roundTripped, "Description"));
        Assert.Equal(1, Convert.ToInt32(Get(nodeType, roundTripped, "Orientation"), CultureInfo.InvariantCulture));
    }

    private static void AssertMissingAssignedNodeDefaults(System.Reflection.Assembly assembly)
    {
        var nodeType = assembly.GetType("IdentityConventions.AssignedNode", throwOnError: true)!;
        var serializer = CreateSerializer(assembly, "IdentityConventions.Generated.AssignedNodeSerializer");
        var node = serializer.Deserialize(EmptyEntity(nodeType, "Id"));

        Assert.Equal(string.Empty, Get(nodeType, node, "Id"));
        Assert.Equal(0, Convert.ToInt32(Get(nodeType, node, "Direction"), CultureInfo.InvariantCulture));
        Assert.Equal(string.Empty, Get(nodeType, node, "Description"));
        Assert.Equal(0, Convert.ToInt32(Get(nodeType, node, "Orientation"), CultureInfo.InvariantCulture));
    }

    private static void AssertMissingBoundNodeDefaults(System.Reflection.Assembly assembly)
    {
        var nodeType = assembly.GetType("IdentityConventions.BoundNode", throwOnError: true)!;
        var serializer = CreateSerializer(assembly, "IdentityConventions.Generated.BoundNodeSerializer");
        var node = serializer.Deserialize(EmptyEntity(nodeType, "Direction"));

        Assert.Equal(string.Empty, Get(nodeType, node, "Id"));
        Assert.Equal(0, Convert.ToInt32(Get(nodeType, node, "Direction"), CultureInfo.InvariantCulture));
        Assert.Equal(string.Empty, Get(nodeType, node, "Description"));
        Assert.Equal(0, Convert.ToInt32(Get(nodeType, node, "Orientation"), CultureInfo.InvariantCulture));
    }

    private static void AssertMissingRelationshipDefaults(System.Reflection.Assembly assembly, string typeName)
    {
        var relationshipType = assembly.GetType($"IdentityConventions.{typeName}", throwOnError: true)!;
        var serializer = CreateSerializer(
            assembly,
            $"IdentityConventions.Generated.{typeName}Serializer");
        var relationship = serializer.Deserialize(EmptyEntity(relationshipType, $"physical_{typeName}"));

        Assert.Equal(string.Empty, Get(relationshipType, relationship, "Id"));
        Assert.Equal(default(RelationshipDirection), Get(relationshipType, relationship, "Direction"));
        Assert.Null(relationshipType.GetProperty("StartNodeId"));
        Assert.Null(relationshipType.GetProperty("EndNodeId"));
    }

    private static EntityInfo EmptyEntity(Type type, string label) => new(
        type,
        label,
        [],
        new Dictionary<string, Property>(),
        new Dictionary<string, Property>());

    private static IEntitySerializer CreateSerializer(System.Reflection.Assembly assembly, string serializerTypeName)
    {
        var serializerType = assembly.GetType(serializerTypeName, throwOnError: true)!;
        return Assert.IsAssignableFrom<IEntitySerializer>(Activator.CreateInstance(serializerType));
    }

    private static object? Get(Type type, object instance, string propertyName) =>
        type.GetProperty(propertyName)!.GetValue(instance);

    private static void Set(Type type, object instance, string propertyName, object value)
    {
        var property = type.GetProperty(propertyName)!;
        if (property.PropertyType.IsEnum && value is int enumValue)
        {
            value = Enum.ToObject(property.PropertyType, enumValue);
        }

        property.SetValue(instance, value);
    }

    private static string RenderIdentifierContract(string generated)
    {
        var contractMarkers = new[]
        {
            "@class",
            "@event",
            "@namespace",
            "@required",
            "@field",
            "café",
            "naïve",
            "entity1",
            "result1",
            "index1",
            "simpleValue1",
            "guidValue1",
            "urlValue",
            "default(KeywordIdentifiers.BusinessRelationshipDirection)",
            "default(Cvoya.Graph.RelationshipDirection)",
        };

        return string.Join(
            "\n",
            generated.Split('\n')
                .Where(line => contractMarkers.Any(marker => line.Contains(marker, StringComparison.Ordinal)))
                .Select(line => line.TrimEnd())) + "\n";
    }
}
