using System;
using System.Linq;
using System.Linq.Expressions;
using Cvoya.Graph.Provider.Model;

namespace Cvoya.Graph.Provider.Neo4j.Linq
{
    public class Neo4jQueryProvider : IQueryProvider
    {
        private readonly Neo4jGraphProvider _provider;
        private readonly Type _rootType;
        private readonly Type _elementType;
        private readonly IGraphTransaction? _transaction;

        public Neo4jQueryProvider(Neo4jGraphProvider provider, Type rootType, IGraphTransaction? transaction)
        {
            _provider = provider;
            _rootType = rootType;
            _elementType = rootType;
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
            var elementType = GetElementTypeFromExpression(expression) ?? _elementType;
            var visitor = new Neo4jExpressionVisitor(_provider, _rootType, elementType, _transaction);
            var cypher = visitor.Translate(expression);
            return visitor.ExecuteQuery(cypher, elementType);
        }

        public TResult Execute<TResult>(Expression expression)
        {
            var elementType = typeof(TResult);
            // If TResult is IEnumerable<T>, get T
            if (elementType.IsGenericType && elementType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                elementType = elementType.GetGenericArguments()[0];
            var visitor = new Neo4jExpressionVisitor(_provider, _rootType, elementType, _transaction);
            var cypher = visitor.Translate(expression);
            var result = visitor.ExecuteQuery(cypher, elementType);
            // If TResult is not IEnumerable, return the first (scalar) result
            if (!typeof(System.Collections.IEnumerable).IsAssignableFrom(typeof(TResult)) || typeof(TResult) == typeof(string))
            {
                if (result is System.Collections.IEnumerable enumerable && !(result is string))
                {
                    var enumerator = enumerable.GetEnumerator();
                    if (enumerator.MoveNext())
                        return (TResult)enumerator.Current!;
                    return default!;
                }
                return (TResult)result!;
            }
            return (TResult)result!;
        }

        private static Type? GetElementTypeFromExpression(Expression expression)
        {
            // Try to extract the element type from the expression tree
            var type = expression.Type;
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IQueryable<>))
                return type.GetGenericArguments()[0];
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                return type.GetGenericArguments()[0];
            return null;
        }
    }
}
