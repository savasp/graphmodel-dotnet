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

namespace Cvoya.Graph.Model.Analyzers.Rules;

/// <summary>
/// Contains all diagnostic descriptors for the Graph.Model analyzers.
/// </summary>
public static class DiagnosticDescriptors
{
    /// <summary>
    /// GM001: Only classes can implement INode or IRelationship. Structs are not supported.
    /// </summary>
    public static readonly DiagnosticDescriptor OnlyClassesCanImplementInterfaces = new(
        id: "GM001",
        title: "Only classes can implement INode or IRelationship",
        messageFormat: "Only classes can implement {0}. Structs are not supported.",
        category: "GraphModel",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Types implementing INode or IRelationship must be classes, not structs, to ensure proper serialization and persistence capabilities.");

    /// <summary>
    /// GM002: Implementing classes must have a parameterless constructor.
    /// </summary>
    public static readonly DiagnosticDescriptor MustHaveParameterlessConstructor = new(
        id: "GM002",
        title: "Type must have a parameterless constructor",
        messageFormat: "Type '{0}' must have a parameterless constructor to implement {1}.",
        category: "GraphModel",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Types implementing INode or IRelationship must have a public or internal parameterless constructor for proper deserialization.");

    /// <summary>
    /// GM003: All properties must have public getters and setters.
    /// </summary>
    public static readonly DiagnosticDescriptor PropertyMustHavePublicAccessors = new(
        id: "GM003",
        title: "Property must have public getter and setter",
        messageFormat: "Property '{0}' in type '{1}' must have both public getter and setter.",
        category: "GraphModel",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "All properties in types implementing INode or IRelationship must have public getters and setters for proper serialization.");

    /// <summary>
    /// GM004: Properties can only be of supported types.
    /// </summary>
    public static readonly DiagnosticDescriptor UnsupportedPropertyType = new(
        id: "GM004",
        title: "Unsupported property type",
        messageFormat: "Property '{0}' has unsupported type '{1}'. Only primitive types, string, date/time types, Point, and collections of these are allowed.",
        category: "GraphModel",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Properties must be of supported types for proper graph database serialization. Supported types include primitives, string, date/time types, enums, Point, and collections thereof.");

    /// <summary>
    /// GM005: Complex type properties in INode implementations must follow specific rules.
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidComplexTypeProperty = new(
        id: "GM005",
        title: "Invalid complex type property",
        messageFormat: "Complex type property '{0}' in INode implementation must be a class with a parameterless constructor and only simple properties.",
        category: "GraphModel",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Complex type properties in INode implementations must be classes with parameterless constructors and contain only simple properties that follow the same type constraints.");
}