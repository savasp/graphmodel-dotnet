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
    private readonly List<string> _returnClauses = [];
    private readonly List<string> _withClauses = [];
    private readonly List<string> _unwindClauses = [];
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
        _withClauses.Add(expression);
    }

    /// <summary>
    /// Adds an UNWIND clause.
    /// </summary>
    public void AddUnwind(string expression)
    {
        _unwindClauses.Add(expression);
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

        // Handle WITH clauses first
        foreach (var withClause in _withClauses)
        {
            builder.AppendLine($"WITH {withClause}");
        }

        // Handle UNWIND clauses
        foreach (var unwindClause in _unwindClauses)
        {
            builder.AppendLine($"UNWIND {unwindClause}");
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