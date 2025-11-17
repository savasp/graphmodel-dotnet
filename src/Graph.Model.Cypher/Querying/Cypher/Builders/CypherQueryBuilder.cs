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

namespace Cvoya.Graph.Model.Cypher.Querying.Cypher.Builders;

using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Cvoya.Graph.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// General-purpose Cypher query builder that works with any Cypher-compatible graph database.
/// Extracted from the Neo4j-specific implementation to provide a provider-agnostic foundation.
/// </summary>
public class CypherQueryBuilder
{
    // private readonly ILogger<CypherQueryBuilder> _logger; // Logging removed
    private readonly ICypherQueryBuilderContext _context;
    private readonly ICypherExpressionProcessor _expressionProcessor;
    private readonly ICypherCollectionProvider _collectionProvider;

    // Focused query parts that handle specific responsibilities
    private readonly MatchQueryPart _matchPart;
    private readonly WhereQueryPart _wherePart;
    private readonly GroupByQueryPart _groupByPart;
    private readonly ReturnQueryPart _returnPart;
    private readonly OrderByQueryPart _orderByPart;
    private readonly PaginationQueryPart _paginationPart;

    /// <summary>
    /// Initializes a new instance of the <see cref="CypherQueryBuilder"/> class.
    /// </summary>
    /// <param name="context">The query builder context providing configuration and services.</param>
    /// <param name="expressionProcessor">The expression processor for handling LINQ expressions.</param>
    /// <param name="collectionProvider">The collection provider for database-specific collection operations.</param>
    public CypherQueryBuilder(
        ICypherQueryBuilderContext context,
        ICypherExpressionProcessor expressionProcessor,
        ICypherCollectionProvider collectionProvider)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _expressionProcessor = expressionProcessor ?? throw new ArgumentNullException(nameof(expressionProcessor));
        _collectionProvider = collectionProvider ?? throw new ArgumentNullException(nameof(collectionProvider));
        // Logging removed
        //_logger = _context.LoggerFactory?.CreateLogger<CypherQueryBuilder>()
        //    ?? NullLogger<CypherQueryBuilder>.Instance;

        // Initialize query parts
        _matchPart = new MatchQueryPart(_context.LoggerFactory);
        _groupByPart = new GroupByQueryPart();
        _returnPart = new ReturnQueryPart();
        _orderByPart = new OrderByQueryPart();
        _paginationPart = new PaginationQueryPart();

        // Initialize WhereQueryPart with the provided expression processor
        _wherePart = new WhereQueryPart(expressionProcessor);
    }

    private record PendingPathSegmentPattern(
        Type SourceType,
        Type RelType,
        Type TargetType,
        string SourceAlias,
        string RelAlias,
        string TargetAlias
    );

    private PendingPathSegmentPattern? _pendingPathSegmentPattern;
    private readonly List<PendingPathSegmentPattern> _pathSegmentPatterns = new();
    private string? _intermediateTargetAlias;

    private static readonly Type[] ComplexPropertyInterfaces =
    [
        typeof(INode),
        typeof(IRelationship),
        typeof(IGraphPathSegment)
    ];

    private readonly Dictionary<string, object?> _parameters = [];
    private bool _isRelationshipQuery;
    private int? _minDepth;
    private int? _maxDepth;

    private bool _includeComplexProperties;
    private string? _mainNodeAlias;
    private int _parameterCounter;
    private bool _loadPathSegment;
    private bool _hasMultiplePathSegments;
    private bool _isTraverseWithPathSegments;  // True for Traverse + PathSegments pattern (complex properties)

    /// <summary>
    /// Gets or sets the projection mode for path segments when querying graph relationships.
    /// </summary>
    public PathSegmentProjectionEnum PathSegmentProjection = PathSegmentProjectionEnum.Full;

    /// <summary>
    /// Gets or sets a value indicating whether a WHERE clause has been applied to the root node.
    /// </summary>
    public bool HasAppliedRootWhere { get; set; }
    
    /// <summary>
    /// Gets or sets the alias used for the root node in the query.
    /// </summary>
    public string? RootNodeAlias { get; set; }

    /// <summary>
    /// Gets or sets the alias used for the source node in path segment queries.
    /// </summary>
    public string? PathSegmentSourceAlias { get; set; }
    
    /// <summary>
    /// Gets or sets the alias used for the relationship in path segment queries.
    /// </summary>
    public string? PathSegmentRelationshipAlias { get; set; }
    
    /// <summary>
    /// Gets or sets the alias used for the target node in path segment queries.
    /// </summary>
    public string? PathSegmentTargetAlias { get; set; }

    // Delegate to the focused query parts
    /// <summary>
    /// Gets a value indicating whether the query contains user-defined projections in the RETURN clause.
    /// </summary>
    public bool HasUserProjections => _returnPart.HasUserProjections;
    
    /// <summary>
    /// Gets a value indicating whether the query has an explicit RETURN clause defined.
    /// </summary>
    public bool HasExplicitReturn => _returnPart.HasExplicitReturn;

    /// <summary>
    /// Gets a value indicating whether this query is specifically targeting relationships rather than nodes.
    /// </summary>
    public bool IsRelationshipQuery => _isRelationshipQuery;

    /// <summary>
    /// Gets the traversal direction for relationship queries (incoming, outgoing, or both).
    /// </summary>
    public GraphTraversalDirection? TraversalDirection { get; private set; }

    /// <summary>
    /// Sets the traversal direction for relationship queries.
    /// </summary>
    /// <param name="direction">The direction to traverse relationships.</param>
    public void SetTraversalDirection(GraphTraversalDirection direction)
    {
        TraversalDirection = direction;
    }

    /// <summary>
    /// Sets the maximum depth for relationship traversal queries.
    /// </summary>
    /// <param name="maxDepth">The maximum number of hops to traverse.</param>
    public void SetDepth(int maxDepth)
    {
        _maxDepth = maxDepth;
        _minDepth = null; // Clear min depth when only max is set
    }

    /// <summary>
    /// Gets a value indicating whether depth constraints have been configured for the query.
    /// </summary>
    public bool HasDepthConstraints => _minDepth.HasValue || _maxDepth.HasValue;

    /// <summary>
    /// Gets the Cypher depth pattern string based on configured minimum and maximum depths.
    /// </summary>
    /// <returns>A string like "1..3", "2..", or "1" representing the depth constraint.</returns>
    public string GetDepthPattern()
    {
        return (_minDepth, _maxDepth) switch
        {
            (null, int max) => $"1..{max}",     // e.g., *1..2
            (int min, int max) => $"{min}..{max}", // e.g., *2..3  
            (int min, null) => $"{min}..",      // e.g., *2.. (unlimited max)
            _ => "1"                            // Default single hop
        };
    }

    /// <summary>
    /// Disables loading of complex properties (nodes, relationships, path segments) in query results.
    /// This optimization is useful when only simple scalar properties are needed.
    /// </summary>
    public void DisableComplexPropertyLoading()
    {
        _includeComplexProperties = false;
        _loadPathSegment = false;
    }

    /// <summary>
    /// Marks the query as an aggregation query, which disables ORDER BY clauses that would be invalid with aggregation.
    /// </summary>
    public void SetAggregationQuery()
    {
        _orderByPart.SetAggregationQuery();

    }

    /// <summary>
    /// Sets both minimum and maximum depth constraints for relationship traversal queries.
    /// </summary>
    /// <param name="minDepth">The minimum number of hops to traverse.</param>
    /// <param name="maxDepth">The maximum number of hops to traverse.</param>
    public void SetDepth(int minDepth, int maxDepth)
    {
        _minDepth = minDepth;
        _maxDepth = maxDepth;
    }

    /// <summary>
    /// Sets up a pending path segment pattern that will be built when the query is finalized.
    /// This is used for complex path segment queries involving multiple entity types.
    /// </summary>
    /// <param name="sourceType">The type of the source node.</param>
    /// <param name="relType">The type of the relationship.</param>
    /// <param name="targetType">The type of the target node.</param>
    /// <param name="sourceAlias">The alias for the source node.</param>
    /// <param name="relAlias">The alias for the relationship.</param>
    /// <param name="targetAlias">The alias for the target node.</param>
    public void SetPendingPathSegmentPattern(
        Type sourceType,
        Type relType,
        Type targetType,
        string sourceAlias,
        string relAlias,
        string targetAlias)
    {
        var pattern = new PendingPathSegmentPattern(
            sourceType, relType, targetType, sourceAlias, relAlias, targetAlias);
        _pathSegmentPatterns.Add(pattern);
        _pendingPathSegmentPattern = pattern; // Keep for backward compatibility

    }

    /// <summary>
    /// Specifies the different ways path segments can be projected in query results.
    /// </summary>
    public enum PathSegmentProjectionEnum
    {
        /// <summary>
        /// Return the complete path segment with source node, relationship, and target node.
        /// </summary>
        Full,        // Return the whole path segment
        /// <summary>
        /// Return only the source (start) node of the path segment.
        /// </summary>
        StartNode,   // Return only the start node  
        /// <summary>
        /// Return only the target (end) node of the path segment.
        /// </summary>
        EndNode,     // Return only the end node
        /// <summary>
        /// Return only the relationship of the path segment.
        /// </summary>
        Relationship // Return only the relationship
    }

    /// <summary>
    /// Sets a pending WHERE clause that will be applied when the query is finalized.
    /// This allows for deferred WHERE clause processing in complex query scenarios.
    /// </summary>
    /// <param name="lambda">The lambda expression representing the WHERE condition.</param>
    /// <param name="alias">The alias to use when processing the expression.</param>
    public void SetPendingWhere(LambdaExpression lambda, string? alias)
    {
        _wherePart.SetPendingWhere(lambda, alias);
    }

    /// <summary>
    /// Configures the query to return a boolean indicating whether matching records exist.
    /// </summary>
    public void SetExistsQuery()
    {
        _returnPart.SetExistsQuery();
    }

    /// <summary>
    /// Configures the query to return a boolean indicating whether no matching records exist.
    /// </summary>
    public void SetNotExistsQuery()
    {
        _returnPart.SetNotExistsQuery();
    }

    /// <summary>
    /// Adds a MATCH clause to the query for finding nodes with the specified alias and optional label.
    /// </summary>
    /// <param name="alias">The alias to assign to matched nodes.</param>
    /// <param name="label">The optional node label to match against.</param>
    /// <param name="pattern">The optional custom pattern string.</param>
    public void AddMatch(string alias, string? label = null, string? pattern = null)
    {
        if (RootNodeAlias is null)
        {
            RootNodeAlias = alias;
        }

        _matchPart.AddMatch(alias, label, pattern);

        // Keep track of the main node alias
        _mainNodeAlias ??= alias;
        _returnPart.SetMainNodeAlias(_mainNodeAlias);
    }

    /// <summary>
    /// Adds a custom MATCH pattern to the query.
    /// </summary>
    /// <param name="fullPattern">The complete MATCH pattern string.</param>
    public void AddMatchPattern(string fullPattern)
    {
        _matchPart.AddMatchPattern(fullPattern);
    }

    /// <summary>
    /// Enables loading of complex properties (nodes, relationships, path segments) in query results.
    /// </summary>
    public void EnableComplexPropertyLoading()
    {
        _includeComplexProperties = true;
    }

    /// <summary>
    /// Gets a value indicating whether complex property loading is currently enabled.
    /// </summary>
    /// <returns>True if complex properties will be loaded, false otherwise.</returns>
    public bool IsComplexPropertyLoadingEnabled()
    {
        return _includeComplexProperties;
    }

    /// <summary>
    /// Gets a value indicating whether path segment loading is currently enabled.
    /// </summary>
    /// <returns>True if path segments will be loaded, false otherwise.</returns>
    public bool IsPathSegmentLoading()
    {
        return _loadPathSegment;
    }

    /// <summary>
    /// Enables loading of path segment data in query results.
    /// </summary>
    public void EnablePathSegmentLoading()
    {
        _includeComplexProperties = true;
        _loadPathSegment = true;
    }

    /// <summary>
    /// Removes all MATCH clauses from the query.
    /// </summary>
    public void ClearMatches()
    {
        _matchPart.ClearMatches();
    }

    /// <summary>
    /// Resets relationship-specific query state so subsequent operations treat the query as a node query.
    /// Used when transforming a relationship-based source into a JOIN that projects a node.
    /// </summary>
    public void ResetRelationshipQueryState()
    {
        _isRelationshipQuery = false;
        _loadPathSegment = false; // disable path segment loading for join returning nodes
        _mainNodeAlias = null; // allow next AddMatch to set node alias
        _returnPart.SetMainNodeAlias(_mainNodeAlias);
    }

    /// <summary>
    /// Removes all user-defined projections from the RETURN clause.
    /// </summary>
    public void ClearUserProjections()
    {
        _returnPart.ClearUserProjections();
    }

    /// <summary>
    /// Removes all WHERE conditions from the query.
    /// </summary>
    public void ClearWhere()
    {
        _wherePart.ClearWhere();
    }

    /// <summary>
    /// Adds a WHERE condition to the query.
    /// </summary>
    /// <param name="condition">The condition string to add to the WHERE clause.</param>
    public void AddWhere(string condition)
    {
        _wherePart.AddWhere(condition);
    }

    /// <summary>
    /// Removes all RETURN clauses from the query.
    /// </summary>
    public void ClearReturn()
    {
        _returnPart.ClearReturn();
    }

    /// <summary>
    /// Sets the main node alias for the query.
    /// </summary>
    /// <param name="alias">The alias to use for the main node.</param>
    public void SetMainNodeAlias(string alias)
    {
        _mainNodeAlias = alias;
        // Ensure the RETURN part uses the updated alias for fallback node wrapping.
        _returnPart.SetMainNodeAlias(alias);
    }

    /// <summary>
    /// Adds an ORDER BY clause to the query.
    /// </summary>
    /// <param name="expression">The expression to order by.</param>
    /// <param name="isDescending">Whether to sort in descending order.</param>
    public void AddOrderBy(string expression, bool isDescending = false)
    {
        _orderByPart.AddOrderBy(expression, isDescending);
    }

    /// <summary>
    /// Sets the number of records to skip in the query results.
    /// </summary>
    /// <param name="skip">The number of records to skip.</param>
    public void SetSkip(int skip) => _paginationPart.SetSkip(skip);
    
    /// <summary>
    /// Sets the maximum number of records to return.
    /// </summary>
    /// <param name="limit">The maximum number of records to return.</param>
    public void SetLimit(int limit) => _paginationPart.SetLimit(limit);
    
    /// <summary>
    /// Sets up an aggregation function in the RETURN clause.
    /// </summary>
    /// <param name="function">The aggregation function name (e.g., "COUNT", "SUM").</param>
    /// <param name="expression">The expression to aggregate.</param>
    public void SetAggregation(string function, string expression) => _returnPart.SetAggregation(function, expression);

    /// <summary>
    /// Adds a parameter to the query and returns its parameter name.
    /// Reuses existing parameters if the same value is added multiple times.
    /// </summary>
    /// <param name="value">The parameter value.</param>
    /// <returns>The parameter name to use in the Cypher query.</returns>
    public string AddParameter(object? value)
    {


        // Check if we already have this value as a parameter
        var existingParam = _parameters.FirstOrDefault(p => Equals(p.Value, value));
        if (existingParam.Key != null)
        {
            return $"${existingParam.Key}";
        }

        var paramName = $"p{_parameterCounter++}";
        _parameters[paramName] = value;
        return $"${paramName}";
    }

    /// <summary>
    /// Builds and returns the complete Cypher query with all configured clauses and parameters.
    /// This method finalizes the query construction, applying any pending patterns or conditions,
    /// and returns a <see cref="CypherQuery"/> object containing the query text and parameters.
    /// </summary>
    /// <returns>A complete Cypher query ready for execution.</returns>
    public CypherQuery Build()
    {


        // Build any pending path segment patterns now that we have full context
        BuildPendingPathSegmentPattern();

        // Handle special query types first
        if (_returnPart.IsExistsQuery)
        {
            return BuildExistsQuery();
        }

        if (_returnPart.IsNotExistsQuery)
        {
            return BuildNotExistsQuery();
        }

        // Handle complex properties if needed
        if (_includeComplexProperties && !HasUserProjections)
        {
            FinalizeWhereClause();

            return BuildWithComplexProperties();
        }

        // Handle mixed projections with special types that need complex properties
        if (_includeComplexProperties && HasUserProjections && _loadPathSegment)
        {
            FinalizeWhereClause();

            return BuildMixedProjectionWithComplexProperties();
        }

        // Build a simple query using the focused query parts
        return BuildSimpleQuery();
    }

    public bool NeedsComplexProperties(Type type)
    {
        var visited = new HashSet<Type>();
        return NeedsComplexPropertiesRecursive(type, visited);
    }

    public void AddRelationshipMatch(string relationshipType)
    {


        _matchPart.AddRelationshipMatch(relationshipType, TraversalDirection, _minDepth, _maxDepth);
        _mainNodeAlias = "r"; // Set main alias to the relationship
        _returnPart.SetMainNodeAlias(_mainNodeAlias);

        _isRelationshipQuery = true;

        // Enable path segment loading since relationships are path segments
        EnablePathSegmentLoading();
    }

    public bool HasOrderBy => _orderByPart.HasOrderBy;

    public PathSegmentProjectionEnum GetPathSegmentProjection()
    {
        return PathSegmentProjection;
    }

    public void SetPathSegmentProjection(PathSegmentProjectionEnum projection)
    {
        PathSegmentProjection = projection;

    }

    public void AddOptionalMatch(string pattern)
    {

        _matchPart.AddOptionalMatch(pattern);
    }

    public void AddLimit(int limit)
    {

        _paginationPart.AddLimit(limit);
    }

    public void AddSkip(int skip)
    {

        _paginationPart.AddSkip(skip);
    }

    public void AddWith(string expression)
    {

        _returnPart.AddWith(expression);
    }

    public void AddUnwind(string expression)
    {

        _returnPart.AddUnwind(expression);
    }

    public void AddFullTextNodeSearch(string indexName, string queryParam, string nodeAlias)
    {

        var searchPattern = $"CALL db.index.fulltext.queryNodes('{indexName}', {queryParam}) YIELD node AS {nodeAlias}";
        _matchPart.AddCallClause(searchPattern);
    }

    public void AddFullTextRelationshipSearch(string indexName, string queryParam, string relAlias, string? relationshipType = null)
    {


        var whereClause = string.IsNullOrEmpty(relationshipType) ? "" : $" WHERE type({relAlias}) = '{relationshipType}'";
        var searchPattern = $@"CALL db.index.fulltext.queryRelationships('{indexName}', {queryParam}) YIELD relationship AS {relAlias}{whereClause}
            MATCH (src)-[{relAlias}]->(tgt)
            RETURN {{ StartNode: {{ Node: src, ComplexProperties: [] }}, Relationship: {relAlias}, EndNode: {{ Node: tgt, ComplexProperties: [] }} }} AS PathSegment";
        _matchPart.AddCallClause(searchPattern);
    }

    public void AddFullTextEntitySearch(string nodeIndexName, string relIndexName, string queryParam, string nodeAlias, string relAlias)
    {

        var unionQuery = $@"
            CALL {{
                CALL db.index.fulltext.queryNodes('{nodeIndexName}', {queryParam}) YIELD node AS {nodeAlias}
                RETURN {nodeAlias} AS entity
                UNION ALL
                CALL db.index.fulltext.queryRelationships('{relIndexName}', {queryParam}) YIELD relationship AS {relAlias}
                MATCH (src)-[{relAlias}]->(tgt)
                RETURN {{ StartNode: {{ Node: src, ComplexProperties: [] }}, Relationship: {relAlias}, EndNode: {{ Node: tgt, ComplexProperties: [] }} }} AS entity
            }}
            RETURN entity";
        _matchPart.AddCallClause(unionQuery);
    }



    public void AddGroupBy(string expression)
    {

        _groupByPart.AddGroupBy(expression);
    }

    public void SetDistinct(bool distinct)
    {

        _returnPart.SetDistinct(distinct);
    }

    public bool HasReturnClause => _returnPart.HasContent;
    public bool IsDistinct => _returnPart.IsDistinct;
    public IReadOnlyList<string> ReturnClauses => _returnPart.ReturnClauses;
    public bool HasPendingPathSegmentPattern => _pendingPathSegmentPattern != null;

    public void AddReturn(string expression, string? alias = null)
    {
        _returnPart.AddReturn(expression, alias);
    }

    public void AddUserProjection(string expression, string? alias = null)
    {
        _returnPart.AddUserProjection(expression, alias);
    }

    public void AddInfrastructureReturn(string expression, string? alias = null)
    {
        _returnPart.AddInfrastructureReturn(expression, alias);
    }

    public void ReverseOrderBy()
    {

        _orderByPart.ReverseOrderBy();
    }

    public void AddMatchClause(string matchClause)
    {
        _matchPart.AddMatchPattern(matchClause);
    }

    private void FinalizeWhereClause()
    {
        // The WhereQueryPart now handles pending WHERE clauses internally
        // This method is kept for compatibility but delegates to the focused part

        _wherePart.FinalizePendingClauses();

        // For path segment patterns, we need to update the WHERE clause aliases
        if (PathSegmentSourceAlias != null && PathSegmentTargetAlias != null)
        {
            // Rules for choosing target alias:
            // 1. Traverse + PathSegments (complex properties): use intermediate target alias.
            // 2. Incoming direction two-segment chain: intermediate alias represents original EndNode.
            // 3. Two-segment chained same-type (outgoing): only replace 'tgt' with final target, keep 'src'.
            // 4. Multi-hop (>2): preserve 'src' untouched, replace both if needed.
            var useIntermediateForIncomingChain = (TraversalDirection == GraphTraversalDirection.Incoming) && _intermediateTargetAlias != null;
            var targetAliasForWhere = _isTraverseWithPathSegments
                ? (_intermediateTargetAlias ?? PathSegmentTargetAlias)
                : (useIntermediateForIncomingChain ? _intermediateTargetAlias! : PathSegmentTargetAlias);

            var isTwoSegmentChained = !_isTraverseWithPathSegments && _intermediateTargetAlias != null && PathSegmentTargetAlias != _intermediateTargetAlias && !_hasMultiplePathSegments;
            if (isTwoSegmentChained)
            {

                _wherePart.UpdateOnlyTargetAlias(targetAliasForWhere);
                _orderByPart.UpdateAliasesForPathSegments("src", targetAliasForWhere);
            }
            else
            {
                var sourceAliasForWhere = _hasMultiplePathSegments ? "src" : PathSegmentSourceAlias;
                _wherePart.UpdateAliasesForPathSegments(sourceAliasForWhere, targetAliasForWhere);
                _orderByPart.UpdateAliasesForPathSegments(sourceAliasForWhere, targetAliasForWhere);
            }
            // Don't update the RETURN clause here - it will be updated in BuildMixedProjectionWithComplexProperties
            // if (PathSegmentTargetAlias != null)
            // {
            //     _returnPart.UpdateAliasesForPathSegments(PathSegmentSourceAlias, PathSegmentTargetAlias);
            // }
        }
    }

    public string GetActualAlias(string originalAlias)
    {
        // Map the aliases based on the path segment context
        if (_pendingPathSegmentPattern != null)
        {
            // For nested path segments, we need to use the aliases from the combined pattern
            // not the individual pending pattern
            if (_pathSegmentPatterns.Count > 1)
            {
                // We have nested path segments - use the PathSegmentXxxAlias properties
                // which are set by BuildCombinedPathSegmentPattern to point to the correct
                // aliases in the combined pattern
                //
                // IMPORTANT: During SELECT processing (before Build() is called), 
                // PathSegmentSourceAlias is null. In this case, we need to map "src" to the 
                // actual source alias in the chain, which is the TARGET of the previous PathSegment.
                bool isTwoSameTypeChain = _pathSegmentPatterns.Count == 2
                    && !_isTraverseWithPathSegments
                    && _pathSegmentPatterns[0].SourceType == _pathSegmentPatterns[0].TargetType
                    && _pathSegmentPatterns[0].TargetType == _pathSegmentPatterns[1].SourceType;

                return originalAlias switch
                {
                    // Only remap 'src' to first segment target alias for two same-type chained PathSegments.
                    // For different-type chains (e.g. Memory->User then Memory->Source) keep original source alias.
                    "src" => isTwoSameTypeChain
                        ? _pathSegmentPatterns[0].TargetAlias
                        : (PathSegmentSourceAlias ?? (_pathSegmentPatterns.Count > 2 ?
                            _pathSegmentPatterns[^2].TargetAlias : _pathSegmentPatterns[0].SourceAlias)),
                    "tgt" => PathSegmentTargetAlias ?? _pathSegmentPatterns[^1].TargetAlias,
                    "r" => PathSegmentRelationshipAlias ?? _pathSegmentPatterns[^1].RelAlias,
                    _ => originalAlias
                };
            }
            else
            {
                // Single path segment - use the pending pattern aliases
                return originalAlias switch
                {
                    "src" => _pendingPathSegmentPattern.SourceAlias,
                    "tgt" => _pendingPathSegmentPattern.TargetAlias,
                    "r" => _pendingPathSegmentPattern.RelAlias,
                    _ => originalAlias
                };
            }
        }

        // For path segments that have already been built or for nested path segments
        if (_loadPathSegment || PathSegmentSourceAlias != null)
        {
            return originalAlias switch
            {
                "src" => PathSegmentSourceAlias ?? originalAlias,
                "tgt" => _intermediateTargetAlias ?? PathSegmentTargetAlias ?? originalAlias, // Use intermediate target for WHERE clause filtering
                "r" => PathSegmentRelationshipAlias ?? originalAlias,
                _ => originalAlias
            };
        }

        // If we're in a path segment context but don't have the pattern yet,
        // we need to defer the alias resolution until the pattern is built
        if (_context.Scope.IsInPathSegmentContext())
        {
            // Return the original alias for now - it will be resolved later
            // when the path segment pattern is built
            return originalAlias;
        }

        return originalAlias;
    }

    public bool HasIntermediateTargetAlias()
    {
        return !string.IsNullOrEmpty(_intermediateTargetAlias);
    }

    public string? GetIntermediateTargetAlias()
    {
        return _intermediateTargetAlias;
    }

    private static bool NeedsComplexPropertiesRecursive(Type type, HashSet<Type> visited)
    {
        if (type is null || !visited.Add(type))
            return false;

        // Dynamic entity types can have complex properties, so we need to check them
        // The old logic skipped them, but that's incorrect for dynamic entities with complex properties

        // Direct match
        if (ComplexPropertyInterfaces.Any(i => i.IsAssignableFrom(type)))
            return true;

        // Handle collections (e.g., IEnumerable<T>)
        if (type.IsGenericType && typeof(System.Collections.IEnumerable).IsAssignableFrom(type))
        {
            var elementType = type.GetGenericArguments().FirstOrDefault();
            if (elementType is not null && NeedsComplexPropertiesRecursive(elementType, visited))
                return true;
        }

        // Check properties recursively
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (NeedsComplexPropertiesRecursive(prop.PropertyType, visited))
                return true;
        }

        return false;
    }

    private CypherQuery BuildSimpleQuery()
    {


        var query = new StringBuilder();

        // IMPORTANT: Process WHERE expressions first to ensure any complex property 
        // MATCH clauses are added before we write the MATCH section
        _wherePart.FinalizePendingClauses();

        // For path segment patterns in simple queries we still need to update alias references
        // in WHERE/ORDER BY clauses. Previously this only happened for complex property paths.
        // This caused chained PathSegments filtering (e.g. Age > 35 on second hop) to use the
        // intermediate alias (tgt) instead of the final target alias (tgt_3) because the replacement
        // never ran for simple queries.
        if (PathSegmentSourceAlias != null && PathSegmentTargetAlias != null)
        {
            var useIntermediateForIncomingChain = (TraversalDirection == GraphTraversalDirection.Incoming) && _intermediateTargetAlias != null;
            var targetAliasForWhere = _isTraverseWithPathSegments
                ? (_intermediateTargetAlias ?? PathSegmentTargetAlias)
                : (useIntermediateForIncomingChain ? _intermediateTargetAlias! : PathSegmentTargetAlias);

            var isTwoSegmentChained = !_isTraverseWithPathSegments && _intermediateTargetAlias != null && PathSegmentTargetAlias != _intermediateTargetAlias && !_hasMultiplePathSegments;
            if (isTwoSegmentChained)
            {

                _wherePart.UpdateOnlyTargetAlias(targetAliasForWhere);
                _orderByPart.UpdateAliasesForPathSegments("src", targetAliasForWhere);
            }
            else
            {
                var sourceAliasForWhere = _hasMultiplePathSegments ? "src" : PathSegmentSourceAlias;
                _wherePart.UpdateAliasesForPathSegments(sourceAliasForWhere, targetAliasForWhere);
                _orderByPart.UpdateAliasesForPathSegments(sourceAliasForWhere, targetAliasForWhere);
            }
        }

        // Build the query using the focused query parts
        var parts = new List<Cvoya.Graph.Model.Cypher.Querying.Cypher.Builders.ICypherQueryPart> { _matchPart, _wherePart, _groupByPart, _returnPart, _orderByPart, _paginationPart };

        foreach (var part in parts.Where(p => p.HasContent).OrderBy(p => p.Order))
        {
            part.AppendTo(query, _parameters);
        }
        var finalQuery = query.ToString().Trim();
        try
        {
            var debugPath = "/workspaces/graphmodel-dotnet/temp/cypher_debug.log";
            System.IO.File.AppendAllText(debugPath,
                $"\n=== SIMPLE QUERY BUILT {DateTime.UtcNow:O} ===\nPathSegmentsCount={_pathSegmentPatterns.Count} Direction={TraversalDirection} TraverseFlag={_isTraverseWithPathSegments} HasMultiple={_hasMultiplePathSegments} SourceAlias={PathSegmentSourceAlias} TargetAlias={PathSegmentTargetAlias} IntermediateAlias={_intermediateTargetAlias}\n{finalQuery}\n");
        }
        catch { }

        return new CypherQuery(finalQuery, new Dictionary<string, object?>(_parameters));
    }

    // Old private methods removed - functionality now handled by focused query parts

    private CypherQuery BuildExistsQuery()
    {


        var query = new StringBuilder();

        // Use focused query parts for EXISTS queries
        _matchPart.AppendTo(query, _parameters);
        _wherePart.AppendTo(query, _parameters);

        // For EXISTS queries, we return a count > 0
        query.AppendLine($"RETURN COUNT({_mainNodeAlias ?? "src"}) > 0 AS exists");

        return new CypherQuery(query.ToString().Trim(), new Dictionary<string, object?>(_parameters));
    }

    private CypherQuery BuildNotExistsQuery()
    {


        var query = new StringBuilder();

        // Use focused query parts for NOT EXISTS queries
        _matchPart.AppendTo(query, _parameters);
        _wherePart.AppendTo(query, _parameters);

        // For NOT EXISTS queries (used by All), return true if no matching nodes exist
        query.AppendLine($"RETURN COUNT({_mainNodeAlias ?? "src"}) = 0 AS all");

        return new CypherQuery(query.ToString().Trim(), new Dictionary<string, object?>(_parameters));
    }

    private CypherQuery BuildWithComplexProperties()
    {


        var query = new StringBuilder();

        // IMPORTANT: Process WHERE expressions first to ensure any complex property 
        // MATCH clauses are added before we write the MATCH section
        _wherePart.FinalizePendingClauses();

        // First part: get the main nodes using focused query parts
        _matchPart.AppendTo(query, _parameters);
        _wherePart.AppendTo(query, _parameters);

        // For path segments, we don't need the intermediate WITH clause
        if (_loadPathSegment)
        {
            // Skip the WITH clause for path segments - go straight to complex property loading
            AppendComplexPropertyMatchesForPathSegment(query);
        }
        else if (!string.IsNullOrEmpty(_mainNodeAlias))
        {
            // For regular node queries, collect main nodes with ordering and pagination
            _orderByPart.AppendTo(query, _parameters);
            _paginationPart.AppendTo(query, _parameters);

            // For nodes, do complex property loading
            AppendComplexPropertyMatchesForSingleNode(query);
        }
        else
        {
            // Fallback - just add complex property matches
            AppendComplexPropertyMatchesForSingleNode(query);
        }

        var finalQuery = query.ToString().Trim();
        try
        {
            var debugPath = "/workspaces/graphmodel-dotnet/temp/cypher_debug.log";
            System.IO.File.AppendAllText(debugPath,
                $"\n=== COMPLEX QUERY BUILT {DateTime.UtcNow:O} ===\nPathSegmentsCount={_pathSegmentPatterns.Count} Direction={TraversalDirection} TraverseFlag={_isTraverseWithPathSegments} HasMultiple={_hasMultiplePathSegments} SourceAlias={PathSegmentSourceAlias} TargetAlias={PathSegmentTargetAlias} IntermediateAlias={_intermediateTargetAlias}\n{finalQuery}\n");
        }
        catch { }
        return new CypherQuery(finalQuery, new Dictionary<string, object?>(_parameters));
    }

    private void AppendComplexPropertyMatchesForSingleNode(StringBuilder query)
    {


        query.AppendLine(@$"
            // All complex property paths from src
            OPTIONAL MATCH src_path = ({_mainNodeAlias})-[rels*1..]->(prop)
            WHERE ALL(rel in rels WHERE type(rel) STARTS WITH '{GraphDataModel.PropertyRelationshipTypeNamePrefix}')
            WITH {_mainNodeAlias},
                CASE 
                    WHEN src_path IS NULL THEN []
                    ELSE [i IN range(0, size(rels)-1) | {{
                        ParentNode: 
                            CASE 
                                WHEN i = 0 THEN {_mainNodeAlias}
                                ELSE nodes(src_path)[i]
                            END,
                        Relationship: rels[i],
                        SequenceNumber: rels[i].SequenceNumber,
                        Property: nodes(src_path)[i+1]
                    }}]
                END AS src_flat_property

            WITH {_mainNodeAlias},
                reduce(flat = [], l IN collect(src_flat_property) | flat + l) AS src_flat_properties
            WITH {_mainNodeAlias}, {_collectionProvider.ToSet("src_flat_properties")} AS src_flat_properties


            RETURN {{
                Node: {_mainNodeAlias},
                ComplexProperties: src_flat_properties
            }} AS Node
        ");
    }

    private void AppendComplexPropertyMatchesForPathSegment(StringBuilder query)
    {


        var src = PathSegmentSourceAlias ?? "src";
        var rel = PathSegmentRelationshipAlias ?? "r";
        var tgt = PathSegmentTargetAlias ?? "tgt";

        // Determine what complex properties we need based on projection
        var (needsSourceProps, needsTargetProps) = PathSegmentProjection switch
        {
            PathSegmentProjectionEnum.StartNode => (true, false),   // Only source
            PathSegmentProjectionEnum.EndNode => (false, true),    // Only target
            PathSegmentProjectionEnum.Relationship => (true, true), // Both for relationship navigation
            PathSegmentProjectionEnum.Full => (true, true),        // Both for full path
            _ => (false, false)
        };



        // Build the WITH clause components we'll need
        // For nested path segments, we need to include the intermediate target alias
        // so that ORDER BY clauses can reference it
        var baseWith = $"{src}, {rel}, {tgt}";

        // If we have nested path segments, we need to include the intermediate target
        // This is the target from the first path segment (e.g., 'tgt' when we have src->tgt->tgt_3)
        if (!string.IsNullOrEmpty(_intermediateTargetAlias) && _intermediateTargetAlias != tgt)
        {
            baseWith = $"{src}, {rel}, {_intermediateTargetAlias}, {tgt}";

        }

        var currentWith = baseWith;

        if (needsSourceProps)
        {
            query.AppendLine($@"
                // Complex properties from source node
                OPTIONAL MATCH src_path = ({src})-[rels*1..]->(prop)
                WHERE ALL(rel in rels WHERE type(rel) STARTS WITH '{GraphDataModel.PropertyRelationshipTypeNamePrefix}')
                WITH {currentWith},
                    CASE 
                        WHEN src_path IS NULL THEN []
                        ELSE [i IN range(0, size(rels)-1) | {{
                            ParentNode: CASE 
                                WHEN i = 0 THEN {src}
                                ELSE nodes(src_path)[i]
                            END,
                            Relationship: rels[i],
                            SequenceNumber: rels[i].SequenceNumber,
                            Property: nodes(src_path)[i+1]
                        }}]
                    END AS src_flat_property
                WITH {currentWith},
                    reduce(flat = [], l IN collect(src_flat_property) | flat + l) AS src_flat_properties
                WITH {currentWith}, {_collectionProvider.ToSet("src_flat_properties")} AS src_flat_properties");

            currentWith += ", src_flat_properties";
        }

        if (needsTargetProps)
        {
            query.AppendLine($@"
                // Complex properties from target node
                OPTIONAL MATCH tgt_path = ({tgt})-[trels*1..]->(tprop)
                WHERE ALL(rel in trels WHERE type(rel) STARTS WITH '{GraphDataModel.PropertyRelationshipTypeNamePrefix}')
                WITH {currentWith},
                    CASE 
                        WHEN tgt_path IS NULL THEN []
                        ELSE [i IN range(0, size(trels)-1) | {{
                            ParentNode: CASE 
                                WHEN i = 0 THEN {tgt}
                                ELSE nodes(tgt_path)[i]
                            END,
                            Relationship: trels[i],
                            SequenceNumber: trels[i].SequenceNumber,
                            Property: nodes(tgt_path)[i+1]
                        }}]
                    END AS tgt_flat_property
                WITH {currentWith},
                    reduce(flat = [], l IN collect(tgt_flat_property) | flat + l) AS tgt_flat_properties
                WITH {currentWith}, {_collectionProvider.ToSet("tgt_flat_properties")} AS tgt_flat_properties");
        }

        // Add ordering and pagination before the final return
        _orderByPart.AppendTo(query, _parameters);
        _paginationPart.AppendTo(query, _parameters);

        // Build the return clause based on what we loaded
        // For combined Traverse + PathSegments pattern, we need to adjust the projection
        // The PathSegments should project the intermediate target (T1) as StartNode, not the original source (S)
        var startNodeAlias = src;
        var endNodeAlias = tgt;
        var relationshipAlias = rel;

        // If we have a combined pattern (Traverse + PathSegments), adjust the aliases
        if (!string.IsNullOrEmpty(_intermediateTargetAlias) && _intermediateTargetAlias != tgt)
        {
            // For combined pattern: StartNode should be the intermediate target (T1), EndNode should be the final target (T2)
            startNodeAlias = _intermediateTargetAlias; // T1 (MemoryWithoutSourceProperty)
            endNodeAlias = tgt; // T2 (MemorySourceNode)
            relationshipAlias = rel; // R1 (MemoryToMemorySourceNode)
        }

        var returnClause = PathSegmentProjection switch
        {
            PathSegmentProjectionEnum.EndNode => $@"
                RETURN {{
                    Node: {endNodeAlias},
                    ComplexProperties: tgt_flat_properties
                }} AS Node",

            PathSegmentProjectionEnum.StartNode => $@"
                RETURN {{
                    Node: {startNodeAlias},
                    ComplexProperties: src_flat_properties
                }} AS Node",

            PathSegmentProjectionEnum.Relationship => $@"
                RETURN {{
                    Relationship: {relationshipAlias},
                    StartNode: {{
                        Node: {startNodeAlias},
                        ComplexProperties: src_flat_properties
                    }},
                    EndNode: {{
                        Node: {endNodeAlias},
                        ComplexProperties: tgt_flat_properties
                    }}
                }} AS PathSegment",

            PathSegmentProjectionEnum.Full => $@"
                RETURN {{
                    StartNode: {{
                        Node: {startNodeAlias},
                        ComplexProperties: src_flat_properties
                    }},
                    Relationship: {relationshipAlias},
                    EndNode: {{
                        Node: {endNodeAlias},
                        ComplexProperties: tgt_flat_properties
                    }}
                }} AS PathSegment",

            _ => throw new ArgumentOutOfRangeException(nameof(PathSegmentProjection), PathSegmentProjection, "Unknown path segment projection")
        };

        query.AppendLine(returnClause);
    }

    private CypherQuery BuildMixedProjectionWithComplexProperties()
    {


        var query = new StringBuilder();

        // Start with the basic MATCH and WHERE
        _matchPart.AppendTo(query, _parameters);
        _wherePart.AppendTo(query, _parameters);

        // Add complex property loading for both source and target nodes
        var src = PathSegmentSourceAlias ?? "src";
        var rel = PathSegmentRelationshipAlias ?? "r";
        var tgt = PathSegmentTargetAlias ?? "tgt";

        // For nested path segments, we need to include the intermediate target alias
        // so that ORDER BY clauses can reference it
        var withClause = $"{src}, {rel}, {tgt}";
        if (!string.IsNullOrEmpty(_intermediateTargetAlias) && _intermediateTargetAlias != tgt)
        {
            withClause = $"{src}, {rel}, {_intermediateTargetAlias}, {tgt}";

        }

        // For combined Traverse + PathSegments pattern, we need to load complex properties
        // for the StartNode of the projection, which is the intermediate target (tgt), not the original source (src)
        var startNodeAlias = src;
        // Only remap StartNode to intermediate target for outgoing (or both) direction combined Traverse+PathSegments.
        // For incoming direction the StartNode must remain the original source alias.
        if (!string.IsNullOrEmpty(_intermediateTargetAlias)
            && _intermediateTargetAlias != tgt
            && TraversalDirection != GraphTraversalDirection.Incoming)
        {
            // Outgoing combined pattern: StartNode should be the intermediate target (T1), not the original source (S)
            startNodeAlias = _intermediateTargetAlias;

        }

        // Load complex properties for StartNode (which is the intermediate target in combined patterns)
        query.AppendLine($@"
            // Complex properties from StartNode (intermediate target in combined patterns)
            OPTIONAL MATCH src_path = ({startNodeAlias})-[rels*1..]->(prop)
            WHERE ALL(rel in rels WHERE type(rel) STARTS WITH '{GraphDataModel.PropertyRelationshipTypeNamePrefix}')
            WITH {withClause},
                CASE 
                    WHEN src_path IS NULL THEN []
                    ELSE [i IN range(0, size(rels)-1) | {{
                        ParentNode: CASE 
                            WHEN i = 0 THEN {startNodeAlias}
                            ELSE nodes(src_path)[i]
                        END,
                        Relationship: rels[i],
                        SequenceNumber: rels[i].SequenceNumber,
                        Property: nodes(src_path)[i+1]
                    }}]
                END AS src_flat_property
            WITH {withClause},
                reduce(flat = [], l IN collect(src_flat_property) | flat + l) AS src_flat_properties
            WITH {withClause}, {_collectionProvider.ToSet("src_flat_properties")} AS src_flat_properties");

        // Load complex properties for target node
        query.AppendLine($@"
            // Complex properties from target node
            OPTIONAL MATCH tgt_path = ({tgt})-[trels*1..]->(tprop)
            WHERE ALL(rel in trels WHERE type(rel) STARTS WITH '{GraphDataModel.PropertyRelationshipTypeNamePrefix}')
            WITH {withClause}, src_flat_properties,
                CASE 
                    WHEN tgt_path IS NULL THEN []
                    ELSE [i IN range(0, size(trels)-1) | {{
                        ParentNode: CASE 
                            WHEN i = 0 THEN {tgt}
                            ELSE nodes(tgt_path)[i]
                        END,
                        Relationship: trels[i],
                        SequenceNumber: trels[i].SequenceNumber,
                        Property: nodes(tgt_path)[i+1]
                    }}]
                END AS tgt_flat_property
            WITH {withClause}, src_flat_properties,
                reduce(flat = [], l IN collect(tgt_flat_property) | flat + l) AS tgt_flat_properties
            WITH {withClause}, src_flat_properties, {_collectionProvider.ToSet("tgt_flat_properties")} AS tgt_flat_properties");

        // For combined Traverse + PathSegments pattern, we need to update the user projections
        // to use the correct aliases (tgt instead of src for StartNode)
        // Only apply this logic for Traverse + PathSegments, not for multiple PathSegments calls
        // For multiple PathSegments, we want to keep src as src for the Memory field
        // Traverse + PathSegments patterns start with (src:...), while multiple PathSegments start with (tgt_2:...)
        if (!string.IsNullOrEmpty(_intermediateTargetAlias) && src == "src" && PathSegmentSourceAlias == "src" && !_hasMultiplePathSegments)
        {


            _returnPart.UpdateAliasesForPathSegments(src, _intermediateTargetAlias);

        }

        // Add ordering and pagination before the final return
        _orderByPart.AppendTo(query, _parameters);
        _paginationPart.AppendTo(query, _parameters);

        // Use the user projections with complex property structures for special types
        _returnPart.AppendTo(query, _parameters);

        var finalQuery = query.ToString().Trim();
        try
        {
            var debugPath = "/workspaces/graphmodel-dotnet/temp/cypher_debug.log";
            System.IO.File.AppendAllText(debugPath,
                $"\n=== MIXED COMPLEX QUERY BUILT {DateTime.UtcNow:O} ===\nPathSegmentsCount={_pathSegmentPatterns.Count} Direction={TraversalDirection} TraverseFlag={_isTraverseWithPathSegments} HasMultiple={_hasMultiplePathSegments} SourceAlias={PathSegmentSourceAlias} TargetAlias={PathSegmentTargetAlias} IntermediateAlias={_intermediateTargetAlias}\n{finalQuery}\n");
        }
        catch { }
        return new CypherQuery(finalQuery, new Dictionary<string, object?>(_parameters));
    }

    private void BuildPendingPathSegmentPattern()
    {
        if (_pathSegmentPatterns.Count == 0) return;

        // If we have multiple path segments, combine them into a single pattern
        if (_pathSegmentPatterns.Count > 1)
        {

            BuildCombinedPathSegmentPattern();
            return;
        }

        var p = _pathSegmentPatterns[0];

        // Get compatible labels for inheritance support
        var sourceLabels = Labels.GetCompatibleLabels(p.SourceType);
        var relLabels = Labels.GetCompatibleLabels(p.RelType);
        var targetLabels = Labels.GetCompatibleLabels(p.TargetType);

        var sourceLabel = sourceLabels.Count == 1 ? sourceLabels[0] : string.Join("|", sourceLabels);
        var relLabel = relLabels.Count == 1 ? relLabels[0] : string.Join("|", relLabels);
        var targetLabel = targetLabels.Count == 1 ? targetLabels[0] : string.Join("|", targetLabels);

        // Determine direction
        var direction = TraversalDirection ?? GraphTraversalDirection.Outgoing;
        string pattern = direction switch
        {
            GraphTraversalDirection.Outgoing => HasDepthConstraints
                ? $"({p.SourceAlias}:{sourceLabel})-[{p.RelAlias}:{relLabel}*{GetDepthPattern()}]->({p.TargetAlias}:{targetLabel})"
                : $"({p.SourceAlias}:{sourceLabel})-[{p.RelAlias}:{relLabel}]->({p.TargetAlias}:{targetLabel})",

            GraphTraversalDirection.Incoming => HasDepthConstraints
                ? $"({p.SourceAlias}:{sourceLabel})<-[{p.RelAlias}:{relLabel}*{GetDepthPattern()}]-({p.TargetAlias}:{targetLabel})"
                : $"({p.SourceAlias}:{sourceLabel})<-[{p.RelAlias}:{relLabel}]-({p.TargetAlias}:{targetLabel})",

            GraphTraversalDirection.Both => HasDepthConstraints
                ? $"({p.SourceAlias}:{sourceLabel})-[{p.RelAlias}:{relLabel}*{GetDepthPattern()}]-({p.TargetAlias}:{targetLabel})"
                : $"({p.SourceAlias}:{sourceLabel})-[{p.RelAlias}:{relLabel}]-({p.TargetAlias}:{targetLabel})",

            _ => throw new NotSupportedException($"Unknown traversal direction: {direction}")
        };



        // Check if we have nested path segments by looking at the source alias
        // If the source alias is "tgt", it means we're extending an existing pattern
        if (p.SourceAlias == "tgt" && p.SourceType.Name == "Memory")
        {


            // We have nested path segments - build a combined pattern
            // The pattern should be: (src:User)-[r:UserMemory]->(tgt:Memory)-[r2:MemoryToMemorySourceNode]->(tgt2:MemorySourceNode)

            // For now, just use the second pattern and let the WHERE clause handle the first part
            // This is a temporary fix - we need to properly combine the patterns


            // Only clear the main match clauses, not the additional match statements (complex properties)
            _matchPart.ClearMainMatches();
            AddMatchPattern(pattern);
        }
        else
        {
            // Only clear the main match clauses, not the additional match statements (complex properties)
            _matchPart.ClearMainMatches();
            AddMatchPattern(pattern);
        }

        // Update the path segment aliases to point to the new target
        PathSegmentSourceAlias = p.SourceAlias;
        PathSegmentRelationshipAlias = p.RelAlias;
        PathSegmentTargetAlias = p.TargetAlias;

        _pathSegmentPatterns.Clear();
        _pendingPathSegmentPattern = null;

        // Note: Don't process WHERE clauses here - they will be processed later in Build()
        // after the path segment aliases are set. The pending WHERE clauses will be processed
        // with the correct aliases when FinalizeWhereClause() is called in Build().
    }

    private void BuildCombinedPathSegmentPattern()
    {
        if (_pathSegmentPatterns.Count < 2) return;



        // Check if this is a Traverse + PathSegments pattern
        // Since Traverse<S, R, T> is internally PathSegments<S, R, T>, we need to chain both patterns
        if (_pathSegmentPatterns.Count == 2)
        {
            var traversePattern = _pathSegmentPatterns[0];  // Traverse pattern (S, R, T1)
            var pathSegmentPattern = _pathSegmentPatterns[1]; // PathSegments pattern (T1, R1, T2)

            // Check if the second pattern's source type matches the first pattern's target type
            // This indicates a potential Traverse + PathSegments OR chained PathSegments pattern
            if (traversePattern.TargetType == pathSegmentPattern.SourceType)
            {
                // Check if this is truly a Traverse + PathSegments pattern (for complex properties)
                // vs chained PathSegments (both same type). The key difference is whether the
                // intermediate target type needs complex properties loaded.
                // Treat as Traverse + PathSegments only when the first hop changes node type.
                // Chained PathSegments of the same node type (e.g. Person->Person->Person) should NOT
                // be marked as Traverse+PathSegments so that WHERE alias rewriting can upgrade 'tgt'
                // to the final target alias (e.g. 'tgt_3').
                var isComplexPropertyTraversal = traversePattern.SourceType != traversePattern.TargetType && NeedsComplexProperties(traversePattern.TargetType);
                
                if (isComplexPropertyTraversal)
                {

                    _isTraverseWithPathSegments = true;
                }
                else
                {

                }

                // Build a combined pattern that chains both PathSegments calls
                // Pattern: (src1:S)-[r1:R]->(tgt1:T1)-[r2:R1]->(tgt2:T2)
                var pathDirection = TraversalDirection ?? GraphTraversalDirection.Outgoing;

                // First part: (src1:S)-[r1:R]->(tgt1:T1)
                var firstSourceLabels = Labels.GetCompatibleLabels(traversePattern.SourceType);
                var firstRelLabels = Labels.GetCompatibleLabels(traversePattern.RelType);
                var firstTargetLabels = Labels.GetCompatibleLabels(traversePattern.TargetType);

                var firstSourceLabel = firstSourceLabels.Count == 1 ? firstSourceLabels[0] : string.Join("|", firstSourceLabels);
                var firstRelLabel = firstRelLabels.Count == 1 ? firstRelLabels[0] : string.Join("|", firstRelLabels);
                var firstTargetLabel = firstTargetLabels.Count == 1 ? firstTargetLabels[0] : string.Join("|", firstTargetLabels);

                // Second part: (tgt1:T1)-[r2:R1]->(tgt2:T2)
                var secondSourceLabels = Labels.GetCompatibleLabels(pathSegmentPattern.SourceType);
                var secondRelLabels = Labels.GetCompatibleLabels(pathSegmentPattern.RelType);
                var secondTargetLabels = Labels.GetCompatibleLabels(pathSegmentPattern.TargetType);

                var secondSourceLabel = secondSourceLabels.Count == 1 ? secondSourceLabels[0] : string.Join("|", secondSourceLabels);
                var secondRelLabel = secondRelLabels.Count == 1 ? secondRelLabels[0] : string.Join("|", secondRelLabels);
                var secondTargetLabel = secondTargetLabels.Count == 1 ? secondTargetLabels[0] : string.Join("|", secondTargetLabels);

                string combinedPathPattern = pathDirection switch
                {
                    GraphTraversalDirection.Outgoing => $"({traversePattern.SourceAlias}:{firstSourceLabel})-[{traversePattern.RelAlias}:{firstRelLabel}]->({traversePattern.TargetAlias}:{firstTargetLabel})-[{pathSegmentPattern.RelAlias}:{secondRelLabel}]->({pathSegmentPattern.TargetAlias}:{secondTargetLabel})",
                    GraphTraversalDirection.Incoming => $"({pathSegmentPattern.TargetAlias}:{secondTargetLabel})<-[{pathSegmentPattern.RelAlias}:{secondRelLabel}]-({traversePattern.TargetAlias}:{firstTargetLabel})<-[{traversePattern.RelAlias}:{firstRelLabel}]-({traversePattern.SourceAlias}:{firstSourceLabel})",
                    GraphTraversalDirection.Both => $"({traversePattern.SourceAlias}:{firstSourceLabel})-[{traversePattern.RelAlias}:{firstRelLabel}]-({traversePattern.TargetAlias}:{firstTargetLabel})-[{pathSegmentPattern.RelAlias}:{secondRelLabel}]-({pathSegmentPattern.TargetAlias}:{secondTargetLabel})",
                    _ => throw new NotSupportedException($"Unknown traversal direction: {pathDirection}")
                };



                // Only clear the main match clauses, not the additional match statements (complex properties)
                _matchPart.ClearMainMatches();
                AddMatchPattern(combinedPathPattern);

                // Update the path segment aliases to point to the combined pattern
                // For incoming direction, the pattern is reversed, so we need to adjust aliases accordingly
                if (pathDirection == GraphTraversalDirection.Incoming)
                {
                    // For incoming direction, the combined pattern is: (tgt2)<-[r2]-(tgt)<-[r1]-(src)
                    // Semantically for the final PathSegments projection we want:
                    //   StartNode = original source (src: MemoryWithoutSourceProperty)
                    //   Relationship = second hop relationship (r2 / pathSegmentPattern.RelAlias)
                    //   EndNode = second pattern's target (tgt2: MemorySourceNode)
                    // The intermediate target alias (tgt: User) is only used for filtering and MUST NOT
                    // replace the StartNode in incoming direction (unlike the outgoing Traverse+PathSegments case).
                    PathSegmentSourceAlias = traversePattern.SourceAlias;          // src
                    PathSegmentRelationshipAlias = pathSegmentPattern.RelAlias;    // r_1
                    PathSegmentTargetAlias = pathSegmentPattern.TargetAlias;       // tgt_2

                    // Store the intermediate target alias (tgt) for WHERE clause filtering only.
                    _intermediateTargetAlias = traversePattern.TargetAlias;        // tgt
                }
                else
                {
                    // For outgoing direction, the pattern is: (src)-[r1]->(tgt)-[r2]->(tgt2)
                    // So the source is the first pattern's source, and the target is the second pattern's target
                    PathSegmentSourceAlias = traversePattern.SourceAlias;
                    PathSegmentRelationshipAlias = traversePattern.RelAlias;
                    PathSegmentTargetAlias = pathSegmentPattern.TargetAlias;

                    // Store the intermediate target alias (T1) for WHERE clause filtering
                    _intermediateTargetAlias = traversePattern.TargetAlias;
                }

                _pathSegmentPatterns.Clear();
                _pendingPathSegmentPattern = null;
                return;
            }
        }

        // Original logic for true nested path segments (multiple PathSegments calls)
        // Build a combined pattern that chains all path segments together
        _hasMultiplePathSegments = true;
        var combinedPattern = new StringBuilder();
        var direction = TraversalDirection ?? GraphTraversalDirection.Outgoing;

        if (direction == GraphTraversalDirection.Incoming)
        {
            // For incoming direction, build the pattern in reverse order
            for (int i = _pathSegmentPatterns.Count - 1; i >= 0; i--)
            {
                var p = _pathSegmentPatterns[i];

                // Get compatible labels for inheritance support
                var sourceLabels = Labels.GetCompatibleLabels(p.SourceType);
                var relLabels = Labels.GetCompatibleLabels(p.RelType);
                var targetLabels = Labels.GetCompatibleLabels(p.TargetType);

                var sourceLabel = sourceLabels.Count == 1 ? sourceLabels[0] : string.Join("|", sourceLabels);
                var relLabel = relLabels.Count == 1 ? relLabels[0] : string.Join("|", relLabels);
                var targetLabel = targetLabels.Count == 1 ? targetLabels[0] : string.Join("|", targetLabels);

                if (i == _pathSegmentPatterns.Count - 1)
                {
                    // Last segment (first in reverse): start with target node
                    combinedPattern.Append($"({p.TargetAlias}:{targetLabel})");
                }

                // Add relationship and source node (in reverse)
                // For the first segment in reverse (which is the last segment), use the target alias
                if (i == 0)
                {
                    combinedPattern.Append($"<-[{p.RelAlias}:{relLabel}]-({p.TargetAlias}:{targetLabel})");
                }
                else
                {
                    combinedPattern.Append($"<-[{p.RelAlias}:{relLabel}]-({p.SourceAlias}:{sourceLabel})");
                }
            }
        }
        else
        {
            // For outgoing and both directions, build in normal order
            for (int i = 0; i < _pathSegmentPatterns.Count; i++)
            {
                var p = _pathSegmentPatterns[i];

                // Get compatible labels for inheritance support
                var sourceLabels = Labels.GetCompatibleLabels(p.SourceType);
                var relLabels = Labels.GetCompatibleLabels(p.RelType);
                var targetLabels = Labels.GetCompatibleLabels(p.TargetType);

                var sourceLabel = sourceLabels.Count == 1 ? sourceLabels[0] : string.Join("|", sourceLabels);
                var relLabel = relLabels.Count == 1 ? relLabels[0] : string.Join("|", relLabels);
                var targetLabel = targetLabels.Count == 1 ? targetLabels[0] : string.Join("|", targetLabels);

                if (i == 0)
                {
                    // First segment: start with source node
                    combinedPattern.Append($"({p.SourceAlias}:{sourceLabel})");
                }

                // Add relationship and target node
                switch (direction)
                {
                    case GraphTraversalDirection.Outgoing:
                        combinedPattern.Append($"-[{p.RelAlias}:{relLabel}]->({p.TargetAlias}:{targetLabel})");
                        break;
                    case GraphTraversalDirection.Both:
                        combinedPattern.Append($"-[{p.RelAlias}:{relLabel}]-({p.TargetAlias}:{targetLabel})");
                        break;
                }
            }
        }

        var pattern = combinedPattern.ToString();


        // Only clear the main match clauses, not the additional match statements (complex properties)
        _matchPart.ClearMainMatches();
        AddMatchPattern(pattern);

        // Update the path segment aliases to point to the final target
        var lastPattern = _pathSegmentPatterns[^1];
        var firstPattern = _pathSegmentPatterns[0];

        // For chained path segments (3+), the "src" in the final projection should be 
        // the source of the LAST PathSegment in the chain, which is the TARGET of the 
        // second-to-last PathSegment
        if (direction == GraphTraversalDirection.Incoming)
        {
            // For incoming: pattern is reversed, so source is at the end
            PathSegmentSourceAlias = _pathSegmentPatterns.Count > 2 ? 
                _pathSegmentPatterns[1].SourceAlias : firstPattern.SourceAlias;
        }
        else
        {
            // For outgoing: the last PathSegment's source is the previous one's target
            PathSegmentSourceAlias = _pathSegmentPatterns.Count > 2 ? 
                _pathSegmentPatterns[^2].TargetAlias : firstPattern.SourceAlias;
        }
        
        PathSegmentRelationshipAlias = lastPattern.RelAlias;
        PathSegmentTargetAlias = lastPattern.TargetAlias;

        // Store the intermediate target alias (tgt) for ORDER BY and WHERE clauses
        // This is needed because ORDER BY might reference the intermediate target
        if (_pathSegmentPatterns.Count > 1)
        {
            var intermediateTarget = _pathSegmentPatterns[0].TargetAlias; // This is 'tgt'

            // Store this for use in complex property loading
            _intermediateTargetAlias = intermediateTarget;
        }

        _pathSegmentPatterns.Clear();
        _pendingPathSegmentPattern = null;
    }
}