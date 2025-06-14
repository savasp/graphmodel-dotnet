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

internal sealed class GraphRelationshipQueryable<TRel> : GraphQueryableBase<TRel>,
    IGraphRelationshipQueryable<TRel>,
    IOrderedGraphRelationshipQueryable<TRel>
    where TRel : IRelationship
{
    private string? _relationshipType;

    public GraphRelationshipQueryable(GraphQueryProvider provider, GraphContext graphContext)
        : this(provider, graphContext, expression: null)
    {
    }

    public GraphRelationshipQueryable(GraphQueryProvider provider, GraphContext graphContext, Expression? expression)
        : base(typeof(TRel), provider, graphContext, expression ?? Expression.Constant(null, typeof(IGraphRelationshipQueryable<TRel>)))
    {
        // Extract relationship type from TRel type if it has a RelationshipAttribute
        _relationshipType = Labels.GetLabelFromType(typeof(TRel));
    }

    #region IGraphRelationshipQueryable Implementation

    public string? RelationshipType => _relationshipType;

    public IGraphTraversalQueryable<TSource, TRel, TTarget> Traverse<TSource, TTarget>()
        where TSource : INode
        where TTarget : INode
    {
        var methodCall = Expression.Call(
            null,
            GetGenericMethod(nameof(Traverse), typeof(TSource), typeof(TTarget)),
            Expression);

        return Provider.CreateTraversalQuery<TSource, TRel, TTarget>(methodCall);
    }

    #endregion

    private static MethodInfo GetGenericMethod(string methodName, params Type[] typeArguments)
    {
        var method = typeof(IGraphRelationshipQueryable<TRel>)
            .GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Method {methodName} not found");

        return typeArguments.Length > 0 ? method.MakeGenericMethod(typeArguments) : method;
    }
}