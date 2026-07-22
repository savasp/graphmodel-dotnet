// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Serialization.CodeGen;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;


internal static class Utils
{
    internal static IEnumerable<IPropertySymbol> GetAllProperties(INamedTypeSymbol type)
    {
        var seenProperties = new HashSet<string>(StringComparer.Ordinal);

        for (var t = type; t != null; t = t.BaseType)
        {
            var props = t.GetMembers()
                .OfType<IPropertySymbol>()
                .Where(p => !p.IsStatic &&
                    !p.IsIndexer &&
                    p.DeclaredAccessibility == Accessibility.Public &&
                    p.GetMethod != null &&
                    seenProperties.Add(p.Name));

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

        // Constructed generic types need a type-identity component. The readable name alone maps
        // GenericThing<int> and GenericThing<string> (and nested types inside generic containers)
        // to the same generated class. Keep existing names stable for non-generic types and append
        // a deterministic suffix only where the generic construction participates in identity.
        if (RequiresGenericIdentitySuffix(type))
        {
            parts.Add(GetStableTypeIdentitySuffix(type));
        }

        // Create a unique class name
        return string.Join("_", parts) + "Serializer";
    }

    internal static string GetStableTypeIdentitySuffix(INamedTypeSymbol type)
    {
        var identity = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(identity));
        var suffix = new StringBuilder(capacity: 32);

        // 128 bits is compact enough for generated identifiers while making accidental collisions
        // between serializer type identities vanishingly unlikely.
        for (var index = 0; index < 16; index++)
        {
            suffix.Append(hash[index].ToString("X2", CultureInfo.InvariantCulture));
        }

        return suffix.ToString();
    }

    internal static bool RequiresGenericIdentitySuffix(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.ContainingType)
        {
            if (current.IsGenericType)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Escapes a consumer-provided string (e.g. a <c>[Node(...)]</c>, <c>[Relationship(...)]</c>, or
    /// <c>[Property(Label = ...)]</c> value, which may legally contain quotes, backslashes, or
    /// control characters) for embedding inside an ordinary generated string literal. The result is
    /// the literal's content only - callers supply the surrounding quotes - and decodes back to the
    /// exact input, so the runtime label value is preserved.
    /// </summary>
    /// <remarks>
    /// <see cref="SymbolDisplay.FormatLiteral(string, bool)"/> only escapes the quote character when
    /// it is also emitting the quotes, so the quoted form is requested and then unwrapped. That form
    /// is always a regular (non-verbatim) literal here, because this overload always escapes
    /// non-printable characters, which rules out the verbatim form even for values containing a
    /// newline. Ordinary labels come back unchanged, keeping generated output byte-stable.
    /// </remarks>
    internal static string EscapeForGeneratedStringLiteral(string value)
    {
        var literal = SymbolDisplay.FormatLiteral(value, quote: true);

        return literal.Substring(1, literal.Length - 2);
    }

    /// <summary>
    /// Escapes a consumer-provided string for embedding inside a generated <em>interpolated</em>
    /// string literal: string-literal escapes first (see
    /// <see cref="EscapeForGeneratedStringLiteral"/>), then doubled braces so the text cannot
    /// terminate the literal or open an interpolation hole. Brace doubling belongs only to this
    /// context - applying it to an ordinary literal would change the runtime value.
    /// </summary>
    internal static string EscapeForGeneratedInterpolatedString(string value) =>
        EscapeForGeneratedStringLiteral(value)
            .Replace("{", "{{")
            .Replace("}", "}}");

    /// <summary>
    /// Emits a consumer-provided semantic name as a valid C# identifier. Roslyn symbols expose the
    /// semantic name without a source-level <c>@</c>, so reserved and contextual keywords are
    /// escaped here while ordinary and Unicode identifiers remain byte-for-byte unchanged.
    /// </summary>
    internal static string EscapeIdentifier(string identifier)
    {
        return SyntaxFacts.GetKeywordKind(identifier) != SyntaxKind.None ||
               SyntaxFacts.GetContextualKeywordKind(identifier) != SyntaxKind.None
            ? $"@{identifier}"
            : identifier;
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
            .FirstOrDefault(a => a.AttributeClass?.Name == "PropertyAttribute" &&
                                 a.AttributeClass.ContainingNamespace?.ToString() == "Cvoya.Graph");

        var labelArgument = propertyAttribute?.NamedArguments
            .FirstOrDefault(argument => argument.Key == "Label");

        if (labelArgument is { Value.Value: string { Length: > 0 } label })
        {
            return label;
        }

        return property.Name;
    }

    internal static bool SerializationShouldSkipProperty(IPropertySymbol property, INamedTypeSymbol type)
    {
        // Static properties and indexers are not part of the serialized property graph.
        if (property.IsStatic || property.IsIndexer)
            return true;

        // Check if property has [Property(Ignore = true)]
        var propertyAttribute = property.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name == "PropertyAttribute" &&
                                 a.AttributeClass?.ContainingNamespace?.ToString() == "Cvoya.Graph");

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
                                 a.AttributeClass?.ContainingNamespace?.ToString() == "Cvoya.Graph");

        if (nodeAttribute is { ConstructorArguments.Length: > 0 })
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
                                 a.AttributeClass?.ContainingNamespace?.ToString() == "Cvoya.Graph");

        if (relationshipAttribute is { ConstructorArguments.Length: > 0 })
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
