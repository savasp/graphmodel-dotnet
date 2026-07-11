// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age;

using System.Text.RegularExpressions;

internal static partial class AgeSqlIdentifier
{
    private const string IdentifierPattern = "^[A-Za-z_][A-Za-z0-9_]*$";

    public static string Validate(string value, string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (!IdentifierRegex().IsMatch(value))
        {
            throw new ArgumentException(
                $"The {description} '{value}' is not valid. AGE SQL identifiers must match {IdentifierPattern}.",
                nameof(value));
        }

        return value;
    }

    public static string Quote(string value, string description) => $"\"{Validate(value, description)}\"";

    [GeneratedRegex(IdentifierPattern, RegexOptions.CultureInvariant)]
    private static partial Regex IdentifierRegex();
}
