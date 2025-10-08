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

namespace Cvoya.Graph.Model.Neo4j.Querying.Cypher.Builders;

using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Cvoya.Graph.Model;
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;


/// <summary>
/// Refactored CypherQueryBuilder that uses focused query parts to eliminate duplication.
/// </summary>
internal class CypherQueryBuilder(CypherQueryContext context)
{
    private readonly ILogger<CypherQueryBuilder> _logger = context.LoggerFactory?.CreateLogger<CypherQueryBuilder>()
        ?? NullLogger<CypherQueryBuilder>.Instance;
    private readonly CypherQueryContext _context = context;

    // Focused query parts that handle specific responsibilities
    private readonly MatchQueryPart _matchPart = new(context);
    private readonly WhereQueryPart _wherePart = new(context);
    private readonly GroupByQueryPart _groupByPart = new();
    private readonly ReturnQueryPart _returnPart = new();
    private readonly OrderByQueryPart _orderByPart = new();
    private readonly PaginationQueryPart _paginationPart = new();

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

    public PathSegmentProjectionEnum PathSegmentProjection = PathSegmentProjectionEnum.Full;

    public bool HasAppliedRootWhere { get; set; }
    public string? RootNodeAlias { get; set; }

    public string? PathSegmentSourceAlias { get; set; }
    public string? PathSegmentRelationshipAlias { get; set; }
    public string? PathSegmentTargetAlias { get; set; }

    // Delegate to the focused query parts
    public bool HasUserProjections => _returnPart.HasUserProjections;
    public bool HasExplicitReturn => _returnPart.HasExplicitReturn;

    public bool IsRelationshipQuery => _isRelationshipQuery;

    public GraphTraversalDirection? TraversalDirection { get; private set; }

    public void SetTraversalDirection(GraphTraversalDirection direction)
    {
        TraversalDirection = direction;
    }

    public void SetDepth(int maxDepth)
    {
        _maxDepth = maxDepth;
        _minDepth = null; // Clear min depth when only max is set
    }

    public bool HasDepthConstraints => _minDepth.HasValue || _maxDepth.HasValue;

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

    public void DisableComplexPropertyLoading()
    {
        _includeComplexProperties = false;
        _loadPathSegment = false;
    }

    public void SetAggregationQuery()
    {
        _orderByPart.SetAggregationQuery();
        _logger.LogDebug("Query marked as aggregation query - ORDER BY clauses will be disabled");
    }

    public void SetDepth(int minDepth, int maxDepth)
    {
        _minDepth = minDepth;
        _maxDepth = maxDepth;
    }

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
        _logger.LogDebug("Added path segment pattern: ({Source}:{SourceType})-[{Rel}:{RelType}]->({Target}:{TargetType})",
            sourceAlias, sourceType.Name, relAlias, relType.Name, targetAlias, targetType.Name);
    }

    public enum PathSegmentProjectionEnum
    {
        Full,        // Return the whole path segment
        StartNode,   // Return only the start node  
        EndNode,     // Return only the end node
        Relationship // Return only the relationship
    }

    public void SetPendingWhere(LambdaExpression lambda, string? alias)
    {
        _wherePart.SetPendingWhere(lambda, alias);
    }

    public void SetExistsQuery()
    {
        _returnPart.SetExistsQuery();
    }

    public void SetNotExistsQuery()
    {
        _returnPart.SetNotExistsQuery();
    }

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

    public void AddMatchPattern(string fullPattern)
    {
        _matchPart.AddMatchPattern(fullPattern);
    }

    public void EnableComplexPropertyLoading()
    {
        _includeComplexProperties = true;
    }

    public bool IsComplexPropertyLoadingEnabled()
    {
        return _includeComplexProperties;
    }

    public bool IsPathSegmentLoading()
    {
        return _loadPathSegment;
    }

    public void EnablePathSegmentLoading()
    {
        _includeComplexProperties = true;
        _loadPathSegment = true;
    }

    public void ClearMatches()
    {
        _matchPart.ClearMatches();
    }

    public void ClearUserProjections()
    {
        _returnPart.ClearUserProjections();
    }

    public void ClearWhere()
    {
        _wherePart.ClearWhere();
    }

    public void AddWhere(string condition)
    {
        _wherePart.AddWhere(condition);
    }

    public void ClearReturn()
    {
        _returnPart.ClearReturn();
    }

    public void SetMainNodeAlias(string alias)
    {
        _mainNodeAlias = alias;
    }

    public void AddOrderBy(string expression, bool isDescending = false)
    {
        _orderByPart.AddOrderBy(expression, isDescending);
    }

    public void SetSkip(int skip) => _paginationPart.SetSkip(skip);
    public void SetLimit(int limit) => _paginationPart.SetLimit(limit);
    public void SetAggregation(string function, string expression) => _returnPart.SetAggregation(function, expression);

    public string AddParameter(object? value)
    {
        _logger.LogDebug("Adding parameter with value: {Value}", value);

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

    public CypherQuery Build()
    {
        _logger.LogDebug("Building Cypher query");

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
        _logger.LogDebug("AddRelationshipMatch called with type: {Type}", relationshipType);

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
        _logger.LogDebug("Set path segment projection: {Projection}", projection);
    }

    public void AddOptionalMatch(string pattern)
    {
        _logger.LogDebug("AddOptionalMatch called with pattern: '{Pattern}'", pattern);
        _matchPart.AddOptionalMatch(pattern);
    }

    public void AddLimit(int limit)
    {
        _logger.LogDebug("AddLimit called with value: {Limit}", limit);
        _paginationPart.AddLimit(limit);
    }

    public void AddSkip(int skip)
    {
        _logger.LogDebug("AddSkip called with value: {Skip}", skip);
        _paginationPart.AddSkip(skip);
    }

    public void AddWith(string expression)
    {
        _logger.LogDebug("AddWith called with expression: '{Expression}'", expression);
        _returnPart.AddWith(expression);
    }

    public void AddUnwind(string expression)
    {
        _logger.LogDebug("AddUnwind called with expression: '{Expression}'", expression);
        _returnPart.AddUnwind(expression);
    }

    public void AddFullTextNodeSearch(string indexName, string queryParam, string nodeAlias)
    {
        _logger.LogDebug("AddFullTextNodeSearch called with index: {IndexName}, query: {QueryParam}, alias: {NodeAlias}", indexName, queryParam, nodeAlias);
        var searchPattern = $"CALL db.index.fulltext.queryNodes('{indexName}', {queryParam}) YIELD node AS {nodeAlias}";
        _matchPart.AddCallClause(searchPattern);
    }

    public void AddFullTextRelationshipSearch(string indexName, string queryParam, string relAlias, string? relationshipType = null)
    {
        _logger.LogDebug("AddFullTextRelationshipSearch called with index: {IndexName}, query: {QueryParam}, alias: {RelAlias}, type: {Type}", indexName, queryParam, relAlias, relationshipType);

        var whereClause = string.IsNullOrEmpty(relationshipType) ? "" : $" WHERE type({relAlias}) = '{relationshipType}'";
        var searchPattern = $@"CALL db.index.fulltext.queryRelationships('{indexName}', {queryParam}) YIELD relationship AS {relAlias}{whereClause}
            MATCH (src)-[{relAlias}]->(tgt)
            RETURN {{ StartNode: {{ Node: src, ComplexProperties: [] }}, Relationship: {relAlias}, EndNode: {{ Node: tgt, ComplexProperties: [] }} }} AS PathSegment";
        _matchPart.AddCallClause(searchPattern);
    }

    public void AddFullTextEntitySearch(string nodeIndexName, string relIndexName, string queryParam, string nodeAlias, string relAlias)
    {
        _logger.LogDebug("AddFullTextEntitySearch called with nodeIndex: {NodeIndexName}, relIndex: {RelIndexName}, query: {QueryParam}, nodeAlias: {NodeAlias}, relAlias: {RelAlias}", nodeIndexName, relIndexName, queryParam, nodeAlias, relAlias);
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
        _logger.LogDebug("AddGroupBy called with expression: '{Expression}'", expression);
        _groupByPart.AddGroupBy(expression);
    }

    public void SetDistinct(bool distinct)
    {
        _logger.LogDebug("SetDistinct called with value: {Value}", distinct);
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
        _logger.LogDebug("Reversing ORDER BY clauses");
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
        _logger.LogDebug("FinalizeWhereClause called - delegating to WhereQueryPart");
        _wherePart.FinalizePendingClauses();

        // For Traverse + PathSegments pattern, we need to update the WHERE clause aliases
        // The WHERE clause should filter on the intermediate target (Memory node), not the final target (MemorySourceNode)
        if (PathSegmentSourceAlias != null && _intermediateTargetAlias != null)
        {
            _wherePart.UpdateAliasesForPathSegments(PathSegmentSourceAlias, _intermediateTargetAlias);
            _orderByPart.UpdateAliasesForPathSegments(PathSegmentSourceAlias, _intermediateTargetAlias);
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
                // We have nested path segments - use the combined pattern aliases
                return originalAlias switch
                {
                    "src" => PathSegmentSourceAlias ?? _pathSegmentPatterns[0].SourceAlias,
                    "tgt" => PathSegmentTargetAlias ?? _pathSegmentPatterns[^1].TargetAlias,
                    "r" => PathSegmentRelationshipAlias ?? _pathSegmentPatterns[0].RelAlias,
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
        _logger.LogDebug("Building simple query using focused query parts");

        var query = new StringBuilder();

        // IMPORTANT: Process WHERE expressions first to ensure any complex property 
        // MATCH clauses are added before we write the MATCH section
        _wherePart.FinalizePendingClauses();

        // Build the query using the focused query parts
        var parts = new List<ICypherQueryPart> { _matchPart, _wherePart, _groupByPart, _returnPart, _orderByPart, _paginationPart };

        foreach (var part in parts.Where(p => p.HasContent).OrderBy(p => p.Order))
        {
            part.AppendTo(query, _parameters);
        }

        return new CypherQuery(query.ToString().Trim(), new Dictionary<string, object?>(_parameters));
    }

    // Old private methods removed - functionality now handled by focused query parts

    private CypherQuery BuildExistsQuery()
    {
        _logger.LogDebug("Building EXISTS query using focused query parts");

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
        _logger.LogDebug("Building NOT EXISTS query using focused query parts");

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
        _logger.LogDebug("Building query with complex properties using focused query parts");

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

        return new CypherQuery(query.ToString().Trim(), new Dictionary<string, object?>(_parameters));
    }

    private void AppendComplexPropertyMatchesForSingleNode(StringBuilder query)
    {
        _logger.LogDebug("Appending complex property matches for single node");

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
            WITH {_mainNodeAlias}, apoc.coll.toSet(src_flat_properties) AS src_flat_properties


            RETURN {{
                Node: {_mainNodeAlias},
                ComplexProperties: src_flat_properties
            }} AS Node
        ");
    }

    private void AppendComplexPropertyMatchesForPathSegment(StringBuilder query)
    {
        _logger.LogDebug("Appending complex property matches for path segment with projection: {Projection}", PathSegmentProjection);

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

        _logger.LogDebug("Complex property loading - Source: {NeedsSource}, Target: {NeedsTarget}",
            needsSourceProps, needsTargetProps);

        // Build the WITH clause components we'll need
        // For nested path segments, we need to include the intermediate target alias
        // so that ORDER BY clauses can reference it
        var baseWith = $"{src}, {rel}, {tgt}";

        // If we have nested path segments, we need to include the intermediate target
        // This is the target from the first path segment (e.g., 'tgt' when we have src->tgt->tgt_3)
        if (!string.IsNullOrEmpty(_intermediateTargetAlias) && _intermediateTargetAlias != tgt)
        {
            baseWith = $"{src}, {rel}, {_intermediateTargetAlias}, {tgt}";
            _logger.LogDebug("Including intermediate target alias in WITH clause: {Alias}", _intermediateTargetAlias);
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
                WITH {currentWith}, apoc.coll.toSet(src_flat_properties) AS src_flat_properties");

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
                WITH {currentWith}, apoc.coll.toSet(tgt_flat_properties) AS tgt_flat_properties");
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
        _logger.LogDebug("Building mixed projection query with complex properties");

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
            _logger.LogDebug("Including intermediate target alias in WITH clause for mixed projection: {Alias}", _intermediateTargetAlias);
        }

        // For combined Traverse + PathSegments pattern, we need to load complex properties
        // for the StartNode of the projection, which is the intermediate target (tgt), not the original source (src)
        var startNodeAlias = src;
        if (!string.IsNullOrEmpty(_intermediateTargetAlias) && _intermediateTargetAlias != tgt)
        {
            // For combined pattern: StartNode should be the intermediate target (T1), not the original source (S)
            startNodeAlias = _intermediateTargetAlias;
            _logger.LogDebug("Using intermediate target alias for StartNode complex properties: {Alias}", startNodeAlias);
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
            WITH {withClause}, apoc.coll.toSet(src_flat_properties) AS src_flat_properties");

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
            WITH {withClause}, src_flat_properties, apoc.coll.toSet(tgt_flat_properties) AS tgt_flat_properties");

        // For combined Traverse + PathSegments pattern, we need to update the user projections
        // to use the correct aliases (tgt instead of src for StartNode)
        if (!string.IsNullOrEmpty(_intermediateTargetAlias) && _intermediateTargetAlias != tgt)
        {
            _logger.LogDebug("Updating user projections for combined pattern - replacing {OldAlias} with {NewAlias}", src, _intermediateTargetAlias);
            _logger.LogDebug("Return clauses before update: {Clauses}", string.Join(", ", _returnPart.ReturnClauses));
            _returnPart.UpdateAliasesForPathSegments(src, _intermediateTargetAlias);
            _logger.LogDebug("Return clauses after update: {Clauses}", string.Join(", ", _returnPart.ReturnClauses));
        }

        // Use the user projections with complex property structures for special types
        _returnPart.AppendTo(query, _parameters);

        return new CypherQuery(query.ToString().Trim(), new Dictionary<string, object?>(_parameters));
    }

    private void BuildPendingPathSegmentPattern()
    {
        if (_pathSegmentPatterns.Count == 0) return;

        // If we have multiple path segments, combine them into a single pattern
        if (_pathSegmentPatterns.Count > 1)
        {
            _logger.LogDebug("Building combined path segment pattern from {Count} segments", _pathSegmentPatterns.Count);
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

        _logger.LogDebug($"Building deferred path segment pattern with direction {direction}: {pattern}");

        // Check if we have nested path segments by looking at the source alias
        // If the source alias is "tgt", it means we're extending an existing pattern
        if (p.SourceAlias == "tgt" && p.SourceType.Name == "Memory")
        {
            _logger.LogDebug("Building combined nested path segment pattern");

            // We have nested path segments - build a combined pattern
            // The pattern should be: (src:User)-[r:UserMemory]->(tgt:Memory)-[r2:MemoryToMemorySourceNode]->(tgt2:MemorySourceNode)

            // For now, just use the second pattern and let the WHERE clause handle the first part
            // This is a temporary fix - we need to properly combine the patterns
            _logger.LogDebug("Using second pattern for now: {Pattern}", pattern);

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

        _logger.LogDebug("Building combined path segment pattern from {Count} segments", _pathSegmentPatterns.Count);

        // Check if this is a Traverse + PathSegments pattern
        // Since Traverse<S, R, T> is internally PathSegments<S, R, T>, we need to chain both patterns
        if (_pathSegmentPatterns.Count == 2)
        {
            var traversePattern = _pathSegmentPatterns[0];  // Traverse pattern (S, R, T1)
            var pathSegmentPattern = _pathSegmentPatterns[1]; // PathSegments pattern (T1, R1, T2)

            // Check if the second pattern's source type matches the first pattern's target type
            // This indicates a Traverse + PathSegments pattern
            if (traversePattern.TargetType == pathSegmentPattern.SourceType)
            {
                _logger.LogDebug("Detected Traverse + PathSegments pattern. Building combined pattern that chains both PathSegments calls.");

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
                    GraphTraversalDirection.Incoming => $"({traversePattern.SourceAlias}:{firstSourceLabel})<-[{traversePattern.RelAlias}:{firstRelLabel}]-({traversePattern.TargetAlias}:{firstTargetLabel})<-[{pathSegmentPattern.RelAlias}:{secondRelLabel}]-({pathSegmentPattern.TargetAlias}:{secondTargetLabel})",
                    GraphTraversalDirection.Both => $"({traversePattern.SourceAlias}:{firstSourceLabel})-[{traversePattern.RelAlias}:{firstRelLabel}]-({traversePattern.TargetAlias}:{firstTargetLabel})-[{pathSegmentPattern.RelAlias}:{secondRelLabel}]-({pathSegmentPattern.TargetAlias}:{secondTargetLabel})",
                    _ => throw new NotSupportedException($"Unknown traversal direction: {pathDirection}")
                };

                _logger.LogDebug("Combined PathSegments pattern: {Pattern}", combinedPathPattern);

                // Only clear the main match clauses, not the additional match statements (complex properties)
                _matchPart.ClearMainMatches();
                AddMatchPattern(combinedPathPattern);

                // Update the path segment aliases to point to the combined pattern
                // The source is the first pattern's source, the target is the second pattern's target
                PathSegmentSourceAlias = traversePattern.SourceAlias;
                PathSegmentRelationshipAlias = traversePattern.RelAlias;
                PathSegmentTargetAlias = pathSegmentPattern.TargetAlias;

                // Store the intermediate target alias (T1) for WHERE clause filtering
                _intermediateTargetAlias = traversePattern.TargetAlias;

                _pathSegmentPatterns.Clear();
                _pendingPathSegmentPattern = null;
                return;
            }
        }

        // Original logic for true nested path segments (multiple PathSegments calls)
        // Build a combined pattern that chains all path segments together
        var combinedPattern = new StringBuilder();
        var direction = TraversalDirection ?? GraphTraversalDirection.Outgoing;

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
                case GraphTraversalDirection.Incoming:
                    combinedPattern.Append($"<-[{p.RelAlias}:{relLabel}]-({p.TargetAlias}:{targetLabel})");
                    break;
                case GraphTraversalDirection.Both:
                    combinedPattern.Append($"-[{p.RelAlias}:{relLabel}]-({p.TargetAlias}:{targetLabel})");
                    break;
            }
        }

        var pattern = combinedPattern.ToString();
        _logger.LogDebug("Combined path segment pattern: {Pattern}", pattern);

        // Only clear the main match clauses, not the additional match statements (complex properties)
        _matchPart.ClearMainMatches();
        AddMatchPattern(pattern);

        // Update the path segment aliases to point to the final target
        var lastPattern = _pathSegmentPatterns[^1];
        var firstPattern = _pathSegmentPatterns[0];

        // For nested path segments, use the aliases from the first pattern for source and relationship
        // since the main MATCH clause uses those aliases
        PathSegmentSourceAlias = firstPattern.SourceAlias;
        PathSegmentRelationshipAlias = firstPattern.RelAlias;
        PathSegmentTargetAlias = lastPattern.TargetAlias;

        // Store the intermediate target alias (tgt) for ORDER BY and WHERE clauses
        // This is needed because ORDER BY might reference the intermediate target
        if (_pathSegmentPatterns.Count > 1)
        {
            var intermediateTarget = _pathSegmentPatterns[0].TargetAlias; // This is 'tgt'
            _logger.LogDebug("Storing intermediate target alias for ORDER BY: {Alias}", intermediateTarget);
            // Store this for use in complex property loading
            _intermediateTargetAlias = intermediateTarget;
        }

        _pathSegmentPatterns.Clear();
        _pendingPathSegmentPattern = null;
    }
}