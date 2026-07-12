// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Querying.Linq.Providers;

using System.Linq.Expressions;
using System.Reflection;
using Cvoya.Graph.Age.Core;
using Cvoya.Graph.Age.Querying.Cypher.Execution;
using Cvoya.Graph.Querying.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

internal sealed class GraphQueryProvider : GraphQueryProviderBase<AgeGraphTransaction>
{
    private readonly AgeGraphContext context;
    private readonly AgeGraphTransaction? transaction;
    private readonly bool isReadOnly;
    private readonly ILogger<GraphQueryProvider> logger;
    private readonly CypherEngine cypherEngine;

    public GraphQueryProvider(AgeGraphContext context, AgeGraphTransaction? transaction, bool isReadOnly = false)
        : base(context?.Graph ?? throw new ArgumentNullException(nameof(context)), transaction is null)
    {
        this.context = context;
        this.transaction = transaction;
        this.isReadOnly = isReadOnly;
        logger = context.LoggerFactory?.CreateLogger<GraphQueryProvider>()
            ?? NullLogger<GraphQueryProvider>.Instance;
        cypherEngine = new CypherEngine(context.EntityFactory, context.LoggerFactory);
    }

    protected override async Task<TResult> ExecuteCoreAsync<TResult>(
        Expression expression,
        CancellationToken cancellationToken)
    {
        var result = await TransactionHelpers.ExecuteInTransactionAsync(
            context,
            transaction,
            tx => cypherEngine.ExecuteAsync<TResult>(expression, tx, cancellationToken),
            "Error executing query",
            logger,
            isReadOnly,
            cancellationToken).ConfigureAwait(false);
        return result!;
    }

    protected override Task<AgeGraphTransaction> GetOrCreateTransactionAsync(CancellationToken cancellationToken) =>
        TransactionHelpers.GetOrCreateTransactionAsync(context, transaction, isReadOnly, cancellationToken);

    protected override IAsyncEnumerable<TResult> StreamCoreAsync<TResult>(
        Expression expression,
        AgeGraphTransaction graphTransaction,
        CancellationToken cancellationToken) =>
        cypherEngine.StreamAsync<TResult>(expression, graphTransaction, cancellationToken);

    protected override bool IsTransactionActive(AgeGraphTransaction graphTransaction) => graphTransaction.IsActive;

    protected override bool IsDriverException(Exception exception) => exception is NpgsqlException;

    protected override bool UnwrapCreateQueryInvocationException => true;

    protected override bool ShouldWrapCreateQueryException(Exception exception) =>
        exception is AmbiguousMatchException or MethodAccessException or ArgumentException;

    protected override void LogExecution(Expression expression, Type resultType, bool streaming)
    {
        if (streaming)
        {
            logger.LogDebugGraphQueryProvider71(resultType.Name);
        }
        else
        {
            logger.LogDebugGraphQueryProvider75(resultType.Name);
        }
        logger.LogDebugGraphQueryProvider77(expression.Type.Name);

        if (logger.IsEnabled(LogLevel.Debug))
        {
            LogExpressionTree(expression);
        }
    }

    protected override void LogExecutionFailure(Exception exception) =>
        logger.LogErrorGraphQueryProvider86(exception);

    protected override void LogRollbackFailure(Exception exception) =>
        logger.LogWarningGraphQueryProvider89(exception);

    private void LogExpressionTree(Expression expression, int depth = 0)
    {
        var indent = new string(' ', depth * 2);

        if (expression is MethodCallExpression methodCall)
        {
            logger.LogDebugGraphQueryProvider97(indent, methodCall.Method.Name, methodCall.Method.DeclaringType?.Name);

            foreach (var argument in methodCall.Arguments)
            {
                LogExpressionTree(argument, depth + 1);
            }
        }
        else if (expression is ConstantExpression constant)
        {
            logger.LogDebugGraphQueryProvider110(indent, constant.Value?.GetType().Name ?? "null");
        }
    }
}
