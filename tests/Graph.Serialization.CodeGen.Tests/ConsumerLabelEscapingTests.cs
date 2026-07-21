// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Serialization.CodeGen.Tests;

using Cvoya.Graph.Serialization;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>
/// No analyzer restricts which characters a consumer may put in a <c>[Node]</c>,
/// <c>[Relationship]</c>, or <c>[Property(Label = ...)]</c> value, and #412 made punctuation-bearing
/// storage labels a supported contract. The generator embeds those labels in generated C# source, so
/// a quote, backslash, brace, or control escape must neither break the generated syntax nor change
/// the value the serializer addresses at runtime (#422).
/// </summary>
public class ConsumerLabelEscapingTests
{
    /// <summary>
    /// Each case is a label a consumer may legally author. <c>Combined</c> puts several
    /// literal-sensitive characters in one value so an escaping-order mistake (for example doubling
    /// braces before escaping backslashes) cannot pass by handling each character in isolation.
    /// </summary>
    public static TheoryData<string, string> HostileLabels => new()
    {
        { "Quote", "label\"with\"quotes" },
        { "Backslash", @"label\with\backslashes" },
        { "OpenBrace", "label{with{open" },
        { "CloseBrace", "label}with}close" },
        { "InterpolationHole", "label{0}{index}" },
        { "Newline", "label\nwith\r\nnewlines" },
        { "Control", "label\twith\0control\a" },
        { "NonAscii", "étiquette-日本語-Ünïcödé" },
        { "SurrogatePair", "label-\U0001F600-emoji" },
        { "Combined", "l\"a\\b{c}d\ne\tf\u0001g'h\"\"{{i}}" },
    };

    [Theory]
    [MemberData(nameof(HostileLabels))]
    public void NodeAndPropertyLabels_CompileAndPreserveExactRuntimeValue(string caseName, string label)
    {
        var simpleLabel = label + "-simple";
        var simpleCollectionLabel = label + "-tags";
        var complexLabel = label + "-complex";
        var collectionLabel = label + "-collection";
        var source = $$"""
            using System.Collections.Generic;
            using Cvoya.Graph;

            namespace HostileLabels;

            [Node({{Literal(label)}})]
            public record Subject : Node
            {
                [Property(Label = {{Literal(simpleLabel)}})]
                public string Value { get; init; } = string.Empty;

                [Property(Label = {{Literal(simpleCollectionLabel)}})]
                public List<string> Tags { get; set; } = new();

                [Property(Label = {{Literal(complexLabel)}})]
                public Detail Detail { get; set; } = new();

                [Property(Label = {{Literal(collectionLabel)}})]
                public List<Detail> Details { get; set; } = new();
            }

            public record Detail
            {
                public string Note { get; set; } = string.Empty;
            }
            """;

        var assembly = GeneratorTestHelpers.CompileAndLoadGeneratedAssembly(
            source,
            $"{nameof(NodeAndPropertyLabels_CompileAndPreserveExactRuntimeValue)}_{caseName}");
        var subjectType = assembly.GetType("HostileLabels.Subject", throwOnError: true)!;
        var detailType = assembly.GetType("HostileLabels.Detail", throwOnError: true)!;
        var serializer = CreateSerializer(assembly, "HostileLabels.Generated.SubjectSerializer");

        var detail = Activator.CreateInstance(detailType)!;
        detailType.GetProperty("Note")!.SetValue(detail, "note");
        var details = (System.Collections.IList)typeof(List<>).MakeGenericType(detailType)
            .GetConstructor(Type.EmptyTypes)!.Invoke(null)!;
        details.Add(detail);

        var subject = Activator.CreateInstance(subjectType)!;
        subjectType.GetProperty("Value")!.SetValue(subject, "value");
        subjectType.GetProperty("Tags")!.SetValue(subject, new List<string> { "tag" });
        subjectType.GetProperty("Detail")!.SetValue(subject, detail);
        subjectType.GetProperty("Details")!.SetValue(subject, details);

        var serialized = serializer.Serialize(subject);

        // The node label reaches EntityInfo verbatim: Node.Labels defaults to empty, so the
        // generated fallback literal - not a runtime-populated label - is what is asserted here.
        Assert.Equal(label, serialized.Label);

        // Property labels are the physical dictionary keys the provider reads and writes. The
        // inherited Id/Labels properties are serialized alongside them, hence the keyed lookups.
        Assert.Contains(simpleLabel, serialized.SimpleProperties.Keys, StringComparer.Ordinal);
        Assert.Contains(simpleCollectionLabel, serialized.SimpleProperties.Keys, StringComparer.Ordinal);
        Assert.Equal(
            new[] { collectionLabel, complexLabel }.OrderBy(key => key, StringComparer.Ordinal),
            serialized.ComplexProperties.Keys.OrderBy(key => key, StringComparer.Ordinal));
        Assert.Equal(simpleLabel, serialized.SimpleProperties[simpleLabel].Label);

        // The generated reflection lookup uses the C# property name, not the label; a broken
        // lookup would have thrown on the null-forgiving `GetProperty(...)!` during Serialize.
        Assert.Equal("Value", serialized.SimpleProperties[simpleLabel].PropertyInfo.Name);

        var roundTripped = serializer.Deserialize(serialized);
        Assert.Equal("value", subjectType.GetProperty("Value")!.GetValue(roundTripped));
        Assert.Equal(new List<string> { "tag" }, subjectType.GetProperty("Tags")!.GetValue(roundTripped));
        Assert.Equal(
            "note",
            detailType.GetProperty("Note")!.GetValue(subjectType.GetProperty("Detail")!.GetValue(roundTripped)));
        var roundTrippedDetails = (System.Collections.IList)subjectType.GetProperty("Details")!.GetValue(roundTripped)!;
        Assert.Equal("note", detailType.GetProperty("Note")!.GetValue(Assert.Single(roundTrippedDetails.Cast<object>())));

        // Schema label, dictionary keys, and PropertyName arguments are emitted separately from the
        // serialization path, so they are asserted separately.
        var schema = serializer.GetSchema();
        Assert.Equal(label, schema.Label);
        Assert.Contains(simpleLabel, schema.SimpleProperties.Keys, StringComparer.Ordinal);
        Assert.Equal(simpleLabel, schema.SimpleProperties[simpleLabel].PropertyName);
        Assert.Equal(simpleCollectionLabel, schema.SimpleProperties[simpleCollectionLabel].PropertyName);
        Assert.Equal(complexLabel, schema.ComplexProperties[complexLabel].PropertyName);
        Assert.Equal(collectionLabel, schema.ComplexProperties[collectionLabel].PropertyName);
    }

    [Theory]
    [MemberData(nameof(HostileLabels))]
    public void RelationshipAndConstructorBoundLabels_CompileAndPreserveExactRuntimeValue(
        string caseName,
        string label)
    {
        var boundLabel = label + "-bound";
        var source = $$"""
            using Cvoya.Graph;

            namespace HostileRelationshipLabels;

            [Relationship({{Literal(label)}})]
            public sealed record Link(
                [property: Property(Label = {{Literal(boundLabel)}})] string Note)
                : Relationship;
            """;

        var assembly = GeneratorTestHelpers.CompileAndLoadGeneratedAssembly(
            source,
            $"{nameof(RelationshipAndConstructorBoundLabels_CompileAndPreserveExactRuntimeValue)}_{caseName}");
        var linkType = assembly.GetType("HostileRelationshipLabels.Link", throwOnError: true)!;
        var serializer = CreateSerializer(assembly, "HostileRelationshipLabels.Generated.LinkSerializer");

        var link = Activator.CreateInstance(linkType, ["note"])!;

        var serialized = serializer.Serialize(link);

        // Relationship.Type defaults to empty, so EntityInfo.Label comes from the generated literal.
        Assert.Equal(label, serialized.Label);
        Assert.Equal(boundLabel, serialized.SimpleProperties[boundLabel].Label);

        // A constructor parameter resolves its value through the matching property's label, so the
        // deserializer's lookup key is consumer-authored too.
        var roundTripped = serializer.Deserialize(serialized);
        Assert.Equal("note", linkType.GetProperty("Note")!.GetValue(roundTripped));

        var schema = serializer.GetSchema();
        Assert.Equal(label, schema.Label);
        Assert.Equal(boundLabel, schema.SimpleProperties[boundLabel].PropertyName);
    }

    [Theory]
    [MemberData(nameof(HostileLabels))]
    public void HostileLabel_InGeneratedComplexCollectionGuards_IsReportedVerbatim(
        string caseName,
        string label)
    {
        // The complex-collection guards interpolate the property label into generated strings on
        // both the serialization and deserialization paths. These contexts need brace doubling on
        // top of literal escaping.
        var source = $$"""
            using System.Collections.Generic;
            using Cvoya.Graph;

            namespace HostileMessageLabels;

            [Node("Subject")]
            public record Subject : Node
            {
                [Property(Label = {{Literal(label)}})]
                public List<Detail> Details { get; set; } = new();
            }

            public record Detail
            {
                public string Note { get; set; } = string.Empty;
            }
            """;

        var assembly = GeneratorTestHelpers.CompileAndLoadGeneratedAssembly(
            source,
            $"{nameof(HostileLabel_InGeneratedComplexCollectionGuards_IsReportedVerbatim)}_{caseName}");
        var subjectType = assembly.GetType("HostileMessageLabels.Subject", throwOnError: true)!;
        var detailType = assembly.GetType("HostileMessageLabels.Detail", throwOnError: true)!;
        var serializer = CreateSerializer(assembly, "HostileMessageLabels.Generated.SubjectSerializer");

        var details = (System.Collections.IList)typeof(List<>).MakeGenericType(detailType)
            .GetConstructor(Type.EmptyTypes)!.Invoke(null)!;
        details.Add(Activator.CreateInstance(detailType));
        var subject = Activator.CreateInstance(subjectType)!;
        subjectType.GetProperty("Details")!.SetValue(subject, details);

        var serialized = serializer.Serialize(subject);
        EntitySerializerRegistry.Instance.Register(detailType, new NullDeserializer(detailType));
        var readException = Assert.Throws<GraphException>(() => serializer.Deserialize(serialized));

        // Doubled braces are an artifact of the interpolated-string context and must not survive
        // into the message the consumer reads.
        Assert.Contains($"'{label}'", readException.Message, StringComparison.Ordinal);

        details.Clear();
        details.Add(null);
        var writeException = Assert.Throws<GraphException>(() => serializer.Serialize(subject));

        Assert.Contains($"'{label}'", writeException.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(HostileLabels))]
    public void HostileLabel_InGeneratedSimpleCollectionGuard_IsReportedVerbatim(string caseName, string label)
    {
        // The second of the three interpolated-message sites: the non-nullable simple-collection
        // null-element guard on the deserialization side.
        var source = $$"""
            using System.Collections.Generic;
            using Cvoya.Graph;

            namespace HostileSimpleCollectionLabels;

            [Node("Subject")]
            public record Subject : Node
            {
                [Property(Label = {{Literal(label)}})]
                public List<string> Tags { get; set; } = new();
            }
            """;

        var assembly = GeneratorTestHelpers.CompileAndLoadGeneratedAssembly(
            source,
            $"{nameof(HostileLabel_InGeneratedSimpleCollectionGuard_IsReportedVerbatim)}_{caseName}");
        var subjectType = assembly.GetType("HostileSimpleCollectionLabels.Subject", throwOnError: true)!;
        var serializer = CreateSerializer(assembly, "HostileSimpleCollectionLabels.Generated.SubjectSerializer");

        var subject = Activator.CreateInstance(subjectType)!;
        subjectType.GetProperty("Tags")!.SetValue(subject, new List<string> { "tag" });
        var serialized = serializer.Serialize(subject);

        // A null element only reaches the guard from stored data, not from a well-formed instance,
        // so the serialized collection is rewritten with a null-valued element.
        var tags = Assert.IsType<SimpleCollection>(serialized.SimpleProperties[label].Value);
        serialized.SimpleProperties[label] = serialized.SimpleProperties[label] with
        {
            Value = tags with { Values = [new SimpleValue(null!, typeof(string))] },
        };

        var exception = Assert.Throws<GraphException>(() => serializer.Deserialize(serialized));

        Assert.Contains($"'{label}'", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(HostileLabels))]
    public void EscapeForGeneratedStringLiteral_RoundTripsThroughTheCompiler(string caseName, string label)
    {
        Assert.NotNull(caseName);

        var expression = SyntaxFactory.ParseExpression($"\"{Utils.EscapeForGeneratedStringLiteral(label)}\"");

        Assert.Empty(expression.GetDiagnostics());
        var literal = Assert.IsType<LiteralExpressionSyntax>(expression);
        Assert.Equal(label, Assert.IsType<string>(literal.Token.Value));
    }

    [Theory]
    [MemberData(nameof(HostileLabels))]
    public void EscapeForGeneratedInterpolatedString_RoundTripsWithoutOpeningAnInterpolationHole(
        string caseName,
        string label)
    {
        Assert.NotNull(caseName);

        var expression = SyntaxFactory.ParseExpression(
            $"$\"{Utils.EscapeForGeneratedInterpolatedString(label)}\"");

        Assert.Empty(expression.GetDiagnostics());
        var interpolated = Assert.IsType<InterpolatedStringExpressionSyntax>(expression);

        // Every brace in the label must have been doubled into literal text: a surviving
        // interpolation hole would splice generated code with a consumer-controlled expression.
        var text = interpolated.Contents.Select(Assert.IsType<InterpolatedStringTextSyntax>);

        // ValueText resolves backslash escapes but leaves brace doubling for the compiler's later
        // interpolation pass, so undoing it here is what the emitted string will evaluate to. The
        // authoritative value check is the compiled-and-executed exception-message test above.
        var evaluated = string.Concat(text.Select(content => content.TextToken.ValueText))
            .Replace("{{", "{")
            .Replace("}}", "}");
        Assert.Equal(label, evaluated);
    }

    [Fact]
    public void OrdinaryLabel_IsEmittedUnchanged()
    {
        // Byte-stability for the common case: escaping must be invisible to models that use ordinary
        // labels, which is what keeps the existing generator snapshots valid.
        Assert.Equal("physical_name", Utils.EscapeForGeneratedStringLiteral("physical_name"));
        Assert.Equal("physical_name", Utils.EscapeForGeneratedInterpolatedString("physical_name"));
    }

    private static string Literal(string value) => SymbolDisplay.FormatLiteral(value, quote: true);

    private static IEntitySerializer CreateSerializer(System.Reflection.Assembly assembly, string serializerTypeName)
    {
        var serializerType = assembly.GetType(serializerTypeName, throwOnError: true)!;
        return Assert.IsAssignableFrom<IEntitySerializer>(Activator.CreateInstance(serializerType));
    }

    private sealed class NullDeserializer(Type entityType) : IEntitySerializer
    {
        public Type EntityType => entityType;

        public EntityInfo Serialize(object obj) => throw new NotSupportedException();

        public object Deserialize(EntityInfo entity) => null!;

        public EntitySchema GetSchema() => throw new NotSupportedException();
    }
}
