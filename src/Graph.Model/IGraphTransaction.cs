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
/// Defines the contract for transaction management in the graph system.
/// </summary>
/// <remarks>
/// Transactions provide atomic, consistent, isolated, and durable (ACID) operations
/// for graph mutations. Implement the using pattern or await using pattern with this interface.
/// </remarks>
/// <example>
/// <code>
/// await using var transaction = await graph.BeginTransaction();
/// try
/// {
///     await graph.CreateNode(person, transaction: transaction);
///     await graph.CreateNode(address, transaction: transaction);
///     var livesAt = new LivesAt { Source = person, Target = address };
///     await graph.CreateRelationship(livesAt, transaction: transaction);
///     await transaction.Commit();
/// }
/// catch
/// {
///     await transaction.Rollback();
///     throw;
/// }
/// </code>
/// </example>
public interface IGraphTransaction : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// Commits the transaction, making all changes permanent.
    /// </summary>
    /// <returns>A task representing the asynchronous commit operation.</returns>
    /// <exception cref="GraphException">Thrown if the commit fails.</exception>
    /// <remarks>
    /// After calling Commit, the transaction is considered complete and cannot be used further.
    /// </remarks>
    Task CommitAsync();

    /// <summary>
    /// Rolls back the transaction, discarding all pending changes.
    /// </summary>
    /// <returns>A task representing the asynchronous rollback operation.</returns>
    /// <exception cref="GraphException">Thrown if the rollback fails.</exception>
    /// <remarks>
    /// After calling Rollback, the transaction is considered complete and cannot be used further.
    /// </remarks>
    Task Rollback();
}
