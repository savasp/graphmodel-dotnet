// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

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

        if (initOnlyProperties.Count > 0)
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
        if (settableProperties.Count > 0)
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
        GenerateValueExtraction(
            sb,
            property.Type,
            variableName,
            propertyName,
            propRepVar,
            8,
            GetMissingPropertyValueExpression(propName, propertyName, property.Type));
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
        GenerateValueExtraction(
            sb,
            parameter.Type,
            paramName,
            propertyName,
            propRepVar,
            8,
            GetMissingParameterValueExpression(parameter));
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
            GenerateValueExtraction(
                sb,
                property.Type,
                $"{variableName}.{propName}",
                propertyName,
                propRepVar,
                8,
                GetMissingPropertyValueExpression(propertyName, propertyName, property.Type));
        }
    }

    private static void GenerateValueExtraction(
        StringBuilder sb,
        TypeReferenceModel targetType,
        string variableName,
        string propertyLabel,
        string propRepVarName,
        int indent,
        string missingValueExpression)
    {
        var indentStr = new string(' ', indent);

        if (targetType.IsCollectionOfSimple || targetType.IsCollectionOfComplex)
        {
            GenerateCollectionDeserialization(
                sb,
                targetType,
                variableName,
                propertyLabel,
                propRepVarName,
                indent,
                missingValueExpression);
        }
        else if (targetType.IsSimple)
        {
            var simpleValueName = $"{propRepVarName}SimpleValue";
            sb.AppendLine($"{indentStr}// {GetSimpleValueExtractionComment(targetType)}");
            sb.AppendLine($"{indentStr}{variableName} = {propRepVarName} is null");
            sb.AppendLine($"{indentStr}    ? {missingValueExpression}");
            sb.AppendLine($"{indentStr}    : {propRepVarName}.Value is SimpleValue {simpleValueName}");
            sb.AppendLine($"{indentStr}        ? {GetSimpleValueConversionExpression(targetType, $"{simpleValueName}.Object")}");
            sb.AppendLine($"{indentStr}        : {GetInvalidSimpleValueExpression(propertyLabel, targetType)};");
        }
        else
        {
            var complexEntityName = $"{propRepVarName}ComplexEntity";
            // Complex type handling - delegate to other serializers, resolved by the serialized
            // entity's ActualType (same dispatch mechanism used for top-level Node/Relationship
            // polymorphism and for complex collections), falling back to the statically-declared
            // type only when no serializer is registered for ActualType (e.g. legacy data).
            sb.AppendLine($"{indentStr}// Extract complex value using registered serializer");
            sb.AppendLine($"{indentStr}{variableName} = {propRepVarName} is null");
            sb.AppendLine($"{indentStr}    ? {missingValueExpression}");
            sb.AppendLine($"{indentStr}    : {propRepVarName}.Value is EntityInfo {complexEntityName}");
            sb.AppendLine($"{indentStr}        ? ({targetType.DisplayName})(_serializerRegistry.GetSerializer({complexEntityName}.ActualType)");
            sb.AppendLine($"{indentStr}            ?? _serializerRegistry.GetSerializer(typeof({targetType.TypeOfName}))");
            sb.AppendLine($"{indentStr}            ?? throw new InvalidOperationException($\"No serializer found for type {{{complexEntityName}.ActualType}}\"))");
            sb.AppendLine($"{indentStr}            .Deserialize({complexEntityName})");
            sb.AppendLine($"{indentStr}        : {GetInvalidComplexValueExpression(propertyLabel, targetType)};");
        }
    }

    private static void GenerateCollectionDeserialization(
        StringBuilder sb,
        TypeReferenceModel collectionType,
        string variableName,
        string propertyLabel,
        string propRepVarName,
        int indent,
        string missingValueExpression)
    {
        var indentStr = new string(' ', indent);
        var elementType = collectionType.ElementType;

        if (elementType == null)
        {
            sb.AppendLine($"{indentStr}// Warning: Could not determine element type for collection");
            sb.AppendLine($"{indentStr}{variableName} = {missingValueExpression};");
            return;
        }

        var isElementSimple = elementType.IsSimple;

        sb.AppendLine($"{indentStr}// Extract collection value");

        if (isElementSimple)
        {
            var simpleCollectionName = $"{propRepVarName}SimpleCollection";
            sb.AppendLine($"{indentStr}{variableName} = {propRepVarName} is null");
            sb.AppendLine($"{indentStr}    ? {missingValueExpression}");
            sb.AppendLine($"{indentStr}    : {propRepVarName}.Value is SimpleCollection {simpleCollectionName}");
            sb.AppendLine($"{indentStr}        ? {simpleCollectionName}.Values");

            if (elementType.IsReferenceType && !elementType.IsNullable)
            {
                sb.AppendLine($"{indentStr}            .Where(simpleValue => simpleValue.Object is not null)");
            }

            sb.AppendLine($"{indentStr}            .Select(simpleValue => {GetSimpleValueConversionExpression(elementType, "simpleValue.Object")})");
        }
        else
        {
            var entityCollectionName = $"{propRepVarName}EntityCollection";
            // Complex collection handling. Each element is resolved by its own EntityInfo.ActualType
            // (the same dispatch mechanism used for top-level Node/Relationship polymorphism), falling
            // back to the statically-declared element type only when no serializer is registered for
            // ActualType (e.g. legacy data serialized before ActualType was recorded per element).
            sb.AppendLine($"{indentStr}{variableName} = {propRepVarName} is null");
            sb.AppendLine($"{indentStr}    ? {missingValueExpression}");
            sb.AppendLine($"{indentStr}    : {propRepVarName}.Value is EntityCollection {entityCollectionName}");
            sb.AppendLine($"{indentStr}        ? {entityCollectionName}.Entities");
            sb.AppendLine($"{indentStr}            .Select(entityItem => (_serializerRegistry.GetSerializer(entityItem.ActualType)");
            sb.AppendLine($"{indentStr}                ?? _serializerRegistry.GetSerializer(typeof({elementType.TypeOfName}))");
            sb.AppendLine($"{indentStr}                ?? throw new InvalidOperationException($\"No serializer found for element type {{entityItem.ActualType}}\"))");
            sb.AppendLine($"{indentStr}                .Deserialize(entityItem))");
            sb.AppendLine($"{indentStr}            .OfType<{elementType.DisplayName}>()");
        }

        if (collectionType.IsArray)
        {
            sb.AppendLine($"{indentStr}            .ToArray()");
        }
        else
        {
            sb.AppendLine($"{indentStr}            .ToList()");
        }

        sb.AppendLine($"{indentStr}        : {GetInvalidCollectionValueExpression(propertyLabel, collectionType)};");
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

    private static string GetMissingPropertyValueExpression(
        string propertyName,
        string propertyLabel,
        TypeReferenceModel propertyType)
    {
        if (propertyType.IsNullable)
        {
            return "null";
        }

        var defaultValue = GetDefaultValueForProperty(propertyName, propertyType, out var shouldThrow);
        return shouldThrow
            ? $"throw new InvalidOperationException(\"No sensible default value for property '{propertyLabel}' of type '{propertyType.DisplayName}'.\")"
            : defaultValue;
    }

    private static string GetMissingParameterValueExpression(ParameterModel parameter)
    {
        if (parameter.HasExplicitDefaultValue)
        {
            return parameter.ExplicitDefaultValueExpression;
        }

        if (parameter.Type.IsNullable)
        {
            return "null";
        }

        var defaultValue = GetDefaultValueForParameter(parameter.Name, parameter.Type, out var shouldThrow);
        return shouldThrow
            ? $"throw new InvalidOperationException(\"No sensible default value for parameter '{parameter.Name}' of type '{parameter.Type.DisplayName}'.\")"
            : defaultValue;
    }

    private static string GetInvalidSimpleValueExpression(string propertyLabel, TypeReferenceModel targetType)
    {
        if (targetType.IsNullable)
        {
            return "null";
        }

        var defaultValue = GetDefaultValueForProperty(propertyLabel, targetType, out var shouldThrow);
        return shouldThrow
            ? $"throw new InvalidOperationException(\"No sensible default value for property '{propertyLabel}' of type '{targetType.DisplayName}'.\")"
            : defaultValue;
    }

    private static string GetInvalidComplexValueExpression(string propertyLabel, TypeReferenceModel targetType)
    {
        return targetType.IsNullable
            ? "null"
            : $"throw new InvalidOperationException(\"Required complex property '{propertyLabel}' is missing or null\")";
    }

    private static string GetInvalidCollectionValueExpression(string propertyLabel, TypeReferenceModel collectionType)
    {
        return collectionType.IsReferenceType && !collectionType.IsNullable
            ? $"throw new InvalidOperationException(\"Required collection property '{propertyLabel}' is missing or null\")"
            : $"default({collectionType.DisplayName})";
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
            var conversion = $"{sourceExpression} is System.Guid guidValue ? guidValue : System.Guid.Parse({sourceExpression}.ToString()!)";
            return targetType.IsNullable
                ? $"{sourceExpression} is null ? ({targetType.DisplayName})null : {conversion}"
                : conversion;
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
