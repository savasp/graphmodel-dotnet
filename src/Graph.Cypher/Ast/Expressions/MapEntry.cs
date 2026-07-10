// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph.Cypher.Internal;

namespace Cvoya.Graph.Cypher.Ast.Expressions;

/// <summary>Represents one entry in a Cypher map expression.</summary>
public sealed record MapEntry
{
    /// <summary>Initializes a map entry.</summary>
    public MapEntry(string key, CypherExpression value)
    {
        Key = ArgumentValidation.RequiredName(key, nameof(key));
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>Gets the map key.</summary>
    public string Key { get; }

    /// <summary>Gets the map value.</summary>
    public CypherExpression Value { get; }
}
