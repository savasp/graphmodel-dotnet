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

internal sealed class CountVisitor(CypherQueryContext context) : AggregationBaseVisitor<CountVisitor>(context)
{
    public void VisitCount(Expression? predicate = null)
    {
        // If there's a predicate, apply it
        if (predicate != null)
        {
            var whereVisitor = new WhereVisitor(Context);
            whereVisitor.Visit(predicate);
        }

        // For Count(), we just need to know the total count
        // Use COUNT() for efficiency
        var alias = Scope.CurrentAlias
            ?? throw new InvalidOperationException("No current alias set when building Count clause");
        Builder.AddReturn($"COUNT({alias}) AS result");
    }
}