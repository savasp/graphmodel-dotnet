// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying;

/// <summary>
/// Describes a provider-independent set union between a graph query and a second graph query.
/// </summary>
/// <remarks>
/// The model can represent the union so that recognition does not lose the second source; whether
/// a provider can execute it is a separate planning concern.
/// </remarks>
public sealed record UnionFragment
{
    /// <summary>
    /// Initializes a new union description.
    /// </summary>
    /// <param name="first">The first query model at the union boundary.</param>
    /// <param name="second">The second query model at the union boundary.</param>
    /// <param name="elementType">The element type shared by both operands at the union boundary.</param>
    public UnionFragment(GraphQueryModel first, GraphQueryModel second, Type elementType)
        : this(first, second, elementType, SetOperationKind.Union)
    {
    }

    /// <summary>Initializes a set-operation description.</summary>
    /// <param name="first">The first query model at the operation boundary.</param>
    /// <param name="second">The second query model at the operation boundary.</param>
    /// <param name="elementType">The element type shared by both operands.</param>
    /// <param name="operation">The distinct or bag-preserving operation.</param>
    public UnionFragment(
        GraphQueryModel first,
        GraphQueryModel second,
        Type elementType,
        SetOperationKind operation)
    {
        First = first ?? throw new ArgumentNullException(nameof(first));
        Second = second ?? throw new ArgumentNullException(nameof(second));
        ElementType = elementType ?? throw new ArgumentNullException(nameof(elementType));
        QueryModelGuard.RequireDefinedEnum(operation, nameof(operation));
        Operation = operation;
    }

    /// <summary>Gets the first query model at the union boundary.</summary>
    public GraphQueryModel First { get; }

    /// <summary>Gets the second query model at the union boundary.</summary>
    public GraphQueryModel Second { get; }

    /// <summary>Gets the element type shared by both operands at the union boundary.</summary>
    public Type ElementType { get; }

    /// <summary>Gets the distinct or bag-preserving operation.</summary>
    public SetOperationKind Operation { get; }
}
