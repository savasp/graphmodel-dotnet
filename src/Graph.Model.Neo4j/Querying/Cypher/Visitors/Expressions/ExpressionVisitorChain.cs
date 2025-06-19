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

using System.Linq.Expressions;
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Core;

/// <summary>
/// Manages the chain of expression visitors for Cypher query generation.
/// </summary>
internal class ExpressionVisitorChain
{
    private readonly ICypherExpressionVisitor _rootVisitor;

    public ExpressionVisitorChain(CypherQueryContext context)
    {
        _rootVisitor = new ExpressionVisitorChainFactory(context).CreateStandardChain();
    }

    /// <summary>
    /// Visits the expression and returns the Cypher representation.
    /// </summary>
    public string Visit(Expression expression) => _rootVisitor.Visit(expression);
}
