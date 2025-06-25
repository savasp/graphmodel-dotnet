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

using Cvoya.Graph.Model.Analyzers.Properties;
using Microsoft.CodeAnalysis;

/// <summary>
/// Diagnostic descriptors for Graph.Model analyzer rules.
/// </summary>
internal static class DiagnosticDescriptors
{
    // GM001: Missing parameterless constructor or constructor that initializes properties
    public static readonly DiagnosticDescriptor MissingParameterlessConstructor = new(
        id: "GM001",
        title: Resources.GM001_Title,
        messageFormat: Resources.GM001_MessageFormat,
        category: "Graph.Model",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: Resources.GM001_Description);

    // GM002: Property must have public getters and setters or initializers
    public static readonly DiagnosticDescriptor PropertyMustHavePublicAccessors = new(
        id: "GM002",
        title: Resources.GM002_Title,
        messageFormat: Resources.GM002_MessageFormat,
        category: "Graph.Model",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: Resources.GM002_Description);

    // GM003: Property cannot be INode or IRelationship type
    public static readonly DiagnosticDescriptor PropertyCannotBeGraphInterfaceType = new(
        id: "GM003",
        title: Resources.GM003_Title,
        messageFormat: Resources.GM003_MessageFormat,
        category: "Graph.Model",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: Resources.GM003_Description);

    // GM004: Invalid property type for INode implementation
    public static readonly DiagnosticDescriptor InvalidPropertyTypeForNode = new(
        id: "GM004",
        title: Resources.GM004_Title,
        messageFormat: Resources.GM004_MessageFormat,
        category: "Graph.Model",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: Resources.GM004_Description);

    // GM005: Invalid property type for IRelationship implementation
    public static readonly DiagnosticDescriptor InvalidPropertyTypeForRelationship = new(
        id: "GM005",
        title: Resources.GM005_Title,
        messageFormat: Resources.GM005_MessageFormat,
        category: "Graph.Model",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: Resources.GM005_Description);

    // GM006: Complex type property contains graph interface types
    public static readonly DiagnosticDescriptor ComplexTypeContainsGraphInterfaceTypes = new(
        id: "GM006",
        title: Resources.GM006_Title,
        messageFormat: Resources.GM006_MessageFormat,
        category: "Graph.Model",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: Resources.GM006_Description);

    // GM007: Duplicate PropertyAttribute label in type hierarchy
    public static readonly DiagnosticDescriptor DuplicatePropertyAttributeLabel = new(
        id: "GM007",
        title: Resources.GM007_Title,
        messageFormat: Resources.GM007_MessageFormat,
        category: "Graph.Model",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: Resources.GM007_Description);

    // GM008: Duplicate RelationshipAttribute label in type hierarchy
    public static readonly DiagnosticDescriptor DuplicateRelationshipAttributeLabel = new(
        id: "GM008",
        title: Resources.GM008_Title,
        messageFormat: Resources.GM008_MessageFormat,
        category: "Graph.Model",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: Resources.GM008_Description);

    // GM009: Duplicate NodeAttribute label in type hierarchy
    public static readonly DiagnosticDescriptor DuplicateNodeAttributeLabel = new(
        id: "GM009",
        title: Resources.GM009_Title,
        messageFormat: Resources.GM009_MessageFormat,
        category: "Graph.Model",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: Resources.GM009_Description);

    // GM010: Circular reference without nullable type
    public static readonly DiagnosticDescriptor CircularReferenceWithoutNullable = new(
        id: "GM010",
        title: Resources.GM010_Title,
        messageFormat: Resources.GM010_MessageFormat,
        category: "Graph.Model",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: Resources.GM010_Description);
}
