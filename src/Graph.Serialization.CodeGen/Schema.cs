// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Serialization.CodeGen;

using System.Text;


internal static class Schema
{
    internal static void GenerateSchemaMethod(StringBuilder sb, SerializableTypeModel type)
    {
        var typeName = type.Type.TypeOfName;
        var label = type.Label;
        var uniqueSerializerName = type.SerializerClassName;

        // Circular-schema detection belongs to the current synchronous call chain, not all
        // callers. A process-wide marker makes a concurrent first caller look recursive and
        // returns an empty schema. The shared cache itself must also support concurrent writes.
        sb.AppendLine("    [ThreadStatic]");
        sb.AppendLine("    private static HashSet<Type>? _currentlyProcessing;");
        sb.AppendLine("    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, EntitySchema> _schemaCache = new();");
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
        sb.AppendLine("        var currentlyProcessing = _currentlyProcessing ??= new HashSet<Type>();");
        sb.AppendLine();
        sb.AppendLine($"        // Return cached schema if available");
        sb.AppendLine($"        if (_schemaCache.TryGetValue(typeKey, out var cachedSchema))");
        sb.AppendLine($"            return cachedSchema;");
        sb.AppendLine();
        sb.AppendLine($"        // Check if we're already processing this type (circular reference)");
        sb.AppendLine($"        if (currentlyProcessing.Contains(typeKey))");
        sb.AppendLine($"        {{");
        sb.AppendLine($"            // Return a schema without nested schemas to break the cycle");
        sb.AppendLine($"            return new EntitySchema(");
        sb.AppendLine($"                ExpectedType: typeof({typeName}),");
        sb.AppendLine($"                Label: \"{label}\",");
        sb.AppendLine($"                IsNullable: {type.Type.IsSchemaNullable.ToString().ToLowerInvariant()},");
        sb.AppendLine($"                IsSimple: {(type.Type.IsSimple || type.Type.IsCollectionOfSimple).ToString().ToLowerInvariant()},");
        sb.AppendLine($"                SimpleProperties: new Dictionary<string, PropertySchema>(),");
        sb.AppendLine($"                ComplexProperties: new Dictionary<string, PropertySchema>()");
        sb.AppendLine($"            );");
        sb.AppendLine($"        }}");
        sb.AppendLine();
        sb.AppendLine($"        // Mark this type as being processed");
        sb.AppendLine($"        currentlyProcessing.Add(typeKey);");
        sb.AppendLine();
        sb.AppendLine($"        try");
        sb.AppendLine($"        {{");
        sb.AppendLine($"            // Build the actual schema");
        sb.AppendLine("            var simpleProperties = new Dictionary<string, PropertySchema>();");
        sb.AppendLine("            var complexProperties = new Dictionary<string, PropertySchema>();");

        foreach (var property in type.SchemaProperties.Items)
        {
            GeneratePropertySchema(sb, property, type);
        }

        sb.AppendLine();
        sb.AppendLine($"            var schema = new EntitySchema(");
        sb.AppendLine($"                ExpectedType: typeof({typeName}),");
        sb.AppendLine($"                Label: \"{label}\",");
        sb.AppendLine($"                IsNullable: {type.Type.IsSchemaNullable.ToString().ToLowerInvariant()},");
        sb.AppendLine($"                IsSimple: {(type.Type.IsSimple || type.Type.IsCollectionOfSimple).ToString().ToLowerInvariant()},");
        sb.AppendLine($"                SimpleProperties: simpleProperties,");
        sb.AppendLine($"                ComplexProperties: complexProperties");
        sb.AppendLine($"            );");
        sb.AppendLine();
        sb.AppendLine($"            // Cache the complete schema");
        sb.AppendLine($"            return _schemaCache.GetOrAdd(typeKey, schema);");
        sb.AppendLine($"        }}");
        sb.AppendLine($"        finally");
        sb.AppendLine($"        {{");
        sb.AppendLine($"            // Remove from processing set");
        sb.AppendLine($"            currentlyProcessing.Remove(typeKey);");
        sb.AppendLine($"        }}");
        sb.AppendLine("    }");
    }

    private static void GeneratePropertySchema(
        StringBuilder sb,
        SerializablePropertyModel property,
        SerializableTypeModel containingType)
    {
        var propertyType = property.Type;
        var propertyName = property.Label;
        var isNullable = propertyType.IsNullable;

        sb.AppendLine($"        // Schema for property: {property.Name}");
        sb.AppendLine("        {");

        // For interface properties, we need to handle the PropertyInfo lookup differently
        if (property.ContainingTypeIsInterface)
        {
            sb.AppendLine($"            var propInfo = typeof({containingType.Type.TypeOfName}).GetProperty(\"{property.Name}\")");
            sb.AppendLine($"                ?? typeof({property.ContainingTypeDisplayName}).GetProperty(\"{property.Name}\")!;");
        }
        else
        {
            sb.AppendLine($"            var propInfo = typeof({containingType.Type.TypeOfName}).GetProperty(\"{property.Name}\")!;");
        }

        if (propertyType.IsSimple)
        {
            sb.AppendLine($"            simpleProperties[\"{propertyName}\"] = new PropertySchema(");
            sb.AppendLine("                PropertyInfo: propInfo,");
            sb.AppendLine($"                PropertyName: \"{propertyName}\",");
            sb.AppendLine("                PropertyType: PropertyType.Simple,");
            sb.AppendLine($"                IsNullable: {isNullable.ToString().ToLowerInvariant()}");
            sb.AppendLine("            );");
        }
        else if (propertyType.IsCollectionOfSimple)
        {
            var elementType = propertyType.ElementType;
            if (elementType is not null)
            {
                sb.AppendLine($"            simpleProperties[\"{propertyName}\"] = new PropertySchema(");
                sb.AppendLine("                PropertyInfo: propInfo,");
                sb.AppendLine($"                PropertyName: \"{propertyName}\",");
                sb.AppendLine("                PropertyType: PropertyType.SimpleCollection,");
                sb.AppendLine($"                ElementType: typeof({elementType.TypeOfName}),");
                sb.AppendLine($"                IsNullable: {isNullable.ToString().ToLowerInvariant()}");
                sb.AppendLine($"            ) {{ IsElementNullable = {elementType.IsNullable.ToString().ToLowerInvariant()} }};");
            }
            else
            {
                // Fallback - treat as simple property if we can't determine element type
                sb.AppendLine($"            // Warning: Could not determine element type for collection property {property.Name}");
                sb.AppendLine($"            simpleProperties[\"{propertyName}\"] = new PropertySchema(");
                sb.AppendLine("                PropertyInfo: propInfo,");
                sb.AppendLine($"                PropertyName: \"{propertyName}\",");
                sb.AppendLine("                PropertyType: PropertyType.Simple,");
                sb.AppendLine($"                IsNullable: {isNullable.ToString().ToLowerInvariant()}");
                sb.AppendLine("            );");
            }
        }
        else if (propertyType.IsCollectionOfComplex)
        {
            // Collection of complex types
            var elementType = propertyType.ElementType;
            if (elementType is not null)
            {
                var nestedSchemaCall = GetNestedSchemaCall(elementType);
                sb.AppendLine($"            complexProperties[\"{propertyName}\"] = new PropertySchema(");
                sb.AppendLine("                PropertyInfo: propInfo,");
                sb.AppendLine($"                PropertyName: \"{propertyName}\",");
                sb.AppendLine("                PropertyType: PropertyType.ComplexCollection,");
                sb.AppendLine($"                ElementType: typeof({elementType.TypeOfName}),");
                sb.AppendLine($"                IsNullable: {isNullable.ToString().ToLowerInvariant()},");
                sb.AppendLine($"                NestedSchema: {nestedSchemaCall},");
                sb.AppendLine("                RelationshipType: GraphDataModel.GetComplexPropertyRelationshipType(propInfo)");
                sb.AppendLine("            );");
            }
        }
        else
        {
            // Single complex property
            var nestedSchemaCall = GetNestedSchemaCall(propertyType);
            sb.AppendLine($"            complexProperties[\"{propertyName}\"] = new PropertySchema(");
            sb.AppendLine("                PropertyInfo: propInfo,");
            sb.AppendLine($"                PropertyName: \"{propertyName}\",");
            sb.AppendLine("                PropertyType: PropertyType.Complex,");
            sb.AppendLine($"                IsNullable: {isNullable.ToString().ToLowerInvariant()},");
            sb.AppendLine($"                NestedSchema: {nestedSchemaCall},");
            sb.AppendLine("                RelationshipType: GraphDataModel.GetComplexPropertyRelationshipType(propInfo)");
            sb.AppendLine("            );");
        }

        sb.AppendLine("        }");
        sb.AppendLine();
    }

    private static string GetNestedSchemaCall(TypeReferenceModel nestedType)
    {
        if (string.IsNullOrEmpty(nestedType.GeneratedNamespaceName) ||
            string.IsNullOrEmpty(nestedType.SerializerClassName))
        {
            return "null";
        }

        return $"{nestedType.GeneratedNamespaceName}.{nestedType.SerializerClassName}.GetSchemaStatic()";
    }
}
