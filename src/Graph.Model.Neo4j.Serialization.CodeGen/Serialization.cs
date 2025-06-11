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

namespace Cvoya.Graph.Model.Neo4j.Serialization.CodeGen;

internal static class Serialization
{
    internal static void GenerateSerializeMethod(StringBuilder sb, INamedTypeSymbol type)
    {
        sb.AppendLine($"    public override Dictionary<string, IntermediateRepresentation> Serialize(object obj)");
        sb.AppendLine("    {");
        sb.AppendLine($"        var entity = ({type.ToDisplayString()})obj;");
        sb.AppendLine("        var result = new Dictionary<string, IntermediateRepresentation>();");
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

            // Generate IntermediateRepresentation creation
            GenerateIntermediateRepresentationCreation(sb, property, propertyType, propertyName);
        }

        sb.AppendLine("        return result;");
        sb.AppendLine("    }");
    }

    private static void GenerateIntermediateRepresentationCreation(StringBuilder sb, IPropertySymbol property, ITypeSymbol propertyType, string propertyName)
    {
        var isSimple = Cvoya.Graph.Model.Neo4j.Serialization.CodeGen.GraphDataModel.IsSimple(propertyType);
        var isCollection = Cvoya.Graph.Model.Neo4j.Serialization.CodeGen.GraphDataModel.IsCollectionOfSimple(propertyType) || GraphDataModel.IsCollectionOfComplex(propertyType);
        var isNullable = propertyType.NullableAnnotation == NullableAnnotation.Annotated;

        // Get collection element type if it's a collection
        var collectionElementType = GraphDataModel.GetCollectionElementType(propertyType)!;

        sb.AppendLine($"        {{");
        sb.AppendLine($"            var propInfo = typeof({property.ContainingType.ToDisplayString()}).GetProperty(\"{property.Name}\")!;");
        sb.AppendLine($"            var value = entity.{property.Name};");
        sb.AppendLine($"            object? serializedValue = null;");
        sb.AppendLine();

        if (isCollection)
        {
            GenerateCollectionSerialization(sb, collectionElementType);
        }
        else if (isSimple)
        {
            sb.AppendLine($"            serializedValue = EntitySerializerBase.ConvertToNeo4jValue(value);");
        }
        else
        {
            // Complex type - recursively serialize to Dictionary<string, IntermediateRepresentation>
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

        sb.AppendLine();
        sb.AppendLine($"            result[\"{propertyName}\"] = new IntermediateRepresentation(");
        sb.AppendLine($"                PropertyInfo: propInfo,");
        sb.AppendLine($"                IsSimple: {isSimple.ToString().ToLower()},");
        sb.AppendLine($"                IsNullable: {isNullable.ToString().ToLower()},");
        sb.AppendLine($"                IsCollection: {isCollection.ToString().ToLower()},");

        if (collectionElementType != null)
        {
            sb.AppendLine($"                CollectionElementType: typeof({GetTypeOfName(collectionElementType)}),");
            sb.AppendLine($"                IsCollectionOfSimple: {Cvoya.Graph.Model.Neo4j.Serialization.CodeGen.GraphDataModel.IsSimple(collectionElementType).ToString().ToLower()},");
        }
        else
        {
            sb.AppendLine($"                CollectionElementType: null,");
        }

        sb.AppendLine($"                Value: serializedValue");
        sb.AppendLine($"            );");
        sb.AppendLine($"        }}");
        sb.AppendLine();
    }

    private static void GenerateCollectionSerialization(StringBuilder sb, ITypeSymbol elementType)
    {
        var isElementSimple = GraphDataModel.IsSimple(elementType);

        sb.AppendLine($"            if (value != null)");
        sb.AppendLine($"            {{");
        sb.AppendLine($"                var collection = new List<object?>();");

        if (isElementSimple)
        {
            // For simple collections, just convert each element without needing a serializer
            sb.AppendLine($"                foreach (var item in value)");
            sb.AppendLine($"                {{");
            sb.AppendLine($"                    collection.Add(EntitySerializerBase.ConvertToNeo4jValue(item));");
            sb.AppendLine($"                }}");
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
            sb.AppendLine($"                foreach (var item in value)");
            sb.AppendLine($"                {{");
            sb.AppendLine($"                    if (item != null)");
            sb.AppendLine($"                    {{");
            sb.AppendLine($"                        collection.Add(itemSerializer.Serialize(item));");
            sb.AppendLine($"                    }}");
            sb.AppendLine($"                    else");
            sb.AppendLine($"                    {{");
            sb.AppendLine($"                        collection.Add(null);");
            sb.AppendLine($"                    }}");
            sb.AppendLine($"                }}");
        }

        sb.AppendLine($"                serializedValue = collection;");
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