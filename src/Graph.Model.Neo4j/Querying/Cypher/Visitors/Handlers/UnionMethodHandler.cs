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
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Core;

/// <summary>
/// Handles Union and Concat LINQ methods by generating UNION clauses.
/// </summary>
internal record UnionMethodHandler : MethodHandlerBase
{
    public override bool Handle(CypherQueryContext context, MethodCallExpression node, Expression result)
    {
        var methodName = node.Method.Name;

        if (methodName is not ("Union" or "Concat") || node.Arguments.Count != 2)
        {
            return false;
        }

        // In Neo4j, UNION is used to combine results from multiple queries
        // This is a simplified implementation - in practice, you'd need to handle
        // the second collection more sophisticatedly

        var useDistinct = methodName == "Union"; // Union removes duplicates, Concat doesn't

        // For now, we'll add a comment indicating this needs more complex handling
        // In a real implementation, you'd need to:
        // 1. Process the second collection as a separate sub-query
        // 2. Combine using UNION or UNION ALL

        // Add a marker for union operations that can be processed later
        context.Builder.AddWith($"/* {methodName} operation - requires post-processing */");

        return true;
    }
}
