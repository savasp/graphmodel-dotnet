// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Analyzers;

using Microsoft.CodeAnalysis;


/// <summary>
/// Compilation-scoped identities for attributes owned by the Cvoya.Graph assembly.
/// </summary>
internal sealed class GraphAttributeSymbols
{
    private const string GraphAssemblyName = "Cvoya.Graph";

    private GraphAttributeSymbols(
        INamedTypeSymbol? nodeAttribute,
        INamedTypeSymbol? relationshipAttribute,
        INamedTypeSymbol? propertyAttribute,
        INamedTypeSymbol? complexPropertyAttribute)
    {
        NodeAttribute = nodeAttribute;
        RelationshipAttribute = relationshipAttribute;
        PropertyAttribute = propertyAttribute;
        ComplexPropertyAttribute = complexPropertyAttribute;
    }

    public INamedTypeSymbol? NodeAttribute { get; }

    public INamedTypeSymbol? RelationshipAttribute { get; }

    public INamedTypeSymbol? PropertyAttribute { get; }

    public INamedTypeSymbol? ComplexPropertyAttribute { get; }

    public static GraphAttributeSymbols Resolve(Compilation compilation) => new(
        ResolveGraphType(compilation, "Cvoya.Graph.NodeAttribute"),
        ResolveGraphType(compilation, "Cvoya.Graph.RelationshipAttribute"),
        ResolveGraphType(compilation, "Cvoya.Graph.PropertyAttribute"),
        ResolveGraphType(compilation, "Cvoya.Graph.ComplexPropertyAttribute"));

    public AttributeData? FindNodeAttribute(ISymbol symbol) =>
        symbol.GetAttributes().FirstOrDefault(attribute => IsOrDerivedFrom(attribute.AttributeClass, NodeAttribute));

    public AttributeData? FindRelationshipAttribute(ISymbol symbol) =>
        symbol.GetAttributes().FirstOrDefault(attribute => IsOrDerivedFrom(attribute.AttributeClass, RelationshipAttribute));

    public AttributeData? FindPropertyAttribute(ISymbol symbol) =>
        symbol.GetAttributes().FirstOrDefault(attribute => IsOrDerivedFrom(attribute.AttributeClass, PropertyAttribute));

    public AttributeData? FindComplexPropertyAttribute(ISymbol symbol) =>
        symbol.GetAttributes().FirstOrDefault(attribute => IsOrDerivedFrom(attribute.AttributeClass, ComplexPropertyAttribute));

    private static bool IsOrDerivedFrom(INamedTypeSymbol? candidate, INamedTypeSymbol? expected)
    {
        if (expected is null)
            return false;

        for (var current = candidate; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, expected))
                return true;
        }

        return false;
    }

    private static INamedTypeSymbol? ResolveGraphType(Compilation compilation, string metadataName)
    {
        var unambiguousType = compilation.GetTypeByMetadataName(metadataName);
        if (IsFromGraphAssembly(unambiguousType))
            return unambiguousType;

        // A consumer can declare a type with the same fully-qualified name. In that case the
        // singular lookup returns the source symbol (or null for reference ambiguity), so filter
        // the complete metadata-name result to the actual Cvoya.Graph assembly.
        return compilation.GetTypesByMetadataName(metadataName).FirstOrDefault(IsFromGraphAssembly);
    }

    private static bool IsFromGraphAssembly(INamedTypeSymbol? type) =>
        type?.ContainingAssembly.Identity.Name == GraphAssemblyName;
}
