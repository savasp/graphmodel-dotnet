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
using Cvoya.Graph.Model;
using Cvoya.Graph.Model.Age.Core;
using Cvoya.Graph.Model.Age.Querying.Linq.Providers;

internal abstract class AgeGraphQueryableBase<TElement> : IGraphQueryable<TElement>, IAsyncDisposable
{
    protected AgeGraphQueryableBase(
        Type elementType,
        AgeGraphQueryProvider provider,
        AgeGraphContext graphContext,
        Expression expression)
    {
        ElementType = elementType ?? throw new ArgumentNullException(nameof(elementType));
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        GraphContext = graphContext ?? throw new ArgumentNullException(nameof(graphContext));
        Expression = expression ?? throw new ArgumentNullException(nameof(expression));
    }

    public Type ElementType { get; }

    public Expression Expression { get; }

    public IGraphQueryProvider Provider { get; }

    public IGraph Graph => GraphContext.Graph;

    protected AgeGraphContext GraphContext { get; }

    IQueryProvider IQueryable.Provider => Provider;

    public IEnumerator<TElement> GetEnumerator()
    {
        return Provider.Execute<IEnumerable<TElement>>(Expression).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
