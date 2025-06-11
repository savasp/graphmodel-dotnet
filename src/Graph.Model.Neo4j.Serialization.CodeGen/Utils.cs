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
using System.Text;
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
}