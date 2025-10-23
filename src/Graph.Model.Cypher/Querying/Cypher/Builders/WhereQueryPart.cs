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

namespace Cvoya.Graph.Model.Cypher.Querying.Cypher.Builders;

using System.Linq.Expressions;
using System.Text;

/// <summary>
/// Handles WHERE clause construction for Cypher queries.
/// Extracted from the monolithic CypherQueryBuilder and processes pending WHERE clauses
/// using the expression visitor pattern.
/// </summary>
public class WhereQueryPart : ICypherQueryPart
{
    private readonly List<string> _whereClauses = [];
    private readonly List<(LambdaExpression Lambda, string Alias)> _pendingWhereClauses = [];
    private readonly ICypherExpressionProcessor _expressionProcessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="WhereQueryPart"/> class.
    /// </summary>
    /// <param name="expressionProcessor">The expression processor for converting lambda expressions to Cypher.</param>
    public WhereQueryPart(ICypherExpressionProcessor expressionProcessor)
    {
        _expressionProcessor = expressionProcessor ?? throw new ArgumentNullException(nameof(expressionProcessor));
    }

    /// <summary>
    /// Gets the order in which this query part should appear in the final query.
    /// </summary>
    public int Order => 2; // WHERE comes after MATCH

    /// <summary>
    /// Gets a value indicating whether this query part has any content to append.
    /// </summary>
    public bool HasContent => _whereClauses.Count > 0 || _pendingWhereClauses.Count > 0;

    /// <summary>
    /// Adds a direct WHERE condition string.
    /// </summary>
    public void AddWhere(string condition)
    {
        // Don't add duplicate WHERE clauses
        if (!_whereClauses.Contains(condition))
        {
            _whereClauses.Add(condition);
        }
    }

    /// <summary>
    /// Adds a pending WHERE clause from a lambda expression.
    /// The expression will be processed when the query is built.
    /// </summary>
    public void SetPendingWhere(LambdaExpression lambda, string? alias)
    {
        if (!string.IsNullOrEmpty(alias))
        {
            _pendingWhereClauses.Add((lambda, alias));
        }
    }

    /// <summary>
    /// Clears all WHERE clauses.
    /// </summary>
    public void ClearWhere()
    {
        _whereClauses.Clear();
        _pendingWhereClauses.Clear();
    }

    /// <summary>
    /// Clears only the processed WHERE clauses, keeping the pending ones.
    /// This is used when we need to reprocess pending WHERE clauses with updated aliases.
    /// </summary>
    public void ClearProcessedWhereClausesOnly()
    {
        _whereClauses.Clear();
    }

    /// <summary>
    /// Appends the WHERE clause content to the query builder.
    /// </summary>
    /// <param name="builder">The string builder to append to.</param>
    /// <param name="parameters">The parameters dictionary for the query.</param>
    public void AppendTo(StringBuilder builder, Dictionary<string, object?> parameters)
    {
        // Process any pending WHERE clauses first
        ProcessPendingWhereClauses();

        if (_whereClauses.Count > 0)
        {
            builder.Append("WHERE ");
            builder.AppendJoin(" AND ", _whereClauses);
            builder.AppendLine();
        }
    }

    /// <summary>
    /// Processes all pending WHERE clauses and converts them to Cypher conditions.
    /// This is where the separation of concerns pays off - handlers set up the context,
    /// and this method uses visitors to process the expressions.
    /// </summary>
    public void FinalizePendingClauses()
    {
        ProcessPendingWhereClauses();
    }

    /// <summary>
    /// Updates aliases in WHERE clauses for path segments.
    /// This is used when we have a Traverse + PathSegments pattern and need to update
    /// the WHERE clause aliases from the Traverse pattern to the PathSegments pattern.
    /// </summary>
    public void UpdateAliasesForPathSegments(string newSourceAlias, string newTargetAlias)
    {
        // Update existing WHERE clauses to use the new aliases
        for (int i = 0; i < _whereClauses.Count; i++)
        {
            var clause = _whereClauses[i];
            // Replace src with newSourceAlias and tgt with newTargetAlias
            // Use word boundaries to avoid replacing partial matches
            clause = System.Text.RegularExpressions.Regex.Replace(clause, @"\bsrc\b", newSourceAlias);
            clause = System.Text.RegularExpressions.Regex.Replace(clause, @"\btgt\b", newTargetAlias);
            _whereClauses[i] = clause;
        }
    }

    /// <summary>
    /// Processes all pending WHERE clauses and converts them to Cypher conditions.
    /// </summary>
    private void ProcessPendingWhereClauses()
    {
        foreach (var (lambda, alias) in _pendingWhereClauses)
        {
            ProcessWhereClause(lambda, alias);
        }
        _pendingWhereClauses.Clear();
    }

    /// <summary>
    /// Processes a single WHERE clause using the expression processor.
    /// </summary>
    private void ProcessWhereClause(LambdaExpression lambda, string alias)
    {
        // Use the abstracted expression processor to convert lambda to Cypher
        var expression = _expressionProcessor.ProcessExpression(lambda.Body, alias);
        
        // Add the resulting condition
        AddWhere(expression);
    }
}