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

namespace Cvoya.Graph.Client.Neo4j;

// TODO: Change this to an IAsyncQueryProvider
internal class Neo4jQueryable<T> : IQueryable<T>, IOrderedQueryable<T>
{
    public Expression Expression { get; }
    public Type ElementType => typeof(T);
    public IQueryProvider Provider { get; }

    public Neo4jQueryable(IQueryProvider provider, Expression expression)
    {
        Provider = provider;
        Expression = expression;
    }

    public IEnumerator<T> GetEnumerator()
    {
        return ((IEnumerable<T>)Provider.Execute(Expression)!).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
