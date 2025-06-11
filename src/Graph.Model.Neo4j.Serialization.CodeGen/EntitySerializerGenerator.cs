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

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cvoya.Graph.Model.Neo4j.Serialization.CodeGen;

[Generator]
public class EntitySerializerGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var entityTypes = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: IsTargetType,
                transform: GetSemanticTarget)
            .Where(m => m is not null)
            .Collect();

        context.RegisterSourceOutput(entityTypes, GenerateSerializers);
    }

    private static bool IsTargetType(SyntaxNode node, CancellationToken ct)
    {
        return node is TypeDeclarationSyntax typeDecl &&
               typeDecl.BaseList?.Types.Count > 0;
    }

    private static INamedTypeSymbol? GetSemanticTarget(GeneratorSyntaxContext context, CancellationToken ct)
    {
        var typeDecl = (TypeDeclarationSyntax)context.Node;
        var symbol = context.SemanticModel.GetDeclaredSymbol(typeDecl, ct) as INamedTypeSymbol;

        if (symbol is null) return null;

        var interfaces = symbol.AllInterfaces;
        var implementsINode = interfaces.Any(i =>
            i.Name == "INode" &&
            i.ContainingNamespace?.ToString() == "Cvoya.Graph.Model");
        var implementsIRelationship = interfaces.Any(i =>
            i.Name == "IRelationship" &&
            i.ContainingNamespace?.ToString() == "Cvoya.Graph.Model");

        return (implementsINode || implementsIRelationship) ? symbol : null;
    }

    private static void GenerateSerializers(SourceProductionContext context, ImmutableArray<INamedTypeSymbol?> types)
    {
        if (types.IsDefaultOrEmpty) return;

        // Discover all complex property types that need serializers
        var allTypesToGenerate = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        // Add the primary entity types
        foreach (var type in types.Where(t => t is not null))
        {
            allTypesToGenerate.Add(type!);
        }

        // Discover complex property types recursively
        var typesToAnalyze = new Queue<INamedTypeSymbol>(allTypesToGenerate);
        var analyzed = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        while (typesToAnalyze.Count > 0)
        {
            var currentType = typesToAnalyze.Dequeue();
            if (analyzed.Contains(currentType)) continue;
            analyzed.Add(currentType);

            // Find complex property types
            var complexPropertyTypes = DiscoverComplexPropertyTypes(currentType);
            foreach (var complexType in complexPropertyTypes)
            {
                if (!allTypesToGenerate.Contains(complexType))
                {
                    allTypesToGenerate.Add(complexType);
                    typesToAnalyze.Enqueue(complexType);
                }
            }
        }

        // Generate individual serializer files for all discovered types
        foreach (var type in allTypesToGenerate)
        {
            GenerateSerializerFile(context, type);
        }

        // Generate registration module
        GenerateRegistrationModule(context, allTypesToGenerate.Select(t => (INamedTypeSymbol?)t).ToImmutableArray());
    }

    private static IEnumerable<INamedTypeSymbol> DiscoverComplexPropertyTypes(INamedTypeSymbol type)
    {
        var complexTypes = new List<INamedTypeSymbol>();

        // Get all properties of the type
        var properties = type.GetMembers().OfType<IPropertySymbol>()
            .Where(p => p.DeclaredAccessibility == Accessibility.Public &&
                       p.GetMethod != null && p.SetMethod != null);

        foreach (var property in properties)
        {
            var propertyType = property.Type;

            // Check if it's a complex type (not simple and not collection of simple)
            if (!GraphDataModel.IsSimple(propertyType) && !GraphDataModel.IsCollectionOfSimple(propertyType))
            {
                // Check if it's a collection of complex types
                if (GraphDataModel.IsCollectionOfComplex(propertyType))
                {
                    var elementType = GraphDataModel.GetCollectionElementType(propertyType);
                    if (elementType is INamedTypeSymbol namedElementType && IsSerializableComplexType(namedElementType))
                    {
                        complexTypes.Add(namedElementType);
                    }
                }
                // Check if it's a single complex type
                else if (propertyType is INamedTypeSymbol namedPropertyType && IsSerializableComplexType(namedPropertyType))
                {
                    complexTypes.Add(namedPropertyType);
                }
            }
        }

        return complexTypes;
    }

    private static bool IsSerializableComplexType(INamedTypeSymbol type)
    {
        // A type is serializable if it's a class (not interface, not abstract) 
        // and has a parameterless constructor
        return type.TypeKind == TypeKind.Class &&
               !type.IsAbstract &&
               type.InstanceConstructors.Any(c => c.DeclaredAccessibility == Accessibility.Public && c.Parameters.Length == 0);
    }

    private static void GenerateSerializerFile(SourceProductionContext context, INamedTypeSymbol type)
    {
        var sb = new StringBuilder();
        var serializerName = $"{type.Name}Serializer";
        var namespaceName = GetNamespaceName(type);

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using Cvoya.Graph.Model.Neo4j.Serialization;");
        sb.AppendLine("using Neo4j.Driver;");
        sb.AppendLine();
        sb.AppendLine($"namespace {namespaceName};");
        sb.AppendLine();
        sb.AppendLine($"internal sealed class {serializerName} : EntitySerializerBase");
        sb.AppendLine("{");
        sb.AppendLine($"    public override Type EntityType => typeof({type.ToDisplayString()});");
        sb.AppendLine();

        Deserialization.GenerateDeserializeMethod(sb, type);
        sb.AppendLine();
        Serialization.GenerateSerializeMethod(sb, type);

        sb.AppendLine("}");

        context.AddSource($"{serializerName}.g.cs", sb.ToString());
    }

    private static void GenerateRegistrationModule(SourceProductionContext context, ImmutableArray<INamedTypeSymbol?> types)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using Cvoya.Graph.Model.Neo4j.Serialization;");
        sb.AppendLine();
        sb.AppendLine("internal static class EntitySerializerRegistration");
        sb.AppendLine("{");
        sb.AppendLine("    [System.Runtime.CompilerServices.ModuleInitializer]");
        sb.AppendLine("    public static void Initialize()");
        sb.AppendLine("    {");

        foreach (var type in types.Where(t => t is not null))
        {
            var typeName = type!.ToDisplayString();
            var namespaceName = GetNamespaceName(type);
            var serializerName = $"{type.Name}Serializer";

            // Check if the type implements IEntity (INode or IRelationship)
            var implementsIEntity = type.AllInterfaces.Any(i =>
                (i.Name == "INode" || i.Name == "IRelationship") &&
                i.ContainingNamespace?.ToString() == "Cvoya.Graph.Model");

            if (implementsIEntity)
            {
                // Use generic registration for entity types
                sb.AppendLine($"        EntitySerializerRegistry.Register<{typeName}>(new {namespaceName}.{serializerName}());");
            }
            else
            {
                // Use non-generic registration for complex property types
                sb.AppendLine($"        EntitySerializerRegistry.Register(typeof({typeName}), new {namespaceName}.{serializerName}());");
            }
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        context.AddSource("EntitySerializerRegistration.g.cs", sb.ToString());
    }

    private static string GetNamespaceName(INamedTypeSymbol type)
    {
        var namespaceName = type.ContainingNamespace?.ToDisplayString();

        if (namespaceName is null || namespaceName == "<global namespace>")
        {
            return "Generated";
        }

        return namespaceName + ".Generated";
    }
}