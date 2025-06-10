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
    // GM001: Must have parameterless constructor or property-initializing constructor
    public static readonly DiagnosticDescriptor MustHaveParameterlessConstructorOrPropertyInitializer = new(
        id: "GM001",
        title: "Missing parameterless constructor or property-initializing constructor",
        messageFormat: "Type '{0}' must have a parameterless constructor or a constructor that initializes all properties",
        category: "Graph.Model",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Types implementing INode or IRelationship must have parameterless constructors or constructors that initialize properties.");

    // GM002: Must have public getters and setters or initializers
    public static readonly DiagnosticDescriptor PropertyMustHavePublicAccessors = new(
        id: "GM002",
        title: "Property must have public getter and setter or initializer",
        messageFormat: "Property '{0}' must have public getter and either public setter or public initializer",
        category: "Graph.Model",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Properties in INode and IRelationship implementations must have public getters and either public setters or public property initializers.");

    // GM003: Properties cannot be INode or IRelationship
    public static readonly DiagnosticDescriptor PropertyCannotBeNodeOrRelationship = new(
        id: "GM003",
        title: "Property cannot be INode or IRelationship",
        messageFormat: "Property '{0}' cannot be of type '{1}' or collection thereof. INode and IRelationship types are not allowed as properties",
        category: "Graph.Model",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Properties of types implementing INode or IRelationship cannot be INode or IRelationship or collections of them.");

    // GM004: Complex properties cannot have INode or IRelationship properties (recursive)
    public static readonly DiagnosticDescriptor ComplexPropertyCannotHaveNodeOrRelationshipProperties = new(
        id: "GM004",
        title: "Complex property contains invalid nested properties",
        messageFormat: "Property '{0}' of complex type '{1}' contains properties that are INode or IRelationship types, which is not allowed",
        category: "Graph.Model",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Properties of complex properties of types implementing INode cannot be INode or IRelationship or collections of them. This rule is applied recursively.");

    // GM005: Invalid property type for INode implementation
    public static readonly DiagnosticDescriptor InvalidPropertyTypeForNode = new(
        id: "GM005",
        title: "Invalid property type for INode implementation",
        messageFormat: "Property '{0}' of type '{1}' is not valid for INode implementations. Must be simple, complex, or collections thereof",
        category: "Graph.Model",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Properties of INode implementations must be simple types, complex types, collections of simple types, or collections of complex types, applied recursively.");

    // GM006: Invalid property type for IRelationship implementation
    public static readonly DiagnosticDescriptor InvalidPropertyTypeForRelationship = new(
        id: "GM006",
        title: "Invalid property type for IRelationship implementation",
        messageFormat: "Property '{0}' of type '{1}' is not valid for IRelationship implementations. Must be simple or collections of simple types",
        category: "Graph.Model",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Properties of IRelationship implementations must be simple types or collections of simple types.");

    // GM007: Complex properties can only contain simple properties or collections of simple properties
    public static readonly DiagnosticDescriptor ComplexPropertyCanOnlyContainSimpleProperties = new(
        id: "GM007",
        title: "Complex properties can only contain simple properties or collections of simple properties",
        messageFormat: "Property '{0}' of complex type '{1}' contains complex properties, which is not allowed. Complex properties can only contain simple properties or collections of simple properties",
        category: "Graph.Model",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Complex properties of INode implementations can only contain simple properties or collections of simple properties, not other complex properties.");
}