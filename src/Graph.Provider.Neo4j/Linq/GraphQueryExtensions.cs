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

namespace Cvoya.Graph.Provider.Neo4j.Linq;

/// <summary>
/// Extension methods for graph query operations.
/// </summary>
public static class GraphQueryExtensions
{
    /// <summary>
    /// Placeholder for connected by operation.
    /// </summary>
    public static IQueryable<TTarget> ConnectedBy<TSource, TRelationship, TTarget>(
        this IQueryable<TSource> source,
        Expression<Func<TRelationship, bool>>? relationshipPredicate = null)
        where TSource : class, INode, new()
        where TRelationship : class, IRelationship, new()
        where TTarget : class, INode, new()
    {
        throw new NotImplementedException("ConnectedBy method is not yet implemented");
    }

    /// <summary>
    /// Placeholder for shortest path operation.
    /// </summary>
    public static IQueryable<TTarget> ShortestPath<TSource, TTarget>(
        this IQueryable<TSource> source,
        Expression<Func<TTarget, bool>>? targetPredicate = null)
        where TSource : class, INode, new()
        where TTarget : class, INode, new()
    {
        throw new NotImplementedException("ShortestPath method is not yet implemented");
    }
}
