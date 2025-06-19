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
/// Handles the ToList LINQ method which is a terminal operation that executes the query.
/// </summary>
internal record ToListMethodHandler : MethodHandlerBase
{
    public override bool Handle(CypherQueryContext context, MethodCallExpression node, Expression result)
    {
        if (node.Method.Name != "ToList" || node.Arguments.Count != 1)
        {
            return false;
        }

        // ToList is a terminal operation - it doesn't modify the query, just executes it
        // The query building has already been done by previous method calls in the chain
        // We just need to ensure the query is properly finalized

        // If no explicit return has been set, add a default return
        if (!context.Builder.HasReturnClause)
        {
            var alias = context.Scope.CurrentAlias ?? "n";
            context.Builder.AddReturn(alias);
        }

        return true;
    }
}
