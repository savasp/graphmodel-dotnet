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
        _pendingPathSegmentPattern = new PendingPathSegmentPattern(
            sourceType, relType, targetType, sourceAlias, relAlias, targetAlias);
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
    }

    public string GetActualAlias(string originalAlias)
    {
        // Map the aliases based on the path segment context
        if (_pendingPathSegmentPattern != null)
        {
            return originalAlias switch
            {
                "src" => _pendingPathSegmentPattern.SourceAlias,
                "tgt" => _pendingPathSegmentPattern.TargetAlias,
                "r" => _pendingPathSegmentPattern.RelAlias,
                _ => originalAlias
            };
        }

        // For path segments that have already been built
        if (_loadPathSegment)
        {
            return originalAlias switch
            {
                "src" => PathSegmentSourceAlias ?? originalAlias,
                "tgt" => PathSegmentTargetAlias ?? originalAlias,
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
        var baseWith = $"{src}, {rel}, {tgt}";
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
        var returnClause = PathSegmentProjection switch
        {
            PathSegmentProjectionEnum.EndNode => $@"
                RETURN {{
                    Node: {tgt},
                    ComplexProperties: tgt_flat_properties
                }} AS Node",

            PathSegmentProjectionEnum.StartNode => $@"
                RETURN {{
                    Node: {src},
                    ComplexProperties: src_flat_properties
                }} AS Node",

            PathSegmentProjectionEnum.Relationship => $@"
                RETURN {{
                    Relationship: {rel},
                    StartNode: {{
                        Node: {src},
                        ComplexProperties: src_flat_properties
                    }},
                    EndNode: {{
                        Node: {tgt},
                        ComplexProperties: tgt_flat_properties
                    }}
                }} AS PathSegment",

            PathSegmentProjectionEnum.Full => $@"
                RETURN {{
                    StartNode: {{
                        Node: {src},
                        ComplexProperties: src_flat_properties
                    }},
                    Relationship: {rel},
                    EndNode: {{
                        Node: {tgt},
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

        // Load complex properties for source node
        query.AppendLine($@"
            // Complex properties from source node
            OPTIONAL MATCH src_path = ({src})-[rels*1..]->(prop)
            WHERE ALL(rel in rels WHERE type(rel) STARTS WITH '{GraphDataModel.PropertyRelationshipTypeNamePrefix}')
            WITH {src}, {rel}, {tgt},
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
            WITH {src}, {rel}, {tgt},
                reduce(flat = [], l IN collect(src_flat_property) | flat + l) AS src_flat_properties
            WITH {src}, {rel}, {tgt}, apoc.coll.toSet(src_flat_properties) AS src_flat_properties");

        // Load complex properties for target node
        query.AppendLine($@"
            // Complex properties from target node
            OPTIONAL MATCH tgt_path = ({tgt})-[trels*1..]->(tprop)
            WHERE ALL(rel in trels WHERE type(rel) STARTS WITH '{GraphDataModel.PropertyRelationshipTypeNamePrefix}')
            WITH {src}, {rel}, {tgt}, src_flat_properties,
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
            WITH {src}, {rel}, {tgt}, src_flat_properties,
                reduce(flat = [], l IN collect(tgt_flat_property) | flat + l) AS tgt_flat_properties
            WITH {src}, {rel}, {tgt}, src_flat_properties, apoc.coll.toSet(tgt_flat_properties) AS tgt_flat_properties");

        // Use the user projections with complex property structures for special types
        _returnPart.AppendTo(query, _parameters);

        return new CypherQuery(query.ToString().Trim(), new Dictionary<string, object?>(_parameters));
    }

    private void BuildPendingPathSegmentPattern()
    {
        if (_pendingPathSegmentPattern is null) return;

        var p = _pendingPathSegmentPattern;

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

        // Only clear the main match clauses, not the additional match statements (complex properties)
        _matchPart.ClearMainMatches();
        AddMatchPattern(pattern);

        PathSegmentSourceAlias = p.SourceAlias;
        PathSegmentRelationshipAlias = p.RelAlias;
        PathSegmentTargetAlias = p.TargetAlias;

        _pendingPathSegmentPattern = null;

        // Note: Don't process WHERE clauses here - they will be processed later in Build()
        // after the path segment aliases are set. The pending WHERE clauses will be processed
        // with the correct aliases when FinalizeWhereClause() is called in Build().
    }
}