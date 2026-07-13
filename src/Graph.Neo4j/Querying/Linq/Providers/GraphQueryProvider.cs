// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Querying.Linq.Providers;

using System.Linq.Expressions;
using Cvoya.Graph.Neo4j.Core;
using Cvoya.Graph.Neo4j.Querying.Cypher.Execution;
using Cvoya.Graph.Querying.Linq;
using global::Neo4j.Driver;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

internal sealed class GraphQueryProvider : GraphQueryProviderBase<GraphTransaction>
{
    private readonly GraphContext context;
    private readonly GraphTransaction? transaction;
    private readonly bool isReadOnly;
    private readonly ILogger<GraphQueryProvider> logger;
    private readonly CypherEngine cypherEngine;

    public GraphQueryProvider(GraphContext context, GraphTransaction? transaction, bool isReadOnly = false)
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

    protected override Task<GraphTransaction> GetOrCreateTransactionAsync(CancellationToken cancellationToken) =>
        TransactionHelpers.GetOrCreateTransactionAsync(context, transaction, isReadOnly, cancellationToken);

    protected override IAsyncEnumerable<TResult> StreamCoreAsync<TResult>(
        Expression expression,
        GraphTransaction graphTransaction,
        CancellationToken cancellationToken) =>
        cypherEngine.StreamAsync<TResult>(expression, graphTransaction, cancellationToken);

    protected override bool IsTransactionActive(GraphTransaction graphTransaction) => graphTransaction.IsActive;

    protected override bool IsDriverException(Exception exception) => exception is Neo4jException;

    protected override void LogExecution(Expression expression, Type resultType, bool streaming)
    {
        if (streaming)
        {
            logger.LogDebugGraphQueryProvider65(resultType.Name);
        }
        else
        {
            logger.LogDebugGraphQueryProvider69(resultType.Name);
        }
        logger.LogDebugGraphQueryProvider71(expression.Type.Name);

        if (logger.IsEnabled(LogLevel.Debug))
        {
            LogExpressionTree(expression);
        }
    }

    protected override void LogExecutionFailure(Exception exception) =>
        logger.LogErrorGraphQueryProvider80(exception);

    protected override void LogRollbackFailure(Exception exception) =>
        logger.LogWarningGraphQueryProvider83(exception);

    private void LogExpressionTree(Expression expression, int depth = 0)
    {
        var indent = new string(' ', depth * 2);

        if (expression is MethodCallExpression methodCall)
        {
            logger.LogDebugGraphQueryProvider91(indent, methodCall.Method.Name, methodCall.Method.DeclaringType?.Name);

            foreach (var argument in methodCall.Arguments)
            {
                LogExpressionTree(argument, depth + 1);
            }
        }
        else if (expression is ConstantExpression constant)
        {
            logger.LogDebugGraphQueryProvider104(indent, constant.Value?.GetType().Name ?? "null");
        }
    }
}
