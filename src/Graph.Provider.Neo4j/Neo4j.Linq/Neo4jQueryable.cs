using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Cvoya.Graph.Provider.Model;

namespace Cvoya.Graph.Provider.Neo4j.Linq
{
    public class Neo4jQueryable<T> : IQueryable<T>
    {
        public Neo4jQueryable(Neo4jQueryProvider provider, Expression expression)
        {
            Provider = provider;
            Expression = expression;
        }

        public Type ElementType => typeof(T);
        public Expression Expression { get; }
        public IQueryProvider Provider { get; }

        public IEnumerator<T> GetEnumerator()
        {
            var result = Provider.Execute<IEnumerable<T>>(Expression);
            return result.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
