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

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace Cvoya.Graph.Model.Analyzers.Rules;

/// <summary>
/// Analyzer that enforces implementation constraints on types that implement INode and IRelationship interfaces.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class NodeAndRelationshipImplementationAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// Gets the supported diagnostics for this analyzer.
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
        DiagnosticDescriptors.OnlyClassesCanImplementInterfaces,
        DiagnosticDescriptors.MustHaveParameterlessConstructor,
        DiagnosticDescriptors.PropertyMustHavePublicAccessors,
        DiagnosticDescriptors.UnsupportedPropertyType,
        DiagnosticDescriptors.InvalidComplexTypeProperty
    );

    /// <summary>
    /// Initializes the analyzer.
    /// </summary>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    /// <summary>
    /// Analyzes named types for INode and IRelationship implementation constraints.
    /// </summary>
    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        if (context.Symbol is not INamedTypeSymbol namedType)
            return;

        // Check if this type implements INode or IRelationship
        var implementsINode = TypeHelpers.ImplementsINode(namedType);
        var implementsIRelationship = TypeHelpers.ImplementsIRelationship(namedType);

        if (!implementsINode && !implementsIRelationship)
            return;

        var interfaceName = implementsINode ? "INode" : "IRelationship";

        // GM001: Only classes can implement INode or IRelationship
        if (namedType.TypeKind != TypeKind.Class)
        {
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.OnlyClassesCanImplementInterfaces,
                namedType.Locations.FirstOrDefault(),
                interfaceName);
            context.ReportDiagnostic(diagnostic);
            return; // Skip further analysis if not a class
        }

        // GM002: Must have parameterless constructor
        if (!TypeHelpers.HasParameterlessConstructor(namedType))
        {
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.MustHaveParameterlessConstructor,
                namedType.Locations.FirstOrDefault(),
                namedType.Name,
                interfaceName);
            context.ReportDiagnostic(diagnostic);
        }

        // Analyze properties
        AnalyzeProperties(context, namedType, implementsINode);
    }

    /// <summary>
    /// Analyzes properties of a type implementing INode or IRelationship.
    /// </summary>
    private static void AnalyzeProperties(SymbolAnalysisContext context, INamedTypeSymbol namedType, bool isNodeImplementation)
    {
        foreach (var member in namedType.GetMembers().OfType<IPropertySymbol>())
        {
            // Skip indexers
            if (member.IsIndexer)
                continue;

            var propertyLocation = member.Locations.FirstOrDefault();

            // GM003: All properties must have public getters and setters
            if (member.GetMethod?.DeclaredAccessibility != Accessibility.Public ||
                member.SetMethod?.DeclaredAccessibility != Accessibility.Public)
            {
                var diagnostic = Diagnostic.Create(
                    DiagnosticDescriptors.PropertyMustHavePublicAccessors,
                    propertyLocation,
                    member.Name,
                    namedType.Name);
                context.ReportDiagnostic(diagnostic);
            }

            // Analyze property type constraints
            AnalyzePropertyType(context, member, namedType, isNodeImplementation);
        }
    }

    /// <summary>
    /// Analyzes the type of a property for constraints.
    /// </summary>
    private static void AnalyzePropertyType(SymbolAnalysisContext context, IPropertySymbol property, INamedTypeSymbol containingType, bool isNodeImplementation)
    {
        var propertyType = property.Type;
        var propertyLocation = property.Locations.FirstOrDefault();

        // Check if it's a simple type or collection of simple types (always allowed)
        if (TypeHelpers.IsSimpleType(propertyType) || TypeHelpers.IsCollectionOfSimpleTypes(propertyType))
        {
            return; // Valid type
        }

        // For INode implementations, check if it's a valid complex type or collection of complex types
        if (isNodeImplementation)
        {
            if (propertyType is INamedTypeSymbol namedPropertyType)
            {
                // Check if it's a valid complex type
                if (TypeHelpers.IsValidComplexType(namedPropertyType, out var complexTypeError))
                {
                    return; // Valid complex type
                }

                // If not a valid complex type, report GM005 if it's a class, GM004 otherwise
                if (namedPropertyType.TypeKind == TypeKind.Class)
                {
                    var diagnostic = Diagnostic.Create(
                        DiagnosticDescriptors.InvalidComplexTypeProperty,
                        propertyLocation,
                        property.Name);
                    context.ReportDiagnostic(diagnostic);
                    return;
                }
            }

            // Check if it's a collection of valid complex types
            if (TypeHelpers.IsCollectionOfValidComplexTypes(propertyType, out var collectionError))
            {
                return; // Valid collection of complex types
            }
        }

        // GM004: Unsupported property type
        var unsupportedDiagnostic = Diagnostic.Create(
            DiagnosticDescriptors.UnsupportedPropertyType,
            propertyLocation,
            property.Name,
            propertyType.ToDisplayString());
        context.ReportDiagnostic(unsupportedDiagnostic);
    }
}