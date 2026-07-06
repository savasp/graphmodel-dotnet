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

using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;


[Generator]
internal class EntitySerializerGenerator : IIncrementalGenerator
{
    private const string NodeAttributeName = "Cvoya.Graph.Model.NodeAttribute";
    private const string RelationshipAttributeName = "Cvoya.Graph.Model.RelationshipAttribute";

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

        // Referenced-assembly entity discovery is keyed off the metadata-reference set, not the
        // full Compilation. MetadataReferencesProvider only changes when the set of references
        // changes (e.g. a package/project reference added or removed) - it is untouched by
        // source-only edits, including edits to unrelated files in the same compilation. This
        // keeps the (relatively expensive) per-assembly type walk out of the hot "keystroke in a
        // non-entity file" path. Resolution happens against a small, reference-only compilation
        // built solely from those references, so no static/process-wide cache is needed: the
        // incremental engine's own step caching (keyed on the reference set) does the job.
        var referencedTypes = context.MetadataReferencesProvider
            .Collect()
            .WithTrackingName("GraphModel.MetadataReferences")
            .Select(static (references, _) => GetEntityTypesFromReferences(references))
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

        // Every concrete type declared in the current compilation, regardless of what it
        // implements. DiscoverComplexPropertyTypes (in GenerateSerializers) only finds a complex
        // property's DECLARED type (e.g. AnimalDescription from `List<AnimalDescription>`) - a
        // derived type used only as a mixed instance in that collection (e.g. DogDescription) is
        // never itself a property type anywhere, so it can only be found by scanning every type
        // declaration for one whose base-type chain reaches a type we already generate a
        // serializer for. Mirrors baseListTypes' shape/cost (a symbol-carrying, not yet
        // equatable-value-modeled provider over CreateSyntaxProvider) rather than introducing a
        // new pattern; see #148 for folding this into that broader incrementality pass.
        var allDeclaredTypes = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: IsTargetType,
                transform: GetConcreteDeclaredType)
            .Where(static symbol => symbol is not null)
            .Select(static (symbol, _) => symbol!)
            .Collect()
            .WithTrackingName("GraphModel.AllConcreteDeclaredTypes");

        context.RegisterSourceOutput(
            allTypes.Combine(allDeclaredTypes),
            static (context, combined) => GenerateSerializers(context, combined.Left, combined.Right));
    }

    private static ImmutableArray<INamedTypeSymbol> GetEntityTypesFromReferences(
        ImmutableArray<MetadataReference> references)
    {
        if (references.IsDefaultOrEmpty)
        {
            return ImmutableArray<INamedTypeSymbol>.Empty;
        }

        // A throwaway, reference-only compilation (no syntax trees) purely to load metadata
        // symbols for the referenced assemblies. It depends only on the reference set, so it is
        // exactly as cacheable as `referencedTypes` above and is never touched by source edits.
        var probeCompilation = CSharpCompilation.Create(
            assemblyName: "GraphModel.EntityTypeDiscoveryProbe",
            references: references);

        var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();

        foreach (var reference in references)
        {
            if (probeCompilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol assembly ||
                !IsCandidateEntityAssembly(assembly))
            {
                continue;
            }

            foreach (var type in GetAllTypesFromAssembly(assembly).Where(ShouldGenerateSerializerFor))
            {
                builder.Add(type);
            }
        }

        return builder.ToImmutable();
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
        var symbol = context.SemanticModel.GetDeclaredSymbol(typeDecl, ct);

        if (symbol is null) return null;

        return ShouldGenerateSerializerFor(symbol) ? symbol : null;
    }

    /// <summary>
    /// Returns the declared symbol for any concrete (non-interface, non-abstract) type
    /// declaration, with no further filtering - used to build the subtype-discovery candidate
    /// set for complex property types (see the `allDeclaredTypes` provider in Initialize).
    /// </summary>
    private static INamedTypeSymbol? GetConcreteDeclaredType(GeneratorSyntaxContext context, CancellationToken ct)
    {
        var typeDecl = (TypeDeclarationSyntax)context.Node;
        var symbol = context.SemanticModel.GetDeclaredSymbol(typeDecl, ct);

        if (symbol is null || symbol.TypeKind == TypeKind.Interface || symbol.IsAbstract)
            return null;

        return symbol;
    }

    /// <summary>
    /// Types among <paramref name="candidates"/> whose base-type chain reaches <paramref name="baseType"/>.
    /// </summary>
    private static IEnumerable<INamedTypeSymbol> FindSubtypes(
        INamedTypeSymbol baseType,
        ImmutableArray<INamedTypeSymbol> candidates)
    {
        foreach (var candidate in candidates)
        {
            var current = candidate.BaseType;
            while (current is not null)
            {
                if (SymbolEqualityComparer.Default.Equals(current, baseType))
                {
                    yield return candidate;
                    break;
                }

                current = current.BaseType;
            }
        }
    }

    private static void GenerateSerializers(
        SourceProductionContext context,
        ImmutableArray<INamedTypeSymbol> types,
        ImmutableArray<INamedTypeSymbol> allDeclaredTypes)
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

            // A collection property declared as the base type (e.g. `List<AnimalDescription>`)
            // may hold mixed derived instances (DogDescription, PoliceDogDescription, ...) that
            // are never themselves a property type anywhere. Without this, such a derived
            // instance silently serializes/deserializes via the base type's serializer instead
            // of its own (see #146/#136) - each subtype needs its own generated serializer and
            // registry entry to preserve its ActualType and derived-only properties.
            foreach (var subtype in FindSubtypes(currentType, allDeclaredTypes)
                .Where(subtype => !allTypesToGenerate.Contains(subtype)))
            {
                allTypesToGenerate.Add(subtype);
                typesToAnalyze.Enqueue(subtype);
            }

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
