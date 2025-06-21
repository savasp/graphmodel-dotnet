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

namespace Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors;

using System.Linq.Expressions;
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Core;
using Microsoft.Extensions.Logging;

internal sealed class FirstVisitor(CypherQueryContext context) : CypherVisitorBase<FirstVisitor>(context)
{
    public void VisitFirst(Expression? predicate = null, bool orDefault = false, bool isLast = false)
    {
        var alias = Scope.CurrentAlias
            ?? throw new InvalidOperationException("No current alias set when building First/Last clause");

        // If there's a predicate, apply it as a WHERE clause
        if (predicate != null)
        {
            var whereVisitor = new WhereVisitor(alias, Context);
            whereVisitor.Visit(predicate);
        }

        // For Last, we need to handle ordering
        if (isLast)
        {
            HandleLastOperation();
        }

        // Add LIMIT 1 to get only the first/last result
        Builder.AddLimit(1);

        // If we don't have a RETURN clause yet, add one
        if (!Builder.HasReturnClause)
        {
            Builder.AddReturn(alias);
        }
    }

    private void HandleLastOperation()
    {
        // Check if we have existing ORDER BY clauses
        if (Builder.HasOrderBy)
        {
            // Reverse the existing order
            Builder.ReverseOrderBy();
            Logger.LogDebug("Reversed existing ORDER BY for Last operation");
        }
        else
        {
            // No existing order - add default ordering by internal ID descending
            var alias = Scope.CurrentAlias
                ?? throw new InvalidOperationException("No current alias set when adding default order for Last");
            Builder.AddOrderBy($"id({alias})", isDescending: true);
            Logger.LogDebug("Added default ORDER BY id() DESC for Last operation");
        }
    }
}