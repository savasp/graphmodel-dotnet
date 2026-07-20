// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying;

using System.Linq.Expressions;
using System.Reflection;

/// <summary>Describes one mapped property assignment in a graph mutation.</summary>
public abstract record GraphPropertyAssignment
{
    /// <summary>Initializes a mapped property assignment.</summary>
    protected GraphPropertyAssignment(
        LambdaExpression propertySelector,
        PropertyInfo? property,
        string storageName,
        bool dynamic)
    {
        PropertySelector = propertySelector ?? throw new ArgumentNullException(nameof(propertySelector));
        Property = property;
        ArgumentException.ThrowIfNullOrWhiteSpace(storageName);
        StorageName = storageName;
        Dynamic = dynamic;
    }

    /// <summary>Gets the original direct property selector.</summary>
    public LambdaExpression PropertySelector { get; }

    /// <summary>Gets the selected CLR property, or <see langword="null"/> for a dynamic bag key.</summary>
    public PropertyInfo? Property { get; }

    /// <summary>Gets the mapped storage property name.</summary>
    public string StorageName { get; }

    /// <summary>Gets whether the storage name came from a dynamic property-bag key.</summary>
    public bool Dynamic { get; }
}
