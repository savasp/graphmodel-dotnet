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

using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Core;

internal sealed class DistinctVisitor(CypherQueryContext context) : ClauseVisitorBase<DistinctVisitor>(context)
{
    public void ApplyDistinct()
    {
        // Mark the query as needing DISTINCT
        Builder.SetDistinct(true);

        // If we don't have a RETURN clause yet, add one
        if (!Builder.HasReturnClause)
        {
            var alias = Scope.CurrentAlias ?? "n";
            Builder.AddReturn($"DISTINCT {alias}");
        }
    }
}