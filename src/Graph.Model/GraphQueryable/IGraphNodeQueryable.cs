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
/// Represents a queryable graph data source that supports LINQ operations over nodes.
/// This interface extends IGraphQueryable&lt;T&gt; with additional functionality specific to graph nodes.
/// It allows for traversing relationships and querying nodes in a graph context.
/// This interface is designed to be used with graph databases and provides methods for traversing relationships
/// </summary>
/// <typeparam name="TNode">An <see cref="INode"/>-derived type.</typeparam>
public interface IGraphNodeQueryable<TNode> : IGraphQueryable<TNode>
    where TNode : INode
{
    /// <summary>
    /// Starts traversing from nodes through a relationship type to a target type.
    /// This method allows for fluent traversal and filtering of relationships and nodes in a graph context.
    /// It enables the construction of complex queries that traverse relationships between nodes.
    /// </summary>
    /// <typeparam name="TRel">The type of the relationship being traversed, which must be an <see cref="IRelationship"/>-derived type.</typeparam>
    /// <typeparam name="TTarget">The type of the target node, which must be an <see cref="INode"/>-derived type.</typeparam>
    /// <returns>An <see cref="IGraphTraversalQueryable{TNode, TRel, TTarget}"/> for fluent traversal and filtering.</returns>
    IGraphTraversalQueryable<TNode, TRel, TTarget> Traverse<TRel, TTarget>()
        where TRel : IRelationship
        where TTarget : INode;

    /// <summary>
    /// Gets an <see cref="IGraphRelationshipQueryable{TRel}"/> for querying relationships of a specific type that connect this node.
    /// </summary>
    /// <typeparam name="TRel">The type of the relationship being queried, which must be an <see cref="IRelationship"/>-derived type.</typeparam>
    /// <returns>An <see cref="IGraphRelationshipQueryable{TRel}"/> for querying relationships of the specified type.</returns>
    IGraphRelationshipQueryable<TRel> Relationships<TRel>()
        where TRel : IRelationship;

    /// <summary>
    /// Gets an <see cref="IGraphTraversalQueryable{TNode, TRel, TTarget}"/> for traversing relationships of a specific type to a target node type.
    /// This method allows for fluent traversal and filtering of relationships and nodes in a graph context.
    /// </summary>
    /// <typeparam name="TRel">The type of the relationship being traversed, which must be an <see cref="IRelationship"/>-derived type.</typeparam>
    /// <typeparam name="TTarget">The type of the target node, which must be an <see cref="INode"/>-derived type.</typeparam>
    /// <returns>An <see cref="IGraphTraversalQueryable{TNode, TRel, TTarget}"/> for fluent traversal and filtering.</returns>
    IGraphTraversalQueryable<TNode, TRel, TTarget> Relationships<TRel, TTarget>()
        where TRel : IRelationship
        where TTarget : INode;

    /// <summary>
    /// Gets an <see cref="IGraphQueryable{T}"/> where T is <see cref="IGraphPathSegment{TNode, TRel, TTarget}"/> for traversing path segments of a specific relationship type to a target node type.
    /// This method allows for fluent traversal and filtering of relationships and nodes in a graph context, specifically focusing on the path segments connected to this node.
    /// </summary>
    /// <typeparam name="TRel">The type of the relationship being traversed, which must be an <see cref="IRelationship"/>-derived type.</typeparam>
    /// <typeparam name="TTarget">The type of the target node, which must be an <see cref="INode"/>-derived type.</typeparam>
    /// <returns>An <see cref="IGraphQueryable{T}"/> where T is <see cref="IGraphPathSegment{TNode, TRel, TTarget}"/> for querying path segments of the specified relationship type.</returns>
    IGraphQueryable<IGraphPathSegment<TNode, TRel, TTarget>> PathSegments<TRel, TTarget>()
        where TRel : IRelationship
        where TTarget : INode;
}