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
/// Represents a traversal query that allows navigating from a source node through relationships to target nodes.
/// </summary>
/// <typeparam name="TSource">
/// The type of the source node, which must implement <see cref="INode"/>.
/// </typeparam>
/// <typeparam name="TRelationship">
/// The type of the relationship, which must implement <see cref="IRelationship"/>.
/// </typeparam>
public interface ITraversalQuery<TSource, TRelationship>
    where TSource : class, INode, new()
    where TRelationship : class, IRelationship, new()
{
    /// <summary>
    /// Complete the traversal to target nodes of the specified type
    /// </summary>
    IQueryable<TTarget> To<TTarget>() where TTarget : class, INode, new();

    /// <summary>
    /// Complete the traversal and return the full path (source, relationship, target)
    /// </summary>
    IQueryable<TraversalPath<TSource, TRelationship, TTarget>> ToPath<TTarget>()
        where TTarget : class, INode, new();
}