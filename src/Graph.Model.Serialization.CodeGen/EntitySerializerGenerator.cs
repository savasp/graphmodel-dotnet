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
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cvoya.Graph.Model.Serialization.CodeGen;

[Generator]
internal class EntitySerializerGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Get types from current compilation
        var currentCompilationTypes = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: IsTargetType,
                transform: GetSemanticTarget)
            .Where(m => m is not null)
            .Select((symbol, _) => symbol!) // Convert from INamedTypeSymbol? to INamedTypeSymbol
            .Collect();

        // Get types from referenced assemblies (like Graph.Model.Tests)
        var referencedTypes = context.CompilationProvider
            .Select((compilation, _) => GetEntityTypesFromReferences(compilation));

        // Combine both sources - now both sides are non-nullable
        var allTypes = currentCompilationTypes
            .Combine(referencedTypes)
            .Select((combined, _) =>
            {
                var current = combined.Left; // Already IEnumerable<INamedTypeSymbol>
                var referenced = combined.Right; // Already ImmutableArray<INamedTypeSymbol>
                return current.Concat(referenced).ToImmutableArray();
            });

        context.RegisterSourceOutput(allTypes, GenerateSerializers);
    }

    private static ImmutableArray<INamedTypeSymbol> GetEntityTypesFromReferences(Compilation compilation)
    {
        var entityTypes = new List<INamedTypeSymbol>();

        foreach (var reference in compilation.References)
        {
            if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assembly)
            {
                // Look specifically for our test assembly
                if (assembly.Name.Contains("Graph.Model.Tests"))
                {
                    var types = GetAllTypesFromAssembly(assembly)
                        .Where(ShouldGenerateSerializerFor)
                        .ToList();

                    entityTypes.AddRange(types);
                }
            }
        }

        return entityTypes.ToImmutableArray();
    }

    private static IEnumerable<INamedTypeSymbol> GetAllTypesFromAssembly(IAssemblySymbol assembly)
    {
        return GetAllTypesFromNamespace(assembly.GlobalNamespace);
    }

    private static IEnumerable<INamedTypeSymbol> GetAllTypesFromNamespace(INamespaceSymbol ns)
    {
        foreach (var type in ns.GetTypeMembers())
        {
            yield return type;

            // Handle nested types recursively
            foreach (var nestedType in GetNestedTypes(type))
            {
                yield return nestedType;
            }
        }

        foreach (var childNamespace in ns.GetNamespaceMembers())
        {
            foreach (var type in GetAllTypesFromNamespace(childNamespace))
            {
                yield return type;
            }
        }
    }

    private static IEnumerable<INamedTypeSymbol> GetNestedTypes(INamedTypeSymbol type)
    {
        foreach (var nestedType in type.GetTypeMembers())
        {
            yield return nestedType;
            foreach (var deeplyNested in GetNestedTypes(nestedType))
            {
                yield return deeplyNested;
            }
        }
    }

    private static bool ShouldGenerateSerializerFor(INamedTypeSymbol type)
    {
        var interfaces = type.AllInterfaces;
        var implementsINode = interfaces.Any(i =>
            i.Name == "INode" &&
            i.ContainingNamespace?.ToString() == "Cvoya.Graph.Model");
        var implementsIRelationship = interfaces.Any(i =>
            i.Name == "IRelationship" &&
            i.ContainingNamespace?.ToString() == "Cvoya.Graph.Model");

        return implementsINode || implementsIRelationship;
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

    private static void GenerateSerializers(SourceProductionContext context, ImmutableArray<INamedTypeSymbol> types)
    {
        if (types.IsDefaultOrEmpty) return;

        // Discover all complex property types that need serializers
        var allTypesToGenerate = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        // Add the primary entity types - no need to check for null anymore
        foreach (var type in types)
        {
            allTypesToGenerate.Add(type);
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
        GenerateRegistrationModule(context, allTypesToGenerate.ToImmutableArray());
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

        // Make the serializer class name unique by including containing types
        var uniqueSerializerName = Utils.GetUniqueSerializerClassName(type);
        var namespaceName = Utils.GetNamespaceName(type);

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("#pragma warning disable CS1591 // Missing XML comments for publicly visible type or members");
        sb.AppendLine();

        // Add all the necessary using statements
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using Cvoya.Graph.Model;");
        sb.AppendLine("using Cvoya.Graph.Model.Serialization;");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(namespaceName))
        {
            sb.AppendLine($"namespace {namespaceName};");
            sb.AppendLine();
        }

        sb.AppendLine($"internal sealed class {uniqueSerializerName} : IEntitySerializer");
        sb.AppendLine("{");
        sb.AppendLine("    private readonly EntitySerializerRegistry _serializerRegistry = EntitySerializerRegistry.Instance;");
        sb.AppendLine($"    public Type EntityType => typeof({Utils.GetTypeOfName(type)});");
        sb.AppendLine();
 
        Deserialization.GenerateDeserializeMethod(sb, type);
        sb.AppendLine();
        Serialization.GenerateSerializeMethod(sb, type);
        sb.AppendLine();

        // Add schema generation
        Schema.GenerateSchemaMethod(sb, type);

        sb.AppendLine("}");

        var hintName = GetUniqueHintName(type);
        context.AddSource(hintName, sb.ToString());
    }

    private static string GetUniqueHintName(INamedTypeSymbol type)
    {
        // Create a safe filename that includes the full hierarchy
        var fullName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            .Replace("global::", "")
            .Replace(".", "_")
            .Replace("<", "_")
            .Replace(">", "_")
            .Replace(",", "_")
            .Replace(" ", "")
            .Replace("?", "_Nullable")
            .Replace("+", "_"); // Handle nested types

        return $"{fullName}Serializer.g.cs";
    }

    private static void GenerateRegistrationModule(SourceProductionContext context, ImmutableArray<INamedTypeSymbol> types)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using Cvoya.Graph.Model;");
        sb.AppendLine("using Cvoya.Graph.Model.Serialization;");
        sb.AppendLine();
        sb.AppendLine("internal static class EntitySerializerRegistration");
        sb.AppendLine("{");
        sb.AppendLine("    [System.Runtime.CompilerServices.ModuleInitializer]");
        sb.AppendLine("    public static void Initialize()");
        sb.AppendLine("    {");

        foreach (var type in types)
        {
            // Use the clean, non-nullable type name for registration
            var cleanTypeName = Utils.GetTypeOfName(type);
            var namespaceName = Utils.GetNamespaceName(type);
            var uniqueSerializerName = Utils.GetUniqueSerializerClassName(type);

            // Check if the type implements INode or IRelationship
            var implementsINode = type.AllInterfaces.Any(i =>
                i.Name == "INode" &&
                i.ContainingNamespace?.ToString() == "Cvoya.Graph.Model");
            var implementsIRelationship = type.AllInterfaces.Any(i =>
                i.Name == "IRelationship" &&
                i.ContainingNamespace?.ToString() == "Cvoya.Graph.Model");

            if (implementsINode || implementsIRelationship)
            {
                // Register entity types with generic method
                sb.AppendLine($"        EntitySerializerRegistry.Instance.Register<{cleanTypeName}>(new {namespaceName}.{uniqueSerializerName}());");
            }
            else
            {
                // Register complex property types with typeof
                sb.AppendLine($"        EntitySerializerRegistry.Instance.Register(typeof({cleanTypeName}), new {namespaceName}.{uniqueSerializerName}());");
            }
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        context.AddSource("EntitySerializerRegistration.g.cs", sb.ToString());
    }
}