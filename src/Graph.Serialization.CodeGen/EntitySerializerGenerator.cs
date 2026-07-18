// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Serialization.CodeGen;

using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;


[Generator]
internal class EntitySerializerGenerator : IIncrementalGenerator
{
    private const string NodeAttributeName = "Cvoya.Graph.NodeAttribute";
    private const string RelationshipAttributeName = "Cvoya.Graph.RelationshipAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var nodeAttributeTypes = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                NodeAttributeName,
                predicate: static (node, _) => node is TypeDeclarationSyntax,
                transform: GetAttributedEntityTarget)
            .Where(static model => model is not null)
            .Select(static (model, _) => model!)
            .Collect()
            .Select(static (sets, _) => TypeDiscoverySet.FromSets(sets))
            .WithTrackingName("GraphModel.NodeAttributeEntityTypes");

        var relationshipAttributeTypes = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                RelationshipAttributeName,
                predicate: static (node, _) => node is TypeDeclarationSyntax,
                transform: GetAttributedEntityTarget)
            .Where(static model => model is not null)
            .Select(static (model, _) => model!)
            .Collect()
            .Select(static (sets, _) => TypeDiscoverySet.FromSets(sets))
            .WithTrackingName("GraphModel.RelationshipAttributeEntityTypes");

        var attributedTypes = nodeAttributeTypes
            .Combine(relationshipAttributeTypes)
            .Select(static (combined, _) => TypeDiscoverySet.Merge(combined.Left, combined.Right))
            .WithTrackingName("GraphModel.AttributedEntityTypes");

        // Keep support for unannotated INode/IRelationship implementations in the current compilation.
        var baseListTypes = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: IsTargetType,
                transform: GetBaseListEntityTarget)
            .Where(static model => model is not null)
            .Select(static (model, _) => model!)
            .Collect()
            .Select(static (sets, _) => TypeDiscoverySet.FromSets(sets))
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
                var currentTypes = TypeDiscoverySet.Merge(combined.Left.Left, combined.Left.Right);
                return TypeDiscoverySet.Merge(currentTypes, combined.Right);
            })
            .WithTrackingName("GraphModel.EntityTypes");

        // Every concrete type declared in the current compilation that has a base list,
        // regardless of what it implements. These value models let the final generation step
        // discover concrete subtypes for base-typed complex collections without carrying symbols
        // across incremental pipeline boundaries.
        var allDeclaredTypes = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: IsTargetType,
                transform: GetConcreteDeclaredType)
            .Where(static model => model is not null)
            .Select(static (model, _) => model!)
            .Collect()
            .Select(static (sets, _) => TypeDiscoverySet.FromSets(sets))
            .WithTrackingName("GraphModel.AllConcreteDeclaredTypes");

        var generationModel = allTypes
            .Combine(allDeclaredTypes)
            .Select(static (combined, _) => GenerationModel.FromDiscoverySets(combined.Left, combined.Right))
            .WithTrackingName("GraphModel.SerializerGenerationInput");

        context.RegisterSourceOutput(
            generationModel,
            static (context, model) => GenerateSerializers(context, model));
    }

    private static TypeDiscoverySet GetEntityTypesFromReferences(
        ImmutableArray<MetadataReference> references)
    {
        if (references.IsDefaultOrEmpty)
        {
            return TypeDiscoverySet.Empty;
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

        return CodeGenModelBuilder.Build(builder);
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
            .Any(static reference => reference.Identity.Name == "Cvoya.Graph");
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

    private static bool ShouldGenerateSerializerFor(INamedTypeSymbol type)
    {
        // Skip interfaces and abstract classes: neither is ever a concrete serialization root.
        if (type.TypeKind == TypeKind.Interface ||
            type.IsAbstract)
        {
            return false;
        }

        // Skip open generic entity roots. The generated serializer is a non-generic class that would
        // reference the entity as `GenericNode<T>` (or a type nested in an open generic) from a scope
        // where the type parameter is unbound, producing invalid C#. These are rejected with an
        // actionable analyzer diagnostic (CG016) instead. A non-generic type built from a closed
        // construction such as `StringNode : GenericNode<string>` has no free type parameters and
        // still generates (#373).
        if (IsOpenGeneric(type))
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
            i.ContainingNamespace?.ToString() == "Cvoya.Graph");
        var implementsIRelationship = interfaces.Any(i =>
            i.Name == "IRelationship" &&
            i.ContainingNamespace?.ToString() == "Cvoya.Graph");

        return implementsINode || implementsIRelationship;
    }

    private static bool IsDynamicType(INamedTypeSymbol type)
    {
        var fullName = type.ToDisplayString();

        // Exclude the built-in dynamic types
        return fullName == "Cvoya.Graph.DynamicNode" ||
               fullName == "Cvoya.Graph.DynamicRelationship";
    }

    /// <summary>
    /// True when <paramref name="type"/> - or any of its containing types - still has an unbound
    /// type parameter: an open generic declaration (<c>GenericNode&lt;T&gt;</c>) or a type nested in
    /// one. A closed construction such as <c>GenericNode&lt;string&gt;</c> has no free type
    /// parameters and returns false, so a non-generic entity derived from it still generates.
    /// </summary>
    private static bool IsOpenGeneric(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.ContainingType)
        {
            if (current.TypeArguments.Any(argument => argument.TypeKind == TypeKind.TypeParameter))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsTargetType(SyntaxNode node, CancellationToken ct)
    {
        return node is TypeDeclarationSyntax typeDecl &&
               typeDecl.BaseList?.Types.Count > 0;
    }

    private static TypeDiscoverySet? GetAttributedEntityTarget(GeneratorAttributeSyntaxContext context, CancellationToken ct)
    {
        return context.TargetSymbol is INamedTypeSymbol type && ShouldGenerateSerializerFor(type)
            ? CodeGenModelBuilder.Build(type)
            : null;
    }

    private static TypeDiscoverySet? GetBaseListEntityTarget(GeneratorSyntaxContext context, CancellationToken ct)
    {
        var typeDecl = (TypeDeclarationSyntax)context.Node;
        var symbol = context.SemanticModel.GetDeclaredSymbol(typeDecl, ct);

        if (symbol is null) return null;

        return ShouldGenerateSerializerFor(symbol) ? CodeGenModelBuilder.Build(symbol) : null;
    }

    /// <summary>
    /// Returns a value model for any concrete (non-interface, non-abstract) type declaration
    /// with a base list, with no graph-entity filtering - used to build the subtype-discovery
    /// candidate set for complex property types (see the `allDeclaredTypes` provider in
    /// Initialize).
    /// </summary>
    private static TypeDiscoverySet? GetConcreteDeclaredType(GeneratorSyntaxContext context, CancellationToken ct)
    {
        var typeDecl = (TypeDeclarationSyntax)context.Node;
        var symbol = context.SemanticModel.GetDeclaredSymbol(typeDecl, ct);

        if (symbol is null || symbol.TypeKind == TypeKind.Interface || symbol.IsAbstract || IsOpenGeneric(symbol))
            return null;

        return CodeGenModelBuilder.Build(symbol);
    }

    private static void GenerateSerializers(
        SourceProductionContext context,
        GenerationModel model)
    {
        if (model.Roots.Items.IsDefaultOrEmpty) return;

        var catalog = new Dictionary<string, SerializableTypeModel>(StringComparer.Ordinal);
        foreach (var type in model.Catalog.Items.Where(type => !catalog.ContainsKey(type.Type.Identity)))
        {
            catalog.Add(type.Type.Identity, type);
        }

        var allTypesToGenerate = new List<SerializableTypeModel>();
        var seenTypes = new HashSet<string>(StringComparer.Ordinal);
        var typesToAnalyze = new Queue<SerializableTypeModel>();

        foreach (var type in model.Roots.Items)
        {
            AddType(type);
        }

        while (typesToAnalyze.Count > 0)
        {
            var currentType = typesToAnalyze.Dequeue();

            // A collection property declared as the base type (e.g. `List<AnimalDescription>`)
            // may hold mixed derived instances (DogDescription, PoliceDogDescription, ...) that
            // are never themselves a property type anywhere. Without this, such a derived
            // instance silently serializes/deserializes via the base type's serializer instead
            // of its own (see #146/#136) - each subtype needs its own generated serializer and
            // registry entry to preserve its ActualType and derived-only properties.
            foreach (var subtype in model.AllDeclaredRoots.Items
                .Where(subtype => subtype.BaseTypeIdentities.Items.Contains(currentType.Type.Identity))
                .Where(subtype => !seenTypes.Contains(subtype.Type.Identity)))
            {
                AddType(subtype);
            }

            foreach (var complexType in currentType.ComplexPropertyTypeIdentities.Items
                .Select(GetCatalogType)
                .OfType<SerializableTypeModel>())
            {
                AddType(complexType);
            }
        }

        foreach (var type in allTypesToGenerate)
        {
            GenerateSerializerFile(context, type);
        }

        GenerateRegistrationModule(context, allTypesToGenerate);

        void AddType(SerializableTypeModel type)
        {
            if (seenTypes.Add(type.Type.Identity))
            {
                allTypesToGenerate.Add(type);
                typesToAnalyze.Enqueue(type);
            }
        }

        SerializableTypeModel? GetCatalogType(string identity)
        {
            return catalog.TryGetValue(identity, out var type) ? type : null;
        }
    }

    private static void GenerateSerializerFile(SourceProductionContext context, SerializableTypeModel type)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("#pragma warning disable CS1591 // Missing XML comments for publicly visible type or members");
        sb.AppendLine();

        // Add all the necessary using statements
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using Cvoya.Graph;");
        sb.AppendLine("using Cvoya.Graph.Serialization;");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(type.NamespaceName))
        {
            sb.AppendLine($"namespace {type.NamespaceName};");
            sb.AppendLine();
        }

        sb.AppendLine($"internal sealed class {type.SerializerClassName} : IEntitySerializer");
        sb.AppendLine("{");
        sb.AppendLine("    private readonly EntitySerializerRegistry _serializerRegistry = EntitySerializerRegistry.Instance;");
        sb.AppendLine($"    public Type EntityType => typeof({type.Type.TypeOfName});");
        sb.AppendLine();

        Deserialization.GenerateDeserializeMethod(sb, type);
        sb.AppendLine();
        Serialization.GenerateSerializeMethod(sb, type);
        sb.AppendLine();

        // Add schema generation
        Schema.GenerateSchemaMethod(sb, type);

        sb.AppendLine("}");

        context.AddSource(type.HintName, sb.ToString());
    }

    private static void GenerateRegistrationModule(SourceProductionContext context, IReadOnlyList<SerializableTypeModel> types)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using Cvoya.Graph;");
        sb.AppendLine("using Cvoya.Graph.Serialization;");
        sb.AppendLine();
        sb.AppendLine("internal static class EntitySerializerRegistration");
        sb.AppendLine("{");
        sb.AppendLine("    [System.Runtime.CompilerServices.ModuleInitializer]");
        sb.AppendLine("    public static void Initialize()");
        sb.AppendLine("    {");

        foreach (var type in types)
        {
            // Use the clean, non-nullable type name for registration
            var cleanTypeName = type.Type.TypeOfName;

            if (type.Kind is SerializableTypeKind.Node or SerializableTypeKind.Relationship)
            {
                // Register entity types with generic method
                sb.AppendLine($"        EntitySerializerRegistry.Instance.Register<{cleanTypeName}>(new {type.NamespaceName}.{type.SerializerClassName}());");
            }
            else
            {
                // Register complex property types with typeof
                sb.AppendLine($"        EntitySerializerRegistry.Instance.Register(typeof({cleanTypeName}), new {type.NamespaceName}.{type.SerializerClassName}());");
            }
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        context.AddSource("EntitySerializerRegistration.g.cs", sb.ToString());
    }
}
