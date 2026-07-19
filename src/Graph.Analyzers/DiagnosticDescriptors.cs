// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Analyzers;

using Cvoya.Graph.Analyzers.Properties;
using Microsoft.CodeAnalysis;


/// <summary>
/// Diagnostic descriptors for Cvoya.Graph analyzer rules.
/// </summary>
internal static class DiagnosticDescriptors
{
    // CG001: Missing parameterless constructor or constructor that initializes properties
    public static readonly DiagnosticDescriptor MissingParameterlessConstructor = new(
        id: "CG001",
        title: Resources.CG001_Title,
        messageFormat: Resources.CG001_MessageFormat,
        category: "Cvoya.Graph",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: Resources.CG001_Description);

    // CG002: Property must have public getters and setters or initializers
    public static readonly DiagnosticDescriptor PropertyMustHavePublicAccessors = new(
        id: "CG002",
        title: Resources.CG002_Title,
        messageFormat: Resources.CG002_MessageFormat,
        category: "Cvoya.Graph",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: Resources.CG002_Description);

    // CG003: Property cannot be INode or IRelationship type
    public static readonly DiagnosticDescriptor PropertyCannotBeGraphInterfaceType = new(
        id: "CG003",
        title: Resources.CG003_Title,
        messageFormat: Resources.CG003_MessageFormat,
        category: "Cvoya.Graph",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: Resources.CG003_Description);

    // CG004: Invalid property type for INode implementation
    public static readonly DiagnosticDescriptor InvalidPropertyTypeForNode = new(
        id: "CG004",
        title: Resources.CG004_Title,
        messageFormat: Resources.CG004_MessageFormat,
        category: "Cvoya.Graph",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: Resources.CG004_Description);

    // CG005: Invalid property type for IRelationship implementation
    public static readonly DiagnosticDescriptor InvalidPropertyTypeForRelationship = new(
        id: "CG005",
        title: Resources.CG005_Title,
        messageFormat: Resources.CG005_MessageFormat,
        category: "Cvoya.Graph",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: Resources.CG005_Description);

    // CG006: Complex type property contains graph interface types
    public static readonly DiagnosticDescriptor ComplexTypeContainsGraphInterfaceTypes = new(
        id: "CG006",
        title: Resources.CG006_Title,
        messageFormat: Resources.CG006_MessageFormat,
        category: "Cvoya.Graph",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: Resources.CG006_Description);

    // CG007: Duplicate PropertyAttribute label in type hierarchy
    public static readonly DiagnosticDescriptor DuplicatePropertyAttributeLabel = new(
        id: "CG007",
        title: Resources.CG007_Title,
        messageFormat: Resources.CG007_MessageFormat,
        category: "Cvoya.Graph",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: Resources.CG007_Description);

    // CG008: Duplicate RelationshipAttribute label in type hierarchy
    public static readonly DiagnosticDescriptor DuplicateRelationshipAttributeLabel = new(
        id: "CG008",
        title: Resources.CG008_Title,
        messageFormat: Resources.CG008_MessageFormat,
        category: "Cvoya.Graph",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: Resources.CG008_Description);

    // CG009: Duplicate NodeAttribute label in type hierarchy
    public static readonly DiagnosticDescriptor DuplicateNodeAttributeLabel = new(
        id: "CG009",
        title: Resources.CG009_Title,
        messageFormat: Resources.CG009_MessageFormat,
        category: "Cvoya.Graph",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: Resources.CG009_Description);

    // CG010: Circular reference without nullable type
    public static readonly DiagnosticDescriptor CircularReferenceWithoutNullable = new(
        id: "CG010",
        title: Resources.CG010_Title,
        messageFormat: Resources.CG010_MessageFormat,
        category: "Cvoya.Graph",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: Resources.CG010_Description);

    // CG011: Type should inherit from base class instead of implementing interface directly
    public static readonly DiagnosticDescriptor ShouldInheritFromBaseClass = new(
        id: "CG011",
        title: Resources.CG011_Title,
        messageFormat: Resources.CG011_MessageFormat,
        category: "Cvoya.Graph",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Resources.CG011_Description);

    // CG012: [Node]/[Relationship] applied to a type that doesn't implement the matching interface
    public static readonly DiagnosticDescriptor MisappliedNodeOrRelationshipAttribute = new(
        id: "CG012",
        title: Resources.CG012_Title,
        messageFormat: Resources.CG012_MessageFormat,
        category: "Cvoya.Graph",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Resources.CG012_Description);

    // CG013: Both [Node] and [Relationship] applied to the same type
    public static readonly DiagnosticDescriptor ConflictingNodeAndRelationshipAttributes = new(
        id: "CG013",
        title: Resources.CG013_Title,
        messageFormat: Resources.CG013_MessageFormat,
        category: "Cvoya.Graph",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: Resources.CG013_Description);

    // CG014: Graph entity types must be reference types
    public static readonly DiagnosticDescriptor EntityTypeMustBeReferenceType = new(
        id: "CG014",
        title: Resources.CG014_Title,
        messageFormat: Resources.CG014_MessageFormat,
        category: "Cvoya.Graph",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: Resources.CG014_Description);

    // CG015: ComplexPropertyAttribute has no effect
    public static readonly DiagnosticDescriptor IneffectiveComplexPropertyAttribute = new(
        id: "CG015",
        title: Resources.CG015_Title,
        messageFormat: Resources.CG015_MessageFormat,
        category: "Cvoya.Graph",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Resources.CG015_Description);

    // CG016: Open generic graph entities are not supported
    public static readonly DiagnosticDescriptor OpenGenericEntityUnsupported = new(
        id: "CG016",
        title: Resources.CG016_Title,
        messageFormat: Resources.CG016_MessageFormat,
        category: "Cvoya.Graph",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: Resources.CG016_Description);

    // CG017: Nullable complex collection elements are not supported
    public static readonly DiagnosticDescriptor NullableComplexCollectionElement = new(
        id: "CG017",
        title: Resources.CG017_Title,
        messageFormat: Resources.CG017_MessageFormat,
        category: "Cvoya.Graph",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: Resources.CG017_Description);
}
