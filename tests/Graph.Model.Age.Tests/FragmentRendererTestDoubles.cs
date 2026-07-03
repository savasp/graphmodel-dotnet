// Copyright 2025 Savas Parastatidis

namespace Cvoya.Graph.Model.Age.Tests;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Cvoya.Graph.Model;
using Cvoya.Graph.Model.Age.Querying.Cypher.Visitors.Core;
using Cvoya.Graph.Model.Cypher.Querying.Cypher.Visitors.Core;

#pragma warning disable GM003 // Allow navigation property and complex node property for testing
public sealed record PersonNode : Node
{
    public override IReadOnlyList<string> Labels { get; set; } = new List<string> { nameof(PersonNode) };
    public int Age { get; init; }
    public string Name { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public List<KnowsRelationship> Friends { get; init; } = new();
}

public sealed record PersonWithAddressNode : Node
{
    public override IReadOnlyList<string> Labels { get; set; } = new List<string> { nameof(PersonWithAddressNode) };
    public string Name { get; init; } = string.Empty;
    public AddressNode HomeAddress { get; init; } = new AddressNode();
}
#pragma warning restore GM003

public sealed record AddressNode : Node
{
    public override IReadOnlyList<string> Labels { get; set; } = new List<string> { nameof(AddressNode) };
    public string Street { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
}

public sealed record KnowsRelationship(string StartNodeId, string EndNodeId, RelationshipDirection Direction = RelationshipDirection.Outgoing) : Relationship(StartNodeId, EndNodeId, Direction)
{
    public override string Type { get; set; } = nameof(KnowsRelationship);
    public int Age { get; init; }
}

internal sealed class TestGraphQueryProvider : IGraphQueryProvider
{
    public IGraph Graph { get; } = new TestGraph();

    public IQueryable CreateQuery(Expression expression)
    {
        var elementType = expression.Type.GetGenericArguments().FirstOrDefault() ?? expression.Type;
        var method = typeof(TestGraphQueryProvider).GetMethod(nameof(CreateUntypedQuery), BindingFlags.Instance | BindingFlags.NonPublic);
        return (IQueryable)(method?.MakeGenericMethod(elementType).Invoke(this, new object[] { expression }) ?? throw new InvalidOperationException("Unable to create query"));
    }

    public IGraphQueryable<TElement> CreateQuery<TElement>(Expression expression)
    {
        if (typeof(INode).IsAssignableFrom(typeof(TElement)))
        {
            var nodeQueryableType = typeof(TestGraphNodeQueryable<>).MakeGenericType(typeof(TElement));
            return (IGraphQueryable<TElement>)Activator.CreateInstance(nodeQueryableType, this, expression)!;
        }

        if (typeof(IRelationship).IsAssignableFrom(typeof(TElement)))
        {
            var relationshipQueryableType = typeof(TestGraphRelationshipQueryable<>).MakeGenericType(typeof(TElement));
            return (IGraphQueryable<TElement>)Activator.CreateInstance(relationshipQueryableType, this, expression)!;
        }

        var queryableType = typeof(TestGraphQueryable<>).MakeGenericType(typeof(TElement));
        return (IGraphQueryable<TElement>)Activator.CreateInstance(queryableType, this, expression)!;
    }

    IQueryable<TElement> IQueryProvider.CreateQuery<TElement>(Expression expression)
        => (IQueryable<TElement>)CreateQuery<TElement>(expression);

    private IQueryable<TElement> CreateUntypedQuery<TElement>(Expression expression)
    {
        return (IQueryable<TElement>)CreateQuery<TElement>(expression);
    }

    public object? Execute(Expression expression) => throw new NotSupportedException("Execute is not supported in test provider");

    public TResult Execute<TResult>(Expression expression) => throw new NotSupportedException("Execute is not supported in test provider");

    public Task<TResult> ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default) => throw new NotSupportedException("ExecuteAsync is not supported in test provider");

    public Task<object?> ExecuteAsync(Expression expression, CancellationToken cancellationToken = default) => throw new NotSupportedException("ExecuteAsync is not supported in test provider");
}

internal sealed class TestGraph : IGraph
{
    public SchemaRegistry SchemaRegistry { get; } = new();
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public Task<IGraphNodeQueryable<DynamicNode>> DynamicNodesAsync(IGraphTransaction? transaction = null) => throw new NotSupportedException();
    public Task<IGraphRelationshipQueryable<DynamicRelationship>> DynamicRelationshipsAsync(IGraphTransaction? transaction = null) => throw new NotSupportedException();
    public Task<DynamicNode> GetDynamicNodeAsync(string id, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<DynamicRelationship> GetDynamicRelationshipAsync(string id, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IGraphNodeQueryable<N>> NodesAsync<N>(IGraphTransaction? transaction = null) where N : INode => throw new NotSupportedException();
    public Task<IGraphRelationshipQueryable<R>> RelationshipsAsync<R>(IGraphTransaction? transaction = null) where R : IRelationship => throw new NotSupportedException();
    public Task<N> GetNodeAsync<N>(string id, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) where N : INode => throw new NotSupportedException();
    public Task<R> GetRelationshipAsync<R>(string id, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) where R : IRelationship => throw new NotSupportedException();
    public Task CreateNodeAsync<N>(N node, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) where N : INode => throw new NotSupportedException();
    public Task CreateRelationshipAsync<R>(R relationship, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) where R : IRelationship => throw new NotSupportedException();
    public Task UpdateNodeAsync<N>(N node, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) where N : INode => throw new NotSupportedException();
    public Task UpdateRelationshipAsync<R>(R relationship, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) where R : IRelationship => throw new NotSupportedException();
    public Task DeleteNodeAsync(string id, bool cascadeDelete = false, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task DeleteRelationshipAsync(string id, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IGraphQueryable<IEntity>> SearchAsync(string query, IGraphTransaction? transaction = null) => throw new NotSupportedException();
    public Task<IGraphNodeQueryable<INode>> SearchNodesAsync(string query, IGraphTransaction? transaction = null) => throw new NotSupportedException();
    public Task<IGraphRelationshipQueryable<IRelationship>> SearchRelationshipsAsync(string query, IGraphTransaction? transaction = null) => throw new NotSupportedException();
    public Task<IGraphNodeQueryable<T>> SearchNodesAsync<T>(string query, IGraphTransaction? transaction = null) where T : INode => throw new NotSupportedException();
    public Task<IGraphRelationshipQueryable<T>> SearchRelationshipsAsync<T>(string query, IGraphTransaction? transaction = null) where T : IRelationship => throw new NotSupportedException();
    public Task RecreateIndexesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IGraphTransaction> GetTransactionAsync() => throw new NotSupportedException();
}

internal class TestGraphQueryable<TElement> : IOrderedGraphQueryable<TElement>
{
    private readonly TestGraphQueryProvider provider;

    public TestGraphQueryable(TestGraphQueryProvider provider)
    {
        this.provider = provider;
        Provider = provider;
        Expression = Expression.Constant(this);
    }

    public TestGraphQueryable(TestGraphQueryProvider provider, Expression expression)
    {
        this.provider = provider;
        Provider = provider;
        Expression = expression;
    }

    public Type ElementType => typeof(TElement);

    public Expression Expression { get; }

    public IGraph Graph => provider.Graph;

    public IGraphQueryProvider Provider { get; }

    IQueryProvider IQueryable.Provider => Provider;

    public IEnumerator<TElement> GetEnumerator() => throw new NotSupportedException();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

internal sealed class TestGraphNodeQueryable<TElement> : TestGraphQueryable<TElement>, IGraphNodeQueryable<TElement>
    where TElement : INode
{
    public TestGraphNodeQueryable(TestGraphQueryProvider provider) : base(provider)
    {
    }

    public TestGraphNodeQueryable(TestGraphQueryProvider provider, Expression expression) : base(provider, expression)
    {
    }
}

internal sealed class TestGraphRelationshipQueryable<TElement> : TestGraphQueryable<TElement>, IGraphRelationshipQueryable<TElement>
    where TElement : IRelationship
{
    public TestGraphRelationshipQueryable(TestGraphQueryProvider provider) : base(provider)
    {
    }

    public TestGraphRelationshipQueryable(TestGraphQueryProvider provider, Expression expression) : base(provider, expression)
    {
    }
}
