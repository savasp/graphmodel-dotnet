// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Serialization.CodeGen;

using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;


/// <summary>
/// Emits syntax-correct, culture-invariant C# literal expressions for the compile-time default
/// values of optional constructor parameters. The generated deserializer substitutes one of these
/// expressions when serialized data omits a constructor argument, so each must compile in a context
/// carrying only the generator's standard usings and must select the declared default value exactly
/// - across escaped strings/chars, numeric widths that need suffixes/casts, and named or cast-only
/// enum values - independent of the build machine's current culture (#372).
/// </summary>
internal static class DefaultValueFormatter
{
    private static readonly SymbolDisplayFormat FullyQualified = SymbolDisplayFormat.FullyQualifiedFormat;

    /// <summary>
    /// Formats <paramref name="value"/> (an <see cref="IParameterSymbol.ExplicitDefaultValue"/>) as a
    /// C# expression assignable to <paramref name="type"/>.
    /// </summary>
    public static string Format(object? value, ITypeSymbol type)
    {
        // `value` is null both for reference-type/nullable defaults (`= null`) and for a value-type
        // `= default`. A reference type takes the bare `null` literal; anything else takes a fully
        // qualified `default(T)` so a value-type default (including `T?`) resolves without usings.
        if (value is null)
        {
            return type.IsReferenceType
                ? "null"
                : $"default({type.ToDisplayString(FullyQualified)})";
        }

        // An enum parameter's ExplicitDefaultValue is the boxed underlying integral, not an enum
        // instance, so format it against the enum type (unwrapping `TEnum?`).
        if (UnwrapNullable(type) is INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType)
        {
            return FormatEnum(value, enumType);
        }

        return value switch
        {
            string text => SymbolDisplay.FormatLiteral(text, quote: true),
            char character => SymbolDisplay.FormatLiteral(character, quote: true),
            bool boolean => boolean ? "true" : "false",
            float single => FormatSingle(single),
            double dbl => FormatDouble(dbl),
            decimal dec => dec.ToString(CultureInfo.InvariantCulture) + "M",
            long or ulong or uint or int or short or ushort or byte or sbyte => FormatIntegral(value),
            _ => SymbolDisplay.FormatPrimitive(value, quoteStrings: true, useHexadecimalNumbers: false)
                ?? $"default({type.ToDisplayString(FullyQualified)})",
        };
    }

    private static string FormatEnum(object value, INamedTypeSymbol enumType)
    {
        var qualifiedName = enumType.ToDisplayString(FullyQualified);

        // A qualified member reference when the value names exactly one enum member...
        foreach (var member in enumType.GetMembers().OfType<IFieldSymbol>())
        {
            if (member is { IsConst: true, HasConstantValue: true } && Equals(member.ConstantValue, value))
            {
                return $"{qualifiedName}.{EscapeIdentifier(member.Name)}";
            }
        }

        // ...otherwise a cast of the correctly formatted underlying constant, covering unnamed or
        // flag-combination values that map to no single member.
        return $"({qualifiedName})({FormatIntegral(value)})";
    }

    private static string EscapeIdentifier(string identifier)
    {
        return SyntaxFacts.GetKeywordKind(identifier) != SyntaxKind.None ||
               SyntaxFacts.GetContextualKeywordKind(identifier) != SyntaxKind.None
            ? $"@{identifier}"
            : identifier;
    }

    private static string FormatIntegral(object value)
    {
        // The narrow integrals (short/ushort/byte/sbyte) have no literal suffix and, written bare,
        // are typed `int`. The deserializer places this expression in a conditional whose other
        // branch is the narrow target type; the conditional's natural type then widens to `int` and
        // the assignment back to the narrow variable fails to compile (CS0266). An explicit cast
        // pins both branches to the exact narrow type. `long`/`ulong`/`uint` carry a width suffix
        // and need no cast.
        return value switch
        {
            long l => l.ToString(CultureInfo.InvariantCulture) + "L",
            ulong ul => ul.ToString(CultureInfo.InvariantCulture) + "UL",
            uint ui => ui.ToString(CultureInfo.InvariantCulture) + "U",
            int i => i.ToString(CultureInfo.InvariantCulture),
            short s => $"(short){s.ToString(CultureInfo.InvariantCulture)}",
            ushort us => $"(ushort){us.ToString(CultureInfo.InvariantCulture)}",
            byte b => $"(byte){b.ToString(CultureInfo.InvariantCulture)}",
            sbyte sb => $"(sbyte){sb.ToString(CultureInfo.InvariantCulture)}",
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "0",
        };
    }

    private static string FormatSingle(float value)
    {
        if (float.IsNaN(value)) return "float.NaN";
        if (float.IsPositiveInfinity(value)) return "float.PositiveInfinity";
        if (float.IsNegativeInfinity(value)) return "float.NegativeInfinity";

        // "G9" is the documented round-trip format for Single (unlike "R", which can fail to
        // round-trip a float on some hosts); the "F" suffix keeps it a float literal in the ternary.
        return value.ToString("G9", CultureInfo.InvariantCulture) + "F";
    }

    private static string FormatDouble(double value)
    {
        if (double.IsNaN(value)) return "double.NaN";
        if (double.IsPositiveInfinity(value)) return "double.PositiveInfinity";
        if (double.IsNegativeInfinity(value)) return "double.NegativeInfinity";

        // "G17" is the documented round-trip format for Double.
        return value.ToString("G17", CultureInfo.InvariantCulture) + "D";
    }

    private static ITypeSymbol UnwrapNullable(ITypeSymbol type)
    {
        return type is INamedTypeSymbol { IsGenericType: true } namedType &&
               namedType.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T
            ? namedType.TypeArguments[0]
            : type;
    }
}
