// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Serialization.CodeGen;

using System.Text;


internal static class Serialization
{
    internal static void GenerateSerializeMethod(StringBuilder sb, SerializableTypeModel type)
    {
        sb.AppendLine($"    public EntityInfo Serialize(object obj)");
        sb.AppendLine("    {");
        sb.AppendLine($"        var entity = ({type.Type.TypeOfName})obj;");
        sb.AppendLine("        var simpleProperties = new Dictionary<string, Property>();");
        sb.AppendLine("        var complexProperties = new Dictionary<string, Property>();");
        sb.AppendLine();

        foreach (var property in type.SerializationProperties.Items)
        {
            sb.AppendLine($"        // Serialize property: {property.Name}");

            // Generate property representation creation
            GenerateIntermediateRepresentationCreation(sb, property, property.Type, property.Label);
        }

        if (type.Kind == SerializableTypeKind.Node)
        {
            // For nodes, populate ActualLabels from the Labels property
            sb.AppendLine($"        var actualLabels = entity.Labels?.ToList() ?? new List<string>();");
            sb.AppendLine($"        var primaryLabel = actualLabels.Count > 0 ? actualLabels[0] : \"{type.Label}\";");
            sb.AppendLine($"        return new EntityInfo(");
            sb.AppendLine($"            ActualType: typeof({type.Type.TypeOfName}),");
            sb.AppendLine($"            Label: primaryLabel,");
            sb.AppendLine($"            ActualLabels: actualLabels,");
            sb.AppendLine($"            SimpleProperties: simpleProperties,");
            sb.AppendLine($"            ComplexProperties: complexProperties");
            sb.AppendLine($"        );");
        }
        else if (type.Kind == SerializableTypeKind.Relationship)
        {
            // For relationships, use the Type property as the label
            sb.AppendLine($"        var relationshipType = !string.IsNullOrEmpty(entity.Type) ? entity.Type : \"{type.Label}\";");
            sb.AppendLine($"        return new EntityInfo(");
            sb.AppendLine($"            ActualType: typeof({type.Type.TypeOfName}),");
            sb.AppendLine($"            Label: relationshipType,");
            sb.AppendLine($"            ActualLabels: new List<string>(),");
            sb.AppendLine($"            SimpleProperties: simpleProperties,");
            sb.AppendLine($"            ComplexProperties: complexProperties");
            sb.AppendLine($"        );");
        }
        else
        {
            // For other types (complex properties), use the type name as label
            sb.AppendLine($"        return new EntityInfo(");
            sb.AppendLine($"            ActualType: typeof({type.Type.TypeOfName}),");
            sb.AppendLine($"            Label: \"{type.Label}\",");
            sb.AppendLine($"            ActualLabels: new List<string>(),");
            sb.AppendLine($"            SimpleProperties: simpleProperties,");
            sb.AppendLine($"            ComplexProperties: complexProperties");
            sb.AppendLine($"        );");
        }
        sb.AppendLine("    }");
    }

    private static void GenerateIntermediateRepresentationCreation(
        StringBuilder sb,
        SerializablePropertyModel property,
        TypeReferenceModel propertyType,
        string propertyName)
    {
        var isSimple = propertyType.IsSimple;
        var isCollection = propertyType.IsCollectionOfSimple || propertyType.IsCollectionOfComplex;
        var isNullable = propertyType.IsNullable;

        sb.AppendLine($"        {{");
        sb.AppendLine($"            var propInfo = typeof({property.ContainingTypeDisplayName}).GetProperty(\"{property.Name}\")!;");
        sb.AppendLine($"            var value = entity.{property.Name};");
        sb.AppendLine($"            Serialized? serializedValue;");
        sb.AppendLine();

        if (isCollection)
        {
            GenerateCollectionSerialization(sb, propertyType);
        }
        else if (isSimple)
        {
            // For value types that can't be null, always serialize. For reference/nullable types, check for null
            if (propertyType.IsValueType && !propertyType.IsNullable)
            {
                // Non-nullable value type - always serialize
                sb.AppendLine($"            serializedValue = new SimpleValue(");
                sb.AppendLine($"                Object: value!,");
                sb.AppendLine($"                Type: typeof({propertyType.TypeOfName})");
                sb.AppendLine($"            );");
            }
            else
            {
                // Reference type or nullable value type - preserve null values.
                sb.AppendLine($"            serializedValue = value is null");
                sb.AppendLine($"                ? null");
                sb.AppendLine($"                : new SimpleValue(");
                sb.AppendLine($"                    Object: value,");
                sb.AppendLine($"                    Type: typeof({propertyType.TypeOfName})");
                sb.AppendLine($"                );");
            }
        }
        else if (propertyType.IsValueType && !propertyType.IsNullable)
        {
            // Non-nullable value-type complex property (e.g. a struct). A `value is null` test is a
            // compile error (CS8121) here, so serialize unconditionally.
            sb.AppendLine($"            serializedValue = (_serializerRegistry.GetSerializer(value.GetType())");
            sb.AppendLine($"                ?? throw new InvalidOperationException($\"No serializer found for type {{value.GetType().Name}}\"))");
            sb.AppendLine($"                .Serialize(value);");
        }
        else
        {
            // Complex type - recursively serialize to Entity.
            sb.AppendLine($"            serializedValue = value is null");
            sb.AppendLine($"                ? null");
            sb.AppendLine($"                : (_serializerRegistry.GetSerializer(value.GetType())");
            sb.AppendLine($"                    ?? throw new InvalidOperationException($\"No serializer found for type {{value.GetType().Name}}\"))");
            sb.AppendLine($"                    .Serialize(value);");
        }

        // Add to appropriate dictionary
        var dictionaryName = isSimple || (isCollection && propertyType.IsCollectionOfSimple) ? "simpleProperties" : "complexProperties";
        sb.AppendLine();
        sb.AppendLine($"            var propertyRep = new Property(");
        sb.AppendLine($"                PropertyInfo: propInfo,");
        sb.AppendLine($"                Label: \"{propertyName}\",");
        sb.AppendLine($"                IsNullable: {isNullable.ToString().ToLower()},");
        var isComplex = !isSimple && (!isCollection || propertyType.IsCollectionOfComplex);
        sb.AppendLine($"                Value: serializedValue{(isComplex ? "," : string.Empty)}");
        if (isComplex)
        {
            sb.AppendLine("                RelationshipType: GraphDataModel.GetComplexPropertyRelationshipType(propInfo)");
        }
        sb.AppendLine($"            );");
        sb.AppendLine($"            {dictionaryName}[\"{propertyName}\"] = propertyRep;");
        sb.AppendLine($"        }}");
        sb.AppendLine();
    }

    private static void GenerateCollectionSerialization(StringBuilder sb, TypeReferenceModel collectionType)
    {
        var elementType = collectionType.ElementType;
        if (elementType == null)
        {
            sb.AppendLine("            serializedValue = null;");
            return;
        }

        var isElementSimple = elementType.IsSimple;

        if (isElementSimple)
        {
            sb.AppendLine("            serializedValue = value is null");
            sb.AppendLine("                ? null");
            sb.AppendLine("                : new SimpleCollection(");
            sb.AppendLine("                    Values: value");
            sb.AppendLine($"                        .Select(item => new SimpleValue(item!, typeof({elementType.TypeOfName})))");
            sb.AppendLine("                        .ToList(),");
            sb.AppendLine($"                    ElementType: typeof({elementType.TypeOfName})");
            sb.AppendLine("                );");
        }
        else
        {
            // For complex collections, each item may be a derived type (a base-typed collection with
            // mixed derived instances). Resolve the serializer per item by its own runtime type so the
            // item's own serializer - not the declared element type's - records EntityInfo.ActualType
            // and captures derived-only properties, falling back to the declared element type's
            // serializer only when no serializer is registered for the runtime type.
            sb.AppendLine("            serializedValue = value is null");
            sb.AppendLine("                ? null");
            sb.AppendLine("                : new EntityCollection(");
            sb.AppendLine($"                    Type: typeof({elementType.TypeOfName}),");
            sb.AppendLine("                    Entities: value");

            if (elementType.IsReferenceType || elementType.IsNullable)
            {
                sb.AppendLine("                        .Where(item => item is not null)");
            }

            sb.AppendLine("                        .Select(item =>");
            sb.AppendLine("                            (_serializerRegistry.GetSerializer(item!.GetType())");
            sb.AppendLine($"                                ?? _serializerRegistry.GetSerializer(typeof({elementType.TypeOfName}))");
            sb.AppendLine("                                ?? throw new InvalidOperationException($\"No serializer found for type {item.GetType()}\"))");
            sb.AppendLine("                                .Serialize(item))");
            sb.AppendLine("                        .ToList()");
            sb.AppendLine("                );");
        }
    }
}
