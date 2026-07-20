// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Core.Tests;

using System.Linq.Expressions;
using Cvoya.Graph.Querying;
using Cvoya.Graph.Querying.Commands;

[Trait("Area", "GraphCommands")]
public sealed class GraphCommandProviderScopeTests
{
    [Fact]
    public void Validate_RejectsDifferentGraphOwnershipBeforeExecution()
    {
        var first = new FakeCommandProvider(new object(), transaction: null);
        var second = new FakeCommandProvider(new object(), transaction: null);

        var exception = Assert.Throws<GraphException>(() => GraphCommandProviderScope.Validate(first, second));

        Assert.Contains("same graph instance", exception.Message, StringComparison.Ordinal);
        Assert.False(first.Executed);
        Assert.False(second.Executed);
    }

    [Fact]
    public void Validate_RejectsDifferentBoundTransactionBeforeExecution()
    {
        var graph = new object();
        var first = new FakeCommandProvider(graph, new FakeTransaction());
        var second = new FakeCommandProvider(graph, new FakeTransaction());

        var exception = Assert.Throws<GraphException>(() => GraphCommandProviderScope.Validate(first, second));

        Assert.Contains("same transaction object", exception.Message, StringComparison.Ordinal);
        Assert.False(first.Executed);
        Assert.False(second.Executed);
    }

    [Fact]
    public void Validate_AcceptsSameGraphAndBoundTransactionObjects()
    {
        var graph = new object();
        var transaction = new FakeTransaction();

        GraphCommandProviderScope.Validate(
            new FakeCommandProvider(graph, transaction),
            new FakeCommandProvider(graph, transaction));
    }

    [Theory]
    [InlineData(GraphEndpointRole.Source, GraphCardinalityFailure.Empty, 0)]
    [InlineData(GraphEndpointRole.Target, GraphCardinalityFailure.Multiple, 2)]
    public async Task SelectExactOneAsync_ThrowsRoleSpecificCardinalityFailure(
        GraphEndpointRole role,
        GraphCardinalityFailure failure,
        int selectedCount)
    {
        var selection = Selection();
        var selected = Enumerable.Range(0, selectedCount)
            .Select(index => new SelectedGraphElement(GraphElementKind.Node, index))
            .ToArray();
        var context = new FakeExecutionContext(selected);

        var exception = await Assert.ThrowsAsync<GraphCardinalityException>(() =>
            GraphCommandSelection.SelectExactOneAsync(
                context,
                selection,
                Expression.Constant(1),
                role,
                TestContext.Current.CancellationToken));

        Assert.Equal(role, exception.Role);
        Assert.Equal(failure, exception.Failure);
        Assert.Equal(1, context.SelectionCalls);
    }

    [Fact]
    public async Task SelectExactOneAsync_FlowsCancellationIntoSelection()
    {
        var context = new FakeExecutionContext([]);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            GraphCommandSelection.SelectExactOneAsync(
                context,
                Selection(),
                Expression.Constant(1),
                GraphEndpointRole.Source,
                cancellation.Token));

        Assert.Equal(1, context.SelectionCalls);
    }

    private static GraphElementSelectionModel Selection() => new(
        new GraphQueryModel(
            new NodeRoot(typeof(TestNode)),
            predicates: [],
            traversal: [],
            projection: null,
            ordering: [],
            new Paging(null, null),
            TerminalOperation.ToListOrArray),
        GraphElementSelectionMode.ExactOne);

    private sealed record TestNode : Node;

    private sealed class FakeExecutionContext(IReadOnlyList<SelectedGraphElement> selected)
        : IGraphCommandExecutionContext
    {
        public int SelectionCalls { get; private set; }

        public IGraphTransaction Transaction { get; } = new FakeTransaction();

        public Task<IReadOnlyList<SelectedGraphElement>> SelectAsync(
            GraphElementSelectionModel selection,
            Expression sourceExpression,
            CancellationToken cancellationToken)
        {
            SelectionCalls++;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(selected);
        }

        public Task<int> ApplyAsync(
            GraphMutationModel mutation,
            Expression mutationExpression,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeCommandProvider(object graphToken, IGraphTransaction? transaction)
        : IGraphCommandProvider
    {
        public bool Executed { get; private set; }

        public object GraphOwnershipToken => graphToken;

        public IGraphTransaction? BoundTransaction => transaction;

        public IGraph Graph => null!;

        public Task<TResult> InWriteTransactionAsync<TResult>(
            Func<IGraphCommandExecutionContext, CancellationToken, Task<TResult>> command,
            CancellationToken cancellationToken)
        {
            Executed = true;
            throw new NotSupportedException();
        }

        public IGraphQueryable<T> CreateQuery<T>(Expression expression) => throw new NotSupportedException();

        IQueryable IQueryProvider.CreateQuery(Expression expression) => throw new NotSupportedException();

        IQueryable<TElement> IQueryProvider.CreateQuery<TElement>(Expression expression) =>
            throw new NotSupportedException();

        public object? Execute(Expression expression) => throw new NotSupportedException();

        public TResult Execute<TResult>(Expression expression) => throw new NotSupportedException();

        public Task<TResult> ExecuteAsync<TResult>(
            Expression expression,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<object?> ExecuteAsync(
            Expression expression,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeTransaction : IGraphTransaction
    {
        public Task CommitAsync() => Task.CompletedTask;

        public Task RollbackAsync() => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
