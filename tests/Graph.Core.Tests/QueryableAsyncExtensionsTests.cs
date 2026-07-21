// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Core.Tests;

using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using Cvoya.Graph.Querying;

public sealed class QueryableAsyncExtensionsTests
{
    [Fact]
    public async Task AverageAsync_NonGraphSourceFallback_MatchesLinqOverloadMatrix()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        Assert.Equal(20.5, await Query(20, 21).AverageAsync(cancellationToken));
        Assert.Equal(100.5, await Query(100L, 101L).AverageAsync(cancellationToken));
        Assert.Equal(1.5f, await Query(1f, 2f).AverageAsync(cancellationToken));
        Assert.Equal(2.5, await Query(2d, 3d).AverageAsync(cancellationToken));
        Assert.Equal(3.5m, await Query(3m, 4m).AverageAsync(cancellationToken));

        Assert.Equal(15, await Query<int?>(10, null, 20).AverageAsync(cancellationToken));
        Assert.Equal(150, await Query<long?>(100, null, 200).AverageAsync(cancellationToken));
        Assert.Equal(2, await Query<float?>(1, null, 3).AverageAsync(cancellationToken));
        Assert.Equal(6, await Query<double?>(4, null, 8).AverageAsync(cancellationToken));
        Assert.Equal(8, await Query<decimal?>(7, null, 9).AverageAsync(cancellationToken));
        Assert.Null(await Query<int?>().AverageAsync(cancellationToken));
        Assert.Null(await Query<int?>(null, null).AverageAsync(cancellationToken));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Query<int>().AverageAsync(cancellationToken));
    }

    [Fact]
    public async Task AverageAsync_NonGraphSelectorFallback_MatchesLinqOverloadMatrix()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var rows = Query(
            new NumericRow(20, 100, 1, 2, 3, 10, 100, 1, 4, 7),
            new NumericRow(21, 101, 2, 3, 4, null, null, null, null, null),
            new NumericRow(22, 102, 3, 4, 5, 20, 200, 3, 8, 9));

        Assert.Equal(21, await rows.AverageAsync(row => row.Int, cancellationToken));
        Assert.Equal(101, await rows.AverageAsync(row => row.Long, cancellationToken));
        Assert.Equal(2, await rows.AverageAsync(row => row.Float, cancellationToken));
        Assert.Equal(3, await rows.AverageAsync(row => row.Double, cancellationToken));
        Assert.Equal(4, await rows.AverageAsync(row => row.Decimal, cancellationToken));
        Assert.Equal(15, await rows.AverageAsync(row => row.NullableInt, cancellationToken));
        Assert.Equal(150, await rows.AverageAsync(row => row.NullableLong, cancellationToken));
        Assert.Equal(2, await rows.AverageAsync(row => row.NullableFloat, cancellationToken));
        Assert.Equal(6, await rows.AverageAsync(row => row.NullableDouble, cancellationToken));
        Assert.Equal(8, await rows.AverageAsync(row => row.NullableDecimal, cancellationToken));

        var empty = Query<NumericRow>();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => empty.AverageAsync(row => row.Int, cancellationToken));
        Assert.Null(await empty.AverageAsync(row => row.NullableInt, cancellationToken));
    }

    [Fact]
    public async Task ElementAtNegativeIndex_ShortCircuitsProviderAndFallbackSources()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var provider = new RecordingGraphQueryProvider();

        Assert.Null(await ProviderQuery<string>(provider).ElementAtOrDefaultAsync(-1, cancellationToken));
        Assert.Equal(0, await ProviderQuery<int>(provider).ElementAtOrDefaultAsync(-1, cancellationToken));
        Assert.Null(await ProviderQuery<int?>(provider).ElementAtOrDefaultAsync(-1, cancellationToken));

        var providerException = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => ProviderQuery<string>(provider).ElementAtAsync(-1, cancellationToken));
        Assert.Equal("index", providerException.ParamName);
        Assert.Equal(0, provider.ExecutionCount);

        Assert.Null(await Query("value").ElementAtOrDefaultAsync(-1, cancellationToken));
        Assert.Null(await Query<string>().ElementAtOrDefaultAsync(-1, cancellationToken));
        Assert.Equal(0, await Query(42).ElementAtOrDefaultAsync(-1, cancellationToken));
        Assert.Equal(0, await Query<int>().ElementAtOrDefaultAsync(-1, cancellationToken));
        Assert.Null(await Query<int?>(42).ElementAtOrDefaultAsync(-1, cancellationToken));
        Assert.Null(await Query<int?>().ElementAtOrDefaultAsync(-1, cancellationToken));

        var fallbackException = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => Query("value").ElementAtAsync(-1, cancellationToken));
        Assert.Equal("index", fallbackException.ParamName);
    }

    [Fact]
    public async Task ElementAtNegativeIndex_PreCanceledTokenTakesPrecedence()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var provider = new RecordingGraphQueryProvider();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => ProviderQuery<int>(provider).ElementAtOrDefaultAsync(-1, cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => ProviderQuery<int>(provider).ElementAtAsync(-1, cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => Query(1).ElementAtOrDefaultAsync(-1, cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => Query(1).ElementAtAsync(-1, cts.Token));

        Assert.Equal(0, provider.ExecutionCount);
    }

    [Theory]
    [InlineData(nameof(QueryableAsyncExtensions.AllAsync))]
    [InlineData(nameof(QueryableAsyncExtensions.SingleAsync))]
    public async Task PredicateTerminal_NullPredicateUsesPublicArgumentContract(string operation)
    {
        var provider = new RecordingGraphQueryProvider();

        var providerException = await Assert.ThrowsAsync<ArgumentNullException>(
            () => InvokeNullPredicateAsync(operation, ProviderQuery<int>(provider)));
        Assert.Equal("predicate", providerException.ParamName);
        Assert.Equal(0, provider.ExecutionCount);

        var fallbackException = await Assert.ThrowsAsync<ArgumentNullException>(
            () => InvokeNullPredicateAsync(operation, Query(1)));
        Assert.Equal("predicate", fallbackException.ParamName);
    }

    [Fact]
    public async Task EveryPublicAsyncTerminal_EmitsRegisteredQueryTerminalMarker()
    {
        var methods = typeof(QueryableAsyncExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(method => typeof(Task).IsAssignableFrom(method.ReturnType))
            .ToArray();

        Assert.NotEmpty(methods);

        foreach (var definition in methods)
        {
            var method = definition.IsGenericMethodDefinition
                ? definition.MakeGenericMethod(
                    Enumerable.Repeat(typeof(int), definition.GetGenericArguments().Length).ToArray())
                : definition;
            var provider = new RecordingGraphQueryProvider();
            var source = CreateProviderQuery(method.GetParameters()[0].ParameterType, provider);
            var arguments = method.GetParameters()
                .Select(parameter => CreateArgument(parameter, source))
                .ToArray();

            var task = Assert.IsAssignableFrom<Task>(method.Invoke(null, arguments));
            await task;

            Assert.Equal(1, provider.ExecutionCount);
            var call = Assert.IsAssignableFrom<MethodCallExpression>(provider.LastExpression);
            Assert.True(
                call.Method.DeclaringType == typeof(QueryTerminals),
                $"{method} emitted a marker declared by '{call.Method.DeclaringType}'.");
            Assert.True(
                LinqOperatorDispatch.Resolve(call.Method) is not null,
                $"{method} emitted unregistered marker '{call.Method}'.");
        }
    }

    private static Task InvokeNullPredicateAsync(string operation, IGraphQueryable<int> source) =>
        operation switch
        {
            nameof(QueryableAsyncExtensions.AllAsync) => source.AllAsync(null!),
            nameof(QueryableAsyncExtensions.SingleAsync) => source.SingleAsync(null!),
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };

    private static object CreateProviderQuery(
        Type queryContract,
        RecordingGraphQueryProvider provider)
    {
        var elementType = queryContract.GetGenericArguments()[0];
        return Activator.CreateInstance(
            typeof(RecordingGraphQueryable<>).MakeGenericType(elementType),
            provider)!;
    }

    private static object? CreateArgument(ParameterInfo parameter, object source)
    {
        if (parameter.Position == 0)
        {
            return source;
        }

        if (parameter.ParameterType == typeof(CancellationToken))
        {
            return CancellationToken.None;
        }

        if (parameter.Name == "index")
        {
            return 0;
        }

        if (parameter.ParameterType.IsGenericType &&
            parameter.ParameterType.GetGenericTypeDefinition() == typeof(Expression<>))
        {
            var delegateType = parameter.ParameterType.GetGenericArguments()[0];
            var signature = delegateType.GetGenericArguments();
            var lambdaParameters = signature[..^1]
                .Select((type, index) => Expression.Parameter(type, $"value{index}"))
                .ToArray();
            var resultType = signature[^1];
            Expression body = resultType == typeof(bool)
                ? Expression.Constant(true)
                : Expression.Default(resultType);
            return Expression.Lambda(delegateType, body, lambdaParameters);
        }

        return parameter.ParameterType.IsValueType
            ? Activator.CreateInstance(parameter.ParameterType)
            : null;
    }

    private static EnumerableGraphQueryable<T> Query<T>(params T[] values) =>
        new EnumerableGraphQueryable<T>(values);

    private static RecordingGraphQueryable<T> ProviderQuery<T>(RecordingGraphQueryProvider provider) =>
        new RecordingGraphQueryable<T>(provider);

    private sealed record NumericRow(
        int Int,
        long Long,
        float Float,
        double Double,
        decimal Decimal,
        int? NullableInt,
        long? NullableLong,
        float? NullableFloat,
        double? NullableDouble,
        decimal? NullableDecimal);

    /// <summary>
    /// Supplies a normal LINQ-to-objects provider through <see cref="IQueryable.Provider"/> while
    /// retaining the graph-queryable marker needed to call the public async extensions.
    /// </summary>
    private sealed class EnumerableGraphQueryable<T>(IEnumerable<T> values) : IGraphQueryable<T>
    {
        private readonly IQueryable<T> queryable = values.AsQueryable();

        public Type ElementType => typeof(T);

        public Expression Expression => queryable.Expression;

        IGraphQueryProvider IGraphQueryable<T>.Provider =>
            throw new NotSupportedException("This fixture intentionally has no graph provider.");

        IQueryProvider IQueryable.Provider => queryable.Provider;

        public IGraph Graph =>
            throw new NotSupportedException("This fixture intentionally has no graph instance.");

        public IEnumerator<T> GetEnumerator() => queryable.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public async IAsyncEnumerator<T> GetAsyncEnumerator(
            CancellationToken cancellationToken = default)
        {
            foreach (var value in queryable)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return value;
                await Task.Yield();
            }
        }
    }

    private sealed class RecordingGraphQueryable<T> : IOrderedGraphQueryable<T>
    {
        public RecordingGraphQueryable(RecordingGraphQueryProvider provider)
            : this(provider, expression: null)
        {
        }

        public RecordingGraphQueryable(RecordingGraphQueryProvider provider, Expression? expression)
        {
            Provider = provider;
            Expression = expression ?? Expression.Constant(this);
        }

        public Type ElementType => typeof(T);

        public Expression Expression { get; }

        public IGraphQueryProvider Provider { get; }

        IQueryProvider IQueryable.Provider => Provider;

        public IGraph Graph => null!;

        public IEnumerator<T> GetEnumerator() => Enumerable.Empty<T>().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public async IAsyncEnumerator<T> GetAsyncEnumerator(
            CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class RecordingGraphQueryProvider : IGraphQueryProvider
    {
        public int ExecutionCount { get; private set; }

        public Expression? LastExpression { get; private set; }

        public IGraph Graph => null!;

        public IQueryable CreateQuery(Expression expression)
        {
            var elementType = ExtensionUtils.GetQueryableElementType(expression.Type);
            return (IQueryable)Activator.CreateInstance(
                typeof(RecordingGraphQueryable<>).MakeGenericType(elementType),
                this,
                expression)!;
        }

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression) =>
            new RecordingGraphQueryable<TElement>(this, expression);

        IGraphQueryable<TElement> IGraphQueryProvider.CreateQuery<TElement>(Expression expression) =>
            new RecordingGraphQueryable<TElement>(this, expression);

        public object? Execute(Expression expression) =>
            throw new NotSupportedException();

        public TResult Execute<TResult>(Expression expression) =>
            throw new NotSupportedException();

        public Task<TResult> ExecuteAsync<TResult>(
            Expression expression,
            CancellationToken cancellationToken = default)
        {
            ExecutionCount++;
            LastExpression = expression;
            var result = typeof(TResult).IsGenericType &&
                typeof(TResult).GetGenericTypeDefinition() == typeof(List<>)
                    ? Activator.CreateInstance<TResult>()
                    : default;
            return Task.FromResult(result!);
        }

        public Task<object?> ExecuteAsync(
            Expression expression,
            CancellationToken cancellationToken = default)
        {
            ExecutionCount++;
            LastExpression = expression;
            return Task.FromResult<object?>(null);
        }
    }
}
