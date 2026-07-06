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

using System.Collections;
using System.Linq.Expressions;
using Cvoya.Graph.Model.Neo4j.Core;
using Cvoya.Graph.Model.Neo4j.Querying.Linq.Providers;


internal sealed class GraphNodeQueryable<TNode> :
    GraphQueryableBase<TNode>,
    IGraphQueryable<TNode>,
    IOrderedGraphQueryable<TNode>
    where TNode : class, INode
{
    public GraphNodeQueryable(GraphQueryProvider provider, GraphTransaction? transaction, GraphContext graphContext)
        : base(typeof(TNode), provider, graphContext, transaction, CreateRootExpression())
    {
    }

    public GraphNodeQueryable(
        GraphQueryProvider provider,
        GraphContext graphContext,
        GraphTransaction? transaction,
        Expression expression)
        : base(typeof(TNode), provider, graphContext, transaction, expression)
    {
    }

    // For the root queryable, we'll create a placeholder that gets replaced during LINQ processing.
    // The placeholder is typed as the (obsolete, internal-use-only) IGraphNodeQueryable<T> marker
    // interface purely so CypherQueryVisitor.VisitConstant can distinguish a node root from a
    // relationship root via a pattern match (`is IGraphNodeQueryable`) - this is an internal
    // implementation detail, unrelated to the public surface deprecation of that interface.
#pragma warning disable CS0618 // internal use of the obsolete node/relationship marker interface - see comment above.
    private static Expression CreateRootExpression()
    {
        return Expression.Constant(CreatePlaceholderQueryable(), typeof(IGraphNodeQueryable<TNode>));
    }

    private static IGraphNodeQueryable<TNode> CreatePlaceholderQueryable()
    {
        // This is a minimal placeholder that provides the element type information
        return new PlaceholderNodeQueryable<TNode>();
    }

    private sealed class PlaceholderNodeQueryable<T> : IGraphNodeQueryable<T> where T : class, INode
    {
        public Type ElementType => typeof(T);
        public Expression Expression => throw new NotSupportedException("Placeholder queryable");
        public IGraphQueryProvider Provider => throw new NotSupportedException("Placeholder queryable");
        public IGraph Graph => throw new NotSupportedException("Placeholder queryable");
        IQueryProvider IQueryable.Provider => throw new NotSupportedException("Placeholder queryable");
        public IEnumerator<T> GetEnumerator() => throw new NotSupportedException("Placeholder queryable");
        IEnumerator IEnumerable.GetEnumerator() => throw new NotSupportedException("Placeholder queryable");
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) => throw new NotSupportedException("Placeholder queryable");
    }
#pragma warning restore CS0618
}
