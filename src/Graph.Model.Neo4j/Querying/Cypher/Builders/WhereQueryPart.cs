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

using System.Linq.Expressions;
using System.Text;
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Core;
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Expressions;

namespace Cvoya.Graph.Model.Neo4j.Querying.Cypher.Builders;

/// <summary>
/// Handles WHERE clause construction for Cypher queries.
/// Extracted from the monolithic CypherQueryBuilder and processes pending WHERE clauses
/// using the expression visitor pattern.
/// </summary>
internal class WhereQueryPart : ICypherQueryPart
{
    private readonly List<string> _whereClauses = [];
    private readonly List<(LambdaExpression Lambda, string Alias)> _pendingWhereClauses = [];
    private readonly CypherQueryContext _context;

    public WhereQueryPart(CypherQueryContext context)
    {
        _context = context;
    }

    public int Order => 2; // WHERE comes after MATCH

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
    /// Processes all pending WHERE clauses and converts them to Cypher conditions.
    /// This is where the separation of concerns pays off - handlers set up the context,
    /// and this method uses visitors to process the expressions.
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
    /// Processes a single WHERE clause using the expression visitor pattern.
    /// </summary>
    private void ProcessWhereClause(LambdaExpression lambda, string alias)
    {
        // Create expression visitor for this specific WHERE clause
        var expressionVisitor = new ExpressionVisitorChainFactory(_context)
            .CreateWhereClauseChain(alias);

        // Visit the lambda body to get the Cypher expression
        var expression = expressionVisitor.Visit(lambda.Body);

        // Add the resulting condition
        AddWhere(expression);
    }
}