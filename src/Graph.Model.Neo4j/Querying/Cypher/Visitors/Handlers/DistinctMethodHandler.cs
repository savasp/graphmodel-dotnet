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
using Microsoft.Extensions.Logging;

/// <summary>
/// Handles the Distinct LINQ method by adding DISTINCT clause to the query.
/// </summary>
internal record DistinctMethodHandler : MethodHandlerBase
{
    public override bool Handle(CypherQueryContext context, MethodCallExpression node, Expression result)
    {
        var logger = context.LoggerFactory?.CreateLogger(nameof(DistinctMethodHandler));
        logger?.LogDebug("DistinctMethodHandler called");

        if (node.Method.Name != "Distinct" || node.Arguments.Count != 1)
        {
            logger?.LogDebug("DistinctMethodHandler: not a Distinct method or wrong arguments");
            return false;
        }

        // Set distinct flag on the builder
        context.Builder.SetDistinct(true);

        // For scalar projections, disable complex property loading
        if (context.Scope.RootType != null && IsScalarOrPrimitive(context.Scope.RootType))
        {
            context.Builder.DisableComplexPropertyLoading();
        }

        logger?.LogDebug("DistinctMethodHandler: completed successfully");
        return true;
    }

    private static bool IsScalarOrPrimitive(Type type)
    {
        if (type.IsPrimitive || type.IsEnum)
            return true;

        if (type == typeof(string) || type == typeof(Guid) || type == typeof(DateTime) ||
            type == typeof(DateTimeOffset) || type == typeof(decimal))
            return true;

        if (Nullable.GetUnderlyingType(type) is { } underlying)
            return IsScalarOrPrimitive(underlying);

        return false;
    }
}
