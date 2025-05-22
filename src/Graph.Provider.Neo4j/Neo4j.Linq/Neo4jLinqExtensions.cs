using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Cvoya.Graph.Provider.Neo4j.Linq
{
    public static class Neo4jLinqExtensions
    {
        // Example: ToListAsync with traversalDepth
        public static Task<List<T>> ToListAsync<T>(this IQueryable<T> source, int traversalDepth = 1)
        {
            // TODO: Implement async query execution with traversal depth
            return Task.FromResult(source.ToList());
        }
    }
}
