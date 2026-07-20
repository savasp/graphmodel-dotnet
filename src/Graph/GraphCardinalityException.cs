// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph;

/// <summary>Thrown when a relationship endpoint selection does not resolve to exactly one graph element.</summary>
public sealed class GraphCardinalityException : GraphException
{
    /// <summary>Initializes a cardinality exception for the specified endpoint and failure.</summary>
    /// <param name="role">The endpoint whose selection failed.</param>
    /// <param name="failure">The cardinality failure.</param>
    public GraphCardinalityException(GraphEndpointRole role, GraphCardinalityFailure failure)
        : base(CreateMessage(role, failure))
    {
        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role));
        }

        if (!Enum.IsDefined(failure))
        {
            throw new ArgumentOutOfRangeException(nameof(failure));
        }

        Role = role;
        Failure = failure;
    }

    /// <summary>Gets the endpoint whose selection failed.</summary>
    public GraphEndpointRole Role { get; }

    /// <summary>Gets the cardinality failure.</summary>
    public GraphCardinalityFailure Failure { get; }

    private static string CreateMessage(GraphEndpointRole role, GraphCardinalityFailure failure) => failure switch
    {
        GraphCardinalityFailure.Empty => $"The {role.ToString().ToLowerInvariant()} endpoint selection matched no nodes.",
        GraphCardinalityFailure.Multiple => $"The {role.ToString().ToLowerInvariant()} endpoint selection matched more than one distinct node.",
        _ => $"The {role.ToString().ToLowerInvariant()} endpoint selection has invalid cardinality.",
    };
}
