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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cvoya.Graph.Model.Neo4j.Linq;

internal class GraphRelationshipQueryable<TRel> : GraphQueryable<TRel>, IGraphRelationshipQueryable<TRel>
    where TRel : IRelationship
{
    private readonly ILogger? logger;

    internal GraphRelationshipQueryable(
        GraphQueryProvider provider,
        GraphContext graphContext,
        GraphQueryContext queryContext,
        Expression? expression = null,
        GraphTransaction? transaction = null) :
        base(provider, graphContext, queryContext, expression, transaction)
    {
        logger = graphContext.LoggerFactory?.CreateLogger<GraphRelationshipQueryable<TRel>>() ?? NullLogger<GraphRelationshipQueryable<TRel>>.Instance;
    }

    public string RelationshipType => Labels.GetLabelFromType(typeof(TRel));

    public IGraphTraversalQueryable<TSource, TRel, TTarget> Traverse<TSource, TTarget>()
        where TSource : INode
        where TTarget : INode
    {
        var traverseMethod = typeof(IGraphRelationshipQueryable<TRel>)
            .GetMethod(nameof(Traverse))!
            .MakeGenericMethod(typeof(TSource), typeof(TTarget));

        var traversalExpression = Expression.Call(
            Expression,
            traverseMethod);

        return Provider.CreateTraversalQuery<TSource, TRel, TTarget>(traversalExpression);
    }

    public IGraphRelationshipQueryable<TRel> WithTransaction(GraphTransaction transaction)
    {
        return new GraphRelationshipQueryable<TRel>(Provider, GraphContext, QueryContext, Expression, transaction);
    }
}