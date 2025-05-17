using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Cvoya.Graph.Provider.Model;

namespace Cvoya.Graph.Client.Neo4j
{
    public static class SubqueryLinqExtensions
    {
        // Subquery stub for future Cypher CALL { ... } support
        public static IQueryable<T> Subquery<T>(this IQueryable<T> source, Expression<Func<IQueryable<T>, IQueryable<T>>> subquery)
        {
            // TODO: Implement Cypher subquery translation
            throw new NotImplementedException("Subquery is not yet implemented.");
        }

        // Execute a Cypher subquery using CALL { ... } and return results
        public static async Task<IEnumerable<dynamic>> Subquery(this IGraphProvider client, string subquery, object? parameters = null)
        {
            // Wrap the subquery in CALL { ... } RETURN ...
            var cypher = $"CALL {{ {subquery} }} RETURN *";
            return await client.ExecuteCypher(cypher, parameters);
        }
    }
}
