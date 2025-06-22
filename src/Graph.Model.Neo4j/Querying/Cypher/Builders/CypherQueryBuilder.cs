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
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Core;
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Expressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

internal class CypherQueryBuilder(CypherQueryContext context)
{
    private readonly ILogger<CypherQueryBuilder> _logger = context.LoggerFactory?.CreateLogger<CypherQueryBuilder>()
        ?? NullLogger<CypherQueryBuilder>.Instance;

    private readonly List<(LambdaExpression Lambda, string Alias)> _pendingWhereClauses = [];

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

    private readonly List<string> _matchClauses = [];
    private readonly List<string> _whereClauses = [];
    private readonly List<string> _returnClauses = [];
    private readonly List<(string Expression, bool IsDescending)> _orderByClauses = [];
    private readonly Dictionary<string, object?> _parameters = [];
    private readonly List<string> _optionalMatchClauses = [];
    private readonly List<string> _withClauses = [];
    private readonly List<string> _unwindClauses = [];
    private readonly List<string> _groupByClauses = [];
    private int? _limit;
    private int? _skip;
    private bool _isDistinct;
    private bool _isRelationshipQuery;
    private int? _minDepth;
    private int? _maxDepth;

    private string? _aggregation;
    private bool _isExistsQuery;
    private bool _isNotExistsQuery;
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

    public bool HasUserProjections { get; set; } = false;

    public bool HasExplicitReturn => _returnClauses.Count > 0 || _aggregation != null || _isExistsQuery || _isNotExistsQuery;

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
        if (!string.IsNullOrEmpty(alias))
        {
            _pendingWhereClauses.Add((lambda, alias));
        }
    }

    public void SetExistsQuery()
    {
        _isExistsQuery = true;
    }

    public void SetNotExistsQuery()
    {
        _isNotExistsQuery = true;
    }

    public void AddMatch(string alias, string? label = null, string? pattern = null)
    {
        if (RootNodeAlias is null)
        {
            RootNodeAlias = alias;
        }

        var match = new StringBuilder($"({alias}");

        if (!string.IsNullOrEmpty(label))
        {
            match.Append($":{label}");
        }

        match.Append(')');

        if (!string.IsNullOrEmpty(pattern))
        {
            match.Append(pattern);
        }

        _matchClauses.Add(match.ToString());

        // Keep track of the main node alias
        _mainNodeAlias ??= alias;
    }

    public void AddMatchPattern(string fullPattern)
    {
        // For relationship patterns, ensure we don't duplicate them
        if (fullPattern.Contains("-[") && _matchClauses.Any(c => c.Contains("-[") && c.Contains("]->")))
        {
            return; // Skip if we already have a relationship pattern
        }
        _matchClauses.Add(fullPattern);
    }

    public void EnableComplexPropertyLoading()
    {
        _includeComplexProperties = true;
    }

    public void EnablePathSegmentLoading()
    {
        _includeComplexProperties = true;
        _loadPathSegment = true;
    }

    public void ClearMatches()
    {
        _matchClauses.Clear();
    }

    public void ClearUserProjections()
    {
        HasUserProjections = false;
        ClearReturn();
    }

    public void ClearWhere()
    {
        _whereClauses.Clear();
    }

    public void AddWhere(string condition)
    {
        // Don't add duplicate WHERE clauses
        if (!_whereClauses.Contains(condition))
        {
            _whereClauses.Add(condition);
        }
    }

    public void ClearReturn()
    {
        _returnClauses.Clear();
    }

    public void SetMainNodeAlias(string alias)
    {
        _mainNodeAlias = alias;
    }

    public void AddOrderBy(string expression, bool isDescending = false)
    {
        _orderByClauses.Add((expression, isDescending));
    }

    public void SetSkip(int skip) => _skip = skip;
    public void SetLimit(int limit) => _limit = limit;
    public void SetAggregation(string function, string expression) => _aggregation = $"{function}({expression})";

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
        if (_isExistsQuery)
        {
            return BuildExistsQuery();
        }

        if (_isNotExistsQuery)
        {
            return BuildNotExistsQuery();
        }

        // Handle complex properties if needed
        if (_includeComplexProperties)
        {
            FinalizeWhereClause();

            return BuildWithComplexProperties();
        }

        // Otherwise build a simple query
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

        // Use the same pattern as path segments
        _matchClauses.Add($"(src)-[r:{relationshipType}]->(tgt)");
        _mainNodeAlias = "r"; // Set main alias to the relationship

        _isRelationshipQuery = true;

        // Enable path segment loading since relationships are path segments
        EnablePathSegmentLoading();
    }

    public bool HasOrderBy => _orderByClauses.Any();

    public PathSegmentProjectionEnum GetPathSegmentProjection()
    {
        return PathSegmentProjection;
    }

    public void AddOptionalMatch(string pattern)
    {
        _logger.LogDebug("AddOptionalMatch called with pattern: '{Pattern}'", pattern);
        _optionalMatchClauses.Add(pattern);
    }

    public void AddLimit(int limit)
    {
        _logger.LogDebug("AddLimit called with value: {Limit}", limit);
        _limit = limit;
    }

    public void AddSkip(int skip)
    {
        _logger.LogDebug("AddSkip called with value: {Skip}", skip);
        _skip = skip;
    }

    public void AddWith(string expression)
    {
        _logger.LogDebug("AddWith called with expression: '{Expression}'", expression);
        _withClauses.Add(expression);
    }

    public void AddUnwind(string expression)
    {
        _logger.LogDebug("AddUnwind called with expression: '{Expression}'", expression);
        _unwindClauses.Add(expression);
    }

    public void AddGroupBy(string expression)
    {
        _logger.LogDebug("AddGroupBy called with expression: '{Expression}'", expression);
        _groupByClauses.Add(expression);
    }

    public void SetDistinct(bool distinct)
    {
        _logger.LogDebug("SetDistinct called with value: {Value}", distinct);
        _isDistinct = distinct;
    }

    public bool HasReturnClause => _returnClauses.Any();

    public void AddReturn(string expression, string? alias = null)
    {
        if (!string.IsNullOrWhiteSpace(alias))
        {
            _returnClauses.Add($"{expression} AS {alias}");
        }
        else
        {
            _returnClauses.Add(expression);
        }
    }

    public void AddUserProjection(string expression, string? alias = null)
    {
        HasUserProjections = true;
        AddReturn(expression, alias);
    }

    public void AddInfrastructureReturn(string expression, string? alias = null)
    {
        // This is for infrastructure returns like path segments - don't mark as user projection
        AddReturn(expression, alias);
    }

    public void ReverseOrderBy()
    {
        _logger.LogDebug("Reversing ORDER BY clauses");

        // Flip the IsDescending flag for all order clauses
        for (int i = 0; i < _orderByClauses.Count; i++)
        {
            var (expression, isDescending) = _orderByClauses[i];
            _orderByClauses[i] = (expression, !isDescending);
        }
    }

    private void FinalizeWhereClause()
    {
        foreach (var (lambda, alias) in _pendingWhereClauses)
        {
            _logger.LogDebug("Processing pending WHERE clause with alias: {Alias}", alias);

            // Map the original alias to the actual alias used in the pattern
            var actualAlias = GetActualAlias(alias);
            _logger.LogDebug("Mapped alias {Original} to {Actual}", alias, actualAlias);

            // Use the factory to create the proper visitor chain with the specific alias
            var expressionVisitor = new ExpressionVisitorChainFactory(context)
                .CreateWhereClauseChain(actualAlias);
            var whereExpression = expressionVisitor.Visit(lambda.Body);
            AddWhere(whereExpression);
        }

        _pendingWhereClauses.Clear();
    }

    private string GetActualAlias(string originalAlias)
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

        return originalAlias;
    }

    private static bool NeedsComplexPropertiesRecursive(Type type, HashSet<Type> visited)
    {
        if (type is null || !visited.Add(type))
            return false;

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
        _logger.LogDebug("Building simple query");

        var query = new StringBuilder();

        // Build the main query structure
        AppendMatchClauses(query);
        AppendWhereClauses(query);
        AppendReturnClause(query);
        AppendOrderByClauses(query);
        AppendPaginationClauses(query);

        return new CypherQuery(query.ToString().Trim(), new Dictionary<string, object?>(_parameters));
    }

    private void AppendMatchClauses(StringBuilder query)
    {
        _logger.LogDebug("Appending MATCH clauses");

        if (_matchClauses.Count > 0)
        {
            query.Append("MATCH ");
            query.AppendJoin(", ", _matchClauses);
            query.AppendLine();
        }

        // Add optional matches if any
        foreach (var optionalMatch in _optionalMatchClauses)
        {
            query.AppendLine($"OPTIONAL MATCH {optionalMatch}");
        }
    }

    private void AppendWhereClauses(StringBuilder query)
    {
        _logger.LogDebug("Appending WHERE clauses");

        if (_whereClauses.Count > 0)
        {
            query.Append("WHERE ");
            query.AppendJoin(" AND ", _whereClauses);
            query.AppendLine();
        }
    }

    private void AppendReturnClause(StringBuilder query)
    {
        _logger.LogDebug("Appending RETURN clause");

        // Handle WITH clauses first
        foreach (var withClause in _withClauses)
        {
            query.AppendLine($"WITH {withClause}");
        }

        // Handle UNWIND clauses
        foreach (var unwindClause in _unwindClauses)
        {
            query.AppendLine($"UNWIND {unwindClause}");
        }

        // Build the RETURN clause
        query.Append("RETURN ");

        if (_aggregation != null)
        {
            query.Append(_aggregation);
        }
        else if (_returnClauses.Count > 0)
        {
            var returnExpression = _isDistinct
                ? $"DISTINCT {string.Join(", ", _returnClauses)}"
                : string.Join(", ", _returnClauses);
            query.Append(returnExpression);
        }
        else
        {
            // Default return - let the complex property handling take care of structured returns
            query.Append(_mainNodeAlias ?? "src");
        }

        query.AppendLine();

        // Add GROUP BY if needed
        if (_groupByClauses.Count > 0)
        {
            query.Append("GROUP BY ");
            query.AppendJoin(", ", _groupByClauses);
            query.AppendLine();
        }
    }

    private void AppendOrderByClauses(StringBuilder query)
    {
        _logger.LogDebug("Appending ORDER BY clauses");

        if (_orderByClauses.Count > 0)
        {
            query.Append("ORDER BY ");
            var orderByParts = _orderByClauses.Select(o =>
                o.IsDescending ? $"{o.Expression} DESC" : o.Expression);
            query.AppendJoin(", ", orderByParts);
            query.AppendLine();
        }
    }

    private void AppendPaginationClauses(StringBuilder query)
    {
        _logger.LogDebug("Appending pagination clauses");

        if (_skip.HasValue)
        {
            query.AppendLine($"SKIP {_skip.Value}");
        }

        if (_limit.HasValue)
        {
            query.AppendLine($"LIMIT {_limit.Value}");
        }
    }

    private CypherQuery BuildExistsQuery()
    {
        _logger.LogDebug("Building EXISTS query");

        var query = new StringBuilder();

        AppendMatchClauses(query);
        AppendWhereClauses(query);

        // For EXISTS queries, we return a count > 0
        query.AppendLine($"RETURN COUNT({_mainNodeAlias ?? "src"}) > 0 AS exists");

        return new CypherQuery(query.ToString().Trim(), new Dictionary<string, object?>(_parameters));
    }

    private CypherQuery BuildNotExistsQuery()
    {
        _logger.LogDebug("Building NOT EXISTS query");

        var query = new StringBuilder();

        AppendMatchClauses(query);
        AppendWhereClauses(query);

        // For NOT EXISTS queries (used by All), return true if no matching nodes exist
        query.AppendLine($"RETURN COUNT({_mainNodeAlias ?? "src"}) = 0 AS all");

        return new CypherQuery(query.ToString().Trim(), new Dictionary<string, object?>(_parameters));
    }

    private CypherQuery BuildWithComplexProperties()
    {
        _logger.LogDebug("Building query with complex properties");

        var query = new StringBuilder();

        // First part: get the main nodes
        AppendMatchClauses(query);
        AppendWhereClauses(query);

        // For path segments, we don't need the intermediate WITH clause
        if (_loadPathSegment)
        {
            // Skip the WITH clause for path segments - go straight to complex property loading
            AppendComplexPropertyMatchesForPathSegment(query);
        }
        else if (!string.IsNullOrEmpty(_mainNodeAlias))
        {
            // For regular node queries, collect main nodes with ordering and pagination
            AppendOrderByClauses(query);
            AppendPaginationClauses(query);
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
                    Property: nodes(tgt_path)[i+1]
                }}]
            END AS tgt_flat_property
        WITH {currentWith},
            reduce(flat = [], l IN collect(tgt_flat_property) | flat + l) AS tgt_flat_properties
        WITH {currentWith}, apoc.coll.toSet(tgt_flat_properties) AS tgt_flat_properties");
        }

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

    private void BuildPendingPathSegmentPattern()
    {
        if (_pendingPathSegmentPattern is null) return;

        var p = _pendingPathSegmentPattern;
        var sourceLabel = Labels.GetLabelFromType(p.SourceType);
        var relLabel = Labels.GetLabelFromType(p.RelType);
        var targetLabel = Labels.GetLabelFromType(p.TargetType);

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

        ClearMatches();
        AddMatchPattern(pattern);

        PathSegmentSourceAlias = p.SourceAlias;
        PathSegmentRelationshipAlias = p.RelAlias;
        PathSegmentTargetAlias = p.TargetAlias;

        _pendingPathSegmentPattern = null;
    }
}