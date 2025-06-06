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

namespace Cvoya.Graph.Model.Neo4j.Linq;

internal class GraphQueryable<T> : GraphQueryable, IGraphQueryable<T>, IOrderedQueryable<T> where T : class
{
    internal GraphQueryable(
        GraphQueryProvider provider,
        GraphContext graphContext,
        GraphQueryContext queryContext,
        Expression? expression = null,
        GraphTransaction? transaction = null) :
        base(typeof(T), provider, graphContext, queryContext, expression, transaction)
    {
    }

    IGraph IGraphQueryable<T>.Graph => GraphContext.Graph;

    IGraphQueryProvider IGraphQueryable<T>.Provider => Provider;

    IQueryProvider IQueryable.Provider => Provider;

    public IEnumerator<T> GetEnumerator()
    {
        var result = Provider.Execute<IEnumerable<T>>(Expression);
        return result.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

}

internal class GraphQueryable
{
    public GraphQueryable(
        Type elementType,
        GraphQueryProvider provider,
        GraphContext graphContext,
        GraphQueryContext queryContext,
        Expression? expression = null,
        GraphTransaction? transaction = null
    )
    {
        ElementType = elementType;
        Provider = provider;
        QueryContext = queryContext;
        GraphContext = graphContext;
        Expression = expression ?? Expression.Constant(this);
        Transaction = transaction;
    }

    public Neo4jGraph Graph => GraphContext.Graph;
    public Type ElementType { get; }
    public GraphQueryProvider Provider { get; }
    public Expression Expression { get; }
    public GraphContext GraphContext { get; }
    public GraphQueryContext QueryContext { get; }
    public GraphTransaction? Transaction { get; }
}
