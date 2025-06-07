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

using System.Collections;
using System.Linq.Expressions;
using Cvoya.Graph.Model;

namespace Cvoya.Graph.Model.Neo4j.Linq;

/// <summary>
/// Simple queryable for projections to value types that don't need the full GraphQueryable functionality
/// </summary>
internal class ProjectionQueryable<T> : IQueryable<T>
{
    private readonly GraphQueryProvider _provider;
    private readonly Expression _expression;
    private readonly IGraphTransaction? _transaction;

    public ProjectionQueryable(GraphQueryProvider provider, Expression expression, IGraphTransaction? transaction)
    {
        _provider = provider;
        _expression = expression;
        _transaction = transaction;
    }

    public Type ElementType => typeof(T);
    public Expression Expression => _expression;
    public IQueryProvider Provider => _provider;

    public IEnumerator<T> GetEnumerator()
    {
        var result = _provider.Execute<IEnumerable<T>>(_expression);
        return result?.GetEnumerator() ?? Enumerable.Empty<T>().GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}