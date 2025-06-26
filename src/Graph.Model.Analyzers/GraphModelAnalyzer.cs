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

namespace Cvoya.Graph.Model.Analyzers;

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;


/// <summary>
/// Analyzer for enforcing Graph.Model implementation rules.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class GraphModelAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    ///  Gets the list of diagnostics that this analyzer supports.
    ///  Each diagnostic descriptor represents a specific rule that the analyzer checks.
    ///  This list is used by the Roslyn framework to determine which diagnostics to run.
    ///  The diagnostics are defined in the DiagnosticDescriptors class.
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
        DiagnosticDescriptors.MissingParameterlessConstructor,
        DiagnosticDescriptors.PropertyMustHavePublicAccessors,
        DiagnosticDescriptors.PropertyCannotBeGraphInterfaceType,
        DiagnosticDescriptors.InvalidPropertyTypeForNode,
        DiagnosticDescriptors.InvalidPropertyTypeForRelationship,
        DiagnosticDescriptors.ComplexTypeContainsGraphInterfaceTypes,
        DiagnosticDescriptors.DuplicatePropertyAttributeLabel,
        DiagnosticDescriptors.DuplicateRelationshipAttributeLabel,
        DiagnosticDescriptors.DuplicateNodeAttributeLabel,
        DiagnosticDescriptors.CircularReferenceWithoutNullable);

    /// <summary>
    /// Initializes the analyzer and registers the symbol action for named types.
    /// This method is called by the Roslyn framework when the analyzer is loaded.
    /// </summary>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;

        // Skip if it's not a class or struct
        if (namedTypeSymbol.TypeKind != TypeKind.Class && namedTypeSymbol.TypeKind != TypeKind.Struct)
            return;

        var helper = new AnalyzerHelper(context.Compilation);

        bool implementsINode = helper.ImplementsINode(namedTypeSymbol);
        bool implementsIRelationship = helper.ImplementsIRelationship(namedTypeSymbol);

        // Skip if it doesn't implement INode or IRelationship
        if (!implementsINode && !implementsIRelationship)
            return;

        // GM001: Check parameterless constructor
        AnalyzeParameterlessConstructor(context, namedTypeSymbol, implementsINode, implementsIRelationship);

        // GM002: Check property accessors
        AnalyzePropertyAccessors(context, namedTypeSymbol);

        // GM003: Check for INode/IRelationship properties
        AnalyzeGraphInterfaceProperties(context, namedTypeSymbol, helper);

        // GM006: Check complex types for graph interface types (run before GM004/GM005)
        AnalyzeComplexTypeProperties(context, namedTypeSymbol, helper);

        // GM004: Check property types for INode
        if (implementsINode)
        {
            AnalyzeNodePropertyTypes(context, namedTypeSymbol, helper);
        }

        // GM005: Check property types for IRelationship
        if (implementsIRelationship)
        {
            AnalyzeRelationshipPropertyTypes(context, namedTypeSymbol, helper);
        }

        // GM007: Check duplicate PropertyAttribute labels
        AnalyzeDuplicatePropertyAttributeLabels(context, namedTypeSymbol);

        // GM008: Check duplicate RelationshipAttribute labels
        if (implementsIRelationship)
        {
            AnalyzeDuplicateRelationshipAttributeLabels(context, namedTypeSymbol);
        }

        // GM009: Check duplicate NodeAttribute labels
        if (implementsINode)
        {
            AnalyzeDuplicateNodeAttributeLabels(context, namedTypeSymbol);
        }

        // GM010: Check circular references
        AnalyzeCircularReferences(context, namedTypeSymbol, helper);
    }

    private static void AnalyzeParameterlessConstructor(SymbolAnalysisContext context, INamedTypeSymbol namedType, bool implementsINode, bool implementsIRelationship)
    {
        var hasParameterlessConstructor = namedType.Constructors.Any(c =>
            c.Parameters.Length == 0 && (c.DeclaredAccessibility == Accessibility.Public || c.DeclaredAccessibility == Accessibility.Internal));

        if (!hasParameterlessConstructor)
        {
            // Check if there are constructors that initialize all properties
            var allProperties = GetAllProperties(namedType);
            var settableProperties = allProperties.Where(p => p.SetMethod != null && p.SetMethod.DeclaredAccessibility == Accessibility.Public).ToList();

            bool hasValidConstructor = namedType.Constructors.Any(constructor =>
            {
                if (constructor.Parameters.Length != settableProperties.Count)
                    return false;

                // This is a simplified check - in a real implementation you'd need to analyze the constructor body
                // to ensure all properties are actually initialized
                return true;
            });

            if (!hasValidConstructor)
            {
                var interfaceName = implementsINode ? "INode" : "IRelationship";
                var diagnostic = Diagnostic.Create(
                    DiagnosticDescriptors.MissingParameterlessConstructor,
                    namedType.Locations.FirstOrDefault(),
                    namedType.Name,
                    interfaceName);

                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private static void AnalyzePropertyAccessors(SymbolAnalysisContext context, INamedTypeSymbol namedType)
    {
        var properties = GetAllProperties(namedType);

        foreach (var property in properties)
        {
            // Skip compiler-generated properties (like EqualityContract in records)
            if (IsCompilerGeneratedProperty(property))
                continue;
            bool hasPublicGetter = property.GetMethod?.DeclaredAccessibility == Accessibility.Public;
            bool hasPublicSetter = property.SetMethod?.DeclaredAccessibility == Accessibility.Public;
            bool hasPublicInit = property.SetMethod?.IsInitOnly == true && property.SetMethod.DeclaredAccessibility == Accessibility.Public;

            if (!hasPublicGetter || (!hasPublicSetter && !hasPublicInit))
            {
                var diagnostic = Diagnostic.Create(
                    DiagnosticDescriptors.PropertyMustHavePublicAccessors,
                    property.Locations.FirstOrDefault(),
                    property.Name,
                    namedType.Name);

                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private static void AnalyzeGraphInterfaceProperties(SymbolAnalysisContext context, INamedTypeSymbol namedType, AnalyzerHelper helper)
    {
        var properties = GetAllProperties(namedType);

        foreach (var property in properties)
        {
            // Skip compiler-generated properties (like EqualityContract in records)
            if (IsCompilerGeneratedProperty(property))
                continue;
            // Check direct graph interface types (but not collections/arrays)
            if (helper.IsGraphInterfaceType(property.Type) && !IsAnyCollectionType(property.Type, helper))
            {
                var diagnostic = Diagnostic.Create(
                    DiagnosticDescriptors.PropertyCannotBeGraphInterfaceType,
                    property.Locations.FirstOrDefault(),
                    property.Name,
                    namedType.Name,
                    GetShortTypeName(property.Type));

                context.ReportDiagnostic(diagnostic);
            }
            // Check collections of graph interface types (separate from direct types)
            else if (IsAnyCollectionType(property.Type, helper))
            {
                var elementType = helper.GetCollectionElementType(property.Type);
                if (elementType != null && helper.IsGraphInterfaceType(elementType))
                {
                    var diagnostic = Diagnostic.Create(
                        DiagnosticDescriptors.PropertyCannotBeGraphInterfaceType,
                        property.Locations.FirstOrDefault(),
                        property.Name,
                        namedType.Name,
                        GetShortTypeName(property.Type));

                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }

    private static bool IsAnyCollectionType(ITypeSymbol type, AnalyzerHelper helper)
    {
        // Arrays are always collections
        if (type is IArrayTypeSymbol)
            return true;

        // Check if it's a generic type that implements IEnumerable (but not string)
        if (type.SpecialType == SpecialType.System_String)
            return false;

        if (type is INamedTypeSymbol namedType)
        {
            // Check if it directly is IEnumerable or IEnumerable<T>
            if (namedType.Name == "IEnumerable" &&
                namedType.ContainingNamespace?.ToDisplayString() is "System.Collections" or "System.Collections.Generic")
            {
                return true;
            }

            // Check implemented interfaces
            foreach (var interfaceType in namedType.AllInterfaces)
            {
                if (interfaceType.Name == "IEnumerable" &&
                    interfaceType.ContainingNamespace?.ToDisplayString() is "System.Collections" or "System.Collections.Generic")
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string GetShortTypeName(ITypeSymbol type)
    {
        // For nullable reference types
        if (type.NullableAnnotation == NullableAnnotation.Annotated && !type.IsValueType)
        {
            return $"{GetShortTypeName(type.WithNullableAnnotation(NullableAnnotation.NotAnnotated))}?";
        }

        // For collections, we need to get the simplified name
        if (type is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            var typeName = namedType.Name;
            var typeArguments = namedType.TypeArguments;

            if (typeArguments.Length > 0)
            {
                var argumentNames = typeArguments.Select(GetShortTypeName);
                return $"{typeName}<{string.Join(", ", argumentNames)}>";
            }

            return typeName;
        }

        // For arrays
        if (type is IArrayTypeSymbol arrayType)
        {
            return $"{GetShortTypeName(arrayType.ElementType)}[]";
        }

        // Handle nullable value types
        if (type is INamedTypeSymbol nullable && nullable.OriginalDefinition?.SpecialType == SpecialType.System_Nullable_T)
        {
            return $"{GetShortTypeName(nullable.TypeArguments[0])}?";
        }

        // For simple types, just return the name
        return type.Name;
    }

    private static void AnalyzeNodePropertyTypes(SymbolAnalysisContext context, INamedTypeSymbol namedType, AnalyzerHelper helper)
    {
        var properties = GetAllProperties(namedType);

        foreach (var property in properties)
        {
            // Skip compiler-generated properties (like EqualityContract in records)
            if (IsCompilerGeneratedProperty(property))
                continue;
            // First, check if GM003 would handle this property (graph interface types)
            if (helper.IsGraphInterfaceType(property.Type))
            {
                // GM003 will handle this, so skip it here
                continue;
            }

            // Also check if GM003 would handle collections of graph interface types
            // This includes arrays and any generic collection containing graph interfaces
            var elementType = helper.GetCollectionElementType(property.Type);
            if (elementType != null && helper.IsGraphInterfaceType(elementType))
            {
                // GM003 will handle this collection of graph interfaces, so skip it here
                continue;
            }

            // First, check if it's a complex type that contains graph interfaces
            // If so, skip this property and let GM006 handle it
            if (helper.IsComplexType(property.Type))
            {
                var result = helper.ValidateComplexType(property.Type);
                if (!result.IsValid)
                {
                    // This complex type contains graph interfaces, so GM006 will handle it
                    continue;
                }
            }

            // Also check if it's a collection of complex types that contain graph interfaces
            if (helper.IsCollectionOfComplexTypes(property.Type))
            {
                var complexElementType = helper.GetCollectionElementType(property.Type);
                if (complexElementType != null && helper.IsComplexType(complexElementType))
                {
                    var result = helper.ValidateComplexType(complexElementType);
                    if (!result.IsValid)
                    {
                        // This collection contains complex types with graph interfaces, so GM006 will handle it
                        continue;
                    }
                }
            }

            // Also check collections where element type contains graph interfaces (even if not "complex")
            if (helper.IsCollectionType(property.Type))
            {
                var collectionElementType = helper.GetCollectionElementType(property.Type);
                if (collectionElementType != null && !helper.IsSimpleType(collectionElementType))
                {
                    var result = helper.ValidateComplexType(collectionElementType);
                    if (!result.IsValid)
                    {
                        // This collection contains types with graph interfaces, so GM006 will handle it
                        continue;
                    }
                }
            }

            // Explicit check for arrays to ensure they are handled by GM006 if they contain graph interfaces
            if (property.Type is IArrayTypeSymbol arrayType)
            {
                var result = helper.ValidateComplexType(arrayType.ElementType);
                if (!result.IsValid)
                {
                    // This array contains types with graph interfaces, so GM006 will handle it
                    continue;
                }
            }

            // Now check if it's a valid node property type
            if (!helper.IsValidNodePropertyType(property.Type))
            {
                var diagnostic = Diagnostic.Create(
                    DiagnosticDescriptors.InvalidPropertyTypeForNode,
                    property.Locations.FirstOrDefault(),
                    property.Name,
                    namedType.Name,
                    GetShortTypeName(property.Type));

                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private static void AnalyzeRelationshipPropertyTypes(SymbolAnalysisContext context, INamedTypeSymbol namedType, AnalyzerHelper helper)
    {
        var properties = GetAllProperties(namedType);

        foreach (var property in properties)
        {
            // Skip compiler-generated properties (like EqualityContract in records)
            if (IsCompilerGeneratedProperty(property))
                continue;
            // First, check if GM003 would handle this property (graph interface types)
            if (helper.IsGraphInterfaceType(property.Type))
            {
                // GM003 will handle this, so skip it here
                continue;
            }

            // Also check if GM003 would handle collections of graph interface types
            // This includes arrays and any generic collection containing graph interfaces
            var elementType = helper.GetCollectionElementType(property.Type);
            if (elementType != null && helper.IsGraphInterfaceType(elementType))
            {
                // GM003 will handle this collection of graph interfaces, so skip it here
                continue;
            }

            // First, check if it's a complex type that contains graph interfaces
            // If so, skip this property and let GM006 handle it
            if (helper.IsComplexType(property.Type))
            {
                var result = helper.ValidateComplexType(property.Type);
                if (!result.IsValid)
                {
                    // This complex type contains graph interfaces, so GM006 will handle it
                    continue;
                }
            }

            // Also check if it's a collection of complex types that contain graph interfaces
            if (helper.IsCollectionOfComplexTypes(property.Type))
            {
                var complexElementType = helper.GetCollectionElementType(property.Type);
                if (complexElementType != null && helper.IsComplexType(complexElementType))
                {
                    var result = helper.ValidateComplexType(complexElementType);
                    if (!result.IsValid)
                    {
                        // This collection contains complex types with graph interfaces, so GM006 will handle it
                        continue;
                    }
                }
            }

            // Also check collections where element type contains graph interfaces (even if not "complex")
            if (helper.IsCollectionType(property.Type))
            {
                var collectionElementType = helper.GetCollectionElementType(property.Type);
                if (collectionElementType != null && !helper.IsSimpleType(collectionElementType))
                {
                    var result = helper.ValidateComplexType(collectionElementType);
                    if (!result.IsValid)
                    {
                        // This collection contains types with graph interfaces, so GM006 will handle it
                        continue;
                    }
                }
            }

            // Explicit check for arrays to ensure they are handled by GM006 if they contain graph interfaces
            if (property.Type is IArrayTypeSymbol arrayType)
            {
                var result = helper.ValidateComplexType(arrayType.ElementType);
                if (!result.IsValid)
                {
                    // This array contains types with graph interfaces, so GM006 will handle it
                    continue;
                }
            }

            // Now check if it's a valid relationship property type
            if (!helper.IsValidRelationshipPropertyType(property.Type))
            {
                var diagnostic = Diagnostic.Create(
                    DiagnosticDescriptors.InvalidPropertyTypeForRelationship,
                    property.Locations.FirstOrDefault(),
                    property.Name,
                    namedType.Name,
                    GetShortTypeName(property.Type));

                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private static void AnalyzeComplexTypeProperties(SymbolAnalysisContext context, INamedTypeSymbol namedType, AnalyzerHelper helper)
    {
        var properties = GetAllProperties(namedType);

        foreach (var property in properties)
        {
            // Skip compiler-generated properties (like EqualityContract in records)
            if (IsCompilerGeneratedProperty(property))
                continue;
            // First, check if GM003 would handle this property (graph interface types)
            if (helper.IsGraphInterfaceType(property.Type))
            {
                // GM003 will handle this, so skip it here
                continue;
            }

            // Also check if GM003 would handle collections of graph interface types
            var elementType = helper.GetCollectionElementType(property.Type);
            if (elementType != null && helper.IsGraphInterfaceType(elementType))
            {
                // GM003 will handle this collection of graph interfaces, so skip it here
                continue;
            }

            // Check direct complex types
            if (helper.IsComplexType(property.Type))
            {
                var result = helper.ValidateComplexType(property.Type);
                if (!result.IsValid)
                {
                    var diagnostic = Diagnostic.Create(
                        DiagnosticDescriptors.ComplexTypeContainsGraphInterfaceTypes,
                        property.Locations.FirstOrDefault(),
                        property.Type.ToDisplayString(),
                        property.Name,
                        namedType.Name);

                    context.ReportDiagnostic(diagnostic);
                }
            }
            // Check collections (both of complex types and of types that contain graph interfaces)
            else if (helper.IsCollectionType(property.Type))
            {
                var collectionElementType = helper.GetCollectionElementType(property.Type);
                if (collectionElementType != null && !helper.IsSimpleType(collectionElementType))
                {
                    // Check if this element type contains graph interfaces
                    var result = helper.ValidateComplexType(collectionElementType);
                    if (!result.IsValid)
                    {
                        var diagnostic = Diagnostic.Create(
                            DiagnosticDescriptors.ComplexTypeContainsGraphInterfaceTypes,
                            property.Locations.FirstOrDefault(),
                            collectionElementType.ToDisplayString(),
                            property.Name,
                            namedType.Name);

                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
            // Explicit check for arrays in case IsCollectionType doesn't catch them
            else if (property.Type is IArrayTypeSymbol arrayType)
            {
                if (!helper.IsSimpleType(arrayType.ElementType))
                {
                    var result = helper.ValidateComplexType(arrayType.ElementType);
                    if (!result.IsValid)
                    {
                        var diagnostic = Diagnostic.Create(
                            DiagnosticDescriptors.ComplexTypeContainsGraphInterfaceTypes,
                            property.Locations.FirstOrDefault(),
                            arrayType.ElementType.ToDisplayString(),
                            property.Name,
                            namedType.Name);

                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }
    }

    private static void AnalyzeDuplicatePropertyAttributeLabels(SymbolAnalysisContext context, INamedTypeSymbol namedType)
    {
        // This dictionary will track property labels across the entire inheritance hierarchy
        var propertyLabels = new Dictionary<string, (IPropertySymbol Property, INamedTypeSymbol ContainingType)>(StringComparer.OrdinalIgnoreCase);

        // Walk through the inheritance hierarchy
        var currentType = namedType;

        while (currentType != null)
        {
            var properties = GetAllProperties(currentType);

            foreach (var property in properties)
            {
                var propertyAttr = property.GetAttributes()
                    .FirstOrDefault(a => a.AttributeClass?.Name == "PropertyAttribute");

                if (propertyAttr != null)
                {
                    // Try to extract the label from NamedArguments first
                    var namedArg = propertyAttr.NamedArguments.FirstOrDefault(arg => arg.Key == "Label");
                    string? label = null;

                    if (!namedArg.Equals(default(KeyValuePair<string, TypedConstant>)))
                    {
                        // Handle TypedConstant properly - check if it's an array
                        if (namedArg.Value.Kind == TypedConstantKind.Array)
                        {
                            // For arrays, take the first value
                            var firstValue = namedArg.Value.Values.FirstOrDefault();
                            label = firstValue.Value?.ToString();
                        }
                        else
                        {
                            label = namedArg.Value.Value?.ToString();
                        }
                    }

                    // If NamedArguments doesn't work (common in test frameworks), 
                    // try to extract from source code
                    if (string.IsNullOrEmpty(label))
                    {
                        label = ExtractLabelFromSource(property);
                    }

                    // Fallback to property name if no label found
                    if (string.IsNullOrEmpty(label))
                    {
                        label = property.Name;
                    }

                    // Only proceed if we have a valid label
                    if (!string.IsNullOrEmpty(label))
                    {
                        if (propertyLabels.TryGetValue(label!, out var existing))
                        {
                            // Use the proper 5-argument diagnostic as intended by the descriptor
                            var diagnostic = Diagnostic.Create(
                                DiagnosticDescriptors.DuplicatePropertyAttributeLabel,
                                property.Locations.FirstOrDefault(),
                                property.Name,
                                currentType.Name,
                                label,
                                existing.Property.Name,
                                existing.ContainingType.Name);

                            context.ReportDiagnostic(diagnostic);
                        }
                        else
                        {
                            propertyLabels[label!] = (property, currentType);
                        }
                    }
                }
            }

            currentType = currentType.BaseType;
        }
    }

    private static string ExtractLabelFromSource(IPropertySymbol property)
    {
        // Try to extract the Label value from the source syntax
        var location = property.Locations.FirstOrDefault();
        if (location == null || location.SourceTree == null)
            return string.Empty;

        var sourceText = location.SourceTree.GetText();
        var propertyLine = sourceText.Lines[location.GetLineSpan().StartLinePosition.Line];
        var lineText = propertyLine.ToString();

        // Look for [Property(Label = "value")] pattern
        var match = System.Text.RegularExpressions.Regex.Match(lineText, @"\[Property\(Label\s*=\s*""([^""]+)""\)]");
        if (match.Success)
        {
            return match.Groups[1].Value;
        }

        // Look for lines above the property for the attribute
        var lineNumber = location.GetLineSpan().StartLinePosition.Line;
        if (lineNumber > 0)
        {
            var previousLine = sourceText.Lines[lineNumber - 1].ToString().Trim();
            var previousMatch = System.Text.RegularExpressions.Regex.Match(previousLine, @"\[Property\(Label\s*=\s*""([^""]+)""\)]");
            if (previousMatch.Success)
            {
                return previousMatch.Groups[1].Value;
            }
        }

        return string.Empty;
    }

    private static void AnalyzeDuplicateRelationshipAttributeLabels(SymbolAnalysisContext context, INamedTypeSymbol namedType)
    {
        var helper = new AnalyzerHelper(context.Compilation);

        // Collect all types in the compilation that implement IRelationship
        var allRelationshipTypes = context.Compilation.GetSymbolsWithName(_ => true, SymbolFilter.Type)
            .OfType<INamedTypeSymbol>()
            .Where(t => helper.ImplementsIRelationship(t))
            .ToList();

        // Extract labels from all relationship types
        var labelToTypeMap = new Dictionary<string, INamedTypeSymbol>(StringComparer.OrdinalIgnoreCase);

        foreach (var type in allRelationshipTypes)
        {
            var relationshipAttr = type.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.Name == "RelationshipAttribute");

            if (relationshipAttr != null)
            {
                var labels = new List<string>();

                // Try to extract labels from NamedArguments first
                var namedArg = relationshipAttr.NamedArguments.FirstOrDefault(arg => arg.Key == "Label");
                string? namedLabel = null;

                if (!namedArg.Equals(default(KeyValuePair<string, TypedConstant>)))
                {
                    // Handle TypedConstant properly - check if it's an array
                    if (namedArg.Value.Kind == TypedConstantKind.Array)
                    {
                        // For arrays, take the first value
                        var firstValue = namedArg.Value.Values.FirstOrDefault();
                        namedLabel = firstValue.Value?.ToString();
                    }
                    else
                    {
                        namedLabel = namedArg.Value.Value?.ToString();
                    }
                }

                if (!string.IsNullOrEmpty(namedLabel))
                {
                    labels.Add(namedLabel!);
                }

                // If NamedArguments doesn't work, try all ConstructorArguments (for multiple labels)
                if (labels.Count == 0)
                {
                    foreach (var arg in relationshipAttr.ConstructorArguments)
                    {
                        // Handle TypedConstant properly - check if it's an array
                        if (arg.Kind == TypedConstantKind.Array)
                        {
                            // For arrays, extract all values
                            foreach (var arrayValue in arg.Values)
                            {
                                var label = arrayValue.Value?.ToString();
                                if (!string.IsNullOrEmpty(label))
                                {
                                    labels.Add(label!);
                                }
                            }
                        }
                        else
                        {
                            var label = arg.Value?.ToString();
                            if (!string.IsNullOrEmpty(label))
                            {
                                labels.Add(label!);
                            }
                        }
                    }
                }

                // If both fail (common in test frameworks), try to extract from source code
                if (labels.Count == 0)
                {
                    var sourceLabels = ExtractRelationshipLabelsFromSource(type);
                    if (sourceLabels.Count > 0)
                    {
                        labels.AddRange(sourceLabels);
                    }
                }

                // Fallback to type name if no labels found
                if (labels.Count == 0)
                {
                    labels.Add(type.Name);
                }

                // Check each label for duplicates
                foreach (var label in labels)
                {
                    if (!string.IsNullOrEmpty(label))
                    {
                        if (labelToTypeMap.TryGetValue(label!, out var existing))
                        {
                            // Only report diagnostic if we're analyzing the type that has the conflict
                            if (type.Equals(namedType, SymbolEqualityComparer.Default))
                            {
                                var diagnostic = Diagnostic.Create(
                                    DiagnosticDescriptors.DuplicateRelationshipAttributeLabel,
                                    type.Locations.FirstOrDefault(),
                                    type.Name,
                                    label,
                                    existing.Name);

                                context.ReportDiagnostic(diagnostic);
                            }
                        }
                        else
                        {
                            labelToTypeMap[label!] = type;
                        }
                    }
                }
            }
        }
    }

    private static List<string> ExtractRelationshipLabelsFromSource(INamedTypeSymbol type)
    {
        var labels = new List<string>();

        // Try to extract the Label value from the source syntax
        var location = type.Locations.FirstOrDefault();
        if (location == null || location.SourceTree == null)
            return labels;

        var sourceText = location.SourceTree.GetText();
        var typeLine = sourceText.Lines[location.GetLineSpan().StartLinePosition.Line];
        var lineText = typeLine.ToString();

        // Look for [Relationship(Label = "value")] pattern (named argument)
        var namedMatch = System.Text.RegularExpressions.Regex.Match(lineText, @"\[Relationship\(Label\s*=\s*""([^""]+)""\)]");
        if (namedMatch.Success)
        {
            labels.Add(namedMatch.Groups[1].Value);
            return labels;
        }

        // Look for [Relationship("value1", "value2", ...)] pattern (constructor arguments)
        var constructorMatch = System.Text.RegularExpressions.Regex.Match(lineText, @"\[Relationship\(([^)]+)\)]");
        if (constructorMatch.Success)
        {
            var argumentsText = constructorMatch.Groups[1].Value;
            // Extract all quoted strings
            var matches = System.Text.RegularExpressions.Regex.Matches(argumentsText, @"""([^""]+)""");
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                labels.Add(match.Groups[1].Value);
            }
            if (labels.Count > 0)
                return labels;
        }

        // Look for lines above the type for the attribute
        var lineNumber = location.GetLineSpan().StartLinePosition.Line;
        if (lineNumber > 0)
        {
            var previousLine = sourceText.Lines[lineNumber - 1].ToString().Trim();

            // Try named argument syntax on previous line
            var previousNamedMatch = System.Text.RegularExpressions.Regex.Match(previousLine, @"\[Relationship\(Label\s*=\s*""([^""]+)""\)]");
            if (previousNamedMatch.Success)
            {
                labels.Add(previousNamedMatch.Groups[1].Value);
                return labels;
            }

            // Try constructor syntax on previous line
            var previousConstructorMatch = System.Text.RegularExpressions.Regex.Match(previousLine, @"\[Relationship\(([^)]+)\)]");
            if (previousConstructorMatch.Success)
            {
                var argumentsText = previousConstructorMatch.Groups[1].Value;
                // Extract all quoted strings
                var matches = System.Text.RegularExpressions.Regex.Matches(argumentsText, @"""([^""]+)""");
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    labels.Add(match.Groups[1].Value);
                }
            }
        }

        return labels;
    }

    private static void AnalyzeDuplicateNodeAttributeLabels(SymbolAnalysisContext context, INamedTypeSymbol namedType)
    {
        var helper = new AnalyzerHelper(context.Compilation);

        // Collect all types in the compilation that implement INode
        var allNodeTypes = context.Compilation.GetSymbolsWithName(_ => true, SymbolFilter.Type)
            .OfType<INamedTypeSymbol>()
            .Where(t => helper.ImplementsINode(t))
            .ToList();

        // Extract labels from all node types
        var labelToTypeMap = new Dictionary<string, INamedTypeSymbol>(StringComparer.OrdinalIgnoreCase);

        foreach (var type in allNodeTypes)
        {
            var nodeAttr = type.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.Name == "NodeAttribute");

            if (nodeAttr != null)
            {
                var labels = new List<string>();

                // Try to extract labels from NamedArguments first
                var namedArg = nodeAttr.NamedArguments.FirstOrDefault(arg => arg.Key == "Label");
                string? namedLabel = null;

                if (!namedArg.Equals(default(KeyValuePair<string, TypedConstant>)))
                {
                    // Handle TypedConstant properly - check if it's an array
                    if (namedArg.Value.Kind == TypedConstantKind.Array)
                    {
                        // For arrays, take the first value
                        var firstValue = namedArg.Value.Values.FirstOrDefault();
                        namedLabel = firstValue.Value?.ToString();
                    }
                    else
                    {
                        namedLabel = namedArg.Value.Value?.ToString();
                    }
                }

                if (!string.IsNullOrEmpty(namedLabel))
                {
                    labels.Add(namedLabel!);
                }

                // If NamedArguments doesn't work, try all ConstructorArguments (for multiple labels)
                if (labels.Count == 0)
                {
                    foreach (var arg in nodeAttr.ConstructorArguments)
                    {
                        // Handle TypedConstant properly - check if it's an array
                        if (arg.Kind == TypedConstantKind.Array)
                        {
                            // For arrays, extract all values
                            foreach (var arrayValue in arg.Values)
                            {
                                var label = arrayValue.Value?.ToString();
                                if (!string.IsNullOrEmpty(label))
                                {
                                    labels.Add(label!);
                                }
                            }
                        }
                        else
                        {
                            var label = arg.Value?.ToString();
                            if (!string.IsNullOrEmpty(label))
                            {
                                labels.Add(label!);
                            }
                        }
                    }
                }

                // If both fail (common in test frameworks), try to extract from source code
                if (labels.Count == 0)
                {
                    var sourceLabels = ExtractNodeLabelsFromSource(type);
                    if (sourceLabels.Count > 0)
                    {
                        labels.AddRange(sourceLabels);
                    }
                }

                // Fallback to type name if no labels found
                if (labels.Count == 0)
                {
                    labels.Add(type.Name);
                }

                // Check each label for duplicates
                foreach (var label in labels)
                {
                    if (!string.IsNullOrEmpty(label))
                    {
                        if (labelToTypeMap.TryGetValue(label!, out var existing))
                        {
                            // Only report diagnostic if we're analyzing the type that has the conflict
                            if (type.Equals(namedType, SymbolEqualityComparer.Default))
                            {
                                var diagnostic = Diagnostic.Create(
                                    DiagnosticDescriptors.DuplicateNodeAttributeLabel,
                                    type.Locations.FirstOrDefault(),
                                    type.Name,
                                    label,
                                    existing.Name);

                                context.ReportDiagnostic(diagnostic);
                            }
                        }
                        else
                        {
                            labelToTypeMap[label!] = type;
                        }
                    }
                }
            }
        }
    }

    private static List<string> ExtractNodeLabelsFromSource(INamedTypeSymbol type)
    {
        var labels = new List<string>();

        // Try to extract the Label value from the source syntax
        var location = type.Locations.FirstOrDefault();
        if (location == null || location.SourceTree == null)
            return labels;

        var sourceText = location.SourceTree.GetText();
        var typeLine = sourceText.Lines[location.GetLineSpan().StartLinePosition.Line];
        var lineText = typeLine.ToString();

        // Look for [Node(Label = "value")] pattern (named argument)
        var namedMatch = System.Text.RegularExpressions.Regex.Match(lineText, @"\[Node\(Label\s*=\s*""([^""]+)""\)]");
        if (namedMatch.Success)
        {
            labels.Add(namedMatch.Groups[1].Value);
            return labels;
        }

        // Look for [Node("value1", "value2", ...)] pattern (constructor arguments)
        var constructorMatch = System.Text.RegularExpressions.Regex.Match(lineText, @"\[Node\(([^)]+)\)]");
        if (constructorMatch.Success)
        {
            var argumentsText = constructorMatch.Groups[1].Value;
            // Extract all quoted strings
            var matches = System.Text.RegularExpressions.Regex.Matches(argumentsText, @"""([^""]+)""");
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                labels.Add(match.Groups[1].Value);
            }
            if (labels.Count > 0)
                return labels;
        }

        // Look for lines above the type for the attribute
        var lineNumber = location.GetLineSpan().StartLinePosition.Line;
        if (lineNumber > 0)
        {
            var previousLine = sourceText.Lines[lineNumber - 1].ToString().Trim();

            // Try named argument syntax on previous line
            var previousNamedMatch = System.Text.RegularExpressions.Regex.Match(previousLine, @"\[Node\(Label\s*=\s*""([^""]+)""\)]");
            if (previousNamedMatch.Success)
            {
                labels.Add(previousNamedMatch.Groups[1].Value);
                return labels;
            }

            // Try constructor syntax on previous line
            var previousConstructorMatch = System.Text.RegularExpressions.Regex.Match(previousLine, @"\[Node\(([^)]+)\)]");
            if (previousConstructorMatch.Success)
            {
                var argumentsText = previousConstructorMatch.Groups[1].Value;
                // Extract all quoted strings
                var matches = System.Text.RegularExpressions.Regex.Matches(argumentsText, @"""([^""]+)""");
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    labels.Add(match.Groups[1].Value);
                }
            }
        }

        return labels;
    }

    private static void AnalyzeCircularReferences(SymbolAnalysisContext context, INamedTypeSymbol namedType, AnalyzerHelper helper)
    {
        // Only analyze graph types (nodes/relationships) for circular references
        if (!helper.ImplementsINode(namedType) && !helper.ImplementsIRelationship(namedType))
            return;

        var properties = GetAllProperties(namedType);
        foreach (var property in properties)
        {
            // Skip compiler-generated properties (like EqualityContract in records)
            if (IsCompilerGeneratedProperty(property))
                continue;
            // Skip properties that would be handled by GM004/GM005 (invalid property types)
            bool isNode = helper.ImplementsINode(namedType);
            bool isRelationship = helper.ImplementsIRelationship(namedType);

            if (isNode && !helper.IsValidNodePropertyType(property.Type))
            {
                // GM004 will handle this invalid node property type
                continue;
            }

            if (isRelationship && !helper.IsValidRelationshipPropertyType(property.Type))
            {
                // GM005 will handle this invalid relationship property type
                continue;
            }

            if (helper.IsComplexType(property.Type))
            {
                // Check if this property type has circular references within itself
                if (property.Type is INamedTypeSymbol namedPropertyType &&
                    HasCircularReference(namedPropertyType, namedPropertyType, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default), helper))
                {
                    var diagnostic = Diagnostic.Create(
                        DiagnosticDescriptors.CircularReferenceWithoutNullable,
                        property.Locations.FirstOrDefault(),
                        property.Name,
                        namedType.Name,
                        GetShortTypeName(property.Type));

                    context.ReportDiagnostic(diagnostic);
                }
            }

            // Check collections
            var elementType = GetElementType(property.Type);
            if (elementType is INamedTypeSymbol namedElementType && helper.IsComplexType(namedElementType))
            {
                // Also check if the collection itself would be handled by GM004/GM005
                if (isNode && !helper.IsValidNodePropertyType(property.Type))
                {
                    // GM004 will handle this invalid node property type
                    continue;
                }

                if (isRelationship && !helper.IsValidRelationshipPropertyType(property.Type))
                {
                    // GM005 will handle this invalid relationship property type
                    continue;
                }

                // Check if the collection element type has circular references within itself
                if (HasCircularReference(namedElementType, namedElementType, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default), helper))
                {
                    var diagnostic = Diagnostic.Create(
                        DiagnosticDescriptors.CircularReferenceWithoutNullable,
                        property.Locations.FirstOrDefault(),
                        property.Name,
                        namedType.Name,
                        GetShortTypeName(namedElementType));

                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }

    private static bool HasCircularReference(INamedTypeSymbol rootType, ITypeSymbol currentType, HashSet<ITypeSymbol> visitedTypes, AnalyzerHelper helper)
    {
        // If we've already visited this type in this path, we found a cycle
        if (visitedTypes.Contains(currentType))
            return true;

        // Only check complex types for circular references
        if (!helper.IsComplexType(currentType))
            return false;

        // Skip framework types from circular reference analysis (like System.Type)
        if (IsFrameworkType(currentType))
            return false;

        visitedTypes.Add(currentType);

        if (currentType is INamedTypeSymbol namedCurrentType)
        {
            var properties = GetAllProperties(namedCurrentType);
            foreach (var property in properties)
            {
                // Skip compiler-generated properties (like EqualityContract in records)
                if (IsCompilerGeneratedProperty(property))
                    continue;
                // Skip nullable references - they break the cycle
                if (IsNullableReference(property.Type, helper))
                    continue;

                // Check if this property leads back to the root type
                if (SymbolEqualityComparer.Default.Equals(property.Type, rootType))
                    return true;

                // Check direct references
                if (HasCircularReference(rootType, property.Type, new HashSet<ITypeSymbol>(visitedTypes, SymbolEqualityComparer.Default), helper))
                    return true;

                // Check collections
                var elementType = GetElementType(property.Type);
                if (elementType != null)
                {
                    if (SymbolEqualityComparer.Default.Equals(elementType, rootType))
                        return true;

                    if (HasCircularReference(rootType, elementType, new HashSet<ITypeSymbol>(visitedTypes, SymbolEqualityComparer.Default), helper))
                        return true;
                }
            }
        }

        return false;
    }

    private static bool IsNullableReference(ITypeSymbol type, AnalyzerHelper helper)
    {
        // Check for nullable reference types
        if (type.NullableAnnotation == NullableAnnotation.Annotated && !type.IsValueType)
            return true;

        // Check for nullable collections
        if (type is INamedTypeSymbol namedType && helper.IsCollectionType(type))
        {
            if (namedType.NullableAnnotation == NullableAnnotation.Annotated)
                return true;
        }

        return false;
    }

    private static ITypeSymbol? GetElementType(ITypeSymbol type)
    {
        // Handle arrays
        if (type is IArrayTypeSymbol arrayType)
            return arrayType.ElementType;

        // Handle generic collections
        if (type is INamedTypeSymbol { IsGenericType: true } genericType)
        {
            return genericType.TypeArguments.FirstOrDefault();
        }

        return null;
    }

    private static IEnumerable<IPropertySymbol> GetAllProperties(INamedTypeSymbol namedType)
    {
        var properties = new List<IPropertySymbol>();
        var currentType = namedType;

        while (currentType != null)
        {
            properties.AddRange(currentType.GetMembers().OfType<IPropertySymbol>());
            currentType = currentType.BaseType;
        }

        return properties;
    }

    private static bool IsCompilerGeneratedProperty(IPropertySymbol property)
    {
        // Check for EqualityContract property generated by records
        if (property.Name == "EqualityContract" && property.Type.Name == "Type" &&
            property.Type.ContainingNamespace?.ToDisplayString() == "System")
        {
            return true;
        }

        // Can add other compiler-generated properties here if needed
        return false;
    }

    private static bool IsFrameworkType(ITypeSymbol type)
    {
        var fullName = type.ToDisplayString();

        // Skip common framework types that might cause false positives
        if (fullName.StartsWith("System."))
        {
            return true;
        }

        return false;
    }
}