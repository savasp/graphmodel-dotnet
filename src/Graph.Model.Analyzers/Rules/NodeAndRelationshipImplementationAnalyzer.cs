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

using System.Collections.Immutable;
using Cvoya.Graph.Model.Analyzers.Rules.Validators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Cvoya.Graph.Model.Analyzers.Rules;

/// <summary>
/// Analyzer for validating INode and IRelationship implementations.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class NodeAndRelationshipImplementationAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableArray<ITypeValidator> Validators =
    [
        new ConstructorValidator(),
        new PropertyValidator()
    ];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
    [
        DiagnosticDescriptors.MustHaveParameterlessConstructorOrPropertyInitializer,
        DiagnosticDescriptors.PropertyMustHavePublicAccessors,
        DiagnosticDescriptors.PropertyCannotBeNodeOrRelationship,
        DiagnosticDescriptors.ComplexPropertyCannotHaveNodeOrRelationshipProperties,
        DiagnosticDescriptors.InvalidPropertyTypeForNode,
        DiagnosticDescriptors.InvalidPropertyTypeForRelationship
    ];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.NamedType);
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context)
    {
        var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;

        // Check if the type implements INode or IRelationship
        if (!ImplementsGraphInterface(namedTypeSymbol))
            return;

        // Run all validators
        foreach (var validator in Validators)
        {
            var diagnostics = validator.Validate(namedTypeSymbol, context);
            foreach (var diagnostic in diagnostics)
            {
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    /*
        private static bool ImplementsGraphInterface(INamedTypeSymbol typeSymbol)
        {
            return typeSymbol.AllInterfaces.Any(i =>
                i.Name is "INode" or "IRelationship" &&
                i.ContainingNamespace?.ToDisplayString() == "Cvoya.Graph.Model");
        }
    */

    private static bool ImplementsGraphInterface(INamedTypeSymbol typeSymbol)
    {
        return typeSymbol.AllInterfaces.Any(i => i.Name is "INode" or "IRelationship");
    }
}