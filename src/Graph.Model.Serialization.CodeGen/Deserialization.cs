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

internal static class Deserialization
{
    internal static void GenerateDeserializeMethod(StringBuilder sb, INamedTypeSymbol type)
    {
        sb.AppendLine("    public object Deserialize(EntityInfo entity)");
        sb.AppendLine("    {");

        // Get all properties from type and its interfaces
        var allProperties = GetAllPropertiesIncludingInterfaces(type)
            .Where(p => !Utils.SerializationShouldSkipProperty(p, type))
            .ToList();

        // Properties that can only be set via constructor or init
        var constructorOnlyProperties = allProperties
            .Where(p => p.SetMethod == null || p.SetMethod.IsInitOnly)
            .ToList();

        // Properties that can be set after construction
        var settableProperties = allProperties
            .Where(p => p.SetMethod != null &&
                       !p.SetMethod.IsInitOnly &&
                       p.SetMethod.DeclaredAccessibility == Accessibility.Public)
            .ToList();

        // Find all public constructors
        var constructors = type.Constructors
            .Where(c => !c.IsStatic && c.DeclaredAccessibility == Accessibility.Public)
            .OrderBy(c => c.Parameters.Length)
            .ToList();

        // We need constructor if we have any readonly/init-only properties or it's a record
        var needsConstructor = constructorOnlyProperties.Any() || type.IsRecord;

        if (!needsConstructor && constructors.Any(c => c.Parameters.Length == 0))
        {
            // Simple case - parameterless constructor and all properties are settable
            sb.AppendLine($"        var result = new {Utils.GetTypeOfName(type)}();");
            GeneratePropertySetters(sb, settableProperties, "result", "entity");
        }
        else if (constructors.Any())
        {
            // Find the best constructor for our needs
            var bestConstructor = FindBestConstructor(constructors, allProperties);
            if (bestConstructor != null)
            {
                GenerateConstructorBasedDeserialization(sb, type, bestConstructor, allProperties);
            }
            else
            {
                GenerateConstructorBasedDeserialization(sb, type, constructors.First(), allProperties);
            }
        }
        else
        {
            sb.AppendLine($"        throw new InvalidOperationException(\"No suitable constructor found for type {type.Name}\");");
        }

        sb.AppendLine("        return result;");
        sb.AppendLine("    }");
    }

    private static List<IPropertySymbol> GetAllPropertiesIncludingInterfaces(INamedTypeSymbol type)
    {
        var properties = new List<IPropertySymbol>();
        var seenProperties = new HashSet<string>();

        // Get properties from all implemented interfaces first (including IEntity, IRelationship)
        foreach (var interfaceType in type.AllInterfaces)
        {
            foreach (var property in interfaceType.GetMembers().OfType<IPropertySymbol>())
            {
                if (property.DeclaredAccessibility == Accessibility.Public &&
                    property.GetMethod != null &&
                    seenProperties.Add(property.Name))
                {
                    properties.Add(property);
                }
            }
        }

        // Then get properties from the type hierarchy
        for (var currentType = type; currentType != null; currentType = currentType.BaseType)
        {
            foreach (var property in currentType.GetMembers().OfType<IPropertySymbol>())
            {
                if (property.DeclaredAccessibility == Accessibility.Public &&
                    property.GetMethod != null &&
                    !property.IsStatic &&
                    seenProperties.Add(property.Name))
                {
                    properties.Add(property);
                }
            }
        }

        return properties;
    }

    private static void GenerateConstructorBasedDeserialization(StringBuilder sb, INamedTypeSymbol type, IMethodSymbol constructor, List<IPropertySymbol> allProperties)
    {
        // Extract values for each constructor parameter (including Direction if it's part of the constructor)
        foreach (var parameter in constructor.Parameters)
        {
            GenerateConstructorParameterExtraction(sb, parameter, "entity");
        }

        // Find properties that need to be set via object initializer (only init-only properties like Id)
        var handledByConstructor = new HashSet<string>(constructor.Parameters
            .Select(p => p.Name ?? "")
            .Where(name => !string.IsNullOrEmpty(name)), StringComparer.OrdinalIgnoreCase);

        var initOnlyProperties = allProperties
            .Where(p => !handledByConstructor.Contains(p.Name))
            .Where(p => p.SetMethod?.IsInitOnly == true)
            .ToList();

        var settableProperties = allProperties
            .Where(p => !handledByConstructor.Contains(p.Name))
            .Where(p => p.SetMethod != null &&
                !p.SetMethod.IsInitOnly &&
                p.SetMethod.DeclaredAccessibility == Accessibility.Public)
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
            sb.AppendLine($"        var result = new {Utils.GetTypeOfName(type)}(");

            var paramNames = constructor.Parameters.Select(p => p.Name).ToList();
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
            sb.AppendLine($"        var result = new {Utils.GetTypeOfName(type)}(");

            var paramNames = constructor.Parameters.Select(p => p.Name).ToList();
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

    private static void GeneratePropertyExtraction(StringBuilder sb, IPropertySymbol property, string entityVar)
    {
        var propertyName = Utils.GetPropertyName(property);
        var propName = property.Name;
        var propertyType = property.Type.ToDisplayString();
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
        var defaultValue = GetDefaultValueForProperty(propName, property.Type);
        sb.AppendLine($"            {variableName} = {defaultValue};");

        sb.AppendLine("        }");
    }

    private static string GetDefaultValueForProperty(string propertyName, ITypeSymbol propertyType)
    {
        // Handle known interface properties with sensible defaults
        return propertyName.ToLowerInvariant() switch
        {
            "direction" when propertyType.Name.Contains("RelationshipDirection") => "RelationshipDirection.Outgoing",
            "id" when propertyType.SpecialType == SpecialType.System_String => "Guid.NewGuid().ToString(\"N\")",
            _ => GetTypeDefault(propertyType)
        };
    }

    private static void GenerateConstructorParameterExtraction(StringBuilder sb, IParameterSymbol parameter, string entityVar)
    {
        var paramName = parameter.Name;
        var paramType = parameter.Type.ToDisplayString();

        // Use parameter-specific variable names to avoid conflicts
        var propRepVar = $"{paramName?.ToLowerInvariant()}PropRep";
        var simplePropVar = $"{paramName?.ToLowerInvariant()}SimpleProp";
        var complexPropVar = $"{paramName?.ToLowerInvariant()}ComplexProp";

        sb.AppendLine($"        // Extracting value for parameter '{paramName}'");
        sb.AppendLine($"        {paramType} {paramName};");

        // Find matching property by parameter name
        var propertyName = FindPropertyNameForParameter(parameter, entityVar);

        sb.AppendLine($"        // Look for property '{propertyName}' in both simple and complex properties");
        sb.AppendLine($"        var {propRepVar} = {entityVar}.SimpleProperties.TryGetValue(\"{propertyName}\", out var {simplePropVar}) ? {simplePropVar}");
        sb.AppendLine($"                    : {entityVar}.ComplexProperties.TryGetValue(\"{propertyName}\", out var {complexPropVar}) ? {complexPropVar} : null;");
        sb.AppendLine($"        if ({propRepVar} != null)");
        sb.AppendLine("        {");

        GenerateValueExtraction(sb, parameter.Type, paramName!, propertyName, propRepVar, 12);

        sb.AppendLine("        }");
        sb.AppendLine("        else");
        sb.AppendLine("        {");

        // Handle missing parameters with proper defaults
        if (parameter.HasExplicitDefaultValue)
        {
            var defaultValue = FormatDefaultValue(parameter.ExplicitDefaultValue, parameter.Type);
            sb.AppendLine($"            {paramName} = {defaultValue};");
        }
        else
        {
            // For required properties, use sensible defaults based on the property name and type
            var defaultValue = GetDefaultValueForParameter(paramName!, parameter.Type);
            sb.AppendLine($"            {paramName} = {defaultValue};");
        }

        sb.AppendLine("        }");
    }

    private static string FindPropertyNameForParameter(IParameterSymbol parameter, string entityVar)
    {
        var paramName = parameter.Name ?? "unknown";

        // For constructor parameters, we need to find the corresponding property name
        // This is especially important for interface properties that might not match parameter names exactly
        return paramName.ToLowerInvariant() switch
        {
            "startnodeid" => "StartNodeId",
            "endnodeid" => "EndNodeId",
            "id" => "Id",
            "direction" => "Direction",
            _ => Utils.GetPropertyNameFromParameter(parameter)
        };
    }

    private static void GeneratePropertySetters(StringBuilder sb, List<IPropertySymbol> properties, string variableName, string entityVar)
    {
        foreach (var property in properties)
        {
            var propertyName = Utils.GetPropertyName(property);
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
        }
    }

    private static void GenerateValueExtraction(StringBuilder sb, ITypeSymbol targetType, string variableName, string propertyLabel, string propRepVarName, int indent)
    {
        var indentStr = new string(' ', indent);

        if (GraphDataModel.IsCollectionOfSimple(targetType) || GraphDataModel.IsCollectionOfComplex(targetType))
        {
            GenerateCollectionDeserialization(sb, targetType, variableName, propertyLabel, propRepVarName, indent);
        }
        else if (GraphDataModel.IsSimple(targetType))
        {
            sb.AppendLine($"{indentStr}// Extract simple value - no conversion, just cast");
            sb.AppendLine($"{indentStr}if ({propRepVarName}.Value is SimpleValue simpleValue)");
            sb.AppendLine($"{indentStr}{{");
            sb.AppendLine($"{indentStr}    {variableName} = ({targetType.ToDisplayString()})simpleValue.Object;");
            sb.AppendLine($"{indentStr}}}");
            sb.AppendLine($"{indentStr}else");
            sb.AppendLine($"{indentStr}{{");

            if (targetType.IsReferenceType && targetType.NullableAnnotation != NullableAnnotation.Annotated)
            {
                sb.AppendLine($"{indentStr}    throw new InvalidOperationException(\"Required simple property '{propertyLabel}' is missing or null\");");
            }
            else
            {
                sb.AppendLine($"{indentStr}    {variableName} = default({targetType.ToDisplayString()});");
            }

            sb.AppendLine($"{indentStr}}}");
        }
        else
        {
            // Complex type handling - delegate to other serializers
            sb.AppendLine($"{indentStr}// Extract complex value using registered serializer");
            sb.AppendLine($"{indentStr}if ({propRepVarName}.Value is EntityInfo complexEntity)");
            sb.AppendLine($"{indentStr}{{");
            sb.AppendLine($"{indentStr}    var complexSerializer = _serializerRegistry.GetSerializer(typeof({Utils.GetTypeOfName(targetType)}));");
            sb.AppendLine($"{indentStr}    if (complexSerializer != null)");
            sb.AppendLine($"{indentStr}    {{");
            sb.AppendLine($"{indentStr}        {variableName} = ({targetType.ToDisplayString()})complexSerializer.Deserialize(complexEntity);");
            sb.AppendLine($"{indentStr}    }}");
            sb.AppendLine($"{indentStr}    else");
            sb.AppendLine($"{indentStr}    {{");
            sb.AppendLine($"{indentStr}        throw new InvalidOperationException($\"No serializer found for type {targetType.ToDisplayString()}\");");
            sb.AppendLine($"{indentStr}    }}");
            sb.AppendLine($"{indentStr}}}");
            sb.AppendLine($"{indentStr}else");
            sb.AppendLine($"{indentStr}{{");

            if (targetType.IsReferenceType && targetType.NullableAnnotation != NullableAnnotation.Annotated)
            {
                sb.AppendLine($"{indentStr}    throw new InvalidOperationException(\"Required complex property '{propertyLabel}' is missing or null\");");
            }
            else
            {
                sb.AppendLine($"{indentStr}    {variableName} = default({targetType.ToDisplayString()});");
            }

            sb.AppendLine($"{indentStr}}}");
        }
    }

    private static void GenerateCollectionDeserialization(StringBuilder sb, ITypeSymbol collectionType, string variableName, string propertyLabel, string propRepVarName, int indent)
    {
        var indentStr = new string(' ', indent);
        var elementType = GraphDataModel.GetCollectionElementType(collectionType);

        if (elementType == null)
        {
            sb.AppendLine($"{indentStr}// Warning: Could not determine element type for collection");
            sb.AppendLine($"{indentStr}{variableName} = default({collectionType.ToDisplayString()});");
            return;
        }

        var isElementSimple = GraphDataModel.IsSimple(elementType);

        sb.AppendLine($"{indentStr}// Extract collection value");

        if (isElementSimple)
        {
            sb.AppendLine($"{indentStr}if ({propRepVarName}.Value is SimpleCollection simpleCollection)");
            sb.AppendLine($"{indentStr}{{");
            sb.AppendLine($"{indentStr}    var collection = new List<{elementType.ToDisplayString()}>();");
            sb.AppendLine($"{indentStr}    foreach (var simpleValue in simpleCollection.Values)");
            sb.AppendLine($"{indentStr}    {{");

            // Pure casting - no conversion logic
            if (elementType.IsReferenceType || elementType.NullableAnnotation == NullableAnnotation.Annotated)
            {
                sb.AppendLine($"{indentStr}        if (simpleValue.Object != null)");
                sb.AppendLine($"{indentStr}        {{");
                sb.AppendLine($"{indentStr}            collection.Add(({elementType.ToDisplayString()})simpleValue.Object);");
                sb.AppendLine($"{indentStr}        }}");
            }
            else
            {
                sb.AppendLine($"{indentStr}        collection.Add(({elementType.ToDisplayString()})simpleValue.Object);");
            }

            sb.AppendLine($"{indentStr}    }}");
        }
        else
        {
            // Complex collection handling
            sb.AppendLine($"{indentStr}if ({propRepVarName}.Value is EntityCollection entityCollection)");
            sb.AppendLine($"{indentStr}{{");
            sb.AppendLine($"{indentStr}    var collection = new List<{elementType.ToDisplayString()}>();");
            sb.AppendLine($"{indentStr}    var itemSerializer = _serializerRegistry.GetSerializer(typeof({Utils.GetTypeOfName(elementType)}));");
            sb.AppendLine($"{indentStr}    if (itemSerializer != null)");
            sb.AppendLine($"{indentStr}    {{");
            sb.AppendLine($"{indentStr}        foreach (var entityItem in entityCollection.Entities)");
            sb.AppendLine($"{indentStr}        {{");
            sb.AppendLine($"{indentStr}            var deserializedItem = itemSerializer.Deserialize(entityItem);");
            sb.AppendLine($"{indentStr}            if (deserializedItem is {elementType.ToDisplayString()} typedItem)");
            sb.AppendLine($"{indentStr}            {{");
            sb.AppendLine($"{indentStr}                collection.Add(typedItem);");
            sb.AppendLine($"{indentStr}            }}");
            sb.AppendLine($"{indentStr}        }}");
            sb.AppendLine($"{indentStr}    }}");
            sb.AppendLine($"{indentStr}    else");
            sb.AppendLine($"{indentStr}    {{");
            sb.AppendLine($"{indentStr}        throw new InvalidOperationException($\"No serializer found for element type {elementType.ToDisplayString()}\");");
            sb.AppendLine($"{indentStr}    }}");
        }

        // Convert to appropriate collection type
        if (collectionType.TypeKind == TypeKind.Array)
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

        if (collectionType.IsReferenceType && collectionType.NullableAnnotation != NullableAnnotation.Annotated)
        {
            sb.AppendLine($"{indentStr}    throw new InvalidOperationException(\"Required collection property '{propertyLabel}' is missing or null\");");
        }
        else
        {
            sb.AppendLine($"{indentStr}    {variableName} = default({collectionType.ToDisplayString()});");
        }

        sb.AppendLine($"{indentStr}}}");
    }

    private static IMethodSymbol? FindBestConstructor(List<IMethodSymbol> constructors, List<IPropertySymbol> allProperties)
    {
        // Score each constructor based on how many properties it can set
        var constructorScores = constructors.Select(ctor =>
        {
            var matchingProps = ctor.Parameters
                .Select(param => allProperties.FirstOrDefault(prop =>
                    string.Equals(prop.Name, param.Name, StringComparison.OrdinalIgnoreCase)))
                .Where(p => p != null)
                .ToList();

            return new
            {
                Constructor = ctor,
                TotalMatches = matchingProps.Count,
                ExtraParams = ctor.Parameters.Length - matchingProps.Count
            };
        }).ToList();

        // Prefer constructors that can set the most properties with fewest extra parameters
        return constructorScores
            .OrderByDescending(x => x.TotalMatches)
            .ThenBy(x => x.ExtraParams)
            .Select(x => x.Constructor)
            .FirstOrDefault();
    }

    private static string GetDefaultValueForParameter(string paramName, ITypeSymbol paramType)
    {
        // Handle known graph model properties with sensible defaults
        return paramName.ToLowerInvariant() switch
        {
            "direction" when paramType.Name.Contains("RelationshipDirection") => "RelationshipDirection.Outgoing",
            "id" when paramType.SpecialType == SpecialType.System_String => "string.Empty",
            "startnodeid" when paramType.SpecialType == SpecialType.System_String => "string.Empty",
            "endnodeid" when paramType.SpecialType == SpecialType.System_String => "string.Empty",
            _ => GetTypeDefault(paramType)
        };
    }

    private static string GetTypeDefault(ITypeSymbol type)
    {
        if (type.IsReferenceType)
        {
            return type.NullableAnnotation == NullableAnnotation.Annotated ? "null" :
                   "throw new InvalidOperationException(\"Required property is missing\")";
        }

        // Use the unified simple type check from GraphDataModel
        if (GraphDataModel.IsSimple(type))
        {
            return type.SpecialType switch
            {
                SpecialType.System_String => "string.Empty",
                SpecialType.System_Int32 => "0",
                SpecialType.System_Int64 => "0L",
                SpecialType.System_Double => "0.0",
                SpecialType.System_Boolean => "false",
                SpecialType.System_DateTime => "DateTime.MinValue",
                _ when type.TypeKind == TypeKind.Enum => $"default({type.ToDisplayString()})",
                _ => $"default({type.ToDisplayString()})"
            };
        }

        return $"default({type.ToDisplayString()})";
    }

    private static string FormatDefaultValue(object? defaultValue, ITypeSymbol type)
    {
        return defaultValue switch
        {
            null when type.IsReferenceType => "null",
            null => $"default({type.ToDisplayString()})",
            string str => $"\"{str}\"",
            bool b => b.ToString().ToLowerInvariant(),
            _ => defaultValue.ToString() ?? "null"
        };
    }
}