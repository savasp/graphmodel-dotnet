using System.Linq.Expressions;
using System.Text;

namespace Cvoya.Graph.Provider.Neo4j.Linq;

/// <summary>
/// Context object for building Cypher queries during LINQ expression processing.
/// Maintains state for different parts of the Cypher query being built.
/// </summary>
internal class CypherBuildContext
{
    private int _aliasCounter = 0;

    /// <summary>
    /// The MATCH clause content being built
    /// </summary>
    public StringBuilder Match { get; } = new();

    /// <summary>
    /// The WHERE clause content being built
    /// </summary>
    public StringBuilder Where { get; } = new();

    /// <summary>
    /// The ORDER BY clause content being built
    /// </summary>
    public StringBuilder OrderBy { get; } = new();

    /// <summary>
    /// The WITH clause content for aggregations and grouping operations
    /// </summary>
    public string? With { get; set; }

    /// <summary>
    /// The current node/entity alias being used
    /// </summary>
    public string CurrentAlias { get; set; } = "n";

    /// <summary>
    /// The RETURN clause content
    /// </summary>
    public string? Return { get; set; }

    /// <summary>
    /// The root type for the query
    /// </summary>
    public Type? RootType { get; set; }

    /// <summary>
    /// Labels for inheritance support when querying base types
    /// </summary>
    public List<string>? InheritanceLabels { get; set; }

    /// <summary>
    /// Whether this is the last operation in the query
    /// </summary>
    public bool IsLastOperation { get; set; }

    /// <summary>
    /// The query root type (Node or Relationship)
    /// </summary>
    public GraphQueryContext.QueryRootType? QueryRootType { get; set; }

    /// <summary>
    /// Parameters for the query
    /// </summary>
    public Dictionary<string, object?> Parameters { get; } = new();

    /// <summary>
    /// Whether to use DISTINCT in the query
    /// </summary>
    public bool IsDistinct { get; set; }

    /// <summary>
    /// Whether this is a count query (COUNT(...))
    /// </summary>
    public bool IsCountQuery { get; set; }

    /// <summary>
    /// Whether this is a boolean query (returning true/false)
    /// </summary>
    public bool IsBooleanQuery { get; set; }

    /// <summary>
    /// Whether this should return a single result
    /// </summary>
    public bool IsSingleResult { get; set; }

    /// <summary>
    /// Labels for node inheritance, mapping node aliases to their labels
    /// </summary>
    public Dictionary<string, List<string>>? NodeInheritanceLabels { get; set; }

    /// <summary>
    /// Whether this query has node filters (e.g., WHERE conditions on nodes)
    /// </summary>
    public bool HasNodeFilters { get; set; }

    /// <summary>
    /// Whether this is a traversal path query (e.g., MATCH p = (n)-[r*1..3]->(m))
    /// </summary>
    public bool IsTraversalPathGroupBy { get; set; }

    /// <summary>
    /// Whether this returns path results
    /// </summary>
    public bool IsPathResult { get; set; }

    /// <summary>
    /// Whether this is a scalar result (single value, e.g., aggregate functions)
    /// </summary>
    public bool IsScalarResult { get; set; }

    /// <summary>
    /// Client-side projection expression for complex projections that can't be handled in Cypher
    /// </summary>
    public LambdaExpression? ClientSideProjection { get; set; }

    /// <summary>
    /// Number of records to skip (SKIP clause)
    /// </summary>
    public int Skip { get; set; }

    /// <summary>
    /// Maximum number of records to return (LIMIT clause)
    /// </summary>
    public int Limit { get; set; }

    /// <summary>
    /// Traversal depth for path queries (single depth)
    /// </summary>
    public int? TraversalDepth { get; set; }

    /// <summary>
    /// Minimum traversal depth for path queries (range depth)
    /// </summary>
    public int? MinTraversalDepth { get; set; }

    /// <summary>
    /// Maximum traversal depth for path queries (range depth)
    /// </summary>
    public int? MaxTraversalDepth { get; set; }

    /// <summary>
    /// Whether this query has a GroupBy operation
    /// </summary>
    public bool IsGroupByQuery { get; set; }

    /// <summary>
    /// The grouping key expression for GroupBy operations
    /// </summary>
    public string? GroupByKey { get; set; }

    /// <summary>
    /// The original key selector lambda for GroupBy operations
    /// </summary>
    public LambdaExpression? GroupByKeySelector { get; set; }

    /// <summary>
    /// Generates the next unique alias for nodes or relationships
    /// </summary>
    /// <param name="prefix">Prefix for the alias (e.g., "n" for nodes, "r" for relationships)</param>
    /// <returns>A unique alias</returns>
    public string GetNextAlias(string prefix = "alias")
    {
        return $"{prefix}{++_aliasCounter}";
    }

    /// <summary>
    /// Adds a parameter to the context and returns the parameter name
    /// </summary>
    /// <param name="value">The parameter value</param>
    /// <param name="name">Optional parameter name. If not provided, a unique name will be generated</param>
    /// <returns>The parameter name to use in the query</returns>
    public string AddParameter(object? value, string? name = null)
    {
        name ??= $"param{Parameters.Count}";

        // Convert Uri objects to strings for Neo4j compatibility
        if (value is Uri uri)
        {
            value = uri.ToString();
        }

        Parameters[name] = value;
        return name;
    }
}
