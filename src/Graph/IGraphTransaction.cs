// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph;

/// <summary>
/// Defines the contract for transaction management in the graph system.
/// </summary>
/// <remarks>
/// Transactions provide atomic, consistent, isolated, and durable (ACID) operations
/// for graph mutations. Implement the using pattern or await using pattern with this interface.
/// </remarks>
/// <example>
/// <code>
/// await using var transaction = await graph.GetTransactionAsync();
/// try
/// {
///     await graph.CreateAsync(person, new LivesAt(), address, transaction: transaction);
///     await transaction.CommitAsync();
/// }
/// catch
/// {
///     await transaction.RollbackAsync();
///     throw;
/// }
/// </code>
/// </example>
public interface IGraphTransaction : IAsyncDisposable
{
    /// <summary>
    /// Commits the transaction, making all changes permanent.
    /// </summary>
    /// <returns>A task representing the asynchronous commit operation.</returns>
    /// <exception cref="GraphException">Thrown if the commit fails.</exception>
    /// <remarks>
    /// After calling <see cref="CommitAsync"/>, the transaction is considered complete and cannot be used further.
    /// </remarks>
    Task CommitAsync();

    /// <summary>
    /// Rolls back the transaction, discarding all pending changes.
    /// </summary>
    /// <returns>A task representing the asynchronous rollback operation.</returns>
    /// <exception cref="GraphException">Thrown if the rollback fails.</exception>
    /// <remarks>
    /// After calling <see cref="RollbackAsync"/>, the transaction is considered complete and cannot be used further.
    /// </remarks>
    Task RollbackAsync();
}
