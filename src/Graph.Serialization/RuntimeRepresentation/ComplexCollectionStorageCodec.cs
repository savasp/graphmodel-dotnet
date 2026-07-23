// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Serialization;

using Cvoya.Graph.Serialization.Results;

/// <summary>Encodes and decodes the private physical layout of complex collection slots.</summary>
internal static class ComplexCollectionStorageCodec
{
    // Nest this namespace below the simple-collection prefix so that its existing user-property
    // escaping also protects every complex-collection companion name from collisions.
    internal const string Prefix = ComplexCollectionStorageNames.Prefix;
    internal const string LengthPrefix = ComplexCollectionStorageNames.LengthPrefix;
    internal const string NullIndexesPrefix = ComplexCollectionStorageNames.NullIndexesPrefix;
    internal const string ElementTypePrefix = ComplexCollectionStorageNames.ElementTypePrefix;
    internal const string RelationshipTypePrefix = ComplexCollectionStorageNames.RelationshipTypePrefix;
    internal const string MutationLockProperty = ComplexCollectionStorageNames.MutationLockProperty;

    internal static string GetLengthPropertyName(string logicalName) =>
        ComplexCollectionStorageNames.GetLengthPropertyName(logicalName);

    internal static string GetNullIndexesPropertyName(string logicalName) =>
        ComplexCollectionStorageNames.GetNullIndexesPropertyName(logicalName);

    internal static string GetElementTypePropertyName(string logicalName) =>
        ComplexCollectionStorageNames.GetElementTypePropertyName(logicalName);

    internal static string GetRelationshipTypePropertyName(string logicalName) =>
        ComplexCollectionStorageNames.GetRelationshipTypePropertyName(logicalName);

    internal static IReadOnlyList<string> GetCompanionPropertyNames(string logicalName) =>
        [
            GetLengthPropertyName(logicalName),
            GetNullIndexesPropertyName(logicalName),
            GetElementTypePropertyName(logicalName),
            GetRelationshipTypePropertyName(logicalName),
        ];

    internal static Dictionary<string, object?> EncodeProperties(
        IDictionary<string, Property> properties,
        Func<object?, object?> convert)
    {
        ArgumentNullException.ThrowIfNull(properties);
        ArgumentNullException.ThrowIfNull(convert);

        var encoded = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (logicalName, property) in properties)
        {
            if (property.Value is not EntityCollection collection)
            {
                continue;
            }

            var nullIndexes = collection.Entities
                .Select((entity, index) => (entity, index))
                .Where(item => item.entity is null)
                .Select(item => convert(item.index))
                .ToList();
            encoded.Add(GetLengthPropertyName(logicalName), convert(collection.Entities.Count));
            encoded.Add(GetNullIndexesPropertyName(logicalName), nullIndexes);
            encoded.Add(
                GetElementTypePropertyName(logicalName),
                convert(SimpleCollectionStorageCodec.GetTypeIdentity(collection.Type)));
            var relationshipType = property.RelationshipType ?? (property.PropertyInfo is not null
                ? GraphDataModel.GetComplexPropertyRelationshipType(property.PropertyInfo)
                : GraphDataModel.PropertyNameToRelationshipTypeName(logicalName));
            encoded.Add(GetRelationshipTypePropertyName(logicalName), convert(relationshipType));
        }

        return encoded;
    }

    internal static EntityCollection? Rehydrate(
        string logicalName,
        Type expectedElementType,
        IReadOnlyDictionary<string, GraphValue> ownerProperties,
        IReadOnlyList<(int SequenceNumber, EntityInfo Entity)> storedEntities,
        string? expectedRelationshipType = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logicalName);
        ArgumentNullException.ThrowIfNull(expectedElementType);
        ArgumentNullException.ThrowIfNull(ownerProperties);
        ArgumentNullException.ThrowIfNull(storedEntities);

        if (!TryReadMetadata(logicalName, ownerProperties, out var metadata))
        {
            if (storedEntities.Count == 0)
            {
                return null;
            }

            // The final reader naturally accepts the former dense in-process shape. This is not a
            // deployed-storage compatibility promise: every newly persisted collection has metadata.
            return new EntityCollection(
                expectedElementType,
                [.. storedEntities.OrderBy(item => item.SequenceNumber).Select(item => item.Entity)]);
        }

        if (expectedElementType != typeof(object) && metadata.ElementType != expectedElementType)
        {
            throw InvalidEncoding(
                logicalName,
                $"the stored element type '{metadata.ElementType}' does not match the declared element type '{expectedElementType}'");
        }

        if (expectedRelationshipType is not null &&
            !string.Equals(metadata.RelationshipType, expectedRelationshipType, StringComparison.Ordinal))
        {
            throw InvalidEncoding(
                logicalName,
                $"the stored relationship type '{metadata.RelationshipType}' does not match the declared relationship type '{expectedRelationshipType}'");
        }

        return Rehydrate(
            logicalName,
            expectedElementType,
            metadata.Length,
            metadata.NullIndexes,
            metadata.ElementType,
            storedEntities);
    }

    internal static EntityCollection Rehydrate(
        string logicalName,
        Type expectedElementType,
        int logicalLength,
        IReadOnlyList<int> nullIndexes,
        Type storedElementType,
        IReadOnlyList<(int SequenceNumber, EntityInfo Entity)> storedEntities)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logicalName);
        ArgumentNullException.ThrowIfNull(expectedElementType);
        ArgumentNullException.ThrowIfNull(nullIndexes);
        ArgumentNullException.ThrowIfNull(storedElementType);
        ArgumentNullException.ThrowIfNull(storedEntities);
        if (logicalLength < 0)
        {
            throw InvalidEncoding(logicalName, "the logical length cannot be negative");
        }

        ValidateNullIndexes(logicalName, nullIndexes, logicalLength);
        if (expectedElementType != typeof(object) && storedElementType != expectedElementType)
        {
            throw InvalidEncoding(
                logicalName,
                $"the stored element type '{storedElementType}' does not match the declared element type '{expectedElementType}'");
        }

        var slots = new EntityInfo?[logicalLength];
        var occupied = new bool[logicalLength];
        foreach (var index in nullIndexes)
        {
            occupied[index] = true;
        }

        foreach (var (sequenceNumber, entity) in storedEntities)
        {
            if (sequenceNumber < 0 || sequenceNumber >= logicalLength)
            {
                throw InvalidEncoding(logicalName, $"child sequence {sequenceNumber} is outside the logical collection length");
            }

            if (occupied[sequenceNumber])
            {
                throw InvalidEncoding(logicalName, $"collection index {sequenceNumber} is represented more than once");
            }

            occupied[sequenceNumber] = true;
            slots[sequenceNumber] = entity;
        }

        var missingIndex = Array.FindIndex(occupied, present => !present);
        if (missingIndex >= 0)
        {
            throw InvalidEncoding(logicalName, $"collection index {missingIndex} has neither a child nor a null slot");
        }

        return new EntityCollection(
            expectedElementType == typeof(object) ? storedElementType : expectedElementType,
            slots);
    }

    internal static IReadOnlyList<string> GetMetadataLogicalNames(
        IReadOnlyDictionary<string, GraphValue> properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var physicalName in properties.Keys.Where(name => name.StartsWith(Prefix, StringComparison.Ordinal)))
        {
            var encodedName = physicalName.StartsWith(LengthPrefix, StringComparison.Ordinal)
                ? physicalName[LengthPrefix.Length..]
                : physicalName.StartsWith(NullIndexesPrefix, StringComparison.Ordinal)
                    ? physicalName[NullIndexesPrefix.Length..]
                    : physicalName.StartsWith(ElementTypePrefix, StringComparison.Ordinal)
                        ? physicalName[ElementTypePrefix.Length..]
                        : physicalName.StartsWith(RelationshipTypePrefix, StringComparison.Ordinal)
                            ? physicalName[RelationshipTypePrefix.Length..]
                            : throw new GraphException($"Invalid private complex-collection property '{physicalName}'.");
            names.Add(ComplexCollectionStorageNames.DecodeName(encodedName));
        }

        foreach (var name in names)
        {
            _ = TryReadMetadata(name, properties, out _);
        }

        return [.. names.Order(StringComparer.Ordinal)];
    }

    internal static bool IsMetadataProperty(string name) =>
        name.StartsWith(LengthPrefix, StringComparison.Ordinal) ||
        name.StartsWith(NullIndexesPrefix, StringComparison.Ordinal) ||
        name.StartsWith(ElementTypePrefix, StringComparison.Ordinal) ||
        name.StartsWith(RelationshipTypePrefix, StringComparison.Ordinal);

    internal static bool HasMetadata(
        string logicalName,
        IReadOnlyDictionary<string, GraphValue> properties) =>
        properties.ContainsKey(GetLengthPropertyName(logicalName)) ||
        properties.ContainsKey(GetNullIndexesPropertyName(logicalName)) ||
        properties.ContainsKey(GetElementTypePropertyName(logicalName)) ||
        properties.ContainsKey(GetRelationshipTypePropertyName(logicalName));

    internal static string GetRelationshipType(
        string logicalName,
        IReadOnlyDictionary<string, GraphValue> properties)
    {
        if (!TryReadMetadata(logicalName, properties, out var metadata))
        {
            throw InvalidEncoding(logicalName, "collection metadata is missing");
        }

        return metadata.RelationshipType;
    }

    private static bool TryReadMetadata(
        string logicalName,
        IReadOnlyDictionary<string, GraphValue> properties,
        out Metadata metadata)
    {
        var hasLength = properties.TryGetValue(GetLengthPropertyName(logicalName), out var lengthValue);
        var hasNullIndexes = properties.TryGetValue(GetNullIndexesPropertyName(logicalName), out var nullIndexesValue);
        var hasElementType = properties.TryGetValue(GetElementTypePropertyName(logicalName), out var elementTypeValue);
        var hasRelationshipType = properties.TryGetValue(
            GetRelationshipTypePropertyName(logicalName),
            out var relationshipTypeValue);
        if (!hasLength && !hasNullIndexes && !hasElementType && !hasRelationshipType)
        {
            metadata = null!;
            return false;
        }

        if (!hasLength || !hasNullIndexes || !hasElementType || !hasRelationshipType)
        {
            throw InvalidEncoding(logicalName, "all four collection companions must be present");
        }

        var length = ReadInteger(logicalName, lengthValue!, "logical length");
        if (length < 0)
        {
            throw InvalidEncoding(logicalName, "the logical length cannot be negative");
        }

        var nullIndexes = ReadNullIndexes(logicalName, nullIndexesValue!, length);
        var elementType = ReadElementType(logicalName, elementTypeValue!);
        var relationshipType = ReadRelationshipType(logicalName, relationshipTypeValue!);
        metadata = new Metadata(length, nullIndexes, elementType, relationshipType);
        return true;
    }

    private static string ReadRelationshipType(string logicalName, GraphValue value)
    {
        if (value.Kind != GraphValueKind.Scalar ||
            value.ScalarValue is not string relationshipType ||
            string.IsNullOrWhiteSpace(relationshipType))
        {
            throw InvalidEncoding(logicalName, "the relationship-type companion is not a non-empty string");
        }

        return relationshipType;
    }

    private static Type ReadElementType(string logicalName, GraphValue value)
    {
        if (value.Kind != GraphValueKind.Scalar || value.ScalarValue is not string typeName)
        {
            throw InvalidEncoding(logicalName, "the element-type companion is not a string");
        }

        var elementType = Type.GetType(typeName, throwOnError: false) ??
            throw InvalidEncoding(logicalName, $"the element type '{typeName}' cannot be resolved");
        if (!string.Equals(SimpleCollectionStorageCodec.GetTypeIdentity(elementType), typeName, StringComparison.Ordinal))
        {
            throw InvalidEncoding(logicalName, $"the element type '{typeName}' is not canonical");
        }

        return elementType;
    }

    private static List<int> ReadNullIndexes(string logicalName, GraphValue value, int length)
    {
        if (value.Kind != GraphValueKind.List)
        {
            throw InvalidEncoding(logicalName, "the null-index companion is not a list");
        }

        var result = new List<int>(value.Items.Count);
        foreach (var item in value.Items)
        {
            var index = ReadInteger(logicalName, item, "null index");
            result.Add(index);
        }

        ValidateNullIndexes(logicalName, result, length);
        return result;
    }

    private static void ValidateNullIndexes(
        string logicalName,
        IReadOnlyList<int> indexes,
        int logicalLength)
    {
        var previous = -1;
        foreach (var index in indexes)
        {
            if (index < 0 || index >= logicalLength || index <= previous)
            {
                throw InvalidEncoding(logicalName, "null indexes must be strictly increasing and within the logical collection");
            }

            previous = index;
        }
    }

    private static int ReadInteger(string logicalName, GraphValue value, string subject)
    {
        try
        {
            if (value.Kind != GraphValueKind.Scalar || value.ScalarValue is null)
            {
                throw new InvalidCastException();
            }

            return value.ScalarValue switch
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
            };
        }
        catch (Exception exception) when (exception is InvalidCastException or OverflowException)
        {
            throw InvalidEncoding(logicalName, $"the {subject} is not an integer");
        }
    }

    private static GraphException InvalidEncoding(string logicalName, string detail) =>
        new($"Invalid complex-collection storage for property '{logicalName}': {detail}.");

    private sealed record Metadata(
        int Length,
        IReadOnlyList<int> NullIndexes,
        Type ElementType,
        string RelationshipType);
}
