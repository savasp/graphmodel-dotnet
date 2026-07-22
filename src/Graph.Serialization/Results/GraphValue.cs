// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using System.Collections.ObjectModel;

namespace Cvoya.Graph.Serialization.Results;

/// <summary>
/// Represents an immutable provider-neutral value returned by a graph database driver.
/// </summary>
public sealed record GraphValue
{
    private readonly object? scalar;

    private static readonly IReadOnlyDictionary<string, GraphValue> EmptyMap =
        new ReadOnlyDictionary<string, GraphValue>(new Dictionary<string, GraphValue>());

    private GraphValue(
        GraphValueKind kind,
        object? scalar,
        string? elementId,
        IReadOnlyList<string>? labels,
        string? relationshipType,
        string? startNodeElementId,
        string? endNodeElementId,
        IReadOnlyDictionary<string, GraphValue>? entries,
        IReadOnlyList<GraphValue>? items,
        Type? collectionElementType = null,
        IReadOnlyDictionary<string, GraphValue>? storageEntries = null)
    {
        Kind = kind;
        this.scalar = scalar is byte[] bytes ? bytes.ToArray() : scalar;
        ElementId = elementId;
        Labels = labels ?? [];
        RelationshipType = relationshipType;
        StartNodeElementId = startNodeElementId;
        EndNodeElementId = endNodeElementId;
        Entries = entries ?? EmptyMap;
        StorageEntries = storageEntries ?? EmptyMap;
        Items = items ?? [];
        CollectionElementType = collectionElementType;
    }

    /// <summary>Gets this value's discriminant.</summary>
    public GraphValueKind Kind { get; }

    /// <summary>Gets the scalar payload; valid only when <see cref="Kind"/> is <see cref="GraphValueKind.Scalar"/>.</summary>
    public object? ScalarValue => scalar is byte[] bytes ? bytes.ToArray() : scalar;

    /// <summary>Gets the provider element ID for node and relationship values.</summary>
    public string? ElementId { get; }

    /// <summary>Gets node labels.</summary>
    public IReadOnlyList<string> Labels { get; }

    /// <summary>Gets the relationship type.</summary>
    public string? RelationshipType { get; }

    /// <summary>Gets the provider element ID of a relationship's physical start node.</summary>
    public string? StartNodeElementId { get; }

    /// <summary>Gets the provider element ID of a relationship's physical end node.</summary>
    public string? EndNodeElementId { get; }

    /// <summary>Gets node/relationship properties or map entries.</summary>
    public IReadOnlyDictionary<string, GraphValue> Entries { get; }

    /// <summary>Gets provider-private storage entries that must not enter a user property bag.</summary>
    internal IReadOnlyDictionary<string, GraphValue> StorageEntries { get; }

    /// <summary>Gets list or path items.</summary>
    public IReadOnlyList<GraphValue> Items { get; }

    internal Type? CollectionElementType { get; }

    /// <summary>Creates a scalar wire value.</summary>
    public static GraphValue Scalar(object? value)
    {
        if (value is GraphValue || value is System.Collections.IDictionary ||
            value is System.Collections.IEnumerable and not string and not byte[])
        {
            throw new ArgumentException(
                "Scalar wire values cannot contain graph values, maps, or collections; use the matching factory.",
                nameof(value));
        }

        return new GraphValue(GraphValueKind.Scalar, value, null, null, null, null, null, null, null);
    }

    /// <summary>Creates a node wire value.</summary>
    public static GraphValue Node(
        string elementId,
        IReadOnlyList<string> labels,
        IReadOnlyDictionary<string, GraphValue> properties)
    {
        return new GraphValue(
            GraphValueKind.Node,
            null,
            Required(elementId, nameof(elementId)),
            CopyStrings(labels, nameof(labels)),
            null,
            null,
            null,
            CopyEntries(properties, nameof(properties)),
            null);
    }

    internal static GraphValue Node(
        string elementId,
        IReadOnlyList<string> labels,
        IReadOnlyDictionary<string, GraphValue> properties,
        IReadOnlyDictionary<string, GraphValue> storageProperties)
    {
        return new GraphValue(
            GraphValueKind.Node,
            null,
            Required(elementId, nameof(elementId)),
            CopyStrings(labels, nameof(labels)),
            null,
            null,
            null,
            CopyEntries(properties, nameof(properties)),
            null,
            storageEntries: CopyEntries(storageProperties, nameof(storageProperties)));
    }

    /// <summary>Creates a relationship wire value.</summary>
    public static GraphValue Relationship(
        string elementId,
        string type,
        string startNodeElementId,
        string endNodeElementId,
        IReadOnlyDictionary<string, GraphValue> properties)
    {
        return new GraphValue(
            GraphValueKind.Relationship,
            null,
            Required(elementId, nameof(elementId)),
            null,
            Required(type, nameof(type)),
            Required(startNodeElementId, nameof(startNodeElementId)),
            Required(endNodeElementId, nameof(endNodeElementId)),
            CopyEntries(properties, nameof(properties)),
            null);
    }

    internal static GraphValue Relationship(
        string elementId,
        string type,
        string startNodeElementId,
        string endNodeElementId,
        IReadOnlyDictionary<string, GraphValue> properties,
        IReadOnlyDictionary<string, GraphValue> storageProperties)
    {
        return new GraphValue(
            GraphValueKind.Relationship,
            null,
            Required(elementId, nameof(elementId)),
            null,
            Required(type, nameof(type)),
            Required(startNodeElementId, nameof(startNodeElementId)),
            Required(endNodeElementId, nameof(endNodeElementId)),
            CopyEntries(properties, nameof(properties)),
            null,
            storageEntries: CopyEntries(storageProperties, nameof(storageProperties)));
    }

    /// <summary>Creates an ordered list wire value.</summary>
    public static GraphValue List(IReadOnlyList<GraphValue> items) => new(
        GraphValueKind.List,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        CopyItems(items, nameof(items)));

    internal static GraphValue List(IReadOnlyList<GraphValue> items, Type elementType)
    {
        ArgumentNullException.ThrowIfNull(elementType);
        return new GraphValue(
            GraphValueKind.List,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            CopyItems(items, nameof(items)),
            elementType);
    }

    /// <summary>Creates a string-keyed map wire value.</summary>
    public static GraphValue Map(IReadOnlyDictionary<string, GraphValue> entries) => new(
        GraphValueKind.Map,
        null,
        null,
        null,
        null,
        null,
        null,
        CopyEntries(entries, nameof(entries)),
        null);

    /// <summary>Creates a validated alternating node/relationship path wire value.</summary>
    public static GraphValue Path(IReadOnlyList<GraphValue> items)
    {
        var copy = CopyItems(items, nameof(items));
        if (copy.Count == 0 || copy.Count % 2 == 0)
        {
            throw new ArgumentException("A graph path must contain an odd, non-zero number of items.", nameof(items));
        }

        for (var index = 0; index < copy.Count; index++)
        {
            var expected = index % 2 == 0 ? GraphValueKind.Node : GraphValueKind.Relationship;
            if (copy[index].Kind != expected)
            {
                throw new ArgumentException(
                    $"Graph path item {index} must be a {expected} value.",
                    nameof(items));
            }
        }

        return new GraphValue(GraphValueKind.Path, null, null, null, null, null, null, null, copy);
    }

    internal object? ToObject()
    {
        return Kind switch
        {
            GraphValueKind.Scalar => ScalarValue,
            GraphValueKind.Node or GraphValueKind.Relationship => this,
            GraphValueKind.List when CollectionElementType is not null => new TypedGraphValueList(
                Items.Select(item => item.ToObject()),
                CollectionElementType),
            GraphValueKind.List or GraphValueKind.Path => Items.Select(item => item.ToObject()).ToList(),
            GraphValueKind.Map => Entries.ToDictionary(pair => pair.Key, pair => pair.Value.ToObject()!, StringComparer.Ordinal),
            _ => throw new GraphException($"Unsupported graph wire value kind '{Kind}'."),
        };
    }

    internal IReadOnlyDictionary<string, object> ObjectEntries =>
        Entries.ToDictionary(pair => pair.Key, pair => pair.Value.ToObject()!, StringComparer.Ordinal);

    internal IReadOnlyDictionary<string, object> Properties => ObjectEntries;

    internal string Type => RelationshipType ?? string.Empty;

    private static string Required(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value;
    }

    private static ReadOnlyCollection<string> CopyStrings(IReadOnlyList<string> values, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        return Array.AsReadOnly(values.Select(value => Required(value, parameterName)).ToArray());
    }

    private static ReadOnlyCollection<GraphValue> CopyItems(IReadOnlyList<GraphValue> values, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        if (values.Any(value => value is null))
        {
            throw new ArgumentException("Wire-value collections cannot contain null entries; use GraphValue.Scalar(null).", parameterName);
        }

        return Array.AsReadOnly(values.ToArray());
    }

    private static ReadOnlyDictionary<string, GraphValue> CopyEntries(
        IReadOnlyDictionary<string, GraphValue> values,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        var copy = new Dictionary<string, GraphValue>(StringComparer.Ordinal);
        foreach (var (key, value) in values)
        {
            copy.Add(Required(key, parameterName), value ?? throw new ArgumentException(
                "Wire-value maps cannot contain null entries; use GraphValue.Scalar(null).",
                parameterName));
        }

        return new ReadOnlyDictionary<string, GraphValue>(copy);
    }

    internal sealed class TypedGraphValueList(IEnumerable<object?> items, Type elementType) : List<object?>(items)
    {
        internal Type ElementType { get; } = elementType;
    }
}
