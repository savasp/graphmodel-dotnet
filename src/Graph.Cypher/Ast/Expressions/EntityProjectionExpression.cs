// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph.Cypher.Internal;

namespace Cvoya.Graph.Cypher.Ast.Expressions;

/// <summary>
/// Represents a node-valued expression in the provider materialization shape.
/// </summary>
public sealed record EntityProjectionExpression : CypherExpression
{
    /// <summary>Initializes a node-valued projection expression.</summary>
    /// <param name="alias">The node variable to project.</param>
    /// <param name="loadComplexProperties">Whether declared complex properties must be included.</param>
    public EntityProjectionExpression(string alias, bool loadComplexProperties)
    {
        Alias = ArgumentValidation.RequiredName(alias, nameof(alias));
        LoadComplexProperties = loadComplexProperties;
    }

    /// <summary>Gets the node variable to project.</summary>
    public string Alias { get; }

    /// <summary>Gets whether declared complex properties must be included.</summary>
    public bool LoadComplexProperties { get; }
}
