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

using System.Text;
using Microsoft.CodeAnalysis;

namespace Cvoya.Graph.Model.Serialization.CodeGen;

internal static class Schema
{
    internal static void GenerateSchemaMethod(StringBuilder sb, INamedTypeSymbol type)
    {
        var typeName = Utils.GetTypeOfName(type);
        var label = Utils.GetLabelFromType(type);
        var uniqueSerializerName = Utils.GetUniqueSerializerClassName(type);

        // Use a set to track what's currently being processed
        sb.AppendLine("    private static readonly HashSet<Type> _currentlyProcessing = new();");
        sb.AppendLine("    private static readonly Dictionary<Type, EntitySchema> _schemaCache = new();");
        sb.AppendLine();

        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Gets the schema information for {type.Name}.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public EntitySchema GetSchema()");
        sb.AppendLine("    {");
        sb.AppendLine($"        return {uniqueSerializerName}.GetSchemaStatic();");
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Gets the schema information for {type.Name}.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    public static EntitySchema GetSchemaStatic()");
        sb.AppendLine("    {");
        sb.AppendLine($"        var typeKey = typeof({typeName});");
        sb.AppendLine();
        sb.AppendLine($"        // Return cached schema if available");
        sb.AppendLine($"        if (_schemaCache.TryGetValue(typeKey, out var cachedSchema))");
        sb.AppendLine($"            return cachedSchema;");
        sb.AppendLine();
        sb.AppendLine($"        // Check if we're already processing this type (circular reference)");
        sb.AppendLine($"        if (_currentlyProcessing.Contains(typeKey))");
        sb.AppendLine($"        {{");
        sb.AppendLine($"            // Return a schema without nested schemas to break the cycle");
        sb.AppendLine($"            return new EntitySchema(");
        sb.AppendLine($"                ExpectedType: typeof({typeName}),");
        sb.AppendLine($"                Label: \"{label}\",");
        sb.AppendLine($"                IsNullable: {(type.NullableAnnotation == NullableAnnotation.Annotated || (type.CanBeReferencedByName && !type.IsValueType)).ToString().ToLowerInvariant()},");
        sb.AppendLine($"                IsSimple: {(GraphDataModel.IsSimple(type) || GraphDataModel.IsCollectionOfSimple(type)).ToString().ToLowerInvariant()},");
        sb.AppendLine($"                SimpleProperties: new Dictionary<string, PropertySchema>(),");
        sb.AppendLine($"                ComplexProperties: new Dictionary<string, PropertySchema>()");
        sb.AppendLine($"            );");
        sb.AppendLine($"        }}");
        sb.AppendLine();
        sb.AppendLine($"        // Mark this type as being processed");
        sb.AppendLine($"        _currentlyProcessing.Add(typeKey);");
        sb.AppendLine();
        sb.AppendLine($"        try");
        sb.AppendLine($"        {{");
        sb.AppendLine($"            // Build the actual schema");
        sb.AppendLine("            var simpleProperties = new Dictionary<string, PropertySchema>();");
        sb.AppendLine("            var complexProperties = new Dictionary<string, PropertySchema>();");

        // Get all serializable properties including interface properties
        var properties = GetAllPropertiesIncludingInterfaces(type)
            .Where(p => !Utils.SerializationShouldSkipProperty(p, type))
            .ToList();

        foreach (var property in properties)
        {
            GeneratePropertySchema(sb, property, type);
        }

        sb.AppendLine();
        sb.AppendLine($"            var schema = new EntitySchema(");
        sb.AppendLine($"                ExpectedType: typeof({typeName}),");
        sb.AppendLine($"                Label: \"{label}\",");
        sb.AppendLine($"                IsNullable: {(type.NullableAnnotation == NullableAnnotation.Annotated || (type.CanBeReferencedByName && !type.IsValueType)).ToString().ToLowerInvariant()},");
        sb.AppendLine($"                IsSimple: {(GraphDataModel.IsSimple(type) || GraphDataModel.IsCollectionOfSimple(type)).ToString().ToLowerInvariant()},");
        sb.AppendLine($"                SimpleProperties: simpleProperties,");
        sb.AppendLine($"                ComplexProperties: complexProperties");
        sb.AppendLine($"            );");
        sb.AppendLine();
        sb.AppendLine($"            // Cache the complete schema");
        sb.AppendLine($"            _schemaCache[typeKey] = schema;");
        sb.AppendLine($"            return schema;");
        sb.AppendLine($"        }}");
        sb.AppendLine($"        finally");
        sb.AppendLine($"        {{");
        sb.AppendLine($"            // Remove from processing set");
        sb.AppendLine($"            _currentlyProcessing.Remove(typeKey);");
        sb.AppendLine($"        }}");
        sb.AppendLine("    }");
    }

    private static List<IPropertySymbol> GetAllPropertiesIncludingInterfaces(INamedTypeSymbol type)
    {
        var properties = new List<IPropertySymbol>();
        var seenProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // First, get properties from all implemented interfaces (including IEntity, IRelationship)
        // This ensures we capture the core properties like Id, StartNodeId, EndNodeId, Direction
        foreach (var interfaceType in type.AllInterfaces)
        {
            foreach (var property in interfaceType.GetMembers().OfType<IPropertySymbol>())
            {
                if (ShouldIncludeProperty(property) && seenProperties.Add(property.Name))
                {
                    properties.Add(property);
                }
            }
        }

        // Then get properties from the type hierarchy (base classes and the type itself)
        for (var currentType = type; currentType != null; currentType = currentType.BaseType)
        {
            foreach (var property in currentType.GetMembers().OfType<IPropertySymbol>())
            {
                if (ShouldIncludeProperty(property) && seenProperties.Add(property.Name))
                {
                    properties.Add(property);
                }
            }
        }

        return properties;
    }

    private static bool ShouldIncludeProperty(IPropertySymbol property)
    {
        // Skip if it's not public
        if (property.DeclaredAccessibility != Accessibility.Public)
            return false;

        // Skip if it's an indexer
        if (property.IsIndexer)
            return false;

        // Skip static properties
        if (property.IsStatic)
            return false;

        // Must have a getter
        if (property.GetMethod == null)
            return false;

        // For interface properties, always include them if they're serializable
        // Interface properties define the contract and don't have setters
        if (property.ContainingType.TypeKind == TypeKind.Interface)
        {
            return GraphDataModel.IsSimple(property.Type) ||
                   GraphDataModel.IsCollectionOfSimple(property.Type);
        }

        // For concrete types, they need to be settable (either regular setter or init-only)
        if (property.SetMethod == null && !IsRecordProperty(property))
            return false;

        // Include all serializable properties (simple, complex, and collections)
        return GraphDataModel.IsSimple(property.Type) ||
               GraphDataModel.IsCollectionOfSimple(property.Type) ||
               !GraphDataModel.IsSimple(property.Type) ||
               GraphDataModel.IsCollectionOfComplex(property.Type);
    }

    private static bool IsRecordProperty(IPropertySymbol property)
    {
        // Check if this property is part of a record's primary constructor (init-only)
        return property.SetMethod?.IsInitOnly == true;
    }

    private static void GeneratePropertySchema(StringBuilder sb, IPropertySymbol property, INamedTypeSymbol containingType)
    {
        var propertyType = property.Type;
        var propertyName = Utils.GetPropertyName(property);
        var isNullable = propertyType.NullableAnnotation == NullableAnnotation.Annotated ||
                        (propertyType.CanBeReferencedByName && !propertyType.IsValueType);

        sb.AppendLine($"        // Schema for property: {property.Name}");
        sb.AppendLine("        {");

        // For interface properties, we need to handle the PropertyInfo lookup differently
        if (property.ContainingType.TypeKind == TypeKind.Interface)
        {
            sb.AppendLine($"            var propInfo = typeof({Utils.GetTypeOfName(containingType)}).GetProperty(\"{property.Name}\")");
            sb.AppendLine($"                ?? typeof({property.ContainingType.ToDisplayString()}).GetProperty(\"{property.Name}\")!;");
        }
        else
        {
            sb.AppendLine($"            var propInfo = typeof({Utils.GetTypeOfName(containingType)}).GetProperty(\"{property.Name}\")!;");
        }

        if (GraphDataModel.IsSimple(propertyType))
        {
            sb.AppendLine($"            simpleProperties[\"{propertyName}\"] = new PropertySchema(");
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
                sb.AppendLine($"            simpleProperties[\"{propertyName}\"] = new PropertySchema(");
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
                sb.AppendLine($"            simpleProperties[\"{propertyName}\"] = new PropertySchema(");
                sb.AppendLine("                PropertyInfo: propInfo,");
                sb.AppendLine($"                Neo4jPropertyName: \"{propertyName}\",");
                sb.AppendLine("                PropertyType: PropertyType.Simple,");
                sb.AppendLine($"                IsNullable: {isNullable.ToString().ToLowerInvariant()}");
                sb.AppendLine("            );");
            }
        }
        else if (GraphDataModel.IsCollectionOfComplex(propertyType))
        {
            // Collection of complex types
            var elementType = GraphDataModel.GetCollectionElementType(propertyType);
            if (elementType is INamedTypeSymbol namedElementType)
            {
                var nestedSchemaCall = Utils.GetNestedSchemaCall(elementType);
                sb.AppendLine($"            complexProperties[\"{propertyName}\"] = new PropertySchema(");
                sb.AppendLine("                PropertyInfo: propInfo,");
                sb.AppendLine($"                Neo4jPropertyName: \"{propertyName}\",");
                sb.AppendLine("                PropertyType: PropertyType.ComplexCollection,");
                sb.AppendLine($"                IsNullable: {isNullable.ToString().ToLowerInvariant()},");
                sb.AppendLine($"                NestedSchema: {nestedSchemaCall}");
                sb.AppendLine("            );");
            }
        }
        else
        {
            // Single complex property
            var nestedType = propertyType is INamedTypeSymbol namedType ? namedType : null;
            if (nestedType != null)
            {
                var nestedSchemaCall = Utils.GetNestedSchemaCall(propertyType);
                sb.AppendLine($"            complexProperties[\"{propertyName}\"] = new PropertySchema(");
                sb.AppendLine("                PropertyInfo: propInfo,");
                sb.AppendLine($"                Neo4jPropertyName: \"{propertyName}\",");
                sb.AppendLine("                PropertyType: PropertyType.Complex,");
                sb.AppendLine($"                IsNullable: {isNullable.ToString().ToLowerInvariant()},");
                sb.AppendLine($"                NestedSchema: {nestedSchemaCall}");
                sb.AppendLine("            );");
            }
        }

        sb.AppendLine("        }");
        sb.AppendLine();
    }
}