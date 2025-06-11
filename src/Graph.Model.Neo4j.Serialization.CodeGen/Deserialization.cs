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
        sb.AppendLine("    public override object Deserialize(Dictionary<string, IntermediateRepresentation> entity)");
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
            sb.AppendLine($"        var result = new {type.ToDisplayString()}();");
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
            sb.AppendLine($"        if (entity.TryGetValue(\"{propertyName}\", out var {param.Name}Info))");
            sb.AppendLine("        {");

            GenerateValueExtraction(sb, param.Type, $"{param.Name}", $"{param.Name}Info", 12);

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
            sb.AppendLine($"        if (entity.TryGetValue(\"{propName}\", out var {coreProp.Name}Info))");
            sb.AppendLine("        {");

            GenerateValueExtraction(sb, coreProp.Type, $"{coreProp.Name}Temp", $"{coreProp.Name}Info", 12);

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
        sb.Append($"        var result = new {type.ToDisplayString()}(");
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

    private static void GenerateValueExtraction(StringBuilder sb, ITypeSymbol targetType, string variableName, string infoVariableName, int indent)
    {
        var indentStr = new string(' ', indent);

        // Determine the type category at compile time - no runtime checks needed
        if (GraphDataModel.IsCollectionOfSimple(targetType) || GraphDataModel.IsCollectionOfComplex(targetType))
        {
            GenerateCollectionDeserialization(sb, targetType, variableName, infoVariableName, indent);
        }
        else if (GraphDataModel.IsSimple(targetType))
        {
            sb.AppendLine($"{indentStr}{variableName} = ({targetType.ToDisplayString()}){infoVariableName}.Value!;");
        }
        else
        {
            // Complex type - recursively deserialize
            sb.AppendLine($"{indentStr}if ({infoVariableName}.Value is Dictionary<string, IntermediateRepresentation> complexDict)");
            sb.AppendLine($"{indentStr}{{");
            sb.AppendLine($"{indentStr}    var complexSerializer = EntitySerializerRegistry.GetSerializer(typeof({GetTypeOfName(targetType)}));");
            sb.AppendLine($"{indentStr}    if (complexSerializer != null)");
            sb.AppendLine($"{indentStr}    {{");
            sb.AppendLine($"{indentStr}        {variableName} = ({targetType.ToDisplayString()})complexSerializer.Deserialize(complexDict);");
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

    private static void GenerateCollectionDeserialization(StringBuilder sb, ITypeSymbol collectionType, string variableName, string infoVariableName, int indent)
    {
        var indentStr = new string(' ', indent);
        var elementType = GraphDataModel.GetCollectionElementType(collectionType);
        var isElementSimple = elementType != null && GraphDataModel.IsSimple(elementType);

        sb.AppendLine($"{indentStr}if ({infoVariableName}.Value is IList<object?> collectionItems)");
        sb.AppendLine($"{indentStr}{{");
        sb.AppendLine($"{indentStr}    var collection = new List<{elementType}>();");
        sb.AppendLine($"{indentStr}    foreach (var item in collectionItems)");
        sb.AppendLine($"{indentStr}    {{");

        if (isElementSimple)
        {
            sb.AppendLine($"{indentStr}        if (item != null)");
            sb.AppendLine($"{indentStr}        {{");
            sb.AppendLine($"{indentStr}            collection.Add(({elementType})item);");
            sb.AppendLine($"{indentStr}        }}");
        }
        else
        {
            sb.AppendLine($"{indentStr}        if (item is Dictionary<string, IntermediateRepresentation> itemDict)");
            sb.AppendLine($"{indentStr}        {{");
            sb.AppendLine($"{indentStr}            var itemSerializer = EntitySerializerRegistry.GetSerializer(typeof({GetTypeOfName(elementType!)}));");
            sb.AppendLine($"{indentStr}            if (itemSerializer != null)");
            sb.AppendLine($"{indentStr}            {{");
            sb.AppendLine($"{indentStr}                collection.Add(({elementType})itemSerializer.Deserialize(itemDict));");
            sb.AppendLine($"{indentStr}            }}");
            sb.AppendLine($"{indentStr}            else");
            sb.AppendLine($"{indentStr}            {{");
            sb.AppendLine($"{indentStr}                throw new InvalidOperationException($\"No serializer found for element type {elementType}\");");
            sb.AppendLine($"{indentStr}            }}");
            sb.AppendLine($"{indentStr}        }}");
        }

        sb.AppendLine($"{indentStr}    }}");

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

            sb.AppendLine($"        if ({entityVar}.TryGetValue(\"{propertyName}\", out var {property.Name.ToLower()}Info))");
            sb.AppendLine("        {");

            GenerateValueExtraction(sb, property.Type, $"{variableName}.{property.Name}", $"{property.Name.ToLower()}Info", 12);

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