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
using Cvoya.Graph.Model.Age.Core;
using Cvoya.Graph.Model.Age.Querying.Linq.Providers;

/// <summary>
/// Queryable for AGE graph relationships.
/// </summary>
internal sealed class AgeGraphRelationshipQueryable<TRelationship> :
    AgeGraphQueryableBase<TRelationship>,
    IGraphRelationshipQueryable<TRelationship>,
    IOrderedGraphRelationshipQueryable<TRelationship>
    where TRelationship : IRelationship
{
    public AgeGraphRelationshipQueryable(AgeGraphQueryProvider provider, AgeGraphTransaction transaction, AgeGraphContext graphContext)
        : base(typeof(TRelationship), provider, graphContext, transaction, CreateRootExpression())
    {
    }

    public AgeGraphRelationshipQueryable(
        AgeGraphQueryProvider provider,
        AgeGraphContext graphContext,
        AgeGraphTransaction transaction,
        Expression expression)
        : base(typeof(TRelationship), provider, graphContext, transaction, expression)
    {
    }

    private static Expression CreateRootExpression()
    {
        return Expression.Constant(CreatePlaceholderQueryable(), typeof(IGraphRelationshipQueryable<TRelationship>));
    }

    private static IGraphRelationshipQueryable<TRelationship> CreatePlaceholderQueryable()
    {
        return new PlaceholderRelationshipQueryable<TRelationship>();
    }

    private sealed class PlaceholderRelationshipQueryable<T> : IGraphRelationshipQueryable<T> where T : IRelationship
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
