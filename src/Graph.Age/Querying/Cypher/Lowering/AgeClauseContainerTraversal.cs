// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Querying.Cypher.Lowering;

using Cvoya.Graph.Cypher.Ast;

/// <summary>Recursively applies AGE lowering to independently scoped clause containers.</summary>
internal static class AgeClauseContainerTraversal
{
    public static CypherStatement RunSetOperationBranches(
        CypherStatement input,
        Func<CypherStatement, CypherStatement> lower)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(lower);

        var parameters = input.Parameters;
        var changed = false;
        var clauses = new ICypherClause[input.Clauses.Count];
        for (var index = 0; index < input.Clauses.Count; index++)
        {
            if (input.Clauses[index] is not SetOperationClause setOperation)
            {
                clauses[index] = input.Clauses[index];
                continue;
            }

            var first = lower(new CypherStatement(setOperation.First, parameters));
            var second = lower(new CypherStatement(setOperation.Second, first.Parameters));
            clauses[index] = new SetOperationClause(
                first.Clauses,
                second.Clauses,
                setOperation.PreserveDuplicates);
            parameters = second.Parameters;
            changed = true;
        }

        return changed
            ? new CypherStatement(clauses, parameters, input.PathTypes)
            : input;
    }
}
