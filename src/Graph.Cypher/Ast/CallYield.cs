// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph.Cypher.Internal;

namespace Cvoya.Graph.Cypher.Ast;

/// <summary>Represents a value yielded by a Cypher procedure call.</summary>
public sealed record CallYield
{
    /// <summary>Initializes a yielded value.</summary>
    public CallYield(string name, string? alias = null)
    {
        Name = ArgumentValidation.RequiredName(name, nameof(name));
        Alias = ArgumentValidation.OptionalName(alias, nameof(alias));
    }

    /// <summary>Gets the yielded name.</summary>
    public string Name { get; }

    /// <summary>Gets the optional local alias.</summary>
    public string? Alias { get; }
}
