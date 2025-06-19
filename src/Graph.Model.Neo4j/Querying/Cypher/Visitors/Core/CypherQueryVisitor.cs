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

namespace Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Core;

using System.Linq.Expressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Main visitor that orchestrates the translation of LINQ expressions to Cypher queries.
/// </summary>
internal class CypherQueryVisitor : ExpressionVisitor
{
    private readonly CypherQueryContext _context;
    private readonly ILogger<CypherQueryVisitor> _logger;

    public CypherQueryVisitor(Type type, ILoggerFactory? loggerFactory = null)
    {
        _context = new CypherQueryContext(type, loggerFactory);
        _logger = loggerFactory?.CreateLogger<CypherQueryVisitor>()
            ?? NullLogger<CypherQueryVisitor>.Instance;
    }

    /// <summary>
    /// Gets the generated Cypher query.
    /// </summary>
    public CypherQuery Query => new(_context.GetQuery(), _context.GetParameters());

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        _logger.LogDebug("Visiting method call: {Method}", node.Method.Name);
        _logger.LogDebug("Method declaring type: {DeclaringType}", node.Method.DeclaringType?.Name);
        _logger.LogDebug("Method arguments count: {ArgsCount}", node.Arguments.Count);

        try
        {
            // First visit the source of the method call
            var result = Visit(node.Arguments[0]);

            // Then try to handle the method using the registry
            if (_context.MethodHandlers.TryHandle(_context, node, result))
            {
                _logger.LogDebug("Method {Method} was handled by registry", node.Method.Name);
                return result;
            }

            _logger.LogDebug("Method {Method} was NOT handled by registry", node.Method.Name);

            // If not handled, check if it's a supported queryable method
            if (IsQueryableMethod(node))
            {
                throw new GraphException(
                    $"LINQ method '{node.Method.Name}' is not yet supported for Cypher translation. " +
                    "Consider using a supported method or restructuring your query.");
            }

            // Otherwise, it might be a method call within an expression
            return base.VisitMethodCall(node);
        }
        catch (GraphException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new GraphException(
                $"Error processing method '{node.Method.Name}': {ex.Message}",
                ex);
        }
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        _logger.LogDebug("VisitConstant called with value type: {ValueType}", node.Value?.GetType().Name ?? "null");

        // Handle queryable sources - these are the actual data sources we need to query
        if (node.Value is IQueryable queryable)
        {
            _logger.LogDebug("Found queryable of element type {Type}", queryable.ElementType.Name);

            // Check if this is a relationship queryable
            if (node.Value is IGraphRelationshipQueryable relationshipQueryable)
            {
                var relLabel = Labels.GetLabelFromType(queryable.ElementType);

                // Use the existing AddRelationshipMatch method
                _context.Builder.AddRelationshipMatch(relLabel);

                // Set the current alias to "r" (which is what AddRelationshipMatch uses)
                _context.Scope.CurrentAlias = "r";
            }
            else if (node.Value is IGraphNodeQueryable)
            {
                // For nodes, generate the MATCH clause using the queryable's element type
                var alias = _context.Scope.GetOrCreateAlias(queryable.ElementType, "n");
                var label = Labels.GetLabelFromType(queryable.ElementType);
                _logger.LogDebug("Adding MATCH clause: ({Alias}:{Label})", alias, label);
                _context.Builder.AddMatch(alias, label);

                // Don't enable complex property loading here - defer the decision
                // until we know if this is a traversal query
                _logger.LogDebug("Deferring complex property loading decision");

                _context.Scope.CurrentAlias = alias;
            }

            return node;
        }

        return base.VisitConstant(node);
    }

    private static bool IsQueryableMethod(MethodCallExpression node)
    {
        return node.Method.DeclaringType == typeof(Queryable) ||
               node.Method.DeclaringType == typeof(Enumerable) ||
               (node.Method.DeclaringType?.Namespace?.StartsWith("System.Linq") ?? false);
    }
}