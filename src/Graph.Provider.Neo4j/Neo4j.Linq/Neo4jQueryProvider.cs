using System;
using System.Linq;
using System.Linq.Expressions;
using Cvoya.Graph.Provider.Model;

namespace Cvoya.Graph.Provider.Neo4j.Linq
{
    public class Neo4jQueryProvider : IQueryProvider
    {
        private readonly Neo4jGraphProvider _provider;
        private readonly Type _elementType;
        private readonly IGraphTransaction? _transaction;

        public Neo4jQueryProvider(Neo4jGraphProvider provider, Type elementType, IGraphTransaction? transaction)
        {
            _provider = provider;
            _elementType = elementType;
            _transaction = transaction;
        }

        public IQueryable CreateQuery(Expression expression)
        {
            var elementType = expression.Type.GetGenericArguments().FirstOrDefault() ?? _elementType;
            var queryableType = typeof(Neo4jQueryable<>).MakeGenericType(elementType);
            return (IQueryable)Activator.CreateInstance(queryableType, this, expression)!;
        }

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            return new Neo4jQueryable<TElement>(this, expression);
        }

        public object? Execute(Expression expression)
        {
            var elementType = expression.Type.IsGenericType && expression.Type.GetGenericTypeDefinition() == typeof(IEnumerable<>)
                ? expression.Type.GetGenericArguments()[0]
                : _elementType;
            var visitor = new Neo4jExpressionVisitor(_provider, elementType, _transaction);
            var cypher = visitor.Translate(expression);
            return visitor.ExecuteQuery(cypher, elementType);
        }

        public TResult Execute<TResult>(Expression expression)
        {
            var result = Execute(expression);
            return (TResult)result!;
        }
    }
}
