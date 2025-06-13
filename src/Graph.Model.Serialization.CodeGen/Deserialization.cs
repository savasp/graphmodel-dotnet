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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Cvoya.Graph.Model.Serialization.CodeGen;

internal static class Deserialization
{
    internal static void GenerateDeserializeMethod(StringBuilder sb, INamedTypeSymbol type)
    {
        sb.AppendLine("    public override object Deserialize(Entity entity, Type targetType)");
        sb.AppendLine("    {");

        // Get all properties that we can deserialize
        var allProperties = Utils.GetAllProperties(type)
            .Where(p => !Utils.SerializationShouldSkipProperty(p, type))
            .ToList();

        // Core properties that must be set (even if readonly)
        var corePropertyNames = new HashSet<string> { "Id", "StartNodeId", "EndNodeId", "Direction" };

        // Separate properties by type
        var coreProperties = allProperties.Where(p => corePropertyNames.Contains(p.Name)).ToList();
        var regularProperties = allProperties.Where(p => !corePropertyNames.Contains(p.Name)).ToList();

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

        // We need constructor if we have any readonly/init-only properties
        var needsConstructor = constructorOnlyProperties.Any() || type.IsRecord;

        if (!needsConstructor && constructors.Any(c => c.Parameters.Length == 0))
        {
            // Simple case - parameterless constructor and all properties are settable
            sb.AppendLine($"        var result = new {Utils.GetTypeOfName(type)}();");
            GeneratePropertySetters(sb, settableProperties, "result", "entity");
        }
        else if (constructors.Any())
        {
            // Find the best constructor that can set the most properties (especially core ones)
            var bestConstructor = FindBestConstructor(constructors, allProperties, coreProperties);
            if (bestConstructor != null)
            {
                GenerateConstructorBasedDeserialization(sb, type, bestConstructor, allProperties);
            }
            else
            {
                // Fall back to first public constructor
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

    private static void GenerateConstructorBasedDeserialization(StringBuilder sb, INamedTypeSymbol type, IMethodSymbol constructor, List<IPropertySymbol> allProperties)
    {
        // Extract values for each constructor parameter
        foreach (var parameter in constructor.Parameters)
        {
            GenerateConstructorParameterExtraction(sb, parameter, "entity");
        }

        // Generate constructor call
        sb.AppendLine();
        sb.AppendLine($"        var result = new {Utils.GetTypeOfName(type)}(");

        var paramNames = constructor.Parameters.Select(p => p.Name).ToList();
        for (int i = 0; i < paramNames.Count; i++)
        {
            var comma = i < paramNames.Count - 1 ? "," : "";
            sb.AppendLine($"            {paramNames[i]}{comma}");
        }

        sb.AppendLine("        );");
        sb.AppendLine();

        // Find properties that weren't handled by the constructor
        var handledByConstructor = new HashSet<IPropertySymbol>(constructor.Parameters
            .Select(p => allProperties.FirstOrDefault(prop =>
                string.Equals(prop.Name, p.Name, StringComparison.OrdinalIgnoreCase)))
            .Where(p => p != null), SymbolEqualityComparer.Default);


        var remainingProperties = allProperties
            .Where(p => !handledByConstructor.Contains(p))
            .Where(p => p.SetMethod != null &&
                !p.SetMethod.IsInitOnly &&
                p.SetMethod.DeclaredAccessibility == Accessibility.Public)
            .ToList();

        // Set remaining properties
        if (remainingProperties.Any())
        {
            sb.AppendLine("        // Set remaining properties");
            GeneratePropertySetters(sb, remainingProperties, "result", "entity");
        }
    }

    private static void GenerateConstructorParameterExtraction(StringBuilder sb, IParameterSymbol parameter, string entityVar)
    {
        var paramName = parameter.Name;
        // For constructor parameters, we'll assume the property name matches the parameter name
        // but with proper casing (camelCase for Neo4j properties)
        var propName = Utils.GetPropertyNameFromParameter(parameter);
        var paramType = parameter.Type.ToDisplayString();

        // Use parameter-specific variable names to avoid conflicts
        var propRepVar = $"{paramName.ToLowerInvariant()}PropRep";
        var simplePropVar = $"{paramName.ToLowerInvariant()}SimpleProp";
        var complexPropVar = $"{paramName.ToLowerInvariant()}ComplexProp";

        sb.AppendLine($"        // Extracting value for parameter '{paramName}'");
        sb.AppendLine($"        {paramType} {paramName};");
        sb.AppendLine($"        // Look for property in both simple and complex properties");
        sb.AppendLine($"        var {propRepVar} = {entityVar}.SimpleProperties.TryGetValue(\"{propName}\", out var {simplePropVar}) ? {simplePropVar}");
        sb.AppendLine($"                    : {entityVar}.ComplexProperties.TryGetValue(\"{propName}\", out var {complexPropVar}) ? {complexPropVar} : null;");
        sb.AppendLine($"        if ({propRepVar} != null)");
        sb.AppendLine("        {");

        // Use the specific variable name directly - no more redundant assignment
        GenerateValueExtraction(sb, parameter.Type, paramName, propName, propRepVar, 12);

        sb.AppendLine("        }");
        sb.AppendLine("        else");
        sb.AppendLine("        {");

        if (parameter.HasExplicitDefaultValue)
        {
            var defaultValue = FormatDefaultValue(parameter.ExplicitDefaultValue, parameter.Type);
            sb.AppendLine($"            {paramName} = {defaultValue};");
        }
        else if (parameter.Type.IsReferenceType)
        {
            sb.AppendLine($"            throw new InvalidOperationException(\"Required property '{paramName}' is missing\");");
        }
        else
        {
            sb.AppendLine($"            {paramName} = default({paramType});");
        }

        sb.AppendLine("        }");
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

            sb.AppendLine($"        // Look for property '{propName}' in both simple and complex properties");
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
            sb.AppendLine($"{indentStr}// Extract simple value using base class conversion");
            sb.AppendLine($"{indentStr}if ({propRepVarName}.Value is SimpleValue simpleValue)");
            sb.AppendLine($"{indentStr}{{");

            // Get the actual type for typeof() - strip nullable annotations for reference types
            var typeForTypeOf = Utils.GetTypeForTypeOf(targetType);
            var castType = targetType.ToDisplayString();

            sb.AppendLine($"{indentStr}    {variableName} = ({castType})ConvertFromNeo4jValue(simpleValue.Object, typeof({typeForTypeOf}))!;");
            sb.AppendLine($"{indentStr}}}");
            sb.AppendLine($"{indentStr}else");
            sb.AppendLine($"{indentStr}{{");

            if (targetType.IsReferenceType && targetType.NullableAnnotation != NullableAnnotation.Annotated)
            {
                sb.AppendLine($"{indentStr}    throw new InvalidOperationException(\"Required simple property is missing or null\");");
            }
            else
            {
                sb.AppendLine($"{indentStr}    {variableName} = default({castType});");
            }

            sb.AppendLine($"{indentStr}}}");
        }
        else
        {
            // Complex type handling stays the same...
            sb.AppendLine($"{indentStr}// Extract complex value");
            sb.AppendLine($"{indentStr}if ({propRepVarName}.Value is Entity complexEntity)");
            sb.AppendLine($"{indentStr}{{");
            sb.AppendLine($"{indentStr}    var complexSerializer = EntitySerializerRegistry.GetSerializer(typeof({Utils.GetTypeOfName(targetType)}));");
            sb.AppendLine($"{indentStr}    if (complexSerializer != null)");
            sb.AppendLine($"{indentStr}    {{");
            sb.AppendLine($"{indentStr}        {variableName} = ({targetType.ToDisplayString()})complexSerializer.Deserialize(complexEntity, targetType);");
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
                sb.AppendLine($"{indentStr}    throw new InvalidOperationException(\"Required complex property is missing or null\");");
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

            var elementTypeForTypeOf = Utils.GetTypeForTypeOf(elementType);
            var elementCastType = elementType.ToDisplayString();

            // Use the base class conversion method for each element
            if (elementType.IsReferenceType || elementType.NullableAnnotation == NullableAnnotation.Annotated)
            {
                sb.AppendLine($"{indentStr}        if (simpleValue.Object != null)");
                sb.AppendLine($"{indentStr}        {{");
                sb.AppendLine($"{indentStr}            var convertedValue = ({elementCastType})ConvertFromNeo4jValue(simpleValue.Object, typeof({elementTypeForTypeOf}))!;");
                sb.AppendLine($"{indentStr}            collection.Add(convertedValue);");
                sb.AppendLine($"{indentStr}        }}");
            }
            else
            {
                sb.AppendLine($"{indentStr}        var convertedValue = ({elementCastType})ConvertFromNeo4jValue(simpleValue.Object, typeof({elementTypeForTypeOf}))!;");
                sb.AppendLine($"{indentStr}        collection.Add(convertedValue);");
            }

            sb.AppendLine($"{indentStr}    }}");
        }
        else
        {
            // Complex collection handling stays the same...
            sb.AppendLine($"{indentStr}if ({propRepVarName}.Value is EntityCollection entityCollection)");
            sb.AppendLine($"{indentStr}{{");
            sb.AppendLine($"{indentStr}    var collection = new List<{elementType.ToDisplayString()}>();");
            sb.AppendLine($"{indentStr}    var itemSerializer = EntitySerializerRegistry.GetSerializer(typeof({Utils.GetTypeOfName(elementType)}));");
            sb.AppendLine($"{indentStr}    if (itemSerializer != null)");
            sb.AppendLine($"{indentStr}    {{");
            sb.AppendLine($"{indentStr}        foreach (var entityItem in entityCollection.Entities)");
            sb.AppendLine($"{indentStr}        {{");
            sb.AppendLine($"{indentStr}            var deserializedItem = itemSerializer.Deserialize(entityItem, targetType);");
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
            sb.AppendLine($"{indentStr}    throw new InvalidOperationException(\"Required collection property is missing or null\");");
        }
        else
        {
            sb.AppendLine($"{indentStr}    {variableName} = default({collectionType.ToDisplayString()});");
        }

        sb.AppendLine($"{indentStr}}}");
    }

    private static IMethodSymbol? FindBestConstructor(List<IMethodSymbol> constructors, List<IPropertySymbol> allProperties, List<IPropertySymbol> coreProperties)
    {
        // Score each constructor
        var constructorScores = constructors.Select(ctor =>
        {
            var matchingProps = ctor.Parameters
                .Select(param => allProperties.FirstOrDefault(prop =>
                    string.Equals(prop.Name, param.Name, StringComparison.OrdinalIgnoreCase)))
                .Where(p => p != null)
                .ToList();

            var coreMatches = matchingProps.Count(p => coreProperties.Contains(p!));
            var regularMatches = matchingProps.Count - coreMatches;

            return new
            {
                Constructor = ctor,
                CoreMatches = coreMatches,
                RegularMatches = regularMatches,
                TotalMatches = matchingProps.Count,
                ExtraParams = ctor.Parameters.Length - matchingProps.Count
            };
        }).ToList();

        // Prefer constructors that:
        // 1. Can set the most core properties (Id, StartNodeId, etc.)
        // 2. Can set the most total properties
        // 3. Have the fewest extra parameters
        return constructorScores
            .OrderByDescending(x => x.CoreMatches)
            .ThenByDescending(x => x.TotalMatches)
            .ThenBy(x => x.ExtraParams)
            .Select(x => x.Constructor)
            .FirstOrDefault();
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