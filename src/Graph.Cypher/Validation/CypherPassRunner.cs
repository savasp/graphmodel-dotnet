// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph.Cypher.Ast;
using Cvoya.Graph.Cypher.Internal;

namespace Cvoya.Graph.Cypher.Validation;

/// <summary>
/// Runs an ordered set of Cypher passes.
/// </summary>
public sealed class CypherPassRunner : ICypherPass
{
    private readonly IReadOnlyList<ICypherPass> passes;

    /// <summary>
    /// Initializes a new instance of the <see cref="CypherPassRunner"/> class.
    /// </summary>
    /// <param name="passes">The passes to apply in order.</param>
    public CypherPassRunner(IReadOnlyList<ICypherPass> passes)
    {
        this.passes = ArgumentValidation.RequiredList(passes, nameof(passes));
    }

    /// <inheritdoc />
    public CypherStatement Run(CypherStatement input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var current = input;
        foreach (var pass in passes)
        {
            current = pass.Run(current);
        }

        return current;
    }
}
