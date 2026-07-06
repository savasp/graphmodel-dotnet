// Copyright 2025 Savas Parastatidis
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      https://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace Cvoya.Graph.Model.Neo4j.Querying.Cypher.Builders;

using System.Text;


/// <summary>
/// Handles RETURN clause construction for Cypher queries.
/// Extracted from the monolithic CypherQueryBuilder to provide focused responsibility.
/// </summary>
internal class ReturnQueryPart : ICypherQueryPart
{
    private enum ClauseKind { With, Unwind }

    private readonly List<string> _returnClauses = [];

    /// <summary>
    /// WITH and UNWIND clauses in the order <see cref="AddWith"/>/<see cref="AddUnwind"/> were
    /// called, not grouped by clause type. This matters: Cypher's WITH is a scope barrier (only
    /// variables it names survive past it) and UNWIND introduces its variable only for clauses
    /// that follow it - a caller building e.g. <c>WITH collect(p) AS __paths</c>, then
    /// <c>UNWIND range(0, size(__paths) - 1) AS pathIndex</c>, then
    /// <c>WITH __paths[pathIndex] AS p, pathIndex</c> depends on that exact interleaving surviving
    /// to <see cref="AppendTo"/> - rendering "all WITHs, then all UNWINDs" (as an earlier version
    /// of this type did) would reference <c>pathIndex</c> in the second WITH before the UNWIND
    /// that defines it ever runs, an undefined-variable error at query execution time (see
    /// CypherQueryVisitor.HandleTraversePaths and the #94 TraversePaths Cypher scoping fix).
    /// </summary>
    private readonly List<(ClauseKind Kind, string Expression)> _withUnwindClauses = [];
    private string? _aggregation;
    private bool _isDistinct;
    private bool _isExistsQuery;
    private bool _isNotExistsQuery;
    private string? _mainNodeAlias;

    public int Order => 6; // RETURN comes near the end

    public bool HasContent => HasExplicitReturn || _mainNodeAlias != null;

    public bool HasUserProjections { get; set; } = false;

    public bool HasExplicitReturn =>
    _returnClauses.Count > 0 ||
    _aggregation != null ||
    _isExistsQuery ||
    _isNotExistsQuery;

    public bool IsExistsQuery => _isExistsQuery;
    public bool IsNotExistsQuery => _isNotExistsQuery;
    public bool IsDistinct => _isDistinct;
    public IReadOnlyList<string> ReturnClauses => _returnClauses.AsReadOnly();

    /// <summary>
    /// Sets the main node alias for fallback return structures.
    /// </summary>
    public void SetMainNodeAlias(string? alias)
    {
        _mainNodeAlias = alias;
    }

    /// <summary>
    /// Adds a user-defined projection (from SELECT clauses).
    /// </summary>
    public void AddUserProjection(string expression, string? alias = null)
    {
        HasUserProjections = true;
        AddReturn(expression, alias);
    }

    /// <summary>
    /// Adds an infrastructure return (for internal query needs).
    /// </summary>
    public void AddInfrastructureReturn(string expression, string? alias = null)
    {
        // This is for infrastructure returns like path segments - don't mark as user projection
        AddReturn(expression, alias);
    }

    /// <summary>
    /// Adds a return expression.
    /// </summary>
    public void AddReturn(string expression, string? alias = null)
    {
        if (!string.IsNullOrWhiteSpace(alias))
        {
            _returnClauses.Add($"{expression} AS {alias}");
        }
        else
        {
            _returnClauses.Add(expression);
        }
    }

    /// <summary>
    /// Sets an aggregation function for the return clause.
    /// </summary>
    public void SetAggregation(string function, string expression)
    {
        _aggregation = $"{function}({expression})";
    }

    /// <summary>
    /// Sets whether the return should be DISTINCT.
    /// </summary>
    public void SetDistinct(bool distinct)
    {
        _isDistinct = distinct;
    }

    /// <summary>
    /// Configures this as an EXISTS query.
    /// </summary>
    public void SetExistsQuery()
    {
        _isExistsQuery = true;
    }

    /// <summary>
    /// Configures this as a NOT EXISTS query.
    /// </summary>
    public void SetNotExistsQuery()
    {
        _isNotExistsQuery = true;
    }

    /// <summary>
    /// Adds a WITH clause.
    /// </summary>
    public void AddWith(string expression)
    {
        _withUnwindClauses.Add((ClauseKind.With, expression));
    }

    /// <summary>
    /// Adds an UNWIND clause.
    /// </summary>
    public void AddUnwind(string expression)
    {
        _withUnwindClauses.Add((ClauseKind.Unwind, expression));
    }

    /// <summary>
    /// Clears all return-related clauses.
    /// </summary>
    public void ClearReturn()
    {
        _returnClauses.Clear();
        _aggregation = null;
        _isDistinct = false;
        _isExistsQuery = false;
        _isNotExistsQuery = false;
        HasUserProjections = false;
    }

    /// <summary>
    /// Clears only user projections.
    /// </summary>
    public void ClearUserProjections()
    {
        HasUserProjections = false;
        ClearReturn();
    }

    /// <summary>
    /// Updates aliases in RETURN clauses for path segments.
    /// This is used when we have a Traverse + PathSegments pattern and need to update
    /// the RETURN clause aliases from the Traverse pattern to the PathSegments pattern.
    /// </summary>
    public void UpdateAliasesForPathSegments(string oldAlias, string newAlias)
    {
        // Update existing RETURN clauses to use the new aliases
        for (int i = 0; i < _returnClauses.Count; i++)
        {
            var clause = _returnClauses[i];
            // Replace "Node: oldAlias" with "Node: newAlias"
            clause = clause.Replace($"Node: {oldAlias}", $"Node: {newAlias}");
            _returnClauses[i] = clause;
        }
    }

    public void AppendTo(StringBuilder builder, Dictionary<string, object?> parameters)
    {
        // Handle special query types first
        if (_isExistsQuery)
        {
            builder.AppendLine($"RETURN COUNT({_mainNodeAlias ?? "src"}) > 0 AS exists");
            return;
        }

        if (_isNotExistsQuery)
        {
            builder.AppendLine($"RETURN COUNT({_mainNodeAlias ?? "src"}) = 0 AS all");
            return;
        }

        // Render WITH/UNWIND clauses in the order they were added (see _withUnwindClauses'
        // doc comment) - Cypher's variable scoping depends on this interleaving, not on
        // grouping every WITH before every UNWIND.
        foreach (var (kind, expression) in _withUnwindClauses)
        {
            builder.AppendLine(kind == ClauseKind.With ? $"WITH {expression}" : $"UNWIND {expression}");
        }

        // Build the RETURN clause
        builder.Append("RETURN ");

        if (_aggregation != null)
        {
            builder.Append(_aggregation);
        }
        else if (_returnClauses.Count > 0)
        {
            var returnExpression = _isDistinct
                ? $"DISTINCT {string.Join(", ", _returnClauses)}"
                : string.Join(", ", _returnClauses);
            builder.Append(returnExpression);
        }
        else
        {
            // Only use the fallback Node structure if there are no projections or aggregations
            // If we're returning a node or set of nodes (and not handling complex properties),
            // wrap it in the required structure.
            var alias = _mainNodeAlias ?? "src";
            builder.AppendLine($@"{{
                Node: {alias},
                ComplexProperties: []
            }} AS Node");
            return;
        }

        builder.AppendLine();
    }
}