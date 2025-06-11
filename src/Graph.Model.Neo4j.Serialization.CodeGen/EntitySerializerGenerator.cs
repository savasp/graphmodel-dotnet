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
        sb.AppendLine("using System.Reflection;");  // Add this for schema generation
        sb.AppendLine("using Cvoya.Graph.Model.Neo4j.Serialization;");
        sb.AppendLine("using Neo4j.Driver;");
        sb.AppendLine();
        sb.AppendLine($"namespace {namespaceName};");
        sb.AppendLine();
        sb.AppendLine($"internal sealed class {serializerName} : EntitySerializerBase");
        sb.AppendLine("{");
        sb.AppendLine($"    public override Type EntityType => typeof({GetTypeOfName(type)});");
        sb.AppendLine();

        Deserialization.GenerateDeserializeMethod(sb, type);
        sb.AppendLine();
        Serialization.GenerateSerializeMethod(sb, type);
        sb.AppendLine();

        // Add schema generation
        GenerateSchemaMethod(sb, type);

        sb.AppendLine("}");

        context.AddSource($"{serializerName}.g.cs", sb.ToString());
    }

    private static void GenerateSchemaMethod(StringBuilder sb, INamedTypeSymbol type)
    {
        var isRelationship = type.AllInterfaces.Any(i =>
            i.Name == "IRelationship" &&
            i.ContainingNamespace?.ToString() == "Cvoya.Graph.Model");

        var schemaType = isRelationship ? "RelationshipSchema" : "EntitySchema";
        var typeName = GetTypeOfName(type);
        var label = GetLabelFromType(type);

        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Gets the schema information for {type.Name}.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    public static {schemaType} GetSchema()");
        sb.AppendLine("    {");
        sb.AppendLine("        var properties = new Dictionary<string, PropertySchema>();");
        sb.AppendLine();

        // Get all serializable properties
        var properties = type.GetMembers().OfType<IPropertySymbol>()
            .Where(p => p.DeclaredAccessibility == Accessibility.Public &&
                       p.GetMethod != null && p.SetMethod != null &&
                       !ShouldSkipProperty(p, type));

        foreach (var property in properties)
        {
            GeneratePropertySchema(sb, property, type);
        }

        sb.AppendLine();
        sb.AppendLine($"        return new {schemaType}(");
        sb.AppendLine($"            Type: typeof({typeName}),");
        sb.AppendLine($"            Label: \"{label}\",");
        sb.AppendLine("            Properties: properties");

        if (isRelationship)
        {
            sb.AppendLine("            // StartNodeLabel and EndNodeLabel can be added later if needed");
        }

        sb.AppendLine("        );");
        sb.AppendLine("    }");
    }

    private static void GeneratePropertySchema(StringBuilder sb, IPropertySymbol property, INamedTypeSymbol containingType)
    {
        var propertyType = property.Type;
        var propertyName = GetPropertyName(property);
        var isNullable = propertyType.NullableAnnotation == NullableAnnotation.Annotated ||
                        (propertyType.CanBeReferencedByName && !propertyType.IsValueType);

        sb.AppendLine($"        // Schema for property: {property.Name}");
        sb.AppendLine("        {");
        sb.AppendLine($"            var propInfo = typeof({GetTypeOfName(containingType)}).GetProperty(\"{property.Name}\")!;");

        if (GraphDataModel.IsSimple(propertyType))
        {
            sb.AppendLine($"            properties[\"{propertyName}\"] = new PropertySchema(");
            sb.AppendLine("                PropertyInfo: propInfo,");
            sb.AppendLine($"                Neo4jPropertyName: \"{propertyName}\",");
            sb.AppendLine("                PropertyType: PropertyType.Simple,");
            sb.AppendLine($"                IsNullable: {isNullable.ToString().ToLowerInvariant()}");
            sb.AppendLine("            );");
        }
        else if (GraphDataModel.IsCollectionOfSimple(propertyType))
        {
            var elementType = GraphDataModel.GetCollectionElementType(propertyType);
            if (elementType is not null)
            {
                sb.AppendLine($"            properties[\"{propertyName}\"] = new PropertySchema(");
                sb.AppendLine("                PropertyInfo: propInfo,");
                sb.AppendLine($"                Neo4jPropertyName: \"{propertyName}\",");
                sb.AppendLine("                PropertyType: PropertyType.SimpleCollection,");
                sb.AppendLine($"                ElementType: typeof({GetTypeOfName(elementType)}),");
                sb.AppendLine($"                IsNullable: {isNullable.ToString().ToLowerInvariant()}");
                sb.AppendLine("            );");
            }
            else
            {
                // Fallback - treat as simple property if we can't determine element type
                sb.AppendLine($"            // Warning: Could not determine element type for collection property {property.Name}");
                sb.AppendLine($"            properties[\"{propertyName}\"] = new PropertySchema(");
                sb.AppendLine("                PropertyInfo: propInfo,");
                sb.AppendLine($"                Neo4jPropertyName: \"{propertyName}\",");
                sb.AppendLine("                PropertyType: PropertyType.Simple,");
                sb.AppendLine($"                IsNullable: {isNullable.ToString().ToLowerInvariant()}");
                sb.AppendLine("            );");
            }
        }
        else if (GraphDataModel.IsCollectionOfComplex(propertyType))
        {
            var elementType = GraphDataModel.GetCollectionElementType(propertyType);
            if (elementType is not null)
            {
                sb.AppendLine($"            properties[\"{propertyName}\"] = new PropertySchema(");
                sb.AppendLine("                PropertyInfo: propInfo,");
                sb.AppendLine($"                Neo4jPropertyName: \"{propertyName}\",");
                sb.AppendLine("                PropertyType: PropertyType.ComplexCollection,");
                sb.AppendLine($"                ElementType: typeof({GetTypeOfName(elementType)}),");
                sb.AppendLine($"                IsNullable: {isNullable.ToString().ToLowerInvariant()},");
                sb.AppendLine($"                NestedSchema: {elementType.Name}Serializer.GetSchema()");
                sb.AppendLine("            );");
            }
            else
            {
                // Fallback - treat as simple property if we can't determine element type
                sb.AppendLine($"            // Warning: Could not determine element type for complex collection property {property.Name}");
                sb.AppendLine($"            properties[\"{propertyName}\"] = new PropertySchema(");
                sb.AppendLine("                PropertyInfo: propInfo,");
                sb.AppendLine($"                Neo4jPropertyName: \"{propertyName}\",");
                sb.AppendLine("                PropertyType: PropertyType.Simple,");
                sb.AppendLine($"                IsNullable: {isNullable.ToString().ToLowerInvariant()}");
                sb.AppendLine("            );");
            }
        }
        else
        {
            // Complex property
            sb.AppendLine($"            properties[\"{propertyName}\"] = new PropertySchema(");
            sb.AppendLine("                PropertyInfo: propInfo,");
            sb.AppendLine($"                Neo4jPropertyName: \"{propertyName}\",");
            sb.AppendLine("                PropertyType: PropertyType.Complex,");
            sb.AppendLine($"                IsNullable: {isNullable.ToString().ToLowerInvariant()},");
            sb.AppendLine($"                NestedSchema: {propertyType.Name}Serializer.GetSchema()");
            sb.AppendLine("            );");
        }

        sb.AppendLine("        }");
        sb.AppendLine();
    }

    private static string GetLabelFromType(INamedTypeSymbol type)
    {
        // Check for NodeAttribute or RelationshipAttribute to get custom label
        foreach (var attr in type.GetAttributes())
        {
            if (attr.AttributeClass?.Name == "NodeAttribute" ||
                attr.AttributeClass?.Name == "RelationshipAttribute")
            {
                // First constructor argument should be the label
                if (attr.ConstructorArguments.Length > 0 &&
                    attr.ConstructorArguments[0].Value is string label)
                {
                    return label;
                }
            }
        }

        // Default to type name
        return type.Name;
    }

    private static string GetPropertyName(IPropertySymbol property)
    {
        // Check for PropertyAttribute to get custom property name
        foreach (var attr in property.GetAttributes())
        {
            if (attr.AttributeClass?.Name == "PropertyAttribute")
            {
                if (attr.ConstructorArguments.Length > 0 &&
                    attr.ConstructorArguments[0].Value is string propName)
                {
                    return propName;
                }
            }
        }

        // Default to property name in camelCase
        return property.Name;
    }

    private static bool ShouldSkipProperty(IPropertySymbol property, INamedTypeSymbol containingType)
    {
        // Check for IgnoreAttribute
        return property.GetAttributes().Any(attr =>
            attr.AttributeClass?.Name == "IgnoreAttribute");
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
            var typeName = GetTypeOfName(type!);
            var namespaceName = GetNamespaceName(type!);
            var serializerName = $"{type!.Name}Serializer";

            // Check if the type implements IEntity (INode or IRelationship)
            var implementsINode = type!.AllInterfaces.Any(i =>
                i.Name == "INode" &&
                i.ContainingNamespace?.ToString() == "Cvoya.Graph.Model");
            var implementsIRelationship = type!.AllInterfaces.Any(i =>
                i.Name == "IRelationship" &&
                i.ContainingNamespace?.ToString() == "Cvoya.Graph.Model");

            if (implementsINode || implementsIRelationship)
            {
                // Register serializer for entity types
                sb.AppendLine($"        EntitySerializerRegistry.Register<{typeName}>(new {namespaceName}.{serializerName}());");
            }
            else
            {
                // Register serializer for complex property types (no schema needed for these)
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

    private static string GetTypeOfName(ITypeSymbol type)
    {
        // For nullable reference types, get the underlying non-nullable type
        if (type.NullableAnnotation == NullableAnnotation.Annotated && !type.IsValueType)
        {
            return type.WithNullableAnnotation(NullableAnnotation.NotAnnotated).ToDisplayString();
        }

        return type.ToDisplayString();
    }
}