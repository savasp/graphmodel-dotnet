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
/// Represents a queryable collection of graph nodes that can be traversed.
/// </summary>
/// <typeparam name="T">The type of the graph nodes.</typeparam>
public interface IGraphQueryable<T> : IQueryable<T>
{
    /// <summary>
    /// Gets the options for the graph operation.
    /// </summary>
    GraphOperationOptions Options { get; }

    /// <summary>
    /// Specifies the depth of the traversal.
    /// </summary>
    /// <param name="depth">The depth of the traversal.</param>
    /// <returns>An instance of <see cref="IGraphQueryable{T}"/> with the specified depth.</returns>
    IGraphQueryable<T> WithDepth(int depth);

    /// <summary>
    /// Specifies the transaction to use for the query.
    /// </summary>
    /// <param name="transaction">The transaction to use for the query.</param>
    /// <returns>An instance of <see cref="IGraphQueryable{T}"/> with the specified transaction.</returns>
    IGraphQueryable<T> InTransaction(IGraphTransaction transaction);

    /// <summary>
    /// Traverses outgoing relationships from nodes of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="R">The type of the relationships to traverse.</typeparam>
    /// <returns>An instance of <see cref="IGraphTraversal{T, R}"/> for the specified relationship type.</returns>
    IGraphTraversal<T, R> Traverse<R>() where R : class, IRelationship, new();
}
