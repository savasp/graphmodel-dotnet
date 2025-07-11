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

namespace Cvoya.Graph.Model.Serialization.CodeGen;

using Microsoft.CodeAnalysis;


internal static class Utils
{
    internal static IEnumerable<IPropertySymbol> GetAllProperties(INamedTypeSymbol type)
    {
        for (var t = type; t != null; t = t.BaseType)
        {
            var props = t.GetMembers()
                .OfType<IPropertySymbol>()
                .Where(p => !p.IsStatic && p.DeclaredAccessibility == Accessibility.Public && p.GetMethod != null);

            foreach (var prop in props)
            {
                yield return prop;
            }
        }
    }

    internal static string GetNamespaceName(INamedTypeSymbol type)
    {
        var containingNamespace = type.ContainingNamespace?.ToDisplayString();

        // Handle global namespace case
        if (string.IsNullOrEmpty(containingNamespace) || containingNamespace == "<global namespace>")
        {
            return "Generated";
        }

        return $"{containingNamespace}.Generated";
    }

    internal static string GetNestedSchemaCall(ITypeSymbol nestedType)
    {
        if (nestedType is not INamedTypeSymbol namedType)
            return "null";

        // Generate the unique serializer class name and its namespace
        var uniqueSerializerName = GetUniqueSerializerClassName(namedType);
        var serializerNamespace = GetNamespaceName(namedType);

        // Return a direct call to GetSchemaStatic which will handle cycle detection
        return $"{serializerNamespace}.{uniqueSerializerName}.GetSchemaStatic()";
    }

    internal static string GetUniqueSerializerClassName(INamedTypeSymbol type)
    {
        var parts = new List<string>();

        // Walk up the containing type hierarchy to handle nested types
        var current = type.ContainingType;
        while (current != null)
        {
            parts.Insert(0, current.Name);
            current = current.ContainingType;
        }

        // Add the type itself
        parts.Add(type.Name);

        // Create a unique class name
        return string.Join("_", parts) + "Serializer";
    }

    internal static string GetTypeOfName(ITypeSymbol type)
    {
        // For nullable reference types, get the underlying non-nullable type
        if (type.NullableAnnotation == NullableAnnotation.Annotated && !type.IsValueType)
        {
            return type.WithNullableAnnotation(NullableAnnotation.NotAnnotated).ToDisplayString();
        }

        return type.ToDisplayString();
    }

    internal static string GetPropertyName(IPropertySymbol property)
    {
        var propertyAttribute = property.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name == "PropertyAttribute");

        if (propertyAttribute?.ConstructorArguments.Length > 0)
        {
            var arg = propertyAttribute.ConstructorArguments[0];
            // Handle TypedConstant properly - check if it's an array
            if (arg.Kind == TypedConstantKind.Array)
            {
                // For arrays, take the first value
                var firstValue = arg.Values.FirstOrDefault();
                return firstValue.Value?.ToString() ?? property.Name;
            }
            else
            {
                return arg.Value?.ToString() ?? property.Name;
            }
        }

        return property.Name;
    }

    internal static bool SerializationShouldSkipProperty(IPropertySymbol property, INamedTypeSymbol type)
    {
        // Skip static properties
        if (property.IsStatic)
            return true;

        // Check if property has [Property(Ignore = true)]
        var propertyAttribute = property.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name == "PropertyAttribute" &&
                                 a.AttributeClass?.ContainingNamespace?.ToString() == "Cvoya.Graph.Model");

        if (propertyAttribute != null)
        {
            var ignoreArg = propertyAttribute.NamedArguments
                .FirstOrDefault(na => na.Key == "Ignore");

            if (ignoreArg.Value.Value is bool ignore && ignore)
                return true;
        }

        // For serialization, we need a getter
        if (property.GetMethod == null || property.DeclaredAccessibility != Accessibility.Public)
            return true;

        return false;
    }

    internal static string GetPropertyNameFromParameter(IParameterSymbol parameter)
    {
        return char.ToUpper(parameter.Name[0]) + parameter.Name.Substring(1);
    }

    internal static string GetLabelFromType(INamedTypeSymbol type)
    {
        // Check for custom label from Node attribute
        var nodeAttribute = type.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name == "NodeAttribute" &&
                                 a.AttributeClass?.ContainingNamespace?.ToString() == "Cvoya.Graph.Model");

        if (nodeAttribute?.ConstructorArguments.Length > 0)
        {
            var arg = nodeAttribute.ConstructorArguments[0];
            // Handle TypedConstant properly - check if it's an array
            if (arg.Kind == TypedConstantKind.Array)
            {
                // For arrays, take the first value
                var firstValue = arg.Values.FirstOrDefault();
                var label = firstValue.Value?.ToString();
                if (label is not null && !string.IsNullOrEmpty(label))
                    return label;
            }
            else
            {
                var label = arg.Value?.ToString();
                if (label is not null && !string.IsNullOrEmpty(label))
                    return label;
            }
        }

        // Check for Label property on Node attribute
        var labelArg = nodeAttribute?.NamedArguments
            .FirstOrDefault(na => na.Key == "Label");
        if (labelArg.HasValue && !labelArg.Equals(default(KeyValuePair<string, TypedConstant>)))
        {
            var typedConstant = labelArg.Value.Value;
            // Handle TypedConstant properly - check if it's an array
            if (typedConstant.Kind == TypedConstantKind.Array)
            {
                // For arrays, take the first value
                var firstValue = typedConstant.Values.FirstOrDefault();
                if (firstValue.Value is string labelValue && !string.IsNullOrEmpty(labelValue))
                    return labelValue;
            }
            else
            {
                if (typedConstant.Value is string labelValue && !string.IsNullOrEmpty(labelValue))
                    return labelValue;
            }
        }

        // Check for custom label from Relationship attribute
        var relationshipAttribute = type.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name == "RelationshipAttribute" &&
                                 a.AttributeClass?.ContainingNamespace?.ToString() == "Cvoya.Graph.Model");

        if (relationshipAttribute?.ConstructorArguments.Length > 0)
        {
            var arg = relationshipAttribute.ConstructorArguments[0];
            // Handle TypedConstant properly - check if it's an array
            if (arg.Kind == TypedConstantKind.Array)
            {
                // For arrays, take the first value
                var firstValue = arg.Values.FirstOrDefault();
                var label = firstValue.Value?.ToString();
                if (label is not null && !string.IsNullOrEmpty(label))
                    return label;
            }
            else
            {
                var label = arg.Value?.ToString();
                if (label is not null && !string.IsNullOrEmpty(label))
                    return label;
            }
        }

        // Check for Label property on Relationship attribute
        var relLabelArg = relationshipAttribute?.NamedArguments
            .FirstOrDefault(na => na.Key == "Label");
        if (relLabelArg.HasValue && !relLabelArg.Equals(default(KeyValuePair<string, TypedConstant>)))
        {
            var typedConstant = relLabelArg.Value.Value;
            // Handle TypedConstant properly - check if it's an array
            if (typedConstant.Kind == TypedConstantKind.Array)
            {
                // For arrays, take the first value
                var firstValue = typedConstant.Values.FirstOrDefault();
                if (firstValue.Value is string relLabelValue && !string.IsNullOrEmpty(relLabelValue))
                    return relLabelValue;
            }
            else
            {
                if (typedConstant.Value is string relLabelValue && !string.IsNullOrEmpty(relLabelValue))
                    return relLabelValue;
            }
        }

        // Fall back to the type name with backticks removed
        return type.Name.Replace("`", "");
    }

    internal static bool IsComplexProperty(IPropertySymbol property)
    {
        var propertyType = property.Type;

        // Skip standard INode/IRelationship properties that aren't really "complex"
        if (property.Name is "Id" or "StartNodeId" or "EndNodeId" or "Direction")
            return false;

        // A property is complex if it's not simple and not a collection of simple types
        return !GraphDataModel.IsSimple(propertyType) &&
               !GraphDataModel.IsCollectionOfSimple(propertyType);
    }

    internal static string GetTypeForTypeOf(ITypeSymbol type)
    {
        // For nullable value types (like int?), get the underlying type
        if (type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            var underlyingType = ((INamedTypeSymbol)type).TypeArguments[0];
            return underlyingType.ToDisplayString();
        }

        // For nullable reference types (like string?), just use the base type
        if (type.IsReferenceType && type.NullableAnnotation == NullableAnnotation.Annotated)
        {
            // Remove the ? annotation for typeof()
            return type.WithNullableAnnotation(NullableAnnotation.NotAnnotated).ToDisplayString();
        }

        // For everything else, use as-is
        return type.ToDisplayString();
    }
}