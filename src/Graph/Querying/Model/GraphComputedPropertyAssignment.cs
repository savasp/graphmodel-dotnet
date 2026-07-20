// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying;

using System.Linq.Expressions;
using System.Reflection;

/// <summary>Assigns a mapped graph property from an expression over the current entity.</summary>
public sealed record GraphComputedPropertyAssignment : GraphPropertyAssignment
{
    /// <summary>Initializes a computed property assignment.</summary>
    public GraphComputedPropertyAssignment(
        LambdaExpression propertySelector,
        PropertyInfo? property,
        string storageName,
        bool dynamic,
        LambdaExpression valueExpression)
        : base(propertySelector, property, storageName, dynamic) =>
        ValueExpression = valueExpression ?? throw new ArgumentNullException(nameof(valueExpression));

    /// <summary>Gets the expression that computes the assigned value.</summary>
    public LambdaExpression ValueExpression { get; }
}
