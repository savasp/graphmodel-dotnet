// Copyright CVOYA. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.
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

namespace Cvoya.Graph.Neo4j.Querying.Linq.Queryables;

using System.Collections;
using System.Linq.Expressions;
using Cvoya.Graph.Neo4j.Core;
using Cvoya.Graph.Neo4j.Querying.Linq.Providers;


internal sealed class GraphRelationshipQueryable<TRel> : GraphQueryableBase<TRel>,
    IGraphQueryable<TRel>,
    IOrderedGraphQueryable<TRel>
    where TRel : class, IRelationship
{
    public GraphRelationshipQueryable(GraphQueryProvider provider, GraphContext graphContext, GraphTransaction? transaction)
        : base(typeof(TRel), provider, graphContext, transaction, CreateRootExpression(), GraphQueryableKind.Relationship)
    {
    }

    public GraphRelationshipQueryable(GraphQueryProvider provider, GraphContext graphContext, GraphTransaction? transaction, Expression expression)
        : base(typeof(TRel), provider, graphContext, transaction, expression, GraphQueryableKind.Relationship)
    {
    }

    // For the root queryable, we'll create a placeholder that gets replaced during LINQ processing.
    private static Expression CreateRootExpression()
    {
        return Expression.Constant(CreatePlaceholderQueryable(), typeof(IGraphQueryable<TRel>));
    }

    private static IGraphQueryable<TRel> CreatePlaceholderQueryable()
    {
        // This is a minimal placeholder that provides the element type information
        return new PlaceholderRelationshipQueryable<TRel>();
    }

    private sealed class PlaceholderRelationshipQueryable<T> : IGraphQueryable<T>, IGraphQueryableKindProvider where T : class, IRelationship
    {
        public Type ElementType => typeof(T);
        public Expression Expression => throw new NotSupportedException("Placeholder queryable");
        public IGraphQueryProvider Provider => throw new NotSupportedException("Placeholder queryable");
        public GraphQueryableKind QueryableKind => GraphQueryableKind.Relationship;
        public IGraph Graph => throw new NotSupportedException("Placeholder queryable");
        IQueryProvider IQueryable.Provider => throw new NotSupportedException("Placeholder queryable");
        public IEnumerator<T> GetEnumerator() => throw new NotSupportedException("Placeholder queryable");
        IEnumerator IEnumerable.GetEnumerator() => throw new NotSupportedException("Placeholder queryable");
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) => throw new NotSupportedException("Placeholder queryable");
    }
}
