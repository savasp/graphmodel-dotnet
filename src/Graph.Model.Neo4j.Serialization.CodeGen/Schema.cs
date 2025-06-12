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
        var uniqueSerializerName = Utils.GetUniqueSerializerClassName(type);

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

        // Check if any properties are complex while generating schemas
        var hasComplexProperties = false;

        foreach (var property in properties)
        {
            GeneratePropertySchema(sb, property, type);

            // Check if this property makes the entity have complex properties
            if (Utils.IsComplexProperty(property))
            {
                hasComplexProperties = true;
            }
        }

        sb.AppendLine();
        sb.AppendLine($"        return new EntitySchema(");
        sb.AppendLine($"            Type: typeof({typeName}),");
        sb.AppendLine($"            Label: \"{label}\",");
        sb.AppendLine($"            HasComplexProperties: {hasComplexProperties.ToString().ToLowerInvariant()},");
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
                var nestedSchemaCall = Utils.GetNestedSchemaCall(elementType);
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
            var nestedSchemaCall = Utils.GetNestedSchemaCall(propertyType);
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
}