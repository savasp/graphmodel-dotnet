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

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Cvoya.Graph.Model.Neo4j.Serialization.CodeGen;

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
        var namespaceName = type.ContainingNamespace?.ToDisplayString();

        if (namespaceName is null || namespaceName == "<global namespace>")
        {
            return "Generated";
        }

        return namespaceName + ".Generated";
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
            return propertyAttribute.ConstructorArguments[0].Value?.ToString() ?? property.Name;
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
            var label = nodeAttribute.ConstructorArguments[0].Value?.ToString();
            if (label is not null && !string.IsNullOrEmpty(label))
                return label;
        }

        // Check for Label property on Node attribute
        var labelArg = nodeAttribute?.NamedArguments
            .FirstOrDefault(na => na.Key == "Label");
        if (labelArg?.Value.Value is string labelValue && !string.IsNullOrEmpty(labelValue))
            return labelValue;

        // Check for custom label from Relationship attribute
        var relationshipAttribute = type.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name == "RelationshipAttribute" &&
                                 a.AttributeClass?.ContainingNamespace?.ToString() == "Cvoya.Graph.Model");

        if (relationshipAttribute?.ConstructorArguments.Length > 0)
        {
            var label = relationshipAttribute.ConstructorArguments[0].Value?.ToString();
            if (label is not null && !string.IsNullOrEmpty(label))
                return label;
        }

        // Check for Label property on Relationship attribute
        var relLabelArg = relationshipAttribute?.NamedArguments
            .FirstOrDefault(na => na.Key == "Label");
        if (relLabelArg?.Value.Value is string relLabelValue && !string.IsNullOrEmpty(relLabelValue))
            return relLabelValue;

        // Fall back to the type name with backticks removed
        return type.Name.Replace("`", "");
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