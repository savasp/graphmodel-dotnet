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
using Neo4j.Driver;

namespace Cvoya.Graph.Provider.Neo4j.Linq;

public class Neo4jQueryable<T> : IOrderedQueryable<T>
{
    private readonly GraphOperationOptions options;

    public Neo4jQueryable(Neo4jQueryProvider provider, GraphOperationOptions options, IGraphTransaction? transaction = null)
    {
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        Expression = Expression.Constant(this);
        Transaction = transaction;
        this.options = options;
    }

    internal Neo4jQueryable(Neo4jQueryProvider provider, GraphOperationOptions options, Expression expression, IGraphTransaction? transaction = null)
    {
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        Transaction = transaction;
        this.options = options;
    }

    public GraphOperationOptions Options => options;

    public Type ElementType => typeof(T);
    public Expression Expression { get; }
    public IQueryProvider Provider { get; }
    public IGraphTransaction? Transaction { get; }

    public IEnumerator<T> GetEnumerator()
    {
        var result = Provider.Execute<IEnumerable<T>>(Expression);
        return result.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
