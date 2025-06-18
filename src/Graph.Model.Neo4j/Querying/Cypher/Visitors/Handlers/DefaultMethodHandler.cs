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
/// Default method handler that does nothing - placeholder for future implementations.
/// </summary>
internal record DefaultMethodHandler : MethodHandlerBase
{

    public override bool Handle(CypherQueryContext context, MethodCallExpression node, Expression result)
    {
        // For now, just return false to indicate the method wasn't handled
        // The actual handling is done by the appropriate visitors
        return false;
    }
}