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

namespace Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Handlers;

using System.Linq.Expressions;
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Builders;
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using static Cvoya.Graph.Model.Neo4j.Querying.Cypher.Builders.CypherQueryBuilder;

/// <summary>
/// Handles the Where LINQ method by generating appropriate WHERE clauses.
/// Refactored to eliminate duplication with WhereVisitor and centralize alias resolution.
/// </summary>
internal record WhereMethodHandler : MethodHandlerBase
{
    public override bool Handle(CypherQueryContext context, MethodCallExpression node, Expression result)
    {
        var logger = CreateLogger(context, nameof(WhereMethodHandler));

        // Validate this is a WHERE method call
        if (!ValidateMethodCall(node, "Where", 2))
        {
            logger.LogDebug("Not a WHERE method call or wrong argument count");
            return false;
        }

        logger.LogDebug("Processing WHERE method call");

        // Mark that we've applied the root WHERE predicate
        context.Builder.HasAppliedRootWhere = true;

        // Extract the lambda expression (predicate)
        var lambda = ExtractLambda(node.Arguments[1]);
        if (lambda == null)
        {
            throw new GraphException("Where method requires a lambda expression predicate");
        }

        // Determine the correct alias for this WHERE clause using centralized logic
        var targetAlias = DetermineContextAlias(context, "Where");
        logger.LogDebug("Using alias '{Alias}' for WHERE clause", targetAlias);

        // Set pending WHERE clause - the expression processing will happen later
        // This is the key separation: handlers orchestrate, visitors process expressions
        context.Builder.SetPendingWhere(lambda, targetAlias);

        logger.LogDebug("WHERE method handled successfully");
        return true;
    }
}
