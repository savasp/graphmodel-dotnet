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
using Cvoya.Graph.Model;
using Cvoya.Graph.Provider.Neo4j.Linq.Builders;

namespace Cvoya.Graph.Provider.Neo4j.Linq.Processors;

/// <summary>
/// Processes LINQ Where clauses by building appropriate Cypher WHERE conditions
/// </summary>
internal class WhereProcessor
{
    private static readonly ExpressionBuilderDispatcher _expressionDispatcher = new();

    public static void ProcessWhere(LambdaExpression predicate, CypherBuildContext context)
    {
        var whereClause = _expressionDispatcher.BuildExpression(predicate.Body, context.CurrentAlias, context);

        if (!string.IsNullOrWhiteSpace(whereClause))
        {
            if (context.Where.Length > 0)
            {
                context.Where.Append(" AND ");
            }

            context.Where.Append(whereClause);
        }
    }
}
