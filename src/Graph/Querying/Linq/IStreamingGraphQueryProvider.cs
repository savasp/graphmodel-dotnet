// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying.Linq;

using System.Linq.Expressions;

internal interface IStreamingGraphQueryProvider : IGraphQueryProvider
{
    IAsyncEnumerable<TResult> StreamAsync<TResult>(
        Expression expression,
        CancellationToken cancellationToken = default);
}
