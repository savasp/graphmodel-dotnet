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

namespace Cvoya.Graph.Model;

/// <summary>
/// Represents a queryable graph data source that supports LINQ operations.
/// This interface extends <see cref="IGraphQueryable{T}"/> with graph-specific functionality.
/// It allows for traversing relationships and querying nodes within a graph.
/// This interface is designed to be used with graph databases and provides methods for traversing relationships.
/// </summary>
/// <typeparam name="TRel">An <see cref="IRelationship"/>-derived type.</typeparam>
public interface IGraphRelationshipQueryable<TRel> : IGraphQueryable<TRel>
    where TRel : IRelationship
{
    /// <summary>
    /// Starts traversing from the relationship to its source and target nodes.
    /// This method allows for fluent traversal and filtering of relationships and nodes in a graph context.
    /// It enables the construction of complex queries that traverse relationships between nodes.
    /// </summary>
    /// <typeparam name="TSource">The type of the source node, which must be an <see cref="INode"/>-derived type.</typeparam>
    /// <typeparam name="TTarget">The type of the target node, which must be an <see cref="INode"/>-derived type.</typeparam>
    /// <returns>An <see cref="IGraphTraversalQueryable{TSource, TRel, TTarget}"/> for fluent traversal and filtering.</returns>
    IGraphTraversalQueryable<TSource, TRel, TTarget> Traverse<TSource, TTarget>()
        where TSource : INode
        where TTarget : INode;
}