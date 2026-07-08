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

namespace Cvoya.Graph.Model.Neo4j.Translation.Tests.Harness;

using System.Linq.Expressions;
using System.Reflection;

/// <summary>
/// A minimal <see cref="IGraphQueryProvider"/> whose only real job is
/// <see cref="CreateQuery{TElement}"/>: it wraps whatever expression the caller built into a
/// new <see cref="FakeGraphQueryable{T}"/>. This mirrors the shape of the real
/// <c>GraphQueryProvider.CreateQuery&lt;TElement&gt;</c> (node vs. relationship vs. plain
/// queryable) closely enough that the public LINQ extension methods on
/// <c>IGraphQueryable&lt;T&gt;</c> can be used to build expression trees without ever touching
/// a Neo4j driver, transaction, or graph context. Execution remains disabled by default, but
/// tests can opt in to recording terminal expressions without a provider.
/// </summary>
internal sealed class FakeGraphQueryProvider(bool allowExecution = false) : IGraphQueryProvider
{
    public List<Expression> ExecutedExpressions { get; } = [];
    public object? ExecutionResult { get; set; }

    public IGraph Graph => throw new NotSupportedException("FakeGraphQueryProvider never executes.");

    public IQueryable CreateQuery(Expression expression)
    {
        var elementType = GetElementType(expression.Type);
        var method = GetType().GetMethod(nameof(CreateQuery), 1, [typeof(Expression)])!
            .MakeGenericMethod(elementType);
        return (IQueryable)method.Invoke(this, [expression])!;
    }

    public IGraphQueryable<TElement> CreateQuery<TElement>(Expression expression)
    {
        if (typeof(INode).IsAssignableFrom(typeof(TElement)))
        {
            var nodeQueryableType = typeof(FakeGraphNodeQueryable<>).MakeGenericType(typeof(TElement));
            return (IGraphQueryable<TElement>)Activator.CreateInstance(nodeQueryableType, this, expression)!;
        }

        if (typeof(IRelationship).IsAssignableFrom(typeof(TElement)))
        {
            var relQueryableType = typeof(FakeGraphRelationshipQueryable<>).MakeGenericType(typeof(TElement));
            return (IGraphQueryable<TElement>)Activator.CreateInstance(relQueryableType, this, expression)!;
        }

        return new FakeGraphQueryable<TElement>(this, expression);
    }

    IQueryable<TElement> IQueryProvider.CreateQuery<TElement>(Expression expression) => CreateQuery<TElement>(expression);

    public object? Execute(Expression expression) =>
        throw new NotSupportedException("FakeGraphQueryProvider never executes.");

    public TResult Execute<TResult>(Expression expression) =>
        throw new NotSupportedException("FakeGraphQueryProvider never executes.");

    public Task<TResult> ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
    {
        if (!allowExecution)
        {
            throw new NotSupportedException("FakeGraphQueryProvider never executes.");
        }

        ExecutedExpressions.Add(expression);
        return Task.FromResult((TResult)ExecutionResult!);
    }

    public Task<object?> ExecuteAsync(Expression expression, CancellationToken cancellationToken = default)
    {
        if (!allowExecution)
        {
            throw new NotSupportedException("FakeGraphQueryProvider never executes.");
        }

        ExecutedExpressions.Add(expression);
        return Task.FromResult(ExecutionResult);
    }

    private static Type GetElementType(Type queryType)
    {
        var enumerableInterface = queryType.GetInterfaces()
            .Prepend(queryType)
            .FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        return enumerableInterface?.GetGenericArguments()[0]
            ?? throw new InvalidOperationException($"Could not determine element type for {queryType}.");
    }
}
