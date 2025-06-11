// Copyright 2025 Savas Parastatidis
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance w            sb.AppendLine($"                            entities.Add(entityItem)");th the License.
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

namespace Cvoya.Graph.Model.Neo4j.Serialization.CodeGen;

internal static class Serialization
{
    internal static void GenerateSerializeMethod(StringBuilder sb, INamedTypeSymbol type)
    {
        sb.AppendLine($"    public override Entity Serialize(object obj)");
        sb.AppendLine("    {");
        sb.AppendLine($"        var entity = ({GetTypeOfName(type)})obj;");
        sb.AppendLine("        var simpleProperties = new Dictionary<string, PropertyRepresentation>();");
        sb.AppendLine("        var complexProperties = new Dictionary<string, PropertyRepresentation>();");
        sb.AppendLine();

        var properties = Utils.GetAllProperties(type);

        foreach (var property in properties)
        {
            // Skip properties that shouldn't be serialized
            if (Utils.SerializationShouldSkipProperty(property, type))
                continue;

            var propertyName = Utils.GetPropertyName(property);
            var propertyType = property.Type;

            sb.AppendLine($"        // Serialize property: {property.Name}");

            // Generate property representation creation
            GenerateIntermediateRepresentationCreation(sb, property, propertyType, propertyName);
        }

        sb.AppendLine($"        return new Entity(");
        sb.AppendLine($"            Type: typeof({GetTypeOfName(type)}),");
        sb.AppendLine($"            Label: \"{Utils.GetLabelFromType(type)}\",");
        sb.AppendLine($"            SimpleProperties: simpleProperties,");
        sb.AppendLine($"            ComplexProperties: complexProperties");
        sb.AppendLine($"        );");
        sb.AppendLine("    }");
    }

    private static void GenerateIntermediateRepresentationCreation(StringBuilder sb, IPropertySymbol property, ITypeSymbol propertyType, string propertyName)
    {
        var isSimple = GraphDataModel.IsSimple(propertyType);
        var isCollection = GraphDataModel.IsCollectionOfSimple(propertyType) || GraphDataModel.IsCollectionOfComplex(propertyType);
        var isNullable = propertyType.NullableAnnotation == NullableAnnotation.Annotated;

        sb.AppendLine($"        {{");
        sb.AppendLine($"            var propInfo = typeof({property.ContainingType.ToDisplayString()}).GetProperty(\"{property.Name}\")!;");
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
            if (propertyType.IsValueType && propertyType.NullableAnnotation != NullableAnnotation.Annotated)
            {
                // Non-nullable value type - always serialize
                sb.AppendLine($"            serializedValue = new SimpleValue(");
                sb.AppendLine($"                Object: EntitySerializerBase.ConvertToNeo4jValue(value)!,");
                sb.AppendLine($"                Type: typeof({GetTypeOfName(propertyType)})");
                sb.AppendLine($"            );");
            }
            else
            {
                // Reference type or nullable value type - check for null
                sb.AppendLine($"            if (value != null)");
                sb.AppendLine($"            {{");
                sb.AppendLine($"                serializedValue = new SimpleValue(");
                sb.AppendLine($"                    Object: EntitySerializerBase.ConvertToNeo4jValue(value)!,");
                sb.AppendLine($"                    Type: typeof({GetTypeOfName(propertyType)})");
                sb.AppendLine($"                );");
                sb.AppendLine($"            }}");
            }
        }
        else
        {
            // Complex type - recursively serialize to Entity
            sb.AppendLine($"            if (value != null)");
            sb.AppendLine($"            {{");
            sb.AppendLine($"                var complexSerializer = EntitySerializerRegistry.GetSerializer(value.GetType());");
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
        var dictionaryName = isSimple || (isCollection && GraphDataModel.IsCollectionOfSimple(propertyType)) ? "simpleProperties" : "complexProperties";
        sb.AppendLine();
        sb.AppendLine($"            var propertyRep = new PropertyRepresentation(");
        sb.AppendLine($"                PropertyInfo: propInfo,");
        sb.AppendLine($"                Label: \"{propertyName}\",");
        sb.AppendLine($"                IsNullable: {isNullable.ToString().ToLower()},");
        sb.AppendLine($"                Value: serializedValue");
        sb.AppendLine($"            );");
        sb.AppendLine($"            {dictionaryName}[\"{propertyName}\"] = propertyRep;");
        sb.AppendLine($"        }}");
        sb.AppendLine();
    }

    private static void GenerateCollectionSerialization(StringBuilder sb, ITypeSymbol collectionType)
    {
        var elementType = GraphDataModel.GetCollectionElementType(collectionType);
        if (elementType == null) return; // Should not happen for valid collections

        var isElementSimple = GraphDataModel.IsSimple(elementType);

        sb.AppendLine($"            if (value != null)");
        sb.AppendLine($"            {{");

        if (isElementSimple)
        {
            sb.AppendLine($"                var values = new List<SimpleValue>();");
            sb.AppendLine($"                foreach (var item in value)");
            sb.AppendLine($"                {{");

            // Only add null check for reference types
            if (elementType.IsReferenceType || elementType.NullableAnnotation == NullableAnnotation.Annotated)
            {
                sb.AppendLine($"                    if (item != null)");
                sb.AppendLine($"                    {{");
                sb.AppendLine($"                        values.Add(new SimpleValue(");
                sb.AppendLine($"                            Object: EntitySerializerBase.ConvertToNeo4jValue(item)!,");
                sb.AppendLine($"                            Type: typeof({GetTypeOfName(elementType)})");
                sb.AppendLine($"                        ));");
                sb.AppendLine($"                    }}");
            }
            else
            {
                // Value types can't be null, so no null check needed
                sb.AppendLine($"                    values.Add(new SimpleValue(");
                sb.AppendLine($"                        Object: EntitySerializerBase.ConvertToNeo4jValue(item)!,");
                sb.AppendLine($"                        Type: typeof({GetTypeOfName(elementType)})");
                sb.AppendLine($"                    ));");
            }

            sb.AppendLine($"                }}");
            sb.AppendLine($"                serializedValue = new SimpleCollection(");
            sb.AppendLine($"                    Values: values,");
            sb.AppendLine($"                    ElementType: typeof({GetTypeOfName(elementType)})");
            sb.AppendLine($"                );");
        }
        else
        {
            // For complex collections, get the serializer once before the loop since all items have the same type
            sb.AppendLine($"                var itemSerializer = EntitySerializerRegistry.GetSerializer(typeof({GetTypeOfName(elementType)}));");
            sb.AppendLine($"                if (itemSerializer == null)");
            sb.AppendLine($"                {{");
            sb.AppendLine($"                    throw new InvalidOperationException($\"No serializer found for type {GetTypeOfName(elementType)}\");");
            sb.AppendLine($"                }}");
            sb.AppendLine();
            sb.AppendLine($"                var entities = new List<Entity>();");
            sb.AppendLine($"                foreach (var item in value)");
            sb.AppendLine($"                {{");

            // Only add null check for reference types
            if (elementType.IsReferenceType || elementType.NullableAnnotation == NullableAnnotation.Annotated)
            {
                sb.AppendLine($"                    if (item != null)");
                sb.AppendLine($"                    {{");
                sb.AppendLine($"                        var serializedItem = itemSerializer.Serialize(item);");
                sb.AppendLine($"                        if (serializedItem is Entity entityItem)");
                sb.AppendLine($"                        {{");
                sb.AppendLine($"                            entities.Add(entityItem);");
                sb.AppendLine($"                        }}");
                sb.AppendLine($"                    }}");
            }
            else
            {
                // Value types can't be null, so no null check needed
                sb.AppendLine($"                    var serializedItem = itemSerializer.Serialize(item);");
                sb.AppendLine($"                    if (serializedItem is Entity entityItem)");
                sb.AppendLine($"                    {{");
                sb.AppendLine($"                        entities.Add(entityItem);");
                sb.AppendLine($"                    }}");
            }

            sb.AppendLine($"                }}");
            sb.AppendLine($"                serializedValue = new EntityCollection(");
            sb.AppendLine($"                    Type: typeof({GetTypeOfName(elementType)}),");
            sb.AppendLine($"                    Entities: entities");
            sb.AppendLine($"                );");
        }

        sb.AppendLine($"            }}");
    }

    /// <summary>
    /// Gets the type name suitable for typeof() expressions, removing nullable reference type annotations
    /// </summary>
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