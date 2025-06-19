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

namespace Cvoya.Graph.Model.Neo4j.Querying.Linq.Helpers;

using System.Linq.Expressions;
using System.Reflection;
using Cvoya.Graph.Model.Neo4j.Core;

/// <summary>
/// Provides method information for graph-specific LINQ operations.
/// </summary>
internal static class GraphMethods
{
    public static readonly MethodInfo WithTransactionMethod;
    public static readonly MethodInfo PathSegmentsMethod;

    static GraphMethods()
    {
        var graphNodeQueryableExtensions = typeof(GraphNodeQueryableExtensions);

        WithTransactionMethod = typeof(GraphQueryableExtensions)
            .GetMethod(nameof(GraphQueryableExtensions.WithTransaction))!;

        // Get the generic method definitions from the interfaces
        var nodeQueryableType = typeof(IGraphNodeQueryable<>);

        PathSegmentsMethod = nodeQueryableType
            .GetMethod(nameof(IGraphNodeQueryable<INode>.PathSegments))!;
    }
}