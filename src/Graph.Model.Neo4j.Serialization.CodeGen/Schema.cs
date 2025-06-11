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
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Cvoya.Graph.Model.Neo4j.Serialization.CodeGen;

internal static class Schema
{
    internal static void GenerateSchemaMethod(StringBuilder sb, INamedTypeSymbol type)
    {
        var typeName = Utils.GetTypeOfName(type);
        var label = Utils.GetLabelFromType(type);
        var uniqueSerializerName = GetUniqueSerializerClassName(type);

        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Gets the schema information for {type.Name}.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public override EntitySchema GetSchema()");
        sb.AppendLine("    {");
        sb.AppendLine($"        return {uniqueSerializerName}.GetSchemaStatic();");
        sb.AppendLine("    }");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Gets the schema information for {type.Name}.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    public static EntitySchema GetSchemaStatic()");
        sb.AppendLine("    {");
        sb.AppendLine("        var properties = new Dictionary<string, PropertySchema>();");
        sb.AppendLine();

        // Get all serializable properties
        var properties = type.GetMembers().OfType<IPropertySymbol>()
            .Where(p => p.DeclaredAccessibility == Accessibility.Public &&
                       p.GetMethod != null && p.SetMethod != null &&
                       !Utils.SerializationShouldSkipProperty(p, type));

        foreach (var property in properties)
        {
            GeneratePropertySchema(sb, property, type);
        }

        sb.AppendLine();
        sb.AppendLine($"        return new EntitySchema(");
        sb.AppendLine($"            Type: typeof({typeName}),");
        sb.AppendLine($"            Label: \"{label}\",");
        sb.AppendLine("            Properties: properties");
        sb.AppendLine("        );");
        sb.AppendLine("    }");
    }

    private static void GeneratePropertySchema(StringBuilder sb, IPropertySymbol property, INamedTypeSymbol containingType)
    {
        var propertyType = property.Type;
        var propertyName = Utils.GetPropertyName(property);
        var isNullable = propertyType.NullableAnnotation == NullableAnnotation.Annotated ||
                        (propertyType.CanBeReferencedByName && !propertyType.IsValueType);

        sb.AppendLine($"        // Schema for property: {property.Name}");
        sb.AppendLine("        {");
        sb.AppendLine($"            var propInfo = typeof({Utils.GetTypeOfName(containingType)}).GetProperty(\"{property.Name}\")!;");

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
                sb.AppendLine($"                ElementType: typeof({Utils.GetTypeOfName(elementType)}),");
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
                var nestedSchemaCall = GetNestedSchemaCall(elementType);
                sb.AppendLine($"            properties[\"{propertyName}\"] = new PropertySchema(");
                sb.AppendLine("                PropertyInfo: propInfo,");
                sb.AppendLine($"                Neo4jPropertyName: \"{propertyName}\",");
                sb.AppendLine("                PropertyType: PropertyType.ComplexCollection,");
                sb.AppendLine($"                ElementType: typeof({Utils.GetTypeOfName(elementType)}),");
                sb.AppendLine($"                IsNullable: {isNullable.ToString().ToLowerInvariant()},");
                sb.AppendLine($"                NestedSchema: {nestedSchemaCall}");
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
            var nestedSchemaCall = GetNestedSchemaCall(propertyType);
            sb.AppendLine($"            properties[\"{propertyName}\"] = new PropertySchema(");
            sb.AppendLine("                PropertyInfo: propInfo,");
            sb.AppendLine($"                Neo4jPropertyName: \"{propertyName}\",");
            sb.AppendLine("                PropertyType: PropertyType.Complex,");
            sb.AppendLine($"                IsNullable: {isNullable.ToString().ToLowerInvariant()},");
            sb.AppendLine($"                NestedSchema: {nestedSchemaCall}");
            sb.AppendLine("            );");
        }

        sb.AppendLine("        }");
        sb.AppendLine();
    }

    private static string GetNestedSchemaCall(ITypeSymbol nestedType)
    {
        if (nestedType is not INamedTypeSymbol namedType)
            return "null";

        // Generate the unique serializer class name and its namespace
        var uniqueSerializerName = GetUniqueSerializerClassName(namedType);
        var serializerNamespace = GetNamespaceName(namedType);

        // Return the fully qualified call to GetSchemaStatic
        return $"{serializerNamespace}.{uniqueSerializerName}.GetSchemaStatic()";
    }

    private static string GetUniqueSerializerClassName(INamedTypeSymbol type)
    {
        var parts = new List<string>();

        // Walk up the containing type hierarchy to handle nested types
        var current = type.ContainingType;
        while (current != null)
        {
            parts.Insert(0, current.Name);
            current = current.ContainingType;
        }

        // Add the type itself
        parts.Add(type.Name);

        // Create a unique class name
        return string.Join("_", parts) + "Serializer";
    }

    private static string GetNamespaceName(INamedTypeSymbol type)
    {
        // Use the containing namespace + ".Generated"
        var namespaceName = type.ContainingNamespace?.ToDisplayString();
        return string.IsNullOrEmpty(namespaceName) ? "Generated" : $"{namespaceName}.Generated";
    }
}