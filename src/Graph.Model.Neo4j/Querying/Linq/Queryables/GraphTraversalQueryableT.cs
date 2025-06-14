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

namespace Cvoya.Graph.Model.Neo4j.Querying.Linq.Queryables;

using System.Linq.Expressions;
using System.Reflection;
using Cvoya.Graph.Model.Neo4j.Core;
using Cvoya.Graph.Model.Neo4j.Querying.Linq.Providers;

internal sealed class GraphTraversalQueryable<TSource, TRel, TTarget> : GraphQueryableBase<TTarget>,
    IGraphTraversalQueryable<TSource, TRel, TTarget>
    where TSource : INode
    where TRel : IRelationship
    where TTarget : INode
{
    private TraversalDirection _direction = TraversalDirection.Outgoing;
    private int _minDepth = 1;
    private int _maxDepth = 1;

    public GraphTraversalQueryable(GraphQueryProvider provider, GraphContext context, Expression expression)
        : base(typeof(TTarget), provider, context, expression)
    {
    }

    #region IGraphTraversalQueryable Implementation

    public IGraphTraversalQueryable<TSource, TRel, TTarget> InDirection(TraversalDirection direction)
    {
        _direction = direction;

        var methodCall = Expression.Call(
            null,
            GetMethod(nameof(InDirection)),
            Expression,
            Expression.Constant(direction));

        var newQueryable = new GraphTraversalQueryable<TSource, TRel, TTarget>(Provider, Context, methodCall)
        {
            _direction = direction,
            _minDepth = _minDepth,
            _maxDepth = _maxDepth,
        };

        if (Transaction != null)
            newQueryable.WithTransaction(Transaction);

        return newQueryable;
    }

    public IGraphTraversalQueryable<TSource, TRel, TTarget> WithDepth(int maxDepth)
    {
        return WithDepth(1, maxDepth);
    }

    public IGraphTraversalQueryable<TSource, TRel, TTarget> WithDepth(int minDepth, int maxDepth)
    {
        if (minDepth < 0)
            throw new ArgumentOutOfRangeException(nameof(minDepth), "Minimum depth must be non-negative");
        if (maxDepth < minDepth)
            throw new ArgumentOutOfRangeException(nameof(maxDepth), "Maximum depth must be greater than or equal to minimum depth");

        _minDepth = minDepth;
        _maxDepth = maxDepth;

        var methodCall = Expression.Call(
            null,
            GetMethod(nameof(WithDepth), 2),
            Expression,
            Expression.Constant(minDepth),
            Expression.Constant(maxDepth));

        var newQueryable = new GraphTraversalQueryable<TSource, TRel, TTarget>(Provider, Context, methodCall)
        {
            _direction = _direction,
            _minDepth = minDepth,
            _maxDepth = maxDepth,
        };

        if (Transaction != null)
            newQueryable.WithTransaction(Transaction);

        return newQueryable;
    }

    public IGraphTraversalQueryable<TSource, TNextRel, TNextTarget> ThenTraverse<TNextRel, TNextTarget>()
        where TNextRel : IRelationship
        where TNextTarget : INode
    {
        var methodCall = Expression.Call(
            null,
            GetGenericMethod(nameof(ThenTraverse), typeof(TNextRel), typeof(TNextTarget)),
            Expression);

        return Provider.CreateTraversalQuery<TSource, TNextRel, TNextTarget>(methodCall);
    }

    public IGraphNodeQueryable<TTarget> To()
    {
        var methodCall = Expression.Call(
            null,
            GetMethod(nameof(To)),
            Expression);

        return Provider.CreateNodeQuery<TTarget>(methodCall);
    }

    public IGraphRelationshipQueryable<TRel> Relationships()
    {
        var methodCall = Expression.Call(
            null,
            GetMethod(nameof(Relationships)),
            Expression);

        return Provider.CreateRelationshipQuery<TRel>(methodCall);
    }

    public IGraphQueryable<IGraphPathSegment<TSource, TRel, TTarget>> PathSegments()
    {
        var methodCall = Expression.Call(
            null,
            GetMethod(nameof(PathSegments)),
            Expression);

        return Provider.CreatePathSegmentQuery<TSource, TRel, TTarget>(methodCall);
    }

    #endregion

    private static MethodInfo GetMethod(string methodName, int parameterCount = 0)
    {
        var methods = typeof(IGraphTraversalQueryable<TSource, TRel, TTarget>)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == methodName && m.GetParameters().Length == parameterCount);

        return methods.FirstOrDefault()
            ?? throw new InvalidOperationException($"Method {methodName} with {parameterCount} parameters not found");
    }

    private static MethodInfo GetGenericMethod(string methodName, params Type[] typeArguments)
    {
        var method = typeof(IGraphTraversalQueryable<TSource, TRel, TTarget>)
            .GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Method {methodName} not found");

        return method.MakeGenericMethod(typeArguments);
    }
}