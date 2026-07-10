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
using Microsoft.CodeAnalysis;


internal static class Deserialization
{
    internal static void GenerateDeserializeMethod(StringBuilder sb, SerializableTypeModel type)
    {
        sb.AppendLine("    public object Deserialize(EntityInfo entity)");
        sb.AppendLine("    {");

        var allProperties = type.DeserializationProperties.Items.ToList();

        var settableProperties = allProperties
            .Where(p => p.HasSetter &&
                       !p.SetterIsInitOnly &&
                       p.SetterDeclaredPublic)
            .ToList();

        if (!type.NeedsConstructor && type.HasParameterlessPublicConstructor)
        {
            // Simple case - parameterless constructor and all properties are settable
            sb.AppendLine($"        var result = new {type.Type.TypeOfName}();");
            GeneratePropertySetters(sb, settableProperties, "result", "entity");
        }
        else if (type.Constructor is not null)
        {
            GenerateConstructorBasedDeserialization(sb, type, type.Constructor, allProperties);
        }
        else
        {
            sb.AppendLine($"        throw new InvalidOperationException(\"No suitable constructor found for type {type.Name}\");");
        }

        sb.AppendLine("        return result;");
        sb.AppendLine("    }");
    }

    private static void GenerateConstructorBasedDeserialization(
        StringBuilder sb,
        SerializableTypeModel type,
        ConstructorModel constructor,
        List<SerializablePropertyModel> allProperties)
    {
        // Extract values for each constructor parameter (including Direction if it's part of the constructor)
        foreach (var parameter in constructor.Parameters.Items)
        {
            GenerateConstructorParameterExtraction(sb, parameter, "entity");
        }

        // Find properties that need to be set via object initializer (only init-only properties like Id)
        var handledByConstructor = new HashSet<string>(constructor.Parameters.Items
            .Select(p => p.Name)
            .Where(name => !string.IsNullOrEmpty(name)), StringComparer.OrdinalIgnoreCase);

        var initOnlyProperties = allProperties
            .Where(p => !handledByConstructor.Contains(p.Name))
            .Where(p => p.SetterIsInitOnly)
            .ToList();

        var settableProperties = allProperties
            .Where(p => !handledByConstructor.Contains(p.Name))
            .Where(p => p.HasSetter &&
                !p.SetterIsInitOnly &&
                p.SetterDeclaredPublic)
            .ToList();

        // Generate init-only property extractions (like Id)
        foreach (var property in initOnlyProperties)
        {
            GeneratePropertyExtraction(sb, property, "entity");
        }

        // Generate constructor call with object initializer for init-only properties
        sb.AppendLine();

        if (initOnlyProperties.Any())
        {
            sb.AppendLine($"        var result = new {type.Type.TypeOfName}(");

            var paramNames = constructor.Parameters.Items.Select(p => p.Name).ToList();
            for (int i = 0; i < paramNames.Count; i++)
            {
                var comma = i < paramNames.Count - 1 ? "," : "";
                sb.AppendLine($"            {paramNames[i]}{comma}");
            }

            sb.AppendLine("        )");
            sb.AppendLine("        {");

            // Add init-only properties to object initializer
            for (int i = 0; i < initOnlyProperties.Count; i++)
            {
                var property = initOnlyProperties[i];
                var comma = i < initOnlyProperties.Count - 1 ? "," : "";
                sb.AppendLine($"            {property.Name} = {property.Name.ToLowerInvariant()}Value{comma}");
            }

            sb.AppendLine("        };");
        }
        else
        {
            sb.AppendLine($"        var result = new {type.Type.TypeOfName}(");

            var paramNames = constructor.Parameters.Items.Select(p => p.Name).ToList();
            for (int i = 0; i < paramNames.Count; i++)
            {
                var comma = i < paramNames.Count - 1 ? "," : "";
                sb.AppendLine($"            {paramNames[i]}{comma}");
            }

            sb.AppendLine("        );");
        }

        sb.AppendLine();

        // Set remaining properties with regular setters
        if (settableProperties.Any())
        {
            sb.AppendLine("        // Set remaining properties with setters");
            GeneratePropertySetters(sb, settableProperties, "result", "entity");
        }
    }

    private static void GeneratePropertyExtraction(StringBuilder sb, SerializablePropertyModel property, string entityVar)
    {
        var propertyName = property.Label;
        var propName = property.Name;
        var propertyType = property.Type.DisplayName;
        var variableName = $"{propName.ToLowerInvariant()}Value";

        // Use property-specific variable names to avoid conflicts
        var propRepVar = $"{propName.ToLowerInvariant()}PropRep";
        var simplePropVar = $"{propName.ToLowerInvariant()}SimpleProp";
        var complexPropVar = $"{propName.ToLowerInvariant()}ComplexProp";

        sb.AppendLine($"        // Extracting init-only property '{propName}'");
        sb.AppendLine($"        {propertyType} {variableName};");
        sb.AppendLine($"        // Look for property '{propertyName}' in both simple and complex properties");
        sb.AppendLine($"        var {propRepVar} = {entityVar}.SimpleProperties.TryGetValue(\"{propertyName}\", out var {simplePropVar}) ? {simplePropVar}");
        sb.AppendLine($"                    : {entityVar}.ComplexProperties.TryGetValue(\"{propertyName}\", out var {complexPropVar}) ? {complexPropVar} : null;");
        sb.AppendLine($"        if ({propRepVar} != null)");
        sb.AppendLine("        {");

        GenerateValueExtraction(sb, property.Type, variableName, propertyName, propRepVar, 12);

        sb.AppendLine("        }");
        sb.AppendLine("        else");
        sb.AppendLine("        {");

        // Handle missing init-only properties with proper defaults
        var defaultValue = GetDefaultValueForProperty(propName, property.Type, out var shouldThrow);

        // Check if the property type is nullable
        if (property.Type.IsNullable)
        {
            // For nullable types, set to null if no value is provided
            sb.AppendLine($"            {variableName} = null;");
        }
        else if (shouldThrow)
        {
            sb.AppendLine($"            throw new InvalidOperationException(\"No sensible default value for property '{propertyName}' of type '{property.Type.DisplayName}'.\");");
        }
        else
        {
            sb.AppendLine($"            {variableName} = {defaultValue};");
        }

        sb.AppendLine("        }");
    }

    private static string GetDefaultValueForProperty(string propertyName, TypeReferenceModel propertyType, out bool shouldThrow)
    {
        // Handle known interface properties with sensible defaults
        shouldThrow = false;
        return propertyName.ToLowerInvariant() switch
        {
            "direction" when propertyType.Name.Contains("RelationshipDirection") => "RelationshipDirection.Outgoing",
            "id" when propertyType.SpecialType == SpecialType.System_String => "Guid.NewGuid().ToString(\"N\")",
            _ => GetTypeDefault(propertyType, out shouldThrow)
        };
    }

    private static void GenerateConstructorParameterExtraction(StringBuilder sb, ParameterModel parameter, string entityVar)
    {
        var paramName = parameter.Name;
        var paramType = parameter.Type.DisplayName;

        // Use parameter-specific variable names to avoid conflicts
        var propRepVar = $"{paramName.ToLowerInvariant()}PropRep";
        var simplePropVar = $"{paramName.ToLowerInvariant()}SimpleProp";
        var complexPropVar = $"{paramName.ToLowerInvariant()}ComplexProp";

        sb.AppendLine($"        // Extracting value for parameter '{paramName}'");
        sb.AppendLine($"        {paramType} {paramName};");

        var propertyName = parameter.PropertyName;

        sb.AppendLine($"        // Look for property '{propertyName}' in both simple and complex properties");
        sb.AppendLine($"        var {propRepVar} = {entityVar}.SimpleProperties.TryGetValue(\"{propertyName}\", out var {simplePropVar}) ? {simplePropVar}");
        sb.AppendLine($"                    : {entityVar}.ComplexProperties.TryGetValue(\"{propertyName}\", out var {complexPropVar}) ? {complexPropVar} : null;");
        sb.AppendLine($"        if ({propRepVar} != null)");
        sb.AppendLine("        {");

        GenerateValueExtraction(sb, parameter.Type, paramName, propertyName, propRepVar, 12);

        sb.AppendLine("        }");
        sb.AppendLine("        else");
        sb.AppendLine("        {");

        // Handle missing parameters with proper defaults
        if (parameter.HasExplicitDefaultValue)
        {
            sb.AppendLine($"            {paramName} = {parameter.ExplicitDefaultValueExpression};");
        }
        else
        {
            // For required properties, use sensible defaults based on the property name and type
            var defaultValue = GetDefaultValueForParameter(paramName, parameter.Type, out var shouldThrow);

            // Check if the parameter type is nullable
            if (parameter.Type.IsNullable)
            {
                // For nullable types, set to null if no value is provided
                sb.AppendLine($"            {paramName} = null;");
            }
            else if (shouldThrow)
            {
                sb.AppendLine($"            throw new InvalidOperationException(\"No sensible default value for parameter '{paramName}' of type '{parameter.Type.DisplayName}'.\");");
            }
            else
            {
                sb.AppendLine($"            {paramName} = {defaultValue};");
            }
        }

        sb.AppendLine("        }");
    }

    private static void GeneratePropertySetters(
        StringBuilder sb,
        List<SerializablePropertyModel> properties,
        string variableName,
        string entityVar)
    {
        foreach (var property in properties)
        {
            var propertyName = property.Label;
            var propName = property.Name;

            // Use property-specific variable names to avoid conflicts
            var propRepVar = $"{propName.ToLowerInvariant()}PropRep";
            var simplePropVar = $"{propName.ToLowerInvariant()}SimpleProp";
            var complexPropVar = $"{propName.ToLowerInvariant()}ComplexProp";

            sb.AppendLine($"        // Look for property '{propertyName}' in both simple and complex properties");
            sb.AppendLine($"        var {propRepVar} = {entityVar}.SimpleProperties.TryGetValue(\"{propertyName}\", out var {simplePropVar}) ? {simplePropVar}");
            sb.AppendLine($"                    : {entityVar}.ComplexProperties.TryGetValue(\"{propertyName}\", out var {complexPropVar}) ? {complexPropVar} : null;");
            sb.AppendLine($"        if ({propRepVar} != null)");
            sb.AppendLine("        {");

            GenerateValueExtraction(sb, property.Type, $"{variableName}.{propName}", propertyName, propRepVar, 12);

            sb.AppendLine("        }");
            sb.AppendLine("        else");
            sb.AppendLine("        {");

            // Handle missing properties
            if (property.Type.IsNullable)
            {
                sb.AppendLine($"            {variableName}.{propName} = null;");
            }
            else
            {
                var defaultValue = GetDefaultValueForProperty(propertyName, property.Type, out var shouldThrow);
                if (shouldThrow)
                {
                    sb.AppendLine($"            throw new InvalidOperationException(\"No sensible default value for property '{propertyName}' of type '{property.Type.DisplayName}'.\");");
                }
                else
                {
                    sb.AppendLine($"            {variableName}.{propName} = {defaultValue};");
                }
            }

            sb.AppendLine("        }");
        }
    }

    private static void GenerateValueExtraction(
        StringBuilder sb,
        TypeReferenceModel targetType,
        string variableName,
        string propertyLabel,
        string propRepVarName,
        int indent)
    {
        var indentStr = new string(' ', indent);

        if (targetType.IsCollectionOfSimple || targetType.IsCollectionOfComplex)
        {
            GenerateCollectionDeserialization(sb, targetType, variableName, propertyLabel, propRepVarName, indent);
        }
        else if (targetType.IsSimple)
        {
            sb.AppendLine($"{indentStr}// {GetSimpleValueExtractionComment(targetType)}");
            sb.AppendLine($"{indentStr}if ({propRepVarName}.Value is SimpleValue simpleValue)");
            sb.AppendLine($"{indentStr}{{");
            GenerateSimpleValueAssignment(sb, targetType, variableName, "simpleValue.Object", indent + 4);
            sb.AppendLine($"{indentStr}}}");
            sb.AppendLine($"{indentStr}else");
            sb.AppendLine($"{indentStr}{{");

            if (targetType.IsNullable)
            {
                sb.AppendLine($"{indentStr}    {variableName} = null;");
            }
            else
            {
                // For non-nullable types, use sensible defaults
                var defaultValue = GetDefaultValueForProperty(propertyLabel, targetType, out var shouldThrow);
                if (shouldThrow)
                {
                    sb.AppendLine($"{indentStr}    throw new InvalidOperationException(\"No sensible default value for property '{propertyLabel}' of type '{targetType.DisplayName}'.\");");
                }
                else
                {
                    sb.AppendLine($"{indentStr}    {variableName} = {defaultValue};");
                }
            }

            sb.AppendLine($"{indentStr}}}");
        }
        else
        {
            // Complex type handling - delegate to other serializers, resolved by the serialized
            // entity's ActualType (same dispatch mechanism used for top-level Node/Relationship
            // polymorphism and for complex collections), falling back to the statically-declared
            // type only when no serializer is registered for ActualType (e.g. legacy data).
            sb.AppendLine($"{indentStr}// Extract complex value using registered serializer");
            sb.AppendLine($"{indentStr}if ({propRepVarName}.Value is EntityInfo complexEntity)");
            sb.AppendLine($"{indentStr}{{");
            sb.AppendLine($"{indentStr}    var complexSerializer = _serializerRegistry.GetSerializer(complexEntity.ActualType)");
            sb.AppendLine($"{indentStr}        ?? _serializerRegistry.GetSerializer(typeof({targetType.TypeOfName}));");
            sb.AppendLine($"{indentStr}    if (complexSerializer != null)");
            sb.AppendLine($"{indentStr}    {{");
            sb.AppendLine($"{indentStr}        {variableName} = ({targetType.DisplayName})complexSerializer.Deserialize(complexEntity);");
            sb.AppendLine($"{indentStr}    }}");
            sb.AppendLine($"{indentStr}    else");
            sb.AppendLine($"{indentStr}    {{");
            sb.AppendLine($"{indentStr}        throw new InvalidOperationException($\"No serializer found for type {{complexEntity.ActualType}}\");");
            sb.AppendLine($"{indentStr}    }}");
            sb.AppendLine($"{indentStr}}}");
            sb.AppendLine($"{indentStr}else");
            sb.AppendLine($"{indentStr}{{");

            if (targetType.IsNullable)
            {
                sb.AppendLine($"{indentStr}    {variableName} = null;");
            }
            else
            {
                // For non-nullable complex types, we can't create a sensible default
                // so we should throw an exception
                sb.AppendLine($"{indentStr}    throw new InvalidOperationException(\"Required complex property '{propertyLabel}' is missing or null\");");
            }

            sb.AppendLine($"{indentStr}}}");
        }
    }

    private static void GenerateCollectionDeserialization(
        StringBuilder sb,
        TypeReferenceModel collectionType,
        string variableName,
        string propertyLabel,
        string propRepVarName,
        int indent)
    {
        var indentStr = new string(' ', indent);
        var elementType = collectionType.ElementType;

        if (elementType == null)
        {
            sb.AppendLine($"{indentStr}// Warning: Could not determine element type for collection");
            sb.AppendLine($"{indentStr}{variableName} = default({collectionType.DisplayName});");
            return;
        }

        var isElementSimple = elementType.IsSimple;

        sb.AppendLine($"{indentStr}// Extract collection value");

        if (isElementSimple)
        {
            sb.AppendLine($"{indentStr}if ({propRepVarName}.Value is SimpleCollection simpleCollection)");
            sb.AppendLine($"{indentStr}{{");
            sb.AppendLine($"{indentStr}    var collection = new List<{elementType.DisplayName}>();");
            sb.AppendLine($"{indentStr}    foreach (var simpleValue in simpleCollection.Values)");
            sb.AppendLine($"{indentStr}    {{");

            // Pure casting - no conversion logic
            if (elementType.IsReferenceType || elementType.IsNullable)
            {
                sb.AppendLine($"{indentStr}        if (simpleValue.Object != null)");
                sb.AppendLine($"{indentStr}        {{");
                sb.AppendLine($"{indentStr}            collection.Add({GetSimpleValueConversionExpression(elementType, "simpleValue.Object")});");
                sb.AppendLine($"{indentStr}        }}");
            }
            else
            {
                sb.AppendLine($"{indentStr}        collection.Add({GetSimpleValueConversionExpression(elementType, "simpleValue.Object")});");
            }

            sb.AppendLine($"{indentStr}    }}");
        }
        else
        {
            // Complex collection handling. Each element is resolved by its own EntityInfo.ActualType
            // (the same dispatch mechanism used for top-level Node/Relationship polymorphism), falling
            // back to the statically-declared element type only when no serializer is registered for
            // ActualType (e.g. legacy data serialized before ActualType was recorded per element).
            sb.AppendLine($"{indentStr}if ({propRepVarName}.Value is EntityCollection entityCollection)");
            sb.AppendLine($"{indentStr}{{");
            sb.AppendLine($"{indentStr}    var collection = new List<{elementType.DisplayName}>();");
            sb.AppendLine($"{indentStr}    foreach (var entityItem in entityCollection.Entities)");
            sb.AppendLine($"{indentStr}    {{");
            sb.AppendLine($"{indentStr}        var itemSerializer = _serializerRegistry.GetSerializer(entityItem.ActualType)");
            sb.AppendLine($"{indentStr}            ?? _serializerRegistry.GetSerializer(typeof({elementType.TypeOfName}));");
            sb.AppendLine($"{indentStr}        if (itemSerializer == null)");
            sb.AppendLine($"{indentStr}        {{");
            sb.AppendLine($"{indentStr}            throw new InvalidOperationException($\"No serializer found for element type {{entityItem.ActualType}}\");");
            sb.AppendLine($"{indentStr}        }}");
            sb.AppendLine($"{indentStr}        var deserializedItem = itemSerializer.Deserialize(entityItem);");
            sb.AppendLine($"{indentStr}        if (deserializedItem is {elementType.DisplayName} typedItem)");
            sb.AppendLine($"{indentStr}        {{");
            sb.AppendLine($"{indentStr}            collection.Add(typedItem);");
            sb.AppendLine($"{indentStr}        }}");
            sb.AppendLine($"{indentStr}    }}");
        }

        // Convert to appropriate collection type
        if (collectionType.IsArray)
        {
            sb.AppendLine($"{indentStr}    {variableName} = collection.ToArray();");
        }
        else
        {
            sb.AppendLine($"{indentStr}    {variableName} = collection;");
        }

        sb.AppendLine($"{indentStr}}}");
        sb.AppendLine($"{indentStr}else");
        sb.AppendLine($"{indentStr}{{");

        if (collectionType.IsReferenceType && !collectionType.IsNullable)
        {
            sb.AppendLine($"{indentStr}    throw new InvalidOperationException(\"Required collection property '{propertyLabel}' is missing or null\");");
        }
        else
        {
            sb.AppendLine($"{indentStr}    {variableName} = default({collectionType.DisplayName});");
        }

        sb.AppendLine($"{indentStr}}}");
    }

    private static string GetDefaultValueForParameter(string paramName, TypeReferenceModel paramType, out bool shouldThrow)
    {
        // Handle known graph model properties with sensible defaults
        shouldThrow = false;
        return paramName.ToLowerInvariant() switch
        {
            "direction" when paramType.Name.Contains("RelationshipDirection") => "RelationshipDirection.Outgoing",
            "id" when paramType.SpecialType == SpecialType.System_String => "string.Empty",
            "startnodeid" when paramType.SpecialType == SpecialType.System_String => "string.Empty",
            "endnodeid" when paramType.SpecialType == SpecialType.System_String => "string.Empty",
            _ => GetTypeDefault(paramType, out shouldThrow)
        };
    }

    private static string GetTypeDefault(TypeReferenceModel type, out bool shouldThrow)
    {
        string? value = null;
        shouldThrow = false;
        if (type.IsReferenceType)
        {
            // Check if the reference type is nullable
            value = type.IsNullable ? "null" :
                   type.Name == "String" ? "string.Empty" :
                   type.Name == "Uri" ? "new System.Uri(\"about:blank\")" :
                   null;

            // Collection-shaped properties (e.g. INode.Labels: IReadOnlyList<string>)
            // that aren't present in the query result get an empty collection rather
            // than throwing - List<T> is assignable to IEnumerable<T>/ICollection<T>/
            // IList<T>/IReadOnlyCollection<T>/IReadOnlyList<T> alike.
            if (value is null && type.ElementType is not null)
            {
                value = type.IsArray
                    ? $"System.Array.Empty<{type.ElementType.DisplayName}>()"
                    : $"new System.Collections.Generic.List<{type.ElementType.DisplayName}>()";
            }
        }
        else if (type.IsSimple)
        {
            // Use the unified simple type check from GraphDataModel
            value = type.SpecialType switch
            {
                SpecialType.System_String => "string.Empty",
                SpecialType.System_Int32 => "0",
                SpecialType.System_Int64 => "0L",
                SpecialType.System_Double => "0.0",
                SpecialType.System_Boolean => "false",
                SpecialType.System_DateTime => "System.DateTime.MinValue",
                _ when IsGuid(type) => "System.Guid.Empty",
                _ when type.IsEnum => $"default({type.DisplayName})",
                _ => null
            };
        }
        if (value is null && type.IsValueType)
        {
            // Structs not covered above (e.g. complex value-type properties like
            // Point, which GraphDataModel treats as "simple" but whose SpecialType
            // switch above doesn't match) still have a well-defined default: C#
            // guarantees every struct supports `new T()`, which runs its
            // parameterless constructor (including any field initializers).
            value = $"new {type.DisplayName}()";
        }

        if (value is null)
        {
            // If we couldn't determine a sensible default, we should throw
            shouldThrow = true;
        }

        return value ?? string.Empty;
    }

    private static void GenerateSimpleValueAssignment(
        StringBuilder sb,
        TypeReferenceModel targetType,
        string variableName,
        string sourceExpression,
        int indent)
    {
        var indentStr = new string(' ', indent);
        sb.AppendLine($"{indentStr}{variableName} = {GetSimpleValueConversionExpression(targetType, sourceExpression)};");
    }

    private static string GetSimpleValueExtractionComment(TypeReferenceModel targetType)
    {
        return RequiresSimpleValueConversion(targetType)
            ? "Extract simple value with conversion from provider wire types"
            : "Extract simple value - no conversion, just cast";
    }

    private static string GetSimpleValueConversionExpression(TypeReferenceModel targetType, string sourceExpression)
    {
        if (IsGuid(targetType))
        {
            return $"{sourceExpression} is System.Guid guidValue ? guidValue : System.Guid.Parse({sourceExpression}.ToString()!)";
        }

        return $"({targetType.DisplayName}){sourceExpression}";
    }

    private static bool RequiresSimpleValueConversion(TypeReferenceModel targetType)
    {
        return IsGuid(targetType);
    }

    private static bool IsGuid(TypeReferenceModel type)
    {
        return type.Name == "Guid" ||
            type.DisplayName == "System.Guid" ||
            type.DisplayName == "System.Guid?" ||
            type.TypeOfName == "System.Guid" ||
            type.TypeOfName == "System.Guid?";
    }
}
