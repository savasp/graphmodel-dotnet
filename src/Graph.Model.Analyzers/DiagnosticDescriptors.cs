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
    // GM001: Only classes can implement INode/IRelationship
    public static readonly DiagnosticDescriptor OnlyClassesCanImplement = new(
        id: "GM001",
        title: Resources.GM001_Title,
        messageFormat: Resources.GM001_MessageFormat,
        category: "Graph.Model",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: Resources.GM001_Description);

    // GM002: Must have parameterless constructor
    public static readonly DiagnosticDescriptor MustHaveParameterlessConstructor = new(
        id: "GM002",
        title: Resources.GM002_Title,
        messageFormat: Resources.GM002_MessageFormat,
        category: "Graph.Model",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: Resources.GM002_Description);

    // GM003: Properties must have public getters and setters
    public static readonly DiagnosticDescriptor PropertyMustHavePublicGetterAndSetter = new(
        id: "GM003",
        title: Resources.GM003_Title,
        messageFormat: Resources.GM003_MessageFormat,
        category: "Graph.Model",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: Resources.GM003_Description);

    // GM004: Unsupported property type
    public static readonly DiagnosticDescriptor UnsupportedPropertyType = new(
        id: "GM004",
        title: Resources.GM004_Title,
        messageFormat: Resources.GM004_MessageFormat,
        category: "Graph.Model",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: Resources.GM004_Description);

    // GM005: Invalid complex type property (INode only)
    public static readonly DiagnosticDescriptor InvalidComplexTypeProperty = new(
        id: "GM005",
        title: Resources.GM005_Title,
        messageFormat: Resources.GM005_MessageFormat,
        category: "Graph.Model",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: Resources.GM005_Description);
}
