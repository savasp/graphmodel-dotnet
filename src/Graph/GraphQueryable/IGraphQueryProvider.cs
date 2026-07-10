// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph;

using System.Linq.Expressions;


/// <summary>
/// Represents a graph query provider that handles graph-specific operations
/// </summary>
public interface IGraphQueryProvider : IQueryProvider
{
    /// <summary>
    /// Gets the graph instance associated with this provider
    /// </summary>
    IGraph Graph { get; }

    /// <summary>
    /// Creates a new graph query with the specified expression
    /// </summary>
    /// <typeparam name="T">The type of the elements in the query</typeparam>
    /// <param name="expression">The expression representing the query</param>
    /// <returns>A new graph query with the specified expression</returns>
    new IGraphQueryable<T> CreateQuery<T>(Expression expression);

    /// <summary>
    /// Asynchronously executes the expression and returns the result.
    /// </summary>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="expression">The expression to execute.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the result of the expression.</returns>
    Task<TResult> ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously executes the expression and returns the result.
    /// </summary>
    /// <param name="expression">The expression to execute.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the result of the expression.</returns>
    Task<object?> ExecuteAsync(Expression expression, CancellationToken cancellationToken = default);
}
