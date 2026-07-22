// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying.Commands;

/// <summary>Internal provider SPI for graph commands and exact-one endpoint selection.</summary>
internal interface IGraphCommandProvider : IGraphQueryProvider
{
    object GraphOwnershipToken { get; }

    IGraphTransaction? BoundTransaction { get; }

    Task PrepareRelationshipCreationAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;

    Task<TResult> InWriteTransactionAsync<TResult>(
        Func<IGraphCommandExecutionContext, CancellationToken, Task<TResult>> command,
        CancellationToken cancellationToken);
}
