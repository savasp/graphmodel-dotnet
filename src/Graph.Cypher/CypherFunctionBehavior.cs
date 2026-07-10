// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Cypher;

/// <summary>Defines how a dialect handles a provider-neutral function.</summary>
public enum CypherFunctionBehavior
{
    /// <summary>Render the mapped function into Cypher.</summary>
    Render,

    /// <summary>Evaluate a parameter-free function during translation and bind its value.</summary>
    EvaluateOnClient,

    /// <summary>Reject the function during translation.</summary>
    Unsupported
}
