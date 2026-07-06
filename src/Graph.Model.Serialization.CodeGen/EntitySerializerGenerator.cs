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

namespace Cvoya.Graph.Model.Serialization.CodeGen;

using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;


[Generator]
internal class EntitySerializerGenerator : IIncrementalGenerator
{
    private const string NodeAttributeName = "Cvoya.Graph.Model.NodeAttribute";
    private const string RelationshipAttributeName = "Cvoya.Graph.Model.RelationshipAttribute";
    private static readonly ConcurrentDictionary<string, ImmutableArray<string>> ReferencedEntityTypeCache = new(StringComparer.Ordinal);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var nodeAttributeTypes = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                NodeAttributeName,
                predicate: static (node, _) => node is TypeDeclarationSyntax,
                transform: GetAttributedEntityTarget)
            .Where(static symbol => symbol is not null)
            .Select(static (symbol, _) => symbol!)
            .Collect()
            .WithTrackingName("GraphModel.NodeAttributeEntityTypes");

        var relationshipAttributeTypes = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                RelationshipAttributeName,
                predicate: static (node, _) => node is TypeDeclarationSyntax,
                transform: GetAttributedEntityTarget)
            .Where(static symbol => symbol is not null)
            .Select(static (symbol, _) => symbol!)
            .Collect()
            .WithTrackingName("GraphModel.RelationshipAttributeEntityTypes");

        var attributedTypes = nodeAttributeTypes
            .Combine(relationshipAttributeTypes)
            .Select(static (combined, _) => DedupeTypes(combined.Left, combined.Right))
            .WithTrackingName("GraphModel.AttributedEntityTypes");

        // Keep support for unannotated INode/IRelationship implementations in the current compilation.
        var baseListTypes = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: IsTargetType,
                transform: GetBaseListEntityTarget)
            .Where(static symbol => symbol is not null)
            .Select(static (symbol, _) => symbol!)
            .Collect()
            .WithTrackingName("GraphModel.BaseListEntityTypes");

        var referencedTypes = context.CompilationProvider
            .Select(static (compilation, _) => GetEntityTypesFromReferences(compilation))
            .WithTrackingName("GraphModel.ReferencedEntityTypes");

        var allTypes = attributedTypes
            .Combine(baseListTypes)
            .Combine(referencedTypes)
            .Select(static (combined, _) =>
            {
                var currentTypes = DedupeTypes(combined.Left.Left, combined.Left.Right);
                return DedupeTypes(currentTypes, combined.Right);
            })
            .WithTrackingName("GraphModel.EntityTypes");

        context.RegisterSourceOutput(allTypes, GenerateSerializers);
    }

    private static ImmutableArray<INamedTypeSymbol> GetEntityTypesFromReferences(Compilation compilation)
    {
        var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();

        foreach (var reference in compilation.References)
        {
            if (compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol assembly ||
                !IsCandidateEntityAssembly(assembly))
            {
                continue;
            }

            var cacheKey = GetReferenceCacheKey(reference, assembly);
            var metadataNames = ReferencedEntityTypeCache.GetOrAdd(
                cacheKey,
                _ => GetEntityTypeMetadataNames(assembly));

            foreach (var type in metadataNames
                .Select(compilation.GetTypeByMetadataName)
                .Where(static type => type is not null)
                .Select(static type => type!)
                .Where(ShouldGenerateSerializerFor))
            {
                builder.Add(type);
            }
        }

        return builder.ToImmutable();
    }

    private static ImmutableArray<string> GetEntityTypeMetadataNames(IAssemblySymbol assembly)
    {
        return GetAllTypesFromAssembly(assembly)
            .Where(ShouldGenerateSerializerFor)
            .Select(GetFullyQualifiedMetadataName)
            .ToImmutableArray();
    }

    private static string GetReferenceCacheKey(MetadataReference reference, IAssemblySymbol assembly)
    {
        var display = reference.Display ?? assembly.Identity.GetDisplayName();
        return $"{display}|{assembly.Identity.GetDisplayName()}";
    }

    private static bool IsCandidateEntityAssembly(IAssemblySymbol assembly)
    {
        var name = assembly.Name;

        if (name.StartsWith("System.") || name.StartsWith("Microsoft.") ||
            name.StartsWith("netstandard") || name.Equals("mscorlib"))
        {
            return false;
        }

        if (name.StartsWith("Newtonsoft.") || name.StartsWith("Serilog") ||
            name.StartsWith("NUnit") || name.StartsWith("xunit") || name.StartsWith("coverlet"))
        {
            return false;
        }

        return assembly.Modules
            .SelectMany(static module => module.ReferencedAssemblySymbols)
            .Any(static reference => reference.Identity.Name == "Cvoya.Graph.Model");
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

    private static string GetFullyQualifiedMetadataName(INamedTypeSymbol type)
    {
        var metadataName = type.MetadataName;

        for (var containingType = type.ContainingType; containingType is not null; containingType = containingType.ContainingType)
        {
            metadataName = $"{containingType.MetadataName}+{metadataName}";
        }

        return type.ContainingNamespace is { IsGlobalNamespace: false } containingNamespace
            ? $"{containingNamespace.ToDisplayString()}.{metadataName}"
            : metadataName;
    }

    private static ImmutableArray<INamedTypeSymbol> DedupeTypes(
        ImmutableArray<INamedTypeSymbol> first,
        ImmutableArray<INamedTypeSymbol> second)
    {
        var seen = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>(first.Length + second.Length);

        AddTypes(first);
        AddTypes(second);

        return builder.ToImmutable();

        void AddTypes(ImmutableArray<INamedTypeSymbol> types)
        {
            foreach (var type in types.Where(seen.Add))
            {
                builder.Add(type);
            }
        }
    }

    private static bool ShouldGenerateSerializerFor(INamedTypeSymbol type)
    {
        // Skip interfaces, abstract classes, and generic type definitions
        if (type.TypeKind == TypeKind.Interface ||
            type.IsAbstract)
        {
            return false;
        }

        // Exclude built-in dynamic types - they handle serialization differently
        if (IsDynamicType(type))
        {
            return false;
        }

        var interfaces = type.AllInterfaces;
        var implementsINode = interfaces.Any(i =>
            i.Name == "INode" &&
            i.ContainingNamespace?.ToString() == "Cvoya.Graph.Model");
        var implementsIRelationship = interfaces.Any(i =>
            i.Name == "IRelationship" &&
            i.ContainingNamespace?.ToString() == "Cvoya.Graph.Model");

        return implementsINode || implementsIRelationship;
    }

    private static bool IsDynamicType(INamedTypeSymbol type)
    {
        var fullName = type.ToDisplayString();

        // Exclude the built-in dynamic types
        return fullName == "Cvoya.Graph.Model.DynamicNode" ||
               fullName == "Cvoya.Graph.Model.DynamicRelationship";
    }

    private static bool IsTargetType(SyntaxNode node, CancellationToken ct)
    {
        return node is TypeDeclarationSyntax typeDecl &&
               typeDecl.BaseList?.Types.Count > 0;
    }

    private static INamedTypeSymbol? GetAttributedEntityTarget(GeneratorAttributeSyntaxContext context, CancellationToken ct)
    {
        return context.TargetSymbol is INamedTypeSymbol type && ShouldGenerateSerializerFor(type)
            ? type
            : null;
    }

    private static INamedTypeSymbol? GetBaseListEntityTarget(GeneratorSyntaxContext context, CancellationToken ct)
    {
        var typeDecl = (TypeDeclarationSyntax)context.Node;
        var symbol = context.SemanticModel.GetDeclaredSymbol(typeDecl, ct) as INamedTypeSymbol;

        if (symbol is null) return null;

        return ShouldGenerateSerializerFor(symbol) ? symbol : null;
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
