// Copyright CVOYA. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.InMemory.Querying;

using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Cvoya.Graph.Querying;

/// <summary>
/// The in-memory <see cref="IGraphQueryProvider"/>: compiles LINQ expression trees to the shared
/// provider-neutral <see cref="GraphQueryModel"/> (the same level-1 IR the Cypher pipeline
/// consumes) and interprets that model with LINQ-to-objects over a store snapshot. No query
/// language is involved anywhere on this path.
/// </summary>
internal sealed class InMemoryQueryProvider : IGraphQueryProvider
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

    public Task<TResult> ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ResultShaper.Shape<TResult>(ExecuteCore(expression, cancellationToken)));
    }

    public Task<object?> ExecuteAsync(Expression expression, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = ExecuteCore(expression, cancellationToken);
        return Task.FromResult(result is DefaultResult ? null : result);
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

        var state = _transaction is not null ? _transaction.View : _store.CurrentState;
        var executor = new InMemoryQueryExecutor(_reader, state, cancellationToken);
        return executor.Execute(model, TerminalHints.From(expression));
    }

    /// <summary>
    /// Maps builder rejections to the exception types the public query surface documents,
    /// mirroring the reference provider so unsupported operators fail identically everywhere.
    /// </summary>
    private static Exception MapTranslationException(GraphQueryTranslationException exception)
    {
        if (exception.Message.Contains("SelectMany", StringComparison.Ordinal))
        {
            return new NotSupportedException("SelectMany is not supported by graph query translation yet; see #100.");
        }

        if (exception.Message.Contains("GroupBy", StringComparison.Ordinal))
        {
            return new NotSupportedException("GroupBy is not supported by graph query translation yet; see #100.");
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
            call.Method.DeclaringType?.FullName != "Cvoya.Graph.QueryTerminals")
        {
            return false;
        }

        var name = call.Method.Name;
        if (name is not ("ToDictionaryAsyncMarker" or "ToLookupAsyncMarker"))
        {
            return false;
        }

        var lambdas = call.Arguments.Skip(1)
            .Select(StripQuotes)
            .OfType<LambdaExpression>()
            .Select(l => l.Compile())
            .ToList();

        source = call.Arguments[0];
        apply = items =>
        {
            var list = ((System.Collections.IEnumerable)(items ?? Array.Empty<object?>())).Cast<object?>().ToList();
            var pairs = list.Select(item => (
                Key: lambdas[0].DynamicInvoke(item),
                Value: lambdas.Count > 1 ? lambdas[1].DynamicInvoke(item) : item));

            return name == "ToDictionaryAsyncMarker"
                ? pairs.ToDictionary(p => p.Key!, p => p.Value)
                : pairs.ToLookup(p => p.Key!, p => p.Value);
        };

        return true;
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
