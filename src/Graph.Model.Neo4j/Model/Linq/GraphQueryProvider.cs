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

using System.Linq.Expressions;
using Cvoya.Graph.Model.Neo4j.Linq;
using Microsoft.Extensions.Logging;

namespace Cvoya.Graph.Model.Neo4j;

/// <summary>
/// Neo4j implementation of the IGraph interface using a modular design with IGraphQueryable support.
/// </summary>
internal class GraphQueryProvider : IGraphQueryProvider
{
    private readonly GraphContext _context;
    private readonly CypherEngine _cypherEngine;


    public GraphQueryProvider(GraphContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _cypherEngine = new CypherEngine(_context);

        _context.Logger.LogInformation($"Neo4jGraphProvider initialized for database '{_context.DatabaseName}'");
    }

    /// <inheritdoc/>
    public IGraph Graph => _context.Graph;

    /// <inheritdoc/>
    public IGraphQueryable<TElement> CreateQuery<TElement>(Expression expression) where TElement : class
    {
        var rootExpression = GetRootGraphQueryable(expression) ??
            throw new ArgumentException("Expression must be a valid graph query expression", nameof(expression));

        return new GraphQueryable<TElement>(
            this,
            rootExpression.GraphContext,
            rootExpression.QueryContext,
            expression,
            rootExpression.Transaction
        );
    }

    /// <inheritdoc/>
    public IQueryable CreateQuery(Expression expression)
    {
        var elementType = expression.Type.GetGenericArguments().FirstOrDefault() ??
            throw new ArgumentException("Expression must be a valid graph query expression", nameof(expression));
        var rootExpression = GetRootGraphQueryable(expression) ??
            throw new ArgumentException("Expression must be a valid graph query expression", nameof(expression));

        var queryableType = typeof(GraphQueryable<>).MakeGenericType(elementType);
        var obj = Activator.CreateInstance(
            queryableType,
            rootExpression.Graph,
            this,
            rootExpression.GraphContext,
            rootExpression.QueryContext,
            expression,
            rootExpression.Transaction
        ) ?? throw new InvalidOperationException($"Could not create queryable type for {elementType}");

        return (IQueryable)obj;
    }

    /// <inheritdoc/>
    public IGraphQueryable<TTarget> CreateTraversalQuery<TRelationship, TTarget>(Expression sourceExpression)
        where TRelationship : class, Model.IRelationship, new()
        where TTarget : class, Model.INode, new()
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public IGraphQueryable<IGraphPath<TSource, TRelationship, TTarget>> CreatePathQuery<TSource, TRelationship, TTarget>(Expression sourceExpression)
        where TSource : class, Model.INode, new()
        where TRelationship : class, Model.IRelationship, new()
        where TTarget : class, Model.INode, new()
    {
        throw new NotImplementedException();
    }

    public object? Execute(Expression expression)
    {
        return ExecuteAsync<object?>(expression).GetAwaiter().GetResult();
    }

    public TResult Execute<TResult>(Expression expression)
    {
        return ExecuteAsync<TResult>(expression).GetAwaiter().GetResult();
    }

    IQueryable<TElement> IQueryProvider.CreateQuery<TElement>(Expression expression)
    {
        throw new NotImplementedException();
    }

    IQueryable IQueryProvider.CreateQuery(Expression expression)
    {
        throw new NotImplementedException();
    }

    private async Task<T> ExecuteAsync<T>(Expression expression)
    {
        var elementType = GetElementTypeFromExpression(expression) ??
            throw new ArgumentException("Expression must be a valid graph query expression", nameof(expression));

        var rootExpression = GetRootGraphQueryable(expression) ??
            throw new ArgumentException("Expression must be a valid graph query expression", nameof(expression));

        var cypher = await _cypherEngine.ExpressionToCypherVisitor(expression, rootExpression.QueryContext);

        var result = await _cypherEngine.ExecuteAsync<T>(
            cypher,
            rootExpression.QueryContext,
            rootExpression.Transaction
        );

        return result;
    }

    private static Type? GetElementTypeFromExpression(Expression expression) => expression.Type switch
    {
        { IsGenericType: true } type when type.GetGenericTypeDefinition() == typeof(IQueryable<>)
            => type.GetGenericArguments()[0],
        { IsGenericType: true } type when type.GetGenericTypeDefinition() == typeof(IEnumerable<>)
            => type.GetGenericArguments()[0],
        _ => null
    };

    private GraphQueryable? GetRootGraphQueryable(Expression expression)
    {
        if (expression is ConstantExpression ce && ce.Value is GraphQueryable rootQueryable)
        {
            return rootQueryable;
        }

        if (expression is MethodCallExpression mce && mce.Arguments.Count > 0)
        {
            // Recursively check the first argument
            return GetRootGraphQueryable(mce.Arguments[0]);
        }

        return null;
    }
}
