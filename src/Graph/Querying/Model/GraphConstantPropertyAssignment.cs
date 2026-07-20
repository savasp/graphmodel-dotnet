// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying;

using System.Linq.Expressions;
using System.Reflection;

/// <summary>Assigns a mapped graph property from a constant or captured value.</summary>
public sealed record GraphConstantPropertyAssignment : GraphPropertyAssignment
{
    /// <summary>Initializes a constant property assignment.</summary>
    public GraphConstantPropertyAssignment(
        LambdaExpression propertySelector,
        PropertyInfo? property,
        string storageName,
        bool dynamic,
        object? value)
        : base(propertySelector, property, storageName, dynamic) => Value = value;

    /// <summary>Gets the value to assign.</summary>
    public object? Value { get; }
}
