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

internal sealed class GraphNodeQueryable<TNode> :
    GraphQueryableBase<TNode>,
    IGraphNodeQueryable<TNode>,
    IOrderedGraphNodeQueryable<TNode>
    where TNode : INode
{
    private string? _label;

    public GraphNodeQueryable(GraphQueryProvider provider, GraphContext graphContext)
        : this(provider, graphContext, expression: null)
    {
    }

    public GraphNodeQueryable(GraphQueryProvider provider, GraphContext graphContext, Expression? expression)
        : base(typeof(TNode), provider, graphContext, expression ?? Expression.Constant(null, typeof(IGraphNodeQueryable<TNode>)))
    {
        // Extract label from TNode type if it has a NodeAttribute
        var nodeAttribute = typeof(TNode).GetCustomAttribute<NodeAttribute>();
        _label = nodeAttribute?.Label;
    }

    #region IGraphNodeQueryable Implementation

    public string? Label => _label;

    public IGraphQueryable<IGraphPathSegment<TNode, TRel, TTarget>> PathSegments<TRel, TTarget>()
        where TRel : IRelationship
        where TTarget : INode
    {
        var methodCall = Expression.Call(
            Expression, // Pass the current instance, not null
            GetGenericMethod(nameof(PathSegments), typeof(TRel), typeof(TTarget)));

        return Provider.CreatePathSegmentQuery<TNode, TRel, TTarget>(methodCall);
    }
    #endregion

    private static MethodInfo GetGenericMethod(string methodName, params Type[] typeArguments)
    {
        var method = typeof(IGraphNodeQueryable<TNode>)
            .GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Method {methodName} not found");

        return typeArguments.Length > 0 ? method.MakeGenericMethod(typeArguments) : method;
    }
}