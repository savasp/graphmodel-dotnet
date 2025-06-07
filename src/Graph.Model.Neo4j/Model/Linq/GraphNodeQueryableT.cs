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

namespace Cvoya.Graph.Model.Neo4j.Linq;

internal class GraphNodeQueryable<T> : GraphQueryable<T>, IGraphNodeQueryable<T>
    where T : INode
{
    internal GraphNodeQueryable(
        GraphQueryProvider provider,
        GraphContext graphContext,
        GraphQueryContext queryContext,
        Expression? expression = null,
        GraphTransaction? transaction = null) :
        base(provider, graphContext, queryContext, expression, transaction)
    {
    }

    public IGraphQueryable<IGraphPathSegment<T, TRel, TTarget>> PathSegments<TRel, TTarget>()
        where TRel : IRelationship
        where TTarget : INode
    {
        throw new NotImplementedException();
    }

    public IGraphRelationshipQueryable<TRel> Relationships<TRel>() where TRel : IRelationship
    {
        throw new NotImplementedException();
    }

    public IGraphTraversalQueryable<T, TRel, TTarget> Relationships<TRel, TTarget>()
        where TRel : IRelationship
        where TTarget : INode
    {
        throw new NotImplementedException();
    }

    public Task<T> SingleAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<List<T>> ToListAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public IGraphTraversalQueryable<T, TRel, TTarget> Traverse<TRel, TTarget>()
        where TRel : IRelationship
        where TTarget : INode
    {
        throw new NotImplementedException();
    }

    public IGraphNodeQueryable<T> WithTransaction(GraphTransaction transaction)
    {
        return new GraphNodeQueryable<T>(Provider, GraphContext, QueryContext, Expression, transaction);
    }
}