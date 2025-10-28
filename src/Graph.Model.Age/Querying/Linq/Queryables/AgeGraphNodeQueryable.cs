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

namespace Cvoya.Graph.Model.Age.Querying.Linq.Queryables;

using System.Collections;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Cvoya.Graph.Model.Age.Core;
using Cvoya.Graph.Model.Age.Querying.Linq.Providers;

/// <summary>
/// Queryable for AGE graph nodes.
/// </summary>
internal sealed class AgeGraphNodeQueryable<TNode> :
    AgeGraphQueryableBase<TNode>,
    IGraphNodeQueryable<TNode>,
    IOrderedGraphNodeQueryable<TNode>,
    IOrderedGraphQueryable<TNode>
    where TNode : INode
{
    public AgeGraphNodeQueryable(AgeGraphQueryProvider provider, AgeGraphContext graphContext)
        : base(typeof(TNode), provider, graphContext, CreateRootExpression())
    {
    }

    public AgeGraphNodeQueryable(
        AgeGraphQueryProvider provider,
        AgeGraphContext graphContext,
        Expression expression)
        : base(typeof(TNode), provider, graphContext, expression)
    {
    }

    private static Expression CreateRootExpression()
    {
        // Create a constant expression representing the root queryable
        return Expression.Constant(CreatePlaceholderQueryable(), typeof(IGraphNodeQueryable<TNode>));
    }

    private static IGraphNodeQueryable<TNode> CreatePlaceholderQueryable()
    {
        return new PlaceholderNodeQueryable<TNode>();
    }

    private sealed class PlaceholderNodeQueryable<T> : IGraphNodeQueryable<T> where T : INode
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
}
