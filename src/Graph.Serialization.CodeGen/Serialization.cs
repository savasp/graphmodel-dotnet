// Copyright CVOYA. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.
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
        sb.AppendLine($"            Serialized? serializedValue = null;");
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
                // Reference type or nullable value type - check for null
                sb.AppendLine($"            if (value != null)");
                sb.AppendLine($"            {{");
                sb.AppendLine($"                serializedValue = new SimpleValue(");
                sb.AppendLine($"                    Object: value!,");
                sb.AppendLine($"                    Type: typeof({propertyType.TypeOfName})");
                sb.AppendLine($"                );");
                sb.AppendLine($"            }}");
            }
        }
        else
        {
            // Complex type - recursively serialize to Entity
            sb.AppendLine($"            if (value != null)");
            sb.AppendLine($"            {{");
            sb.AppendLine($"                var complexSerializer = _serializerRegistry.GetSerializer(value.GetType());");
            sb.AppendLine($"                if (complexSerializer != null)");
            sb.AppendLine($"                {{");
            sb.AppendLine($"                    serializedValue = complexSerializer.Serialize(value);");
            sb.AppendLine($"                }}");
            sb.AppendLine($"                else");
            sb.AppendLine($"                {{");
            sb.AppendLine($"                    throw new InvalidOperationException($\"No serializer found for type {{value.GetType().Name}}\");");
            sb.AppendLine($"                }}");
            sb.AppendLine($"            }}");
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
        if (elementType == null) return; // Should not happen for valid collections

        var isElementSimple = elementType.IsSimple;

        sb.AppendLine($"            if (value != null)");
        sb.AppendLine($"            {{");

        if (isElementSimple)
        {
            sb.AppendLine($"                var values = new List<SimpleValue>();");
            sb.AppendLine($"                foreach (var item in value)");
            sb.AppendLine($"                {{");

            // Only add null check for nullable types
            if (elementType.IsNullable)
            {
                sb.AppendLine($"                    if (item != null)");
                sb.AppendLine($"                    {{");
                sb.AppendLine($"                        values.Add(new SimpleValue(");
                sb.AppendLine($"                            Object: item!,");
                sb.AppendLine($"                            Type: typeof({elementType.TypeOfName})");
                sb.AppendLine($"                        ));");
                sb.AppendLine($"                    }}");
            }
            else
            {
                // Value types can't be null, so no null check needed
                sb.AppendLine($"                    values.Add(new SimpleValue(");
                sb.AppendLine($"                        Object: item!,");
                sb.AppendLine($"                        Type: typeof({elementType.TypeOfName})");
                sb.AppendLine($"                    ));");
            }

            sb.AppendLine($"                }}");
            sb.AppendLine($"                serializedValue = new SimpleCollection(");
            sb.AppendLine($"                    Values: values,");
            sb.AppendLine($"                    ElementType: typeof({elementType.TypeOfName})");
            sb.AppendLine($"                );");
        }
        else
        {
            // For complex collections, each item may be a derived type (a base-typed collection with
            // mixed derived instances). Resolve the serializer per item by its own runtime type so the
            // item's own serializer - not the declared element type's - records EntityInfo.ActualType
            // and captures derived-only properties, falling back to the declared element type's
            // serializer only when no serializer is registered for the runtime type.
            sb.AppendLine($"                var entities = new List<EntityInfo>();");
            sb.AppendLine($"                foreach (var item in value)");
            sb.AppendLine($"                {{");

            // Only add null check for reference types
            if (elementType.IsReferenceType || elementType.IsNullable)
            {
                sb.AppendLine($"                    if (item != null)");
                sb.AppendLine($"                    {{");
                sb.AppendLine($"                        var itemSerializer = _serializerRegistry.GetSerializer(item.GetType())");
                sb.AppendLine($"                            ?? _serializerRegistry.GetSerializer(typeof({elementType.TypeOfName}));");
                sb.AppendLine($"                        if (itemSerializer == null)");
                sb.AppendLine($"                        {{");
                sb.AppendLine($"                            throw new InvalidOperationException($\"No serializer found for type {{item.GetType()}}\");");
                sb.AppendLine($"                        }}");
                sb.AppendLine($"                        var serializedItem = itemSerializer.Serialize(item);");
                sb.AppendLine($"                        if (serializedItem is EntityInfo entityItem)");
                sb.AppendLine($"                        {{");
                sb.AppendLine($"                            entities.Add(entityItem);");
                sb.AppendLine($"                        }}");
                sb.AppendLine($"                    }}");
            }
            else
            {
                // Value types can't be null, so no null check needed
                sb.AppendLine($"                    var itemSerializer = _serializerRegistry.GetSerializer(item!.GetType())");
                sb.AppendLine($"                        ?? _serializerRegistry.GetSerializer(typeof({elementType.TypeOfName}));");
                sb.AppendLine($"                    if (itemSerializer == null)");
                sb.AppendLine($"                    {{");
                sb.AppendLine($"                        throw new InvalidOperationException($\"No serializer found for type {{item.GetType()}}\");");
                sb.AppendLine($"                    }}");
                sb.AppendLine($"                    var serializedItem = itemSerializer.Serialize(item);");
                sb.AppendLine($"                    if (serializedItem is EntityInfo entityItem)");
                sb.AppendLine($"                    {{");
                sb.AppendLine($"                        entities.Add(entityItem);");
                sb.AppendLine($"                    }}");
            }

            sb.AppendLine($"                }}");
            sb.AppendLine($"                serializedValue = new EntityCollection(");
            sb.AppendLine($"                    Type: typeof({elementType.TypeOfName}),");
            sb.AppendLine($"                    Entities: entities");
            sb.AppendLine($"                );");
        }

        sb.AppendLine($"            }}");
    }
}
