// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Querying.Cypher.Lowering;

using Cvoya.Graph.Cypher.Ast;
using Cvoya.Graph.Cypher.Ast.Expressions;
using Cvoya.Graph.Cypher.Validation;

/// <summary>
/// Reorders paging and ordering clauses into the positions accepted by Apache AGE.
/// </summary>
internal sealed class AgeClauseOrderPass : ICypherPass
{
    private static readonly HashSet<string> AggregateFunctions = new(
        ["count", "sum", "min", "max", "avg"],
        StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public CypherStatement Run(CypherStatement input)
    {
        ArgumentNullException.ThrowIfNull(input);

        // The shared renderer carries post-paging sort keys through entity hydration and reapplies
        // them before its final simple RETURN. That pipeline already has the WITH boundaries AGE
        // requires; moving either ORDER BY would separate it from the row shape it orders.
        if (input.Clauses.OfType<EntityProjectionClause>().Any(projection => projection.Ordering.Count > 0))
        {
            return input;
        }

        var pathCollection = FindPathCollection(input.Clauses);
        var trailing = new List<ICypherClause>();
        var retained = new List<ICypherClause>();
        var hasActiveWith = false;
        var currentWithStage = 0;
        int? primaryPagingWithStage = null;
        var changed = false;

        for (var index = 0; index < input.Clauses.Count; index++)
        {
            var clause = input.Clauses[index];
            var beforePathCollection = pathCollection is { Index: var pathIndex } && index < pathIndex;

            if (clause is SkipClause or LimitClause)
            {
                if (beforePathCollection)
                {
                    if (!HasPagingPipe(retained))
                    {
                        retained.Add(new WithClause(
                            [new ReturnItem(new VariableRef(pathCollection!.Value.PathAlias), null)],
                            distinct: false));
                        changed = true;
                    }

                    retained.Add(clause);
                }
                else if (hasActiveWith)
                {
                    primaryPagingWithStage ??= currentWithStage;
                    if (primaryPagingWithStage == currentWithStage)
                    {
                        retained.Add(clause);
                    }
                    else
                    {
                        trailing.Add(clause);
                        changed = true;
                    }
                }
                else
                {
                    trailing.Add(clause);
                    changed = true;
                }

                continue;
            }

            if (clause is OrderByClause orderBy)
            {
                if (hasActiveWith &&
                    (primaryPagingWithStage is null || primaryPagingWithStage == currentWithStage))
                {
                    retained.Add(orderBy);
                }
                else
                {
                    trailing.Add(orderBy);
                    changed = true;
                }

                continue;
            }

            retained.Add(clause);
            if (clause is WithClause)
            {
                hasActiveWith = true;
                currentWithStage++;
            }
            else if (BreaksWithStage(clause))
            {
                hasActiveWith = false;
            }
        }

        if (trailing.Count > 0)
        {
            if (ContainsAggregateReturn(retained))
            {
                var returnIndex = retained.FindLastIndex(clause => clause is ReturnClause);
                var withIndex = retained.FindLastIndex(returnIndex, clause => clause is WithClause);
                retained.InsertRange(withIndex >= 0 ? withIndex + 1 : returnIndex, trailing);
            }
            else
            {
                retained.AddRange(trailing);
            }
        }

        return changed
            ? new CypherStatement(retained, input.Parameters, input.PathTypes)
            : input;
    }

    private static (int Index, string PathAlias)? FindPathCollection(IReadOnlyList<ICypherClause> clauses)
    {
        for (var index = 0; index < clauses.Count; index++)
        {
            if (clauses[index] is not WithClause with)
            {
                continue;
            }

            foreach (var item in with.Items)
            {
                if (item is
                    {
                        Alias: "__paths",
                        Expression: FunctionCall
                        {
                            Name: "collect",
                            Arguments: [VariableRef path]
                        }
                    })
                {
                    return (index, path.Alias);
                }
            }
        }

        return null;
    }

    private static bool HasPagingPipe(List<ICypherClause> clauses)
    {
        for (var index = clauses.Count - 1; index >= 0; index--)
        {
            if (clauses[index] is SkipClause or LimitClause)
            {
                continue;
            }

            return clauses[index] is WithClause;
        }

        return false;
    }

    private static bool BreaksWithStage(ICypherClause clause) => clause is
        MatchClause or
        WhereClause or
        ReturnClause or
        UnwindClause or
        CallClause or
        CallSubqueryClause or
        FullTextSearchClause or
        EntityProjectionClause;

    private static bool ContainsAggregateReturn(IEnumerable<ICypherClause> clauses) => clauses
        .OfType<ReturnClause>()
        .Any(@return => @return.Items.Count > 0 &&
            @return.Items[0].Expression is FunctionCall function &&
            AggregateFunctions.Contains(function.Name));
}
