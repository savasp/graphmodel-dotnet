// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph;

using System.Text;

/// <summary>Defines the provider-neutral physical names for complex-collection layout metadata.</summary>
internal static class ComplexCollectionStorageNames
{
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    internal const string Prefix = "__cvoya_sc:v1:c:";
    internal const string LengthPrefix = Prefix + "l:";
    internal const string NullIndexesPrefix = Prefix + "n:";
    internal const string ElementTypePrefix = Prefix + "t:";
    internal const string RelationshipTypePrefix = Prefix + "r:";
    internal const string MutationLockProperty = Prefix + "lock";

    internal static string GetLengthPropertyName(string logicalName) =>
        LengthPrefix + EncodeName(logicalName);

    internal static string GetNullIndexesPropertyName(string logicalName) =>
        NullIndexesPrefix + EncodeName(logicalName);

    internal static string GetElementTypePropertyName(string logicalName) =>
        ElementTypePrefix + EncodeName(logicalName);

    internal static string GetRelationshipTypePropertyName(string logicalName) =>
        RelationshipTypePrefix + EncodeName(logicalName);

    internal static string EncodeName(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return Convert.ToBase64String(StrictUtf8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    internal static string DecodeName(string value)
    {
        try
        {
            if (value.Length == 0 || value.Any(character =>
                character is not (>= 'A' and <= 'Z') and
                    not (>= 'a' and <= 'z') and
                    not (>= '0' and <= '9') and
                    not '-' and not '_'))
            {
                throw new FormatException();
            }

            var base64 = value.Replace('-', '+').Replace('_', '/');
            base64 += new string('=', (4 - base64.Length % 4) % 4);
            var decoded = StrictUtf8.GetString(Convert.FromBase64String(base64));
            if (!string.Equals(EncodeName(decoded), value, StringComparison.Ordinal))
            {
                throw new FormatException();
            }

            return decoded;
        }
        catch (Exception exception) when (exception is FormatException or DecoderFallbackException)
        {
            throw new GraphException("Invalid Base64Url name in private complex-collection storage.", exception);
        }
    }
}
