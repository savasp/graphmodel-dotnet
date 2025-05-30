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

namespace Cvoya.Graph.Model.Analyzers;

/// <summary>
/// Diagnostic descriptors for Graph.Model analyzer rules.
/// </summary>
public static class DiagnosticDescriptors
{
    // GM001: Only classes can implement INode/IRelationship
    public static readonly DiagnosticDescriptor OnlyClassesCanImplement = new(
        id: "GM001",
        title: "Structs cannot implement INode or IRelationship",
        messageFormat: "Struct '{0}' cannot implement {1}. Only classes are supported.",
        category: "Graph.Model",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "INode and IRelationship can only be implemented by classes, not structs.");

    // GM002: Must have parameterless constructor
    public static readonly DiagnosticDescriptor MustHaveParameterlessConstructor = new(
        id: "GM002",
        title: "Missing parameterless constructor",
        messageFormat: "Type '{0}' must have a parameterless constructor (public or internal)",
        category: "Graph.Model",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Types implementing INode or IRelationship must have a parameterless constructor.");

    // GM003: Properties must have public getters and setters
    public static readonly DiagnosticDescriptor PropertyMustHavePublicGetterAndSetter = new(
        id: "GM003",
        title: "Property must have public getter and setter",
        messageFormat: "Property '{0}' must have public getter and setter",
        category: "Graph.Model",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "All properties in INode and IRelationship implementations must have public getters and setters.");

    // GM004: Unsupported property type
    public static readonly DiagnosticDescriptor UnsupportedPropertyType = new(
        id: "GM004",
        title: "Unsupported property type",
        messageFormat: "Property '{0}' has unsupported type '{1}'",
        category: "Graph.Model",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Properties can only be of supported types (primitives, string, date/time, Point, collections).");

    // GM005: Invalid complex type property (INode only)
    public static readonly DiagnosticDescriptor InvalidComplexTypeProperty = new(
        id: "GM005",
        title: "Invalid complex type property",
        messageFormat: "Property '{0}' of type '{1}' is not a valid complex type for INode implementations",
        category: "Graph.Model",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Complex type properties in INode implementations must be classes with parameterless constructors and only simple properties.");
}