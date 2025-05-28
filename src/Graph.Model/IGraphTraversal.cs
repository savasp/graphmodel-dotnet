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

namespace Cvoya.Graph.Model;

/// <summary>
/// Interface for traversing a graph structure.
/// </summary>
public interface IGraphTraversal
{
    /// <summary>
    /// Traverses outgoing relationships from a node.
    /// </summary>
    /// <typeparam name="TNode">The node type.</typeparam>
    /// <typeparam name="TRelationship">The relationship type.</typeparam>
    /// <param name="depth">The depth of traversal. Default is 1.</param>
    /// <returns>The traversal operation with the specified parameters.</returns>
    IGraphTraversal TraverseOut<TNode, TRelationship>(int depth = 1)
        where TNode : INode
        where TRelationship : IRelationship;

    /// <summary>
    /// Traverses incoming relationships to a node.
    /// </summary>
    /// <typeparam name="TNode">The node type.</typeparam>
    /// <typeparam name="TRelationship">The relationship type.</typeparam>
    /// <param name="depth">The depth of traversal. Default is 1.</param>
    /// <returns>The traversal operation with the specified parameters.</returns>
    IGraphTraversal TraverseIn<TNode, TRelationship>(int depth = 1)
        where TNode : INode
        where TRelationship : IRelationship;

    /// <summary>
    /// Traverses both incoming and outgoing relationships of a node.
    /// </summary>
    /// <typeparam name="TNode">The node type.</typeparam>
    /// <typeparam name="TRelationship">The relationship type.</typeparam>
    /// <param name="depth">The depth of traversal. Default is 1.</param>
    /// <returns>The traversal operation with the specified parameters.</returns>
    IGraphTraversal TraverseBoth<TNode, TRelationship>(int depth = 1)
        where TNode : INode
        where TRelationship : IRelationship;

    /// <summary>
    /// Filters traversal to only include the specified relationship types.
    /// </summary>
    /// <param name="relationshipTypes">The relationship types to include.</param>
    /// <returns>The traversal operation with the specified filter.</returns>
    IGraphTraversal WithRelationshipTypes(params string[] relationshipTypes);

    /// <summary>
    /// Filters traversal to exclude the specified relationship types.
    /// </summary>
    /// <param name="relationshipTypes">The relationship types to exclude.</param>
    /// <returns>The traversal operation with the specified filter.</returns>
    IGraphTraversal WithoutRelationshipTypes(params string[] relationshipTypes);
    
    /// <summary>
    /// Filters traversal to exclude property relationships (those starting with "__PROPERTY__").
    /// </summary>
    /// <returns>The traversal operation with property relationships excluded.</returns>
    IGraphTraversal WithoutPropertyRelationships();

    /// <summary>
    /// Applies a filter to nodes during traversal.
    /// </summary>
    /// <typeparam name="TNode">The node type.</typeparam>
    /// <param name="filter">The filter expression to apply.</param>
    /// <returns>The traversal operation with the specified filter.</returns>
    IGraphTraversal WhereNode<TNode>(Expression<Func<TNode, bool>> filter) where TNode : INode;
}