// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.InMemory.Querying;

using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Cvoya.Graph.Querying;
using Cvoya.Graph.Querying.Commands;

/// <summary>
/// The in-memory <see cref="IGraphQueryProvider"/>: compiles LINQ expression trees to the shared
/// provider-neutral <see cref="GraphQueryModel"/> (the same level-1 IR the Cypher pipeline
/// consumes) and interprets that model with LINQ-to-objects over a store snapshot. No query
/// language is involved anywhere on this path.
/// </summary>
internal sealed class InMemoryQueryProvider : IGraphQueryProvider, IGraphCommandProvider
{
    private readonly InMemoryStore _store;
    private readonly InMemoryTransaction? _transaction;
    private readonly EntityReader _reader;

    public InMemoryQueryProvider(
        IGraph graph,
        InMemoryStore store,
        InMemoryTransaction? transaction,
        EntityReader reader)
    {
        Graph = graph;
        _store = store;
        _transaction = transaction;
        _reader = reader;
    }

    public IGraph Graph { get; }

    public IGraphQueryable<TElement> CreateQuery<TElement>(Expression expression) =>
        new InMemoryQueryable<TElement>(this, expression);

    IQueryable IQueryProvider.CreateQuery(Expression expression)
    {
        var elementType = GetElementType(expression.Type);
        var queryableType = typeof(InMemoryQueryable<>).MakeGenericType(elementType);
        return (IQueryable)Activator.CreateInstance(queryableType, this, expression)!;
    }

    IQueryable<TElement> IQueryProvider.CreateQuery<TElement>(Expression expression) =>
        CreateQuery<TElement>(expression);

    public object? Execute(Expression expression) => ExecuteCore(expression, CancellationToken.None);

    public TResult Execute<TResult>(Expression expression) =>
        ResultShaper.Shape<TResult>(ExecuteCore(expression, CancellationToken.None));

    public async Task<TResult> ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (GraphMutationModelBuilder.IsMutation(expression))
        {
            var mutation = GraphMutationModelBuilder.Build(expression);
            var affected = await ((IGraphCommandProvider)this).InWriteTransactionAsync(
                (context, token) => context.ApplyAsync(mutation, expression, token),
                cancellationToken).ConfigureAwait(false);
            return ResultShaper.Shape<TResult>(affected);
        }

        return ResultShaper.Shape<TResult>(ExecuteCore(expression, cancellationToken));
    }

    public async Task<object?> ExecuteAsync(Expression expression, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (GraphMutationModelBuilder.IsMutation(expression))
        {
            return await ExecuteAsync<int>(expression, cancellationToken).ConfigureAwait(false);
        }

        var result = ExecuteCore(expression, cancellationToken);
        return result is DefaultResult ? null : result;
    }

    object IGraphCommandProvider.GraphOwnershipToken => _store;

    IGraphTransaction? IGraphCommandProvider.BoundTransaction => _transaction;

    Task<TResult> IGraphCommandProvider.InWriteTransactionAsync<TResult>(
        Func<IGraphCommandExecutionContext, CancellationToken, Task<TResult>> command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return TransactionRunner.ExecuteAsync(
            _store,
            _transaction,
            transaction => command(
                new Commands.InMemoryGraphCommandExecutionContext(transaction, _reader, Graph.SchemaRegistry),
                cancellationToken),
            "Failed to execute an in-memory graph command",
            cancellationToken);
    }

    /// <summary>
    /// Streams query results. Execution is snapshot-based, so cancelling or abandoning the
    /// stream mid-way leaves no store state behind.
    /// </summary>
    public async IAsyncEnumerable<TResult> Stream<TResult>(
        Expression expression,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var results = ResultShaper.Shape<IEnumerable<TResult>>(ExecuteCore(expression, cancellationToken)) ?? [];
        foreach (var item in results)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
            await Task.Yield();
        }
    }

    private object? ExecuteCore(Expression expression, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // ToDictionary/ToLookup markers are not part of the shared operator table (their
        // selectors have no graph-side meaning); materialize the source and apply them here.
        if (TryUnwrapClientMaterializer(expression, out var source, out var apply))
        {
            var items = ExecuteCore(source!, cancellationToken);
            return apply!(items);
        }

        GraphQueryModel model;
        try
        {
            model = GraphQueryModelBuilder.Build(expression);
        }
        catch (GraphQueryTranslationException exception)
        {
            throw MapTranslationException(exception);
        }

        EnsureSupported(model);
        GraphQueryModelValidator.Validate(model);

        var state = _transaction is not null ? _transaction.View : _store.CurrentState;
        var executor = new InMemoryQueryExecutor(_reader, state, Graph.SchemaRegistry, cancellationToken);
        return executor.Execute(model, TerminalHints.From(expression));
    }

    private static void EnsureSupported(GraphQueryModel model)
    {
        if (model.SelectMany is not null)
        {
            throw new NotSupportedException(
                "SelectMany is not supported by graph query translation yet; see #100.");
        }

        if (model.GroupBy is { } groupBy && !groupBy.GroupsByPathSegmentStartNode &&
            ScalarGroupByValidation.DescribeUnsupported(model) is { } reason)
        {
            // The #120 correlated collection-projection shape (grouping by a path-segment start node)
            // and structurally supported scalar-key aggregation grouping (#306) both flow through to
            // the interpreter; only genuinely unsupported grouping shapes are rejected here, with the
            // same reason the Cypher planner reports so rejections match across providers.
            throw new NotSupportedException(ScalarGroupByValidation.BuildMessage(reason));
        }

        // Enforce the correlated grouped-projection grammar with the same shared boundary the Cypher
        // planner uses, so an unsupported member (for example First/Take/Skip over the group) fails with
        // the identical exception type and message here as on Neo4j — rather than the CLR silently
        // executing a shape Cypher cannot lower. This throw is outside the builder's translation-exception
        // mapping, so the GraphQueryTranslationException is not downgraded to NotSupportedException.
        if (CorrelatedGroupProjectionValidation.Validate(model) is { } correlatedReason)
        {
            throw new GraphQueryTranslationException(
                CorrelatedGroupProjectionValidation.BuildMessage(correlatedReason));
        }

    }

    /// <summary>
    /// Maps builder rejections to the exception types the public query surface documents,
    /// mirroring the reference provider so unsupported operators fail identically everywhere.
    /// </summary>
    private static Exception MapTranslationException(GraphQueryTranslationException exception)
    {
        if (exception.Message.StartsWith(
            "Cannot translate the correlated grouped projection:", StringComparison.Ordinal))
        {
            return exception;
        }

        if (exception.Message.Contains("SelectMany", StringComparison.Ordinal))
        {
            return new NotSupportedException("SelectMany is not supported by graph query translation yet; see #100.");
        }

        if (exception.Message.Contains("GroupBy", StringComparison.Ordinal))
        {
            // Preserve the precise builder rejection (e.g. chained GroupBy) rather than a generic
            // message, so scalar-key grouping (#306) surfaces the same actionable reason everywhere.
            return new NotSupportedException(exception.Message);
        }

        if (exception.Message.Contains("Union", StringComparison.Ordinal))
        {
            return new GraphException("Union operations are not yet fully implemented in the refactored architecture");
        }

        if (exception.Message.Contains("requires an explicit OrderBy", StringComparison.Ordinal))
        {
            return new GraphException(exception.Message);
        }

        if (exception.Message.Contains("chained after 'TraversePaths", StringComparison.Ordinal))
        {
            return new NotSupportedException(exception.Message);
        }

        return exception;
    }

    private static bool TryUnwrapClientMaterializer(
        Expression expression,
        out Expression? source,
        out Func<object?, object?>? apply)
    {
        source = null;
        apply = null;

        if (expression is not MethodCallExpression call ||
            call.Method.DeclaringType != typeof(QueryTerminals))
        {
            return false;
        }

        var name = call.Method.Name;
        if (name is not (nameof(QueryTerminals.ToDictionaryAsyncMarker) or
            nameof(QueryTerminals.ToLookupAsyncMarker)))
        {
            return false;
        }

        var lambdas = call.Arguments.Skip(1)
            .Select(StripQuotes)
            .OfType<LambdaExpression>()
            .Select(l => l.Compile())
            .ToList();

        var resultTypes = call.Type.GetGenericArguments();
        var materializerName = name == nameof(QueryTerminals.ToDictionaryAsyncMarker)
            ? nameof(BuildDictionary)
            : nameof(BuildLookup);
        var materializer = typeof(InMemoryQueryProvider)
            .GetMethod(materializerName, BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(resultTypes);

        source = call.Arguments[0];
        apply = items => InvokeMaterializer(
            materializer,
            items,
            lambdas[0],
            lambdas.Count > 1 ? lambdas[1] : null);

        return true;
    }

    private static Dictionary<TKey, TElement> BuildDictionary<TKey, TElement>(
        object? items,
        Delegate keySelector,
        Delegate? elementSelector)
        where TKey : notnull =>
        Enumerate(items).ToDictionary(
            item => (TKey)InvokeSelector(keySelector, item)!,
            item => elementSelector is null
                ? (TElement)item!
                : (TElement)InvokeSelector(elementSelector, item)!);

    private static ILookup<TKey, TElement> BuildLookup<TKey, TElement>(
        object? items,
        Delegate keySelector,
        Delegate? elementSelector) =>
        Enumerate(items).ToLookup(
            item => (TKey)InvokeSelector(keySelector, item)!,
            item => elementSelector is null
                ? (TElement)item!
                : (TElement)InvokeSelector(elementSelector, item)!);

    private static IEnumerable<object?> Enumerate(object? items) =>
        ((System.Collections.IEnumerable)(items ?? Array.Empty<object?>())).Cast<object?>();

    private static object? InvokeMaterializer(MethodInfo materializer, params object?[] arguments)
    {
        try
        {
            return materializer.Invoke(null, arguments);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    private static object? InvokeSelector(Delegate selector, object? item)
    {
        try
        {
            return selector.DynamicInvoke(item);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    private static Expression StripQuotes(Expression expression) =>
        expression is UnaryExpression { NodeType: ExpressionType.Quote } unary ? unary.Operand : expression;

    private static Type GetElementType(Type type)
    {
        if (type.IsGenericType)
        {
            var arguments = type.GetGenericArguments();
            if (arguments.Length == 1)
            {
                return arguments[0];
            }
        }

        var enumerable = type.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        return enumerable?.GetGenericArguments()[0] ?? typeof(object);
    }
}
