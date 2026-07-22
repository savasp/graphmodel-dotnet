// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Analyzers;

using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;


/// <summary>
/// Analyzer for enforcing Cvoya.Graph implementation rules.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class GraphAnalyzer : DiagnosticAnalyzer
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
        DiagnosticDescriptors.CircularReferenceWithoutNullable,
        DiagnosticDescriptors.ShouldInheritFromBaseClass,
        DiagnosticDescriptors.MisappliedNodeOrRelationshipAttribute,
        DiagnosticDescriptors.ConflictingNodeAndRelationshipAttributes,
        DiagnosticDescriptors.EntityTypeMustBeReferenceType,
        DiagnosticDescriptors.IneffectiveComplexPropertyAttribute,
        DiagnosticDescriptors.OpenGenericEntityUnsupported,
        DiagnosticDescriptors.InvalidPropertySchemaDeclaration);

    /// <summary>
    /// Initializes the analyzer and registers the symbol action for named types.
    /// This method is called by the Roslyn framework when the analyzer is loaded.
    /// </summary>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(startContext =>
        {
            var state = new AnalyzerCompilationState(startContext.Compilation);
            startContext.RegisterSymbolAction(
                symbolContext => AnalyzeNamedType(symbolContext, state),
                SymbolKind.NamedType);
        });
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context, AnalyzerCompilationState state)
    {
        var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;

        // Skip if it's not a class or struct
        if (namedTypeSymbol.TypeKind != TypeKind.Class && namedTypeSymbol.TypeKind != TypeKind.Struct)
            return;

        var helper = new AnalyzerHelper(context.Compilation, state.GraphAttributes);

        bool implementsINode = AnalyzerHelper.ImplementsINode(namedTypeSymbol);
        bool implementsIRelationship = AnalyzerHelper.ImplementsIRelationship(namedTypeSymbol);

        // CG012: Check for [Node]/[Relationship] applied to a type that doesn't implement the
        // matching interface. Must run before the early-return below, since its whole point is to
        // flag types that implement neither (or the wrong) interface.
        AnalyzeMisappliedNodeOrRelationshipAttribute(
            context,
            namedTypeSymbol,
            implementsINode,
            implementsIRelationship,
            state.GraphAttributes);

        // CG013: Check for both [Node] and [Relationship] applied to the same type. Same reasoning
        // as CG012 - must run regardless of which interfaces (if any) the type implements.
        AnalyzeConflictingNodeAndRelationshipAttributes(context, namedTypeSymbol, state.GraphAttributes);

        // CG014: Check that entity types (implementing INode/IRelationship, directly or through a
        // derived interface) are reference types, not structs. Must run before the early-return
        // below - a struct never "implements INode" in the sense that matters for the rest of this
        // method (it can't be a valid entity type regardless), but it is exactly the case CG014
        // exists to flag.
        AnalyzeEntityTypeIsReferenceType(context, namedTypeSymbol, helper);

        // CG015: Check for ComplexPropertyAttribute configurations that are silent no-ops. Must run
        // before the early-return below: the attribute is equally consumed - and equally inert when
        // misconfigured - on complex property types that are not themselves graph entities.
        AnalyzeComplexPropertyAttributes(context, namedTypeSymbol, helper, state.GraphAttributes);

        // Skip if it doesn't implement INode or IRelationship
        if (!implementsINode && !implementsIRelationship)
            return;

        // CG016: Reject open generic entity roots. A concrete (non-abstract) entity with unbound type
        // parameters - or one nested in an open generic - makes the source generator emit a
        // non-generic serializer that references those unbound parameters, i.e. invalid C#. Report
        // once at the declaration and skip the remaining entity rules: the type is unsupported as
        // declared, and its other diagnostics belong on the non-generic concrete subtype the fix
        // introduces. Abstract generic bases are intentionally exempt - they remain supported when
        // inherited only by a closed, non-generic concrete entity.
        if (!namedTypeSymbol.IsAbstract && IsOpenGenericEntity(namedTypeSymbol))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.OpenGenericEntityUnsupported,
                namedTypeSymbol.Locations.FirstOrDefault(),
                namedTypeSymbol.Name));
            return;
        }

        // CG011: Check if type directly implements INode/IRelationship without inheriting from base class
        AnalyzeBaseClassInheritance(context, namedTypeSymbol, implementsINode, implementsIRelationship);

        // CG001: Check parameterless constructor
        AnalyzeParameterlessConstructor(context, namedTypeSymbol, implementsINode, implementsIRelationship);

        // CG002: Check property accessors
        AnalyzePropertyAccessors(context, namedTypeSymbol, helper);

        // CG003: Check for INode/IRelationship properties
        AnalyzeGraphInterfaceProperties(context, namedTypeSymbol, helper);

        // CG006: Check complex types for graph interface types (run before CG004/CG005)
        AnalyzeComplexTypeProperties(context, namedTypeSymbol, helper);

        // CG004: Check property types for INode
        if (implementsINode)
        {
            AnalyzeNodePropertyTypes(context, namedTypeSymbol, helper);
        }

        // CG005: Check property types for IRelationship
        if (implementsIRelationship)
        {
            AnalyzeRelationshipPropertyTypes(context, namedTypeSymbol, helper);
        }

        // CG007: Check duplicate PropertyAttribute labels
        AnalyzeDuplicatePropertyAttributeLabels(context, namedTypeSymbol, state.GraphAttributes);

        // CG008: Check duplicate RelationshipAttribute labels
        if (implementsIRelationship)
        {
            AnalyzeDuplicateRelationshipAttributeLabels(context, namedTypeSymbol, state.GraphAttributes);
        }

        // CG009: Check duplicate NodeAttribute labels
        if (implementsINode)
        {
            AnalyzeDuplicateNodeAttributeLabels(context, namedTypeSymbol, state.GraphAttributes);
        }

        // CG010: Check circular references
        AnalyzeCircularReferences(context, namedTypeSymbol, helper);

        // CG018: Check opt-in key shapes and contradictory ignored-property flags
        AnalyzePropertySchemaDeclarations(
            context,
            namedTypeSymbol,
            implementsINode,
            implementsIRelationship,
            helper,
            state);
    }

    /// <summary>
    /// CG018: Reports invalid opt-in domain keys and schema behavior requested for ignored
    /// properties. A key-specific report is intentionally limited to property types that the
    /// entity could otherwise serialize; CG003-CG006 remain the single diagnostic when the
    /// property's shape is invalid independently of <c>IsKey</c>.
    /// </summary>
    private static void AnalyzePropertySchemaDeclarations(
        SymbolAnalysisContext context,
        INamedTypeSymbol namedType,
        bool implementsINode,
        bool implementsIRelationship,
        AnalyzerHelper helper,
        AnalyzerCompilationState state)
    {
        foreach (var property in GetSchemaProperties(namedType))
        {
            var attribute = state.GraphAttributes.FindPropertyAttribute(property);
            if (attribute is null)
                continue;

            var isKey = HasTrueNamedArgument(attribute, "IsKey");
            var isIgnored = HasTrueNamedArgument(attribute, "Ignore");

            string? reason = null;
            if (isIgnored)
            {
                var conflictingFlags = new List<string>();
                AddFlagIfTrue(attribute, "IsKey", conflictingFlags);
                AddFlagIfTrue(attribute, "IsUnique", conflictingFlags);
                AddFlagIfTrue(attribute, "IsIndexed", conflictingFlags);
                AddFlagIfTrue(attribute, "IsRequired", conflictingFlags);

                if (conflictingFlags.Count > 0)
                {
                    var configuredFlags = conflictingFlags.Select(flag => $"{flag} = true");
                    reason = $"Ignore = true cannot be combined with {string.Join(", ", configuredFlags)}";
                }
            }

            if (reason is null && isKey)
            {
                // If the property is unsupported even without IsKey, the existing property-type
                // rule is the more fundamental and sufficient diagnostic. Do not report both.
                var isOtherwiseValid = (!implementsINode || helper.IsValidNodePropertyType(property.Type)) &&
                    (!implementsIRelationship || helper.IsValidRelationshipPropertyType(property.Type));
                if (!isOtherwiseValid)
                    continue;

                if (AnalyzerHelper.IsNullableType(property.Type) ||
                    (property.Type.IsReferenceType && property.Type.NullableAnnotation == NullableAnnotation.Annotated))
                {
                    reason = "IsKey = true requires a non-nullable property";
                }
                else if (!AnalyzerHelper.IsSimpleType(property.Type))
                {
                    reason = $"IsKey = true requires a graph-storable scalar; '{GetShortTypeName(property.Type)}' is not a scalar";
                }
            }

            if (reason is null)
                continue;

            var location = property.Locations.FirstOrDefault(candidate => candidate.IsInSource);
            if (location is null || !state.TryReport("CG018", location))
                continue;

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.InvalidPropertySchemaDeclaration,
                location,
                property.Name,
                property.ContainingType.Name,
                reason));
        }
    }

    private static bool HasTrueNamedArgument(AttributeData attribute, string name)
    {
        return attribute.NamedArguments.Any(argument =>
            argument.Key == name && argument.Value.Value is true);
    }

    private static void AddFlagIfTrue(AttributeData attribute, string name, List<string> flags)
    {
        if (HasTrueNamedArgument(attribute, name))
        {
            flags.Add(name);
        }
    }

    /// <summary>
    /// Mirrors reflection's public-instance schema property set while keeping ignored properties,
    /// which CG018 must inspect for contradictory schema flags.
    /// </summary>
    private static IEnumerable<IPropertySymbol> GetSchemaProperties(INamedTypeSymbol type)
    {
        var seenProperties = new HashSet<string>(StringComparer.Ordinal);
        for (var current = type; current is not null; current = current.BaseType)
        {
            foreach (var property in current.GetMembers().OfType<IPropertySymbol>())
            {
                if (property.IsStatic ||
                    property.DeclaredAccessibility != Accessibility.Public ||
                    !seenProperties.Add(property.Name))
                {
                    continue;
                }

                yield return property;
            }
        }
    }

    /// <summary>
    /// True when <paramref name="type"/> - or any of its containing types - still has an unbound
    /// type parameter: an open generic entity declaration (<c>GenericNode&lt;T&gt;</c>) or a type
    /// nested in one. A closed construction such as <c>GenericNode&lt;string&gt;</c> has no free
    /// type parameters and returns false, so a non-generic entity derived from it is not flagged.
    /// </summary>
    private static bool IsOpenGenericEntity(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.ContainingType)
        {
            if (current.TypeArguments.Any(argument => argument.TypeKind == TypeKind.TypeParameter))
            {
                return true;
            }
        }

        return false;
    }

    private static void AnalyzeBaseClassInheritance(SymbolAnalysisContext context, INamedTypeSymbol namedType, bool implementsINode, bool implementsIRelationship)
    {
        // Skip abstract types and interfaces
        if (namedType.IsAbstract || namedType.TypeKind == TypeKind.Interface)
            return;

        // Check if the type directly implements INode/IRelationship without proper base class
        bool inheritsFromNode = false;
        bool inheritsFromRelationship = false;

        var baseType = namedType.BaseType;
        while (baseType != null)
        {
            if (baseType.Name == "Node" && baseType.ContainingNamespace.ToDisplayString() == "Cvoya.Graph")
            {
                inheritsFromNode = true;
                break;
            }
            if (baseType.Name == "Relationship" && baseType.ContainingNamespace.ToDisplayString() == "Cvoya.Graph")
            {
                inheritsFromRelationship = true;
                break;
            }
            baseType = baseType.BaseType;
        }

        if (implementsINode && !inheritsFromNode)
        {
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.ShouldInheritFromBaseClass,
                namedType.Locations.FirstOrDefault(),
                namedType.Name,
                "Node",
                "INode");

            context.ReportDiagnostic(diagnostic);
        }

        if (implementsIRelationship && !inheritsFromRelationship)
        {
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.ShouldInheritFromBaseClass,
                namedType.Locations.FirstOrDefault(),
                namedType.Name,
                "Relationship",
                "IRelationship");

            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void AnalyzeMisappliedNodeOrRelationshipAttribute(
        SymbolAnalysisContext context,
        INamedTypeSymbol namedType,
        bool implementsINode,
        bool implementsIRelationship,
        GraphAttributeSymbols graphAttributes)
    {
        var nodeAttr = graphAttributes.FindNodeAttribute(namedType);
        if (nodeAttr != null && !implementsINode)
        {
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.MisappliedNodeOrRelationshipAttribute,
                GetAttributeLocation(nodeAttr, namedType),
                namedType.Name,
                "Node",
                "INode");

            context.ReportDiagnostic(diagnostic);
        }

        var relationshipAttr = graphAttributes.FindRelationshipAttribute(namedType);
        if (relationshipAttr != null && !implementsIRelationship)
        {
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.MisappliedNodeOrRelationshipAttribute,
                GetAttributeLocation(relationshipAttr, namedType),
                namedType.Name,
                "Relationship",
                "IRelationship");

            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void AnalyzeConflictingNodeAndRelationshipAttributes(
        SymbolAnalysisContext context,
        INamedTypeSymbol namedType,
        GraphAttributeSymbols graphAttributes)
    {
        var nodeAttr = graphAttributes.FindNodeAttribute(namedType);
        var relationshipAttr = graphAttributes.FindRelationshipAttribute(namedType);

        if (nodeAttr != null && relationshipAttr != null)
        {
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.ConflictingNodeAndRelationshipAttributes,
                namedType.Locations.FirstOrDefault(),
                namedType.Name);

            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void AnalyzeEntityTypeIsReferenceType(SymbolAnalysisContext context, INamedTypeSymbol namedType, AnalyzerHelper helper)
    {
        // Only structs (including record structs and readonly structs) are at risk here - classes
        // and record classes are already reference types.
        if (namedType.TypeKind != TypeKind.Struct)
            return;

        // Pick the first interface found (INode before IRelationship) purely for a stable, single
        // diagnostic - a struct implementing both is vanishingly unlikely, and CG013 separately
        // flags a type carrying both [Node] and [Relationship] attributes.
        string? interfaceName = AnalyzerHelper.ImplementsINode(namedType)
            ? "INode"
            : AnalyzerHelper.ImplementsIRelationship(namedType)
                ? "IRelationship"
                : null;

        if (interfaceName is null)
            return;

        var kindDescription = namedType.IsRecord ? "Record struct" : "Struct";

        var diagnostic = Diagnostic.Create(
            DiagnosticDescriptors.EntityTypeMustBeReferenceType,
            namedType.Locations.FirstOrDefault(),
            kindDescription,
            namedType.Name,
            interfaceName);

        context.ReportDiagnostic(diagnostic);
    }

    private static Location? GetAttributeLocation(AttributeData attribute, ISymbol fallbackSymbol)
    {
        var syntaxReference = attribute.ApplicationSyntaxReference;
        if (syntaxReference != null)
        {
            return Location.Create(syntaxReference.SyntaxTree, syntaxReference.Span);
        }

        return fallbackSymbol.Locations.FirstOrDefault();
    }

    private static void AnalyzeComplexPropertyAttributes(
        SymbolAnalysisContext context,
        INamedTypeSymbol namedType,
        AnalyzerHelper helper,
        GraphAttributeSymbols graphAttributes)
    {
        foreach (var property in namedType.GetMembers().OfType<IPropertySymbol>())
        {
            var attribute = graphAttributes.FindComplexPropertyAttribute(property);
            if (attribute is null)
            {
                continue;
            }

            string? reason = null;
            if (AnalyzerHelper.IsSimpleType(property.Type) || AnalyzerHelper.IsCollectionOfSimpleTypes(property.Type))
            {
                reason = $"property type '{GetShortTypeName(property.Type)}' is simple or a simple collection";
            }
            else if (TryGetConfiguredComplexPropertyRelationshipType(attribute, out var configured) &&
                string.IsNullOrWhiteSpace(configured))
            {
                reason = "RelationshipType is null, empty, or whitespace, so convention-based naming is used";
            }

            if (reason is not null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.IneffectiveComplexPropertyAttribute,
                    GetAttributeLocation(attribute, property),
                    property.Name,
                    reason));
            }
        }
    }

    private static bool TryGetConfiguredComplexPropertyRelationshipType(AttributeData attribute, out string? value)
    {
        foreach (var argument in attribute.NamedArguments.Where(argument => argument.Key == "RelationshipType"))
        {
            value = argument.Value.Value as string;
            return true;
        }

        value = null;
        return false;
    }

    private static void AnalyzeParameterlessConstructor(SymbolAnalysisContext context, INamedTypeSymbol namedType, bool implementsINode, bool implementsIRelationship)
    {
        // Records are always valid - they have compiler-generated constructors and proper initialization
        if (namedType.IsRecord)
            return;

        // Skip types that inherit from Node or Relationship base classes - they handle constructors properly
        bool inheritsFromNode = false;
        bool inheritsFromRelationship = false;

        var baseType = namedType.BaseType;
        while (baseType != null)
        {
            if (baseType.Name == "Node" && baseType.ContainingNamespace.ToDisplayString() == "Cvoya.Graph")
            {
                inheritsFromNode = true;
                break;
            }
            if (baseType.Name == "Relationship" && baseType.ContainingNamespace.ToDisplayString() == "Cvoya.Graph")
            {
                inheritsFromRelationship = true;
                break;
            }
            baseType = baseType.BaseType;
        }

        // Skip if inheriting from base classes
        if (inheritsFromNode || inheritsFromRelationship)
            return;

        var hasParameterlessConstructor = namedType.Constructors.Any(c =>
            c.Parameters.Length == 0 && (c.DeclaredAccessibility == Accessibility.Public || c.DeclaredAccessibility == Accessibility.Internal));

        if (!hasParameterlessConstructor)
        {
            // Check if there are constructors that initialize all properties
            var allProperties = GetAllProperties(namedType);
            // Include both set and init accessors - both can be initialized via constructors
            var settableProperties = allProperties.Where(p =>
                p.SetMethod != null &&
                p.SetMethod.DeclaredAccessibility == Accessibility.Public
            ).ToList();

            bool hasValidConstructor = namedType.Constructors.Any(constructor =>
            {
                // Constructor should have a parameter for each settable property
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

    private static void AnalyzePropertyAccessors(
        SymbolAnalysisContext context,
        INamedTypeSymbol namedType,
        AnalyzerHelper helper)
    {
        var properties = helper.GetSerializedProperties(namedType);

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
        var properties = helper.GetSerializedProperties(namedType);

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
                var elementType = AnalyzerHelper.GetCollectionElementType(property.Type);
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

        // Handle nullable value types before the general generic branch so Nullable<Address>
        // renders as the C# declaration consumers need to write: Address?.
        if (type is INamedTypeSymbol nullable &&
            nullable.OriginalDefinition?.SpecialType == SpecialType.System_Nullable_T)
        {
            return $"{GetShortTypeName(nullable.TypeArguments[0])}?";
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
            return $"{GetShortTypeName(arrayType.ElementType)}[{new string(',', arrayType.Rank - 1)}]";
        }

        // Preserve the namespace for the unsupported spatial lookalike so CG004/CG005 do not say
        // that the supported Cvoya.Graph.Point is invalid. Generic/nullable wrappers recurse here,
        // producing equally unambiguous names for those shapes (#387).
        if (type is INamedTypeSymbol { Name: "Point" } pointType &&
            pointType.ContainingNamespace?.ToDisplayString() == "System.Drawing")
        {
            return "System.Drawing.Point";
        }

        // For simple types, just return the name
        return type.Name;
    }

    private static void AnalyzeNodePropertyTypes(SymbolAnalysisContext context, INamedTypeSymbol namedType, AnalyzerHelper helper)
    {
        var properties = helper.GetSerializedProperties(namedType);

        foreach (var property in properties)
        {
            // Skip compiler-generated properties (like EqualityContract in records)
            if (IsCompilerGeneratedProperty(property))
                continue;
            // First, check if CG003 would handle this property (graph interface types)
            if (helper.IsGraphInterfaceType(property.Type))
            {
                // CG003 will handle this, so skip it here
                continue;
            }

            // Also check if CG003 would handle collections of graph interface types
            // This includes arrays and any generic collection containing graph interfaces
            var elementType = AnalyzerHelper.GetCollectionElementType(property.Type);
            if (elementType != null && helper.IsGraphInterfaceType(elementType))
            {
                // CG003 will handle this collection of graph interfaces, so skip it here
                continue;
            }

            // First, check if it's a complex type that contains graph interfaces
            // If so, skip this property and let CG006 handle it
            if (helper.IsComplexType(property.Type))
            {
                var result = helper.ValidateComplexType(property.Type);
                if (!result.IsValid && result.ContainsGraphInterface)
                {
                    // This complex type contains graph interfaces, so CG006 will handle it
                    continue;
                }
            }

            // Also check if it's a collection of complex types that contain graph interfaces
            if (helper.IsCollectionOfComplexTypes(property.Type))
            {
                var complexElementType = AnalyzerHelper.GetCollectionElementType(property.Type);
                if (complexElementType != null && helper.IsComplexType(complexElementType))
                {
                    var result = helper.ValidateComplexType(complexElementType);
                    if (!result.IsValid && result.ContainsGraphInterface)
                    {
                        // This collection contains complex types with graph interfaces, so CG006 will handle it
                        continue;
                    }
                }
            }

            // Also check collections where element type contains graph interfaces (even if not "complex")
            if (helper.IsCollectionType(property.Type))
            {
                var collectionElementType = AnalyzerHelper.GetCollectionElementType(property.Type);
                if (collectionElementType != null && !AnalyzerHelper.IsSimpleType(collectionElementType))
                {
                    var result = helper.ValidateComplexType(collectionElementType);
                    if (!result.IsValid && result.ContainsGraphInterface)
                    {
                        // This collection contains types with graph interfaces, so CG006 will handle it
                        continue;
                    }
                }
            }

            // Explicit check for arrays to ensure they are handled by CG006 if they contain graph interfaces
            if (property.Type is IArrayTypeSymbol arrayType)
            {
                var result = helper.ValidateComplexType(arrayType.ElementType);
                if (!result.IsValid && result.ContainsGraphInterface)
                {
                    // This array contains types with graph interfaces, so CG006 will handle it
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
        var properties = helper.GetSerializedProperties(namedType);

        foreach (var property in properties)
        {
            // Skip compiler-generated properties (like EqualityContract in records)
            if (IsCompilerGeneratedProperty(property))
                continue;
            // First, check if CG003 would handle this property (graph interface types)
            if (helper.IsGraphInterfaceType(property.Type))
            {
                // CG003 will handle this, so skip it here
                continue;
            }

            // Also check if CG003 would handle collections of graph interface types
            // This includes arrays and any generic collection containing graph interfaces
            var elementType = AnalyzerHelper.GetCollectionElementType(property.Type);
            if (elementType != null && helper.IsGraphInterfaceType(elementType))
            {
                // CG003 will handle this collection of graph interfaces, so skip it here
                continue;
            }

            // First, check if it's a complex type that contains graph interfaces
            // If so, skip this property and let CG006 handle it
            if (helper.IsComplexType(property.Type))
            {
                var result = helper.ValidateComplexType(property.Type);
                if (!result.IsValid && result.ContainsGraphInterface)
                {
                    // This complex type contains graph interfaces, so CG006 will handle it
                    continue;
                }
            }

            // Also check if it's a collection of complex types that contain graph interfaces
            if (helper.IsCollectionOfComplexTypes(property.Type))
            {
                var complexElementType = AnalyzerHelper.GetCollectionElementType(property.Type);
                if (complexElementType != null && helper.IsComplexType(complexElementType))
                {
                    var result = helper.ValidateComplexType(complexElementType);
                    if (!result.IsValid && result.ContainsGraphInterface)
                    {
                        // This collection contains complex types with graph interfaces, so CG006 will handle it
                        continue;
                    }
                }
            }

            // Also check collections where element type contains graph interfaces (even if not "complex")
            if (helper.IsCollectionType(property.Type))
            {
                var collectionElementType = AnalyzerHelper.GetCollectionElementType(property.Type);
                if (collectionElementType != null && !AnalyzerHelper.IsSimpleType(collectionElementType))
                {
                    var result = helper.ValidateComplexType(collectionElementType);
                    if (!result.IsValid && result.ContainsGraphInterface)
                    {
                        // This collection contains types with graph interfaces, so CG006 will handle it
                        continue;
                    }
                }
            }

            // Explicit check for arrays to ensure they are handled by CG006 if they contain graph interfaces
            if (property.Type is IArrayTypeSymbol arrayType)
            {
                var result = helper.ValidateComplexType(arrayType.ElementType);
                if (!result.IsValid && result.ContainsGraphInterface)
                {
                    // This array contains types with graph interfaces, so CG006 will handle it
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
        var properties = helper.GetSerializedProperties(namedType);

        foreach (var property in properties)
        {
            // Skip compiler-generated properties (like EqualityContract in records)
            if (IsCompilerGeneratedProperty(property))
                continue;
            // First, check if CG003 would handle this property (graph interface types)
            if (helper.IsGraphInterfaceType(property.Type))
            {
                // CG003 will handle this, so skip it here
                continue;
            }

            // Also check if CG003 would handle collections of graph interface types
            var elementType = AnalyzerHelper.GetCollectionElementType(property.Type);
            if (elementType != null && helper.IsGraphInterfaceType(elementType))
            {
                // CG003 will handle this collection of graph interfaces, so skip it here
                continue;
            }

            // Check direct complex types
            if (helper.IsComplexType(property.Type))
            {
                var result = helper.ValidateComplexType(property.Type);
                if (!result.IsValid && result.ContainsGraphInterface)
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
                var collectionElementType = AnalyzerHelper.GetCollectionElementType(property.Type);
                if (collectionElementType != null && !AnalyzerHelper.IsSimpleType(collectionElementType))
                {
                    // Check if this element type contains graph interfaces
                    var result = helper.ValidateComplexType(collectionElementType);
                    if (!result.IsValid && result.ContainsGraphInterface)
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
                if (!AnalyzerHelper.IsSimpleType(arrayType.ElementType))
                {
                    var result = helper.ValidateComplexType(arrayType.ElementType);
                    if (!result.IsValid && result.ContainsGraphInterface)
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

    private static void AnalyzeDuplicatePropertyAttributeLabels(
        SymbolAnalysisContext context,
        INamedTypeSymbol namedType,
        GraphAttributeSymbols graphAttributes)
    {
        // This dictionary will track property labels across the entire inheritance hierarchy
        var propertyLabels = new Dictionary<string, (IPropertySymbol Property, INamedTypeSymbol ContainingType)>(StringComparer.Ordinal);

        // First, collect all properties from base types (to check against)
        var baseType = namedType.BaseType;
        while (baseType != null)
        {
            // Get only properties directly declared in this base type
            var baseProperties = baseType.GetMembers().OfType<IPropertySymbol>();

            foreach (var property in baseProperties)
            {
                var propertyAttr = graphAttributes.FindPropertyAttribute(property);

                if (propertyAttr != null)
                {
                    var label = ExtractPropertyLabel(property, propertyAttr);

                    // Only proceed if we have a valid label
                    if (!string.IsNullOrEmpty(label))
                    {
                        propertyLabels[label!] = (property, baseType);
                    }
                }
            }

            baseType = baseType.BaseType;
        }

        // Now check properties directly declared in the current type being analyzed
        var currentTypeProperties = namedType.GetMembers().OfType<IPropertySymbol>();

        foreach (var property in currentTypeProperties)
        {
            var propertyAttr = graphAttributes.FindPropertyAttribute(property);

            if (propertyAttr != null)
            {
                var label = ExtractPropertyLabel(property, propertyAttr);

                // Only proceed if we have a valid label
                if (!string.IsNullOrEmpty(label))
                {
                    if (propertyLabels.TryGetValue(label!, out var existing))
                    {
                        // Always report diagnostic on the conflicting property (better UX)
                        // The message will indicate which property it conflicts with
                        var diagnostic = Diagnostic.Create(
                            DiagnosticDescriptors.DuplicatePropertyAttributeLabel,
                            property.Locations.FirstOrDefault(),
                            property.Name,
                            namedType.Name,
                            label,
                            existing.Property.Name,
                            existing.ContainingType.Name);

                        context.ReportDiagnostic(diagnostic);
                    }
                    else
                    {
                        propertyLabels[label!] = (property, namedType);
                    }
                }
            }
        }
    }

    private static string ExtractPropertyLabel(IPropertySymbol property, AttributeData propertyAttr)
    {
        return GetAttributeLabel(propertyAttr, property.Name);
    }

    private static string GetAttributeLabel(AttributeData attribute, string fallback)
    {
        foreach (var argument in attribute.NamedArguments)
        {
            if (argument.Key == "Label")
            {
                return argument.Value.Value is string { Length: > 0 } namedLabel
                    ? namedLabel
                    : fallback;
            }
        }

        foreach (var argument in attribute.ConstructorArguments)
        {
            if (argument.Value is string { Length: > 0 } constructorLabel)
                return constructorLabel;
        }

        return fallback;
    }

    private static void AnalyzeDuplicateRelationshipAttributeLabels(
        SymbolAnalysisContext context,
        INamedTypeSymbol namedType,
        GraphAttributeSymbols graphAttributes)
    {
        // Collect all types in the compilation that implement IRelationship
        var allRelationshipTypes = context.Compilation.GetSymbolsWithName(_ => true, SymbolFilter.Type)
            .OfType<INamedTypeSymbol>()
            .Where(t => AnalyzerHelper.ImplementsIRelationship(t))
            .ToList();

        // Extract labels from all relationship types
        var labelToTypeMap = new Dictionary<string, INamedTypeSymbol>(StringComparer.OrdinalIgnoreCase);

        foreach (var type in allRelationshipTypes)
        {
            var relationshipAttr = graphAttributes.FindRelationshipAttribute(type);

            if (relationshipAttr != null)
            {
                var label = GetAttributeLabel(relationshipAttr, type.Name);

                if (labelToTypeMap.TryGetValue(label, out var existing))
                {
                    // Only report the duplicate owned by the symbol action currently running.
                    if (type.Equals(namedType, SymbolEqualityComparer.Default))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            DiagnosticDescriptors.DuplicateRelationshipAttributeLabel,
                            type.Locations.FirstOrDefault(),
                            type.Name,
                            label,
                            existing.Name));
                    }
                }
                else
                {
                    labelToTypeMap[label] = type;
                }
            }
        }
    }

    private static void AnalyzeDuplicateNodeAttributeLabels(
        SymbolAnalysisContext context,
        INamedTypeSymbol namedType,
        GraphAttributeSymbols graphAttributes)
    {
        // Collect all types in the compilation that implement INode
        var allNodeTypes = context.Compilation.GetSymbolsWithName(_ => true, SymbolFilter.Type)
            .OfType<INamedTypeSymbol>()
            .Where(t => AnalyzerHelper.ImplementsINode(t))
            .ToList();

        // Extract labels from all node types
        var labelToTypeMap = new Dictionary<string, INamedTypeSymbol>(StringComparer.OrdinalIgnoreCase);

        foreach (var type in allNodeTypes)
        {
            var nodeAttr = graphAttributes.FindNodeAttribute(type);

            if (nodeAttr != null)
            {
                var label = GetAttributeLabel(nodeAttr, type.Name);

                if (labelToTypeMap.TryGetValue(label, out var existing))
                {
                    // Only report the duplicate owned by the symbol action currently running.
                    if (type.Equals(namedType, SymbolEqualityComparer.Default))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            DiagnosticDescriptors.DuplicateNodeAttributeLabel,
                            type.Locations.FirstOrDefault(),
                            type.Name,
                            label,
                            existing.Name));
                    }
                }
                else
                {
                    labelToTypeMap[label] = type;
                }
            }
        }
    }

    private static void AnalyzeCircularReferences(SymbolAnalysisContext context, INamedTypeSymbol namedType, AnalyzerHelper helper)
    {
        // Only analyze graph types (nodes/relationships) for circular references
        if (!AnalyzerHelper.ImplementsINode(namedType) && !AnalyzerHelper.ImplementsIRelationship(namedType))
            return;

        var properties = GetAllProperties(namedType);
        foreach (var property in properties)
        {
            // Skip compiler-generated properties (like EqualityContract in records)
            if (IsCompilerGeneratedProperty(property))
                continue;
            // Only serialized properties can create serialization cycles; static, non-public,
            // getterless, and ignored properties never round-trip.
            if (!helper.IsSerializedProperty(property))
                continue;
            // Skip properties that would be handled by CG004/CG005 (invalid property types)
            bool isNode = AnalyzerHelper.ImplementsINode(namedType);
            bool isRelationship = AnalyzerHelper.ImplementsIRelationship(namedType);

            if (isNode && !helper.IsValidNodePropertyType(property.Type))
            {
                // CG004 will handle this invalid node property type
                continue;
            }

            if (isRelationship && !helper.IsValidRelationshipPropertyType(property.Type))
            {
                // CG005 will handle this invalid relationship property type
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
                // Also check if the collection itself would be handled by CG004/CG005
                if (isNode && !helper.IsValidNodePropertyType(property.Type))
                {
                    // CG004 will handle this invalid node property type
                    continue;
                }

                if (isRelationship && !helper.IsValidRelationshipPropertyType(property.Type))
                {
                    // CG005 will handle this invalid relationship property type
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
                // Only serialized properties can create serialization cycles; a static self-typed
                // member (IntPtr.MaxValue, a Default/Empty singleton) is not a stored cycle.
                if (!helper.IsSerializedProperty(property))
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

    private static List<IPropertySymbol> GetAllProperties(INamedTypeSymbol namedType)
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

    /// <summary>
    /// Compilation-scoped analyzer state. Derived-type discovery is built once, and concurrent
    /// symbol actions share both the visited-type set and per-rule reported source locations so a
    /// reachable declaration is analyzed and reported at most once for each diagnostic.
    /// </summary>
    private sealed class AnalyzerCompilationState
    {
        private readonly Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>> directDerivedTypes =
            new(SymbolEqualityComparer.Default);
        private readonly ConcurrentDictionary<ITypeSymbol, byte> analyzedTypes =
            new(SymbolEqualityComparer.Default);
        private readonly ConcurrentDictionary<(string Id, SyntaxTree Tree, int Start, int Length), byte> reportedLocations = new();

        public AnalyzerCompilationState(Compilation compilation)
        {
            GraphAttributes = GraphAttributeSymbols.Resolve(compilation);

            foreach (var type in GetAllNamedTypes(compilation.Assembly.GlobalNamespace))
            {
                if (type.BaseType is not { } baseType)
                    continue;

                if (!directDerivedTypes.TryGetValue(baseType, out var derivedTypes))
                {
                    derivedTypes = [];
                    directDerivedTypes.Add(baseType, derivedTypes);
                }

                derivedTypes.Add(type);
            }
        }

        public GraphAttributeSymbols GraphAttributes { get; }

        public bool TryAnalyze(ITypeSymbol type) => analyzedTypes.TryAdd(type, 0);

        public IEnumerable<INamedTypeSymbol> GetDirectDerivedTypes(INamedTypeSymbol type) =>
            directDerivedTypes.TryGetValue(type, out var derivedTypes) ? derivedTypes : [];

        public bool TryReport(string diagnosticId, Location location)
        {
            var tree = location.SourceTree;
            return tree is not null &&
                   reportedLocations.TryAdd((diagnosticId, tree, location.SourceSpan.Start, location.SourceSpan.Length), 0);
        }

        private static IEnumerable<INamedTypeSymbol> GetAllNamedTypes(INamespaceSymbol namespaceSymbol)
        {
            foreach (var namespaceMember in namespaceSymbol.GetNamespaceMembers())
            {
                foreach (var type in GetAllNamedTypes(namespaceMember))
                    yield return type;
            }

            foreach (var typeMember in namespaceSymbol.GetTypeMembers())
            {
                foreach (var type in GetTypeAndNestedTypes(typeMember))
                    yield return type;
            }
        }

        private static IEnumerable<INamedTypeSymbol> GetTypeAndNestedTypes(INamedTypeSymbol type)
        {
            yield return type;
            foreach (var nestedType in type.GetTypeMembers())
            {
                foreach (var candidate in GetTypeAndNestedTypes(nestedType))
                    yield return candidate;
            }
        }
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
