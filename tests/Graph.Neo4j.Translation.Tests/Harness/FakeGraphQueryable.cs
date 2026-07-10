// Copyright 2025 Savas Parastatidis
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      https://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace Cvoya.Graph.Neo4j.Translation.Tests.Harness;

using System.Collections;
using System.Linq.Expressions;
using Cvoya.Graph.Neo4j.Querying.Linq.Queryables;

/// <summary>
/// A minimal <see cref="IGraphQueryable{T}"/> that carries only an expression tree and a
/// <see cref="FakeGraphQueryProvider"/>. It never executes: it exists purely so that the
/// public <c>GraphQueryableExtensions</c>/<c>GraphTraversalExtensions</c> LINQ surface can be
/// used to build expression trees for feeding into <c>CypherQueryVisitor</c> directly, without
/// a live Neo4j driver or transaction. Also implements <see cref="IOrderedGraphQueryable{T}"/>
/// (a marker interface with no extra members) since <c>GraphQueryableExtensions.OrderBy</c>/
/// <c>ThenBy</c> cast the provider's <c>CreateQuery&lt;T&gt;</c> result to it.
/// </summary>
internal class FakeGraphQueryable<T> : IGraphQueryable<T>, IOrderedGraphQueryable<T>, IGraphQueryableKindProvider
{
    public FakeGraphQueryable(
        FakeGraphQueryProvider provider,
        Expression expression,
        GraphQueryableKind queryableKind = GraphQueryableKind.General)
    {
        Provider = provider;
        Expression = expression;
        QueryableKind = queryableKind;
    }

    public Type ElementType => typeof(T);

    public Expression Expression { get; }

    public FakeGraphQueryProvider Provider { get; }

    public GraphQueryableKind QueryableKind { get; }

    IGraphQueryProvider IGraphQueryable<T>.Provider => Provider;

    IQueryProvider IQueryable.Provider => Provider;

    public IGraph Graph => throw new NotSupportedException("FakeGraphQueryable never executes.");

    public IEnumerator<T> GetEnumerator() => throw new NotSupportedException("FakeGraphQueryable never executes.");

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("FakeGraphQueryable never executes.");

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// Node-flavored counterpart of <see cref="FakeGraphQueryable{T}"/>.
/// </summary>
internal sealed class FakeGraphNodeQueryable<T> : FakeGraphQueryable<T>
    where T : class, INode
{
    public FakeGraphNodeQueryable(FakeGraphQueryProvider provider, Expression expression)
        : base(provider, expression, GraphQueryableKind.Node)
    {
    }
}

/// <summary>
/// Relationship-flavored counterpart of <see cref="FakeGraphQueryable{T}"/>.
/// </summary>
internal sealed class FakeGraphRelationshipQueryable<T> : FakeGraphQueryable<T>
    where T : class, IRelationship
{
    public FakeGraphRelationshipQueryable(FakeGraphQueryProvider provider, Expression expression)
        : base(provider, expression, GraphQueryableKind.Relationship)
    {
    }
}
