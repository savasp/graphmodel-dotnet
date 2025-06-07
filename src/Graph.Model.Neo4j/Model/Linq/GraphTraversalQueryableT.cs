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

internal class GraphTraversalQueryable<T, TRel, TTarget> :
    GraphQueryable<TTarget>,
    IGraphTraversalQueryable<T, TRel, TTarget>
    where T : INode
    where TRel : IRelationship
    where TTarget : INode
{
    internal GraphTraversalQueryable(
        GraphQueryProvider provider,
        GraphContext graphContext,
        GraphQueryContext queryContext,
        Expression? traversalExpression = null,
        Expression? sourceExpression = null,
        GraphTransaction? transaction = null) :
        base(provider, graphContext, queryContext, sourceExpression, transaction)
    {
    }

    IGraph IGraphQueryable<TTarget>.Graph => Graph;

    IGraphQueryProvider IGraphQueryable<TTarget>.Provider => Provider;

    public IGraphQueryable<TRel> Relationships()
    {
        throw new NotImplementedException();
    }

    public IGraphTraversalQueryable<T, TRel, TTarget> WithDepth(int maxDepth)
    {
        throw new NotImplementedException();
    }

    public IGraphTraversalQueryable<T, TRel, TTarget> WithDepth(int minDepth, int maxDepth)
    {
        throw new NotImplementedException();
    }

    public IGraphTraversalQueryable<T, TRel, TTarget> WithOptions(TraversalOptions options)
    {
        throw new NotImplementedException();
    }

    public IGraphTraversalQueryable<T, TNextRel, TNextTarget> ThenTraverse<TNextRel, TNextTarget>()
        where TNextRel : IRelationship
        where TNextTarget : INode
    {
        throw new NotImplementedException();
    }

    public IGraphQueryable<TNewTarget> To<TNewTarget>()
        where TNewTarget : INode
    {
        throw new NotImplementedException();
    }

    public IGraphTraversalQueryable<T, TRel, TTarget> InDirection(TraversalDirection direction)
    {
        throw new NotImplementedException();
    }
}