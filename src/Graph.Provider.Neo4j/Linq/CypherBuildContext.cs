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
    /// Whether this returns path results
    /// </summary>
    public bool IsPathResult { get; set; }

    /// <summary>
    /// Whether this is a scalar result (single value, e.g., aggregate functions)
    /// </summary>
    public bool IsScalarResult { get; set; }

    /// <summary>
    /// Number of records to skip (SKIP clause)
    /// </summary>
    public int Skip { get; set; }

    /// <summary>
    /// Maximum number of records to return (LIMIT clause)
    /// </summary>
    public int Limit { get; set; }

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
        Parameters[name] = value;
        return name;
    }
}
