// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Serialization.CodeGen;

using Microsoft.CodeAnalysis;


/// <summary>
/// Assembly-scoped identities for attributes owned by the Cvoya.Graph assembly.
/// </summary>
internal sealed class GraphAttributeSymbols
{
    private const string GraphAssemblyName = "Cvoya.Graph";

    private GraphAttributeSymbols(
        INamedTypeSymbol? nodeAttribute,
        INamedTypeSymbol? relationshipAttribute,
        INamedTypeSymbol? propertyAttribute)
    {
        NodeAttribute = nodeAttribute;
        RelationshipAttribute = relationshipAttribute;
        PropertyAttribute = propertyAttribute;
    }

    private INamedTypeSymbol? NodeAttribute { get; }

    private INamedTypeSymbol? RelationshipAttribute { get; }

    private INamedTypeSymbol? PropertyAttribute { get; }

    public static GraphAttributeSymbols Resolve(IAssemblySymbol contextAssembly)
    {
        var graphAssembly = contextAssembly.Identity.Name == GraphAssemblyName
            ? contextAssembly
            : contextAssembly.Modules
                .SelectMany(module => module.ReferencedAssemblySymbols)
                .FirstOrDefault(assembly => assembly.Identity.Name == GraphAssemblyName);

        return new GraphAttributeSymbols(
            graphAssembly?.GetTypeByMetadataName("Cvoya.Graph.NodeAttribute"),
            graphAssembly?.GetTypeByMetadataName("Cvoya.Graph.RelationshipAttribute"),
            graphAssembly?.GetTypeByMetadataName("Cvoya.Graph.PropertyAttribute"));
    }

    public AttributeData? FindNodeAttribute(ISymbol symbol) =>
        symbol.GetAttributes().FirstOrDefault(attribute => IsOrDerivedFrom(attribute.AttributeClass, NodeAttribute));

    public AttributeData? FindRelationshipAttribute(ISymbol symbol) =>
        symbol.GetAttributes().FirstOrDefault(attribute => IsOrDerivedFrom(attribute.AttributeClass, RelationshipAttribute));

    public AttributeData? FindPropertyAttribute(ISymbol symbol) =>
        symbol.GetAttributes().FirstOrDefault(attribute => IsOrDerivedFrom(attribute.AttributeClass, PropertyAttribute));

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
}
