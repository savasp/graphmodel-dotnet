// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying;

/// <summary>
/// Identifies the terminal LINQ operation represented by a query model.
/// </summary>
/// <remarks>
/// Distinct is not a terminal operation: it is carried by <see cref="GraphQueryModel.Distinct"/>
/// so that a query such as <c>Distinct().CountAsync()</c> can represent both semantics at once.
/// </remarks>
public enum TerminalOperation
{
    /// <summary>
    /// Materialize the query as a list or array.
    /// </summary>
    ToListOrArray,

    /// <summary>
    /// Return the first element, or the default value for the element type.
    /// </summary>
    First,

    /// <summary>
    /// Return the only element, or the default value for the element type.
    /// </summary>
    Single,

    /// <summary>
    /// Return the last element, or the default value for the element type.
    /// </summary>
    Last,

    /// <summary>
    /// Test whether any element exists or satisfies a predicate.
    /// </summary>
    Any,

    /// <summary>
    /// Test whether every element satisfies a predicate.
    /// </summary>
    All,

    /// <summary>
    /// Count matching elements.
    /// </summary>
    Count,

    /// <summary>
    /// Sum matching values.
    /// </summary>
    Sum,

    /// <summary>
    /// Average matching values.
    /// </summary>
    Average,

    /// <summary>
    /// Return the minimum matching value.
    /// </summary>
    Min,

    /// <summary>
    /// Return the maximum matching value.
    /// </summary>
    Max,

    /// <summary>
    /// Test whether the query contains a value.
    /// </summary>
    Contains,

    /// <summary>
    /// Return the element at an index.
    /// </summary>
    ElementAt,

    /// <summary>
    /// Return the element at an index, or the default value for the element type.
    /// </summary>
    ElementAtOrDefault,
}
