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

namespace Cvoya.Graph.Model.Neo4j.Serialization.CodeGen;

internal class Deserialization
{
    internal static void GenerateDeserializeMethod(StringBuilder sb, INamedTypeSymbol type)
    {
        sb.AppendLine("    public override object Deserialize(Entity entity)");
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
            sb.AppendLine($"        var result = new {GetTypeOfName(type)}();");
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

    private static void GenerateConstructorBasedDeserialization(
            StringBuilder sb,
            INamedTypeSymbol type,
            IMethodSymbol constructor,
            List<IPropertySymbol> allProperties)
    {
        // Get core properties that aren't handled by constructor
        var corePropertyNames = new HashSet<string> { "Id", "StartNodeId", "EndNodeId", "Direction" };
        var constructorParamNames = new HashSet<string>(
            constructor.Parameters.Select(p => p.Name),
            StringComparer.OrdinalIgnoreCase);

        var unhandledCoreProperties = allProperties
            .Where(p => corePropertyNames.Contains(p.Name) && !constructorParamNames.Contains(p.Name))
            .ToList();

        // Generate parameter extraction for constructor params
        foreach (var param in constructor.Parameters)
        {
            var matchingProperty = allProperties.FirstOrDefault(p =>
                string.Equals(p.Name, param.Name, StringComparison.OrdinalIgnoreCase));

            var propertyName = matchingProperty != null ? Utils.GetPropertyName(matchingProperty) : Utils.GetPropertyNameFromParameter(param);
            var paramType = param.Type.ToDisplayString();
            var isNullable = param.Type.NullableAnnotation == NullableAnnotation.Annotated;

            sb.AppendLine($"        // Extracting value for parameter '{param.Name}'");
            sb.AppendLine($"        {paramType} {param.Name};");
            sb.AppendLine($"        // Look for property in both simple and complex properties");
            sb.AppendLine($"        var propRep = entity.SimpleProperties.TryGetValue(\"{propertyName}\", out var simpleProp) ? simpleProp");
            sb.AppendLine($"                    : entity.ComplexProperties.TryGetValue(\"{propertyName}\", out var complexProp) ? complexProp : null;");
            sb.AppendLine($"        if (propRep != null)");
            sb.AppendLine("        {");

            GenerateValueExtraction(sb, param.Type, $"{param.Name}", propertyName, 12);

            sb.AppendLine("        }");
            sb.AppendLine("        else");
            sb.AppendLine("        {");

            if (!isNullable && param.Type.IsReferenceType)
            {
                sb.AppendLine($"            throw new InvalidOperationException(\"Required property '{propertyName}' is missing\");");
            }
            else if (param.Type.IsReferenceType)
            {
                sb.AppendLine($"            {param.Name} = default({paramType})!;");
            }
            else
            {
                sb.AppendLine($"            {param.Name} = default({paramType});");
            }

            sb.AppendLine("        }");
        }

        // Extract values for unhandled core properties with init setters
        var corePropsWithInitSetters = new List<IPropertySymbol>();

        foreach (var coreProp in unhandledCoreProperties)
        {
            var propName = Utils.GetPropertyName(coreProp);
            var propType = coreProp.Type.ToDisplayString();

            sb.AppendLine($"        // Extracting core property '{coreProp.Name}' not in constructor");
            sb.AppendLine($"        {propType} {coreProp.Name}Temp;");
            sb.AppendLine($"        // Look for property in both simple and complex properties");
            sb.AppendLine($"        var {coreProp.Name.ToLower()}PropRep = entity.SimpleProperties.TryGetValue(\"{propName}\", out var {coreProp.Name.ToLower()}SimpleProp) ? {coreProp.Name.ToLower()}SimpleProp");
            sb.AppendLine($"                    : entity.ComplexProperties.TryGetValue(\"{propName}\", out var {coreProp.Name.ToLower()}ComplexProp) ? {coreProp.Name.ToLower()}ComplexProp : null;");
            sb.AppendLine($"        if ({coreProp.Name.ToLower()}PropRep != null)");
            sb.AppendLine("        {");
            sb.AppendLine($"            var propRep = {coreProp.Name.ToLower()}PropRep;");

            GenerateValueExtraction(sb, coreProp.Type, $"{coreProp.Name}Temp", propName, 12);

            sb.AppendLine("        }");
            sb.AppendLine("        else");
            sb.AppendLine("        {");

            if (coreProp.Type.IsReferenceType && coreProp.Type.NullableAnnotation != NullableAnnotation.Annotated)
            {
                sb.AppendLine($"            throw new InvalidOperationException(\"Required core property '{propName}' is missing\");");
            }
            else if (coreProp.Type.IsReferenceType)
            {
                sb.AppendLine($"            {coreProp.Name}Temp = default({propType})!;");
            }
            else
            {
                sb.AppendLine($"            {coreProp.Name}Temp = default({propType});");
            }

            sb.AppendLine("        }");

            // Track props with init setters for later
            if (coreProp.SetMethod?.IsInitOnly == true)
            {
                corePropsWithInitSetters.Add(coreProp);
            }
        }

        // Call constructor with object initializer syntax if we have init-only properties
        sb.Append($"        var result = new {GetTypeOfName(type)}(");
        sb.Append(string.Join(", ", constructor.Parameters.Select(p => p.Name)));
        sb.Append(")");

        // Add object initializer for init-only properties
        if (corePropsWithInitSetters.Any())
        {
            sb.AppendLine();
            sb.AppendLine("        {");
            foreach (var coreProp in corePropsWithInitSetters)
            {
                sb.AppendLine($"            {coreProp.Name} = {coreProp.Name}Temp,");
            }
            sb.AppendLine("        };");
        }
        else
        {
            sb.AppendLine(";");
        }

        // Set any remaining properties that weren't handled by the constructor
        var remainingSettableProperties = allProperties
            .Where(p => p.SetMethod != null &&
                       !p.SetMethod.IsInitOnly &&
                       p.SetMethod.DeclaredAccessibility == Accessibility.Public &&
                       !constructorParamNames.Contains(p.Name))
            .ToList();

        if (remainingSettableProperties.Any())
        {
            sb.AppendLine();
            sb.AppendLine("        // Set remaining properties not handled by constructor");
            GeneratePropertySetters(sb, remainingSettableProperties, "result", "entity");
        }
    }

    private static IMethodSymbol? FindBestConstructor(
        List<IMethodSymbol> constructors,
        List<IPropertySymbol> allProperties,
        List<IPropertySymbol> coreProperties)
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

    private static void GenerateValueExtraction(StringBuilder sb, ITypeSymbol targetType, string variableName, string propertyLabel, int indent)
    {
        var indentStr = new string(' ', indent);

        // Determine the type category at compile time - no runtime checks needed
        if (GraphDataModel.IsCollectionOfSimple(targetType) || GraphDataModel.IsCollectionOfComplex(targetType))
        {
            GenerateCollectionDeserialization(sb, targetType, variableName, propertyLabel, indent);
        }
        else if (GraphDataModel.IsSimple(targetType))
        {
            sb.AppendLine($"{indentStr}// Extract simple value");
            sb.AppendLine($"{indentStr}if (propRep.Value is SimpleValue simpleValue)");
            sb.AppendLine($"{indentStr}{{");
            sb.AppendLine($"{indentStr}    {variableName} = ({targetType.ToDisplayString()})simpleValue.Object;");
            sb.AppendLine($"{indentStr}}}");
            sb.AppendLine($"{indentStr}else");
            sb.AppendLine($"{indentStr}{{");
            if (targetType.IsReferenceType && targetType.NullableAnnotation != NullableAnnotation.Annotated)
            {
                sb.AppendLine($"{indentStr}    throw new InvalidOperationException(\"Required simple property is missing or null\");");
            }
            else if (targetType.IsReferenceType)
            {
                sb.AppendLine($"{indentStr}    {variableName} = default({targetType.ToDisplayString()})!;");
            }
            else
            {
                sb.AppendLine($"{indentStr}    {variableName} = default({targetType.ToDisplayString()});");
            }
            sb.AppendLine($"{indentStr}}}");
        }
        else
        {
            // Complex type - recursively deserialize
            sb.AppendLine($"{indentStr}// Extract complex value");
            sb.AppendLine($"{indentStr}if (propRep.Value is Entity complexEntity)");
            sb.AppendLine($"{indentStr}{{");
            sb.AppendLine($"{indentStr}    var complexSerializer = EntitySerializerRegistry.GetSerializer(typeof({GetTypeOfName(targetType)}));");
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
                sb.AppendLine($"{indentStr}    throw new InvalidOperationException(\"Required complex property is missing or null\");");
            }
            else if (targetType.IsReferenceType)
            {
                sb.AppendLine($"{indentStr}    {variableName} = default({targetType.ToDisplayString()})!;");
            }
            else
            {
                sb.AppendLine($"{indentStr}    {variableName} = default({targetType.ToDisplayString()});");
            }
            sb.AppendLine($"{indentStr}}}");
        }
    }

    private static void GenerateCollectionDeserialization(StringBuilder sb, ITypeSymbol collectionType, string variableName, string propertyLabel, int indent)
    {
        var indentStr = new string(' ', indent);
        var elementType = GraphDataModel.GetCollectionElementType(collectionType);
        if (elementType == null) return; // Should not happen for valid collections

        var isElementSimple = GraphDataModel.IsSimple(elementType);

        sb.AppendLine($"{indentStr}// Extract collection value");
        if (isElementSimple)
        {
            sb.AppendLine($"{indentStr}if (propRep.Value is SimpleCollection simpleCollection)");
            sb.AppendLine($"{indentStr}{{");
            sb.AppendLine($"{indentStr}    var collection = new List<{elementType.ToDisplayString()}>();");
            sb.AppendLine($"{indentStr}    foreach (var simpleValue in simpleCollection.Values)");
            sb.AppendLine($"{indentStr}    {{");

            // Only add null check for reference types
            if (elementType.IsReferenceType || elementType.NullableAnnotation == NullableAnnotation.Annotated)
            {
                sb.AppendLine($"{indentStr}        if (simpleValue.Object != null)");
                sb.AppendLine($"{indentStr}        {{");
                sb.AppendLine($"{indentStr}            collection.Add(({elementType.ToDisplayString()})simpleValue.Object);");
                sb.AppendLine($"{indentStr}        }}");
            }
            else
            {
                // Value types can't be null, so no null check needed
                sb.AppendLine($"{indentStr}        collection.Add(({elementType.ToDisplayString()})simpleValue.Object);");
            }

            sb.AppendLine($"{indentStr}    }}");
        }
        else
        {
            sb.AppendLine($"{indentStr}if (propRep.Value is EntityCollection entityCollection)");
            sb.AppendLine($"{indentStr}{{");
            sb.AppendLine($"{indentStr}    var collection = new List<{elementType.ToDisplayString()}>();");
            sb.AppendLine($"{indentStr}    var itemSerializer = EntitySerializerRegistry.GetSerializer(typeof({GetTypeOfName(elementType)}));");
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
            sb.AppendLine($"{indentStr}    throw new InvalidOperationException(\"Required collection property is missing or null\");");
        }
        else if (collectionType.IsReferenceType)
        {
            sb.AppendLine($"{indentStr}    {variableName} = default({collectionType.ToDisplayString()})!;");
        }
        else
        {
            sb.AppendLine($"{indentStr}    {variableName} = default({collectionType.ToDisplayString()});");
        }
        sb.AppendLine($"{indentStr}}}");
    }

    private static void GeneratePropertySetters(StringBuilder sb, List<IPropertySymbol> properties, string variableName, string entityVar)
    {
        foreach (var property in properties)
        {
            var propertyName = Utils.GetPropertyName(property);

            sb.AppendLine($"        // Look for property in both simple and complex properties");
            sb.AppendLine($"        var {property.Name.ToLower()}PropRep = {entityVar}.SimpleProperties.TryGetValue(\"{propertyName}\", out var {property.Name.ToLower()}SimpleProp) ? {property.Name.ToLower()}SimpleProp");
            sb.AppendLine($"                    : {entityVar}.ComplexProperties.TryGetValue(\"{propertyName}\", out var {property.Name.ToLower()}ComplexProp) ? {property.Name.ToLower()}ComplexProp : null;");
            sb.AppendLine($"        if ({property.Name.ToLower()}PropRep != null)");
            sb.AppendLine("        {");
            sb.AppendLine($"            var propRep = {property.Name.ToLower()}PropRep;");

            GenerateValueExtraction(sb, property.Type, $"{variableName}.{property.Name}", propertyName, 12);

            sb.AppendLine("        }");
        }
    }

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