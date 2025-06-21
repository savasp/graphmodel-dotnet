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

namespace Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Expressions;

using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Core;

/// <summary>
/// Factory for creating standardized expression visitor chains
/// </summary>
internal class ExpressionVisitorChainFactory(CypherQueryContext context)
{
    // Define standard chains that can be reused
    public ICypherExpressionVisitor CreateStandardChain() =>
        new ExpressionVisitorChainBuilder(context)
            .AddMemberExpressions() // Add member expression handling early to detect complex property access
            .AddConversions()       // Handle conversions first (op_Implicit, etc.)
            .AddCollectionMethods()
            .AddDateTimeMethods()
            .AddStringMethods()
            .AddBinary()
            .AddBase()             // Base should be last as the fallback
            .Build();

    public ICypherExpressionVisitor CreateWhereClauseChain(string alias) =>
            new ExpressionVisitorChainBuilder(context)
                .AddMemberExpressions(alias)
                .AddConversions()
                .AddCollectionMethods()
                .AddDateTimeMethods()
                .AddStringMethods()
                .AddBinary()
                .AddBase()
                .Build();

    public ICypherExpressionVisitor CreateSelectClauseChain(string alias) =>
        new ExpressionVisitorChainBuilder(context)
            .AddMemberExpressions(alias) // Especially important for SELECT to detect complex property access
            .AddConversions()
            .AddAggregations()      // SELECT might need aggregation support
            .AddCollectionMethods()
            .AddDateTimeMethods()
            .AddStringMethods()
            .AddBinary()
            .AddBase()
            .Build();

    public ICypherExpressionVisitor CreateOrderByChain() =>
        new ExpressionVisitorChainBuilder(context)
            .AddMemberExpressions() // ORDER BY often accesses properties
            .AddConversions()
            .AddStringMethods()     // Often need string operations in ORDER BY
            .AddBinary()
            .AddBase()
            .Build();

    public ICypherExpressionVisitor CreateGroupByChain() =>
        new ExpressionVisitorChainBuilder(context)
            .AddMemberExpressions() // GROUP BY accesses properties
            .AddConversions()
            .AddBinary()
            .AddBase()
            .Build();
}