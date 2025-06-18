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
/// Base class for method handlers.
/// </summary>
internal abstract record MethodHandlerBase() : IMethodHandler
{
    public abstract bool Handle(CypherQueryContext context, MethodCallExpression node, Expression result);

    protected void ValidateArgumentCount(MethodCallExpression node, int expectedCount)
    {
        if (node.Arguments.Count != expectedCount)
        {
            throw new GraphException(
                $"Method {node.Method.Name} expects {expectedCount} arguments, " +
                $"but received {node.Arguments.Count}");
        }
    }
}

