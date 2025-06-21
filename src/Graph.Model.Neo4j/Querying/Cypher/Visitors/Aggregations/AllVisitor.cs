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

internal sealed class AllVisitor(CypherQueryContext context) : AggregationBaseVisitor<AllVisitor>(context)
{
    public void VisitAll(LambdaExpression predicate)
    {
        var alias = Scope.CurrentAlias
            ?? throw new InvalidOperationException("No current alias set when building All clause");

        // In Cypher, ALL(x IN collection WHERE predicate) checks if all match
        // But since we're working with matched nodes, we need a different approach

        // Strategy: Count all nodes and count nodes matching predicate
        // If they're equal, all nodes match

        // First, get total count
        var totalCountParam = Builder.AddParameter($"__totalCount_{alias}");
        Builder.AddWith($"COUNT({alias}) AS {totalCountParam}");

        // Then apply the predicate
        var whereVisitor = new WhereVisitor(alias, Context);
        whereVisitor.Visit(predicate.Body);

        // Count matching nodes and compare
        Builder.AddReturn($"COUNT({alias}) = {totalCountParam} AS result");
    }
}