// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Serialization;

using System.Collections;
using System.Text;
using Cvoya.Graph.Serialization.Results;

/// <summary>Encodes and decodes the private physical representation of simple collections.</summary>
internal static class SimpleCollectionStorageCodec
{
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    internal const string Prefix = "__cvoya_sc:v1:";
    internal const string NullIndexesPrefix = Prefix + "n:";
    internal const string ElementTypePrefix = Prefix + "t:";
    private const string UserPropertyPrefix = Prefix + "u:";

    internal static string GetPayloadPropertyName(string logicalName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logicalName);
        return logicalName.StartsWith(Prefix, StringComparison.Ordinal)
            ? UserPropertyPrefix + EncodeName(logicalName)
            : logicalName;
    }

    internal static string GetNullIndexesPropertyName(string logicalName) =>
        NullIndexesPrefix + EncodeName(logicalName);

    internal static string GetElementTypePropertyName(string logicalName) =>
        ElementTypePrefix + EncodeName(logicalName);

    internal static IReadOnlyList<string> GetCompanionPropertyNames(string logicalName) =>
        [GetNullIndexesPropertyName(logicalName), GetElementTypePropertyName(logicalName)];

    internal static Dictionary<string, object?> EncodeProperties(
        IDictionary<string, Property> properties,
        bool omitNullPayloads,
        Func<object?, object?> convert)
    {
        ArgumentNullException.ThrowIfNull(properties);
        ArgumentNullException.ThrowIfNull(convert);

        var encoded = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (logicalName, property) in properties)
        {
            switch (property.Value)
            {
                case null:
                    break;
                case SimpleValue scalar:
                    encoded.Add(GetPayloadPropertyName(logicalName), convert(scalar.Object));
                    break;
                case SimpleCollection collection:
                    foreach (var value in EncodeCollection(logicalName, collection, omitNullPayloads, convert))
                    {
                        encoded.Add(value.StorageName, value.Value);
                    }

                    break;
                default:
                    throw new GraphException("Unexpected value type in simple properties.");
            }
        }

        return encoded;
    }

    internal static IReadOnlyList<StoredPropertyValue> EncodeValue(
        string logicalName,
        Type? declaredType,
        object? value,
        bool omitNullPayloads,
        Func<object?, object?> convert)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logicalName);
        ArgumentNullException.ThrowIfNull(convert);

        if (TryGetCollectionElementType(declaredType, value?.GetType(), out var elementType) &&
            value is IEnumerable enumerable)
        {
            var items = enumerable.Cast<object?>()
                .Select(item => new SimpleValue(item!, elementType))
                .ToList();
            return EncodeCollection(
                logicalName,
                new SimpleCollection(items, elementType),
                omitNullPayloads,
                convert);
        }

        return
        [
            new(GetPayloadPropertyName(logicalName), convert(value)),
            new(GetNullIndexesPropertyName(logicalName), null),
            new(GetElementTypePropertyName(logicalName), null),
        ];
    }

    internal static IReadOnlyDictionary<string, GraphValue> DecodeProperties(
        IReadOnlyDictionary<string, GraphValue> physicalProperties,
        bool payloadOmitsNulls)
    {
        ArgumentNullException.ThrowIfNull(physicalProperties);

        var decoded = new Dictionary<string, GraphValue>(StringComparer.Ordinal);
        foreach (var (physicalName, value) in physicalProperties)
        {
            if (IsCompanionProperty(physicalName))
            {
                continue;
            }

            var logicalName = DecodePayloadPropertyName(physicalName);
            var typeName = GetElementTypePropertyName(logicalName);
            var nullIndexesName = GetNullIndexesPropertyName(logicalName);
            var hasType = physicalProperties.TryGetValue(typeName, out var typeValue);
            var hasNullIndexes = physicalProperties.TryGetValue(nullIndexesName, out var nullIndexesValue);
            if (!hasType && !hasNullIndexes)
            {
                decoded.Add(logicalName, value);
                continue;
            }

            if (!hasType || !hasNullIndexes)
            {
                throw InvalidEncoding(logicalName, "both collection companions must be present");
            }

            var elementType = ReadElementType(logicalName, typeValue!);
            var nullIndexes = ReadNullIndexes(logicalName, nullIndexesValue!);
            if (value.Kind != GraphValueKind.List)
            {
                throw InvalidEncoding(logicalName, "the collection payload is not a list");
            }

            var logicalLength = payloadOmitsNulls
                ? checked(value.Items.Count + nullIndexes.Count)
                : value.Items.Count;
            ValidateNullIndexes(logicalName, nullIndexes, logicalLength);

            IReadOnlyList<GraphValue> items;
            if (payloadOmitsNulls)
            {
                var nullIndexSet = nullIndexes.ToHashSet();
                var payloadIndex = 0;
                var reconstructed = new List<GraphValue>(logicalLength);
                for (var index = 0; index < logicalLength; index++)
                {
                    reconstructed.Add(nullIndexSet.Contains(index)
                        ? GraphValue.Scalar(null)
                        : value.Items[payloadIndex++]);
                }

                items = reconstructed;
            }
            else
            {
                var actualNullIndexes = value.Items
                    .Select((item, index) => (item, index))
                    .Where(pair => pair.item.Kind == GraphValueKind.Scalar && pair.item.ScalarValue is null)
                    .Select(pair => pair.index)
                    .ToArray();
                if (!actualNullIndexes.SequenceEqual(nullIndexes))
                {
                    throw InvalidEncoding(logicalName, "the null-index companion does not match the native list");
                }

                items = value.Items;
            }

            decoded.Add(logicalName, GraphValue.List(items, elementType));
        }

        foreach (var physicalName in physicalProperties.Keys.Where(name => name.StartsWith(Prefix, StringComparison.Ordinal)))
        {
            if (!IsCompanionProperty(physicalName) && !physicalName.StartsWith(UserPropertyPrefix, StringComparison.Ordinal))
            {
                throw new GraphException($"Invalid private simple-collection property '{physicalName}'.");
            }

            if (IsCompanionProperty(physicalName) && !HasMatchingPayload(physicalProperties, physicalName))
            {
                throw new GraphException($"Orphaned private simple-collection companion '{physicalName}'.");
            }
        }

        return decoded;
    }

    internal static string GetTypeIdentity(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        var identity = type.AssemblyQualifiedName ?? type.FullName ?? type.Name;
        const string marker = ", Version=";
        var versionStart = identity.IndexOf(marker, StringComparison.Ordinal);
        if (versionStart < 0)
        {
            return identity;
        }

        var result = new StringBuilder(identity.Length);
        var copyStart = 0;
        while (versionStart >= 0)
        {
            result.Append(identity, copyStart, versionStart - copyStart);
            var versionEnd = identity.IndexOfAny([',', ']'], versionStart + marker.Length);
            if (versionEnd < 0)
            {
                return result.ToString();
            }

            copyStart = versionEnd;
            versionStart = identity.IndexOf(marker, copyStart, StringComparison.Ordinal);
        }

        result.Append(identity, copyStart, identity.Length - copyStart);
        return result.ToString();
    }

    private static IReadOnlyList<StoredPropertyValue> EncodeCollection(
        string logicalName,
        SimpleCollection collection,
        bool omitNullPayloads,
        Func<object?, object?> convert)
    {
        var payload = new List<object?>();
        var nullIndexes = new List<int>();
        var index = 0;
        foreach (var item in collection.Values)
        {
            if (item.Object is null)
            {
                nullIndexes.Add(index);
                if (!omitNullPayloads)
                {
                    payload.Add(null);
                }
            }
            else
            {
                payload.Add(convert(item.Object));
            }

            index++;
        }

        return
        [
            new(GetPayloadPropertyName(logicalName), payload),
            new(GetNullIndexesPropertyName(logicalName), nullIndexes),
            new(GetElementTypePropertyName(logicalName), GetTypeIdentity(collection.ElementType)),
        ];
    }

    private static bool TryGetCollectionElementType(Type? declaredType, Type? runtimeType, out Type elementType)
    {
        // byte[] classifies as both a simple scalar and a simple collection; EntityFactory gives
        // IsSimple precedence, so such values must stay native scalars here too.
        var sourceType = declaredType ?? runtimeType;
        if (sourceType is null || GraphDataModel.IsSimple(sourceType))
        {
            elementType = null!;
            return false;
        }

        elementType = GraphResultTypeHelpers.GetCollectionElementType(sourceType)!;
        return elementType is not null && GraphDataModel.IsSimple(elementType);
    }

    private static Type ReadElementType(string logicalName, GraphValue value)
    {
        if (value.Kind != GraphValueKind.Scalar || value.ScalarValue is not string typeName)
        {
            throw InvalidEncoding(logicalName, "the element-type companion is not a string");
        }

        var elementType = Type.GetType(typeName, throwOnError: false) ??
            throw InvalidEncoding(logicalName, $"the element type '{typeName}' cannot be resolved");
        if (!GraphDataModel.IsSimple(elementType))
        {
            throw InvalidEncoding(logicalName, $"the element type '{typeName}' is not a simple graph value");
        }

        if (!string.Equals(GetTypeIdentity(elementType), typeName, StringComparison.Ordinal))
        {
            throw InvalidEncoding(logicalName, $"the element type '{typeName}' is not canonical");
        }

        return elementType;
    }

    private static List<int> ReadNullIndexes(string logicalName, GraphValue value)
    {
        if (value.Kind != GraphValueKind.List)
        {
            throw InvalidEncoding(logicalName, "the null-index companion is not a list");
        }

        var result = new List<int>(value.Items.Count);
        foreach (var item in value.Items)
        {
            try
            {
                if (item.Kind != GraphValueKind.Scalar || item.ScalarValue is null)
                {
                    throw new InvalidCastException();
                }

                result.Add(item.ScalarValue switch
                {
                    sbyte number => number,
                    byte number => number,
                    short number => number,
                    ushort number => number,
                    int number => number,
                    uint number => checked((int)number),
                    long number => checked((int)number),
                    ulong number => checked((int)number),
                    _ => throw new InvalidCastException(),
                });
            }
            catch (Exception exception) when (exception is InvalidCastException or FormatException or OverflowException)
            {
                throw InvalidEncoding(logicalName, "the null-index companion contains a non-integer index");
            }
        }

        return result;
    }

    private static void ValidateNullIndexes(string logicalName, IReadOnlyList<int> indexes, int logicalLength)
    {
        var previous = -1;
        foreach (var index in indexes)
        {
            if (index < 0 || index >= logicalLength || index <= previous)
            {
                throw InvalidEncoding(logicalName, "null indexes must be strictly increasing and within the logical list");
            }

            previous = index;
        }
    }

    private static bool IsCompanionProperty(string name) =>
        name.StartsWith(NullIndexesPrefix, StringComparison.Ordinal) ||
        name.StartsWith(ElementTypePrefix, StringComparison.Ordinal);

    private static bool HasMatchingPayload(IReadOnlyDictionary<string, GraphValue> properties, string companionName)
    {
        var encodedName = companionName.StartsWith(NullIndexesPrefix, StringComparison.Ordinal)
            ? companionName[NullIndexesPrefix.Length..]
            : companionName[ElementTypePrefix.Length..];
        var logicalName = DecodeName(encodedName);
        return properties.ContainsKey(GetPayloadPropertyName(logicalName));
    }

    private static string DecodePayloadPropertyName(string physicalName)
    {
        if (!physicalName.StartsWith(UserPropertyPrefix, StringComparison.Ordinal))
        {
            return physicalName;
        }

        var logicalName = DecodeName(physicalName[UserPropertyPrefix.Length..]);
        if (!logicalName.StartsWith(Prefix, StringComparison.Ordinal))
        {
            throw new GraphException($"Invalid escaped simple-collection property '{physicalName}'.");
        }

        return logicalName;
    }

    private static string EncodeName(string value) => Convert.ToBase64String(StrictUtf8.GetBytes(value))
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');

    private static string DecodeName(string value)
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
            throw new GraphException("Invalid Base64Url name in private simple-collection storage.", exception);
        }
    }

    private static GraphException InvalidEncoding(string logicalName, string detail) =>
        new($"Invalid simple-collection storage for property '{logicalName}': {detail}.");

    internal sealed record StoredPropertyValue(string StorageName, object? Value);
}
