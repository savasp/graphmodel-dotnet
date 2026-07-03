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

namespace Cvoya.Graph.Model.Age.Querying.Cypher.Visitors;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Cvoya.Graph.Model;
using Cvoya.Graph.Model.Age.Querying.Cypher.Visitors.Core;
using Cvoya.Graph.Model.Cypher.Querying.Cypher.Visitors.Core;
using Microsoft.Extensions.Logging;
using static Cvoya.Graph.Model.Age.Querying.Cypher.Visitors.Core.ExpressionTranslationHelper;

/// <summary>
/// Handles MemberExpression translation to Cypher expressions.
/// Resolves path segment properties, static members, IGrouping keys, and nested property chains.
/// </summary>
internal sealed class MemberExpressionHandler
{
    /// <summary>
    /// Cache for compiled expressions to avoid recompilation of identical expression trees.
    /// Uses ConditionalWeakTable which holds weak references to keys (expressions),
    /// allowing GC to reclaim cached entries when expressions are no longer referenced.
    /// </summary>
    private static readonly ConditionalWeakTable<Expression, object?> ExpressionCache = new();

    private readonly CypherQueryContext _context;
    private readonly ILogger _logger;
    private readonly string _alias;
    private readonly string? _sourceAlias;
    private readonly string? _relationshipAlias;
    private readonly string? _targetAlias;
    private readonly bool _isPathSegmentContext;
    private readonly Func<MemberExpression, bool, (bool success, object? value)> _tryEvaluateStatic;
    private readonly Func<Expression, string> _visitAndReturnCypher;
    private readonly Func<object?, string> _addParameter;

    public MemberExpressionHandler(
        CypherQueryContext context,
        ILogger logger,
        string alias,
        string? sourceAlias,
        string? relationshipAlias,
        string? targetAlias,
        bool isPathSegmentContext,
        Func<MemberExpression, bool, (bool, object?)> tryEvaluateStatic,
        Func<Expression, string> visitAndReturnCypher,
        Func<object?, string> addParameter)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _alias = alias;
        _sourceAlias = sourceAlias;
        _relationshipAlias = relationshipAlias;
        _targetAlias = targetAlias;
        _isPathSegmentContext = isPathSegmentContext;
        _tryEvaluateStatic = tryEvaluateStatic ?? throw new ArgumentNullException(nameof(tryEvaluateStatic));
        _visitAndReturnCypher = visitAndReturnCypher ?? throw new ArgumentNullException(nameof(visitAndReturnCypher));
        _addParameter = addParameter ?? throw new ArgumentNullException(nameof(addParameter));
    }

    public Expression VisitMember(MemberExpression node)
    {
        // Path segment nested access: ps.EndNode.FirstName
        if (TryHandlePathSegmentNestedAccess(node, out var pathResult))
            return pathResult!;

        // Static DateTime properties (DateTime.Now, etc.)
        if (TryHandleStaticDateTime(node, out var dtResult))
            return dtResult!;

        // Parameter member access: p.FirstName, p.Key (IGrouping), ps.StartNode (PathSegment)
        if (node.Expression is ParameterExpression param)
        {
            if (TryHandleParameterMember(node, param, out var paramResult))
                return paramResult!;
        }

        // Nested path segment through nested member: ps.EndNode.FirstName
        if (TryHandleNestedPathSegmentMember(node, out var nestedResult))
            return nestedResult!;

        // Converted parameter: ((IEntity)p).Id
        if (TryHandleConvertedParameter(node, out var convResult))
            return convResult!;

        // Chained member access: p.FirstName.Length, p.Address.City
        if (TryHandleChainedMember(node, out var chainResult))
            return chainResult!;

        // Fallback evaluation
        return FallbackEvaluate(node);
    }

    /// <summary>
    /// Resolves an IGraphPathSegment property name to its corresponding alias.
    /// </summary>
    private string ResolveSegmentAlias(string segmentProperty)
        => segmentProperty switch
        {
            nameof(IGraphPathSegment.StartNode) => _sourceAlias ?? "src0",
            nameof(IGraphPathSegment.EndNode) => _targetAlias ?? "tgt0",
            nameof(IGraphPathSegment.Relationship) => _relationshipAlias ?? "r0",
            _ => throw new NotSupportedException($"Path segment property '{segmentProperty}' is not supported")
        };

    private bool TryHandlePathSegmentNestedAccess(MemberExpression node, out Expression? result)
    {
        result = null;
        if (_isPathSegmentContext &&
            node.Expression is MemberExpression pathSegmentMember &&
            pathSegmentMember.Expression is ParameterExpression pathParam)
        {
            _logger.LogDebug("Processing path segment property access: {Parameter}.{SegmentProperty}.{NodeProperty}",
                pathParam.Name, pathSegmentMember.Member.Name, node.Member.Name);

            var segmentProperty = pathSegmentMember.Member.Name;
            var nodeProperty = node.Member.Name;

            var alias = ResolveSegmentAlias(segmentProperty);

            var resolved = $"{alias}.{MapPropertyName(nodeProperty)}";
            _logger.LogDebug("Mapped path segment property {SegmentProperty}.{NodeProperty} to {Result}",
                segmentProperty, nodeProperty, resolved);
            result = Expression.Constant(resolved);
            return true;
        }
        return false;
    }

    private bool TryHandleStaticDateTime(MemberExpression node, out Expression? result)
    {
        result = null;
        if (node.Member.DeclaringType == typeof(DateTime) && node.Expression == null)
        {
            var (success, evaluated) = _tryEvaluateStatic(node, true);
            if (success)
            {
                result = Expression.Constant(_addParameter(evaluated));
                return true;
            }

            throw new NotSupportedException($"DateTime static property {node.Member.Name} is not supported");
        }
        return false;
    }

    private bool TryHandleParameterMember(MemberExpression node, ParameterExpression param, out Expression? result)
    {
        result = null;

        // IGrouping parameter (g.Key)
        if (param.Type.IsGenericType && param.Type.GetGenericTypeDefinition().Name.Contains("IGrouping"))
        {
            _logger.LogDebug("Processing IGrouping parameter property: {Property}", node.Member.Name);
            if (node.Member.Name == "Key")
            {
                var groupByFragment = _context.FragmentSequence.OfType<GroupByFragment>().LastOrDefault();
                if (groupByFragment == null)
                    throw new InvalidOperationException("GroupBy fragment not found in context for g.Key access");
                _logger.LogDebug("Mapped g.Key to GROUP BY expression: {Expression}", groupByFragment.Expression);
                result = Expression.Constant(groupByFragment.Expression);
                return true;
            }
            throw new NotSupportedException($"IGrouping property '{node.Member.Name}' is not supported. Use g.Key for the grouping key.");
        }

        // Path segment parameter (ps.StartNode)
        if (typeof(IGraphPathSegment).IsAssignableFrom(param.Type))
        {
            _logger.LogDebug("Processing path segment parameter property: {Property}", node.Member.Name);
            var propertyMapping = ResolveSegmentAlias(node.Member.Name);
            _logger.LogDebug("Mapped path segment property {Property} to alias {Alias}", node.Member.Name, propertyMapping);
            result = Expression.Constant(propertyMapping);
            return true;
        }

        // Check if the accessed property is a complex property (INode/IRelationship).
        // If so, route through the cp_ alias from the OptionalMatchFragment for targeted loading.
        var propertyName = MapPropertyName(node.Member.Name);
        var propertyInfo = param.Type.GetProperty(node.Member.Name,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (propertyInfo != null)
        {
            var propType = propertyInfo.PropertyType;
            if (typeof(INode).IsAssignableFrom(propType) || typeof(IRelationship).IsAssignableFrom(propType))
            {
                // Complex property — use cp_ alias
                var cpAlias = $"cp_{propertyName}";
                _logger.LogDebug("Mapped complex property access {Property} to {CpAlias}", node.Member.Name, cpAlias);
                result = Expression.Constant(cpAlias);
                return true;
            }
        }

        // Regular simple property: p.FirstName → alias.FirstName
        result = Expression.Constant($"{_alias}.{propertyName}");
        return true;
    }

    private bool TryHandleNestedPathSegmentMember(MemberExpression node, out Expression? result)
    {
        result = null;
        if (node.Expression is MemberExpression nestedMember &&
            nestedMember.Expression is ParameterExpression nestedParam &&
            typeof(IGraphPathSegment).IsAssignableFrom(nestedParam.Type))
        {
            _logger.LogDebug("Processing nested path segment property access: {Expression}", node);
            var segmentProperty = nestedMember.Member.Name;
            var alias = ResolveSegmentAlias(segmentProperty);

            if (segmentProperty == nameof(IGraphPathSegment.StartNode) && _sourceAlias == null)
                _logger.LogError("CRITICAL: _sourceAlias is null for StartNode access - falling back to 'src'");
            if (segmentProperty == nameof(IGraphPathSegment.Relationship) && _relationshipAlias == null)
                _logger.LogError("CRITICAL: _relationshipAlias is null for Relationship access - falling back to 'r'");

            var resolved = $"{alias}.{MapPropertyName(node.Member.Name)}";
            _logger.LogDebug("Mapped path segment nested property {SegmentProperty}.{NodeProperty} to {Result}",
                segmentProperty, node.Member.Name, resolved);
            result = Expression.Constant(resolved);
            return true;
        }
        return false;
    }

    private bool TryHandleConvertedParameter(MemberExpression node, out Expression? result)
    {
        result = null;
        if (node.Expression is UnaryExpression unary &&
            (unary.NodeType == ExpressionType.Convert || unary.NodeType == ExpressionType.ConvertChecked) &&
            unary.Operand is ParameterExpression)
        {
            result = Expression.Constant($"{_alias}.{MapPropertyName(node.Member.Name)}");
            return true;
        }
        return false;
    }

    private bool TryHandleChainedMember(MemberExpression node, out Expression? result)
    {
        result = null;
        if (node.Expression is MemberExpression innerMember)
        {
            // string.Length
            if (node.Member.Name == "Length" && node.Member.DeclaringType == typeof(string))
            {
                var innerValue = _visitAndReturnCypher(innerMember);
                result = Expression.Constant($"size({innerValue})");
                return true;
            }

            // DateTime properties (Year, Month, Day, etc.)
            if (node.Member.DeclaringType == typeof(DateTime))
            {
                var innerValue = _visitAndReturnCypher(innerMember);
                result = node.Member.Name switch
                {
                    "Year" => Expression.Constant($"toInteger(substring({innerValue}, 0, 4))"),
                    "Month" => Expression.Constant($"toInteger(substring({innerValue}, 5, 2))"),
                    "Day" => Expression.Constant($"toInteger(substring({innerValue}, 8, 2))"),
                    "Hour" => Expression.Constant($"toInteger(substring({innerValue}, 11, 2))"),
                    "Minute" => Expression.Constant($"toInteger(substring({innerValue}, 14, 2))"),
                    "Second" => Expression.Constant($"toInteger(substring({innerValue}, 17, 2))"),
                    "DayOfWeek" => Expression.Constant($"pg_catalog.date_part('dow', CAST(substring(toString({innerValue}), 0, 10) AS date))::integer"),
                    _ => throw new NotSupportedException($"DateTime property {node.Member.Name} is not supported")
                };
                return true;
            }

            // Nested property access (p.Address.City)
            var propertyPath = new List<string>();
            Expression? current = node;
            Expression? baseExpression = null;
            // Track the declaring type of each property in the chain
            var declaringTypes = new List<Type>();

            while (current != null)
            {
                if (current is not MemberExpression currentMember)
                    break;
                propertyPath.Insert(0, MapPropertyName(currentMember.Member.Name));
                declaringTypes.Insert(0, currentMember.Member.DeclaringType ?? typeof(object));

                if (currentMember.Expression is MemberExpression nextMember)
                {
                    current = nextMember;
                }
                else if (currentMember.Expression is ParameterExpression baseParam)
                {
                    baseExpression = baseParam;
                    break;
                }
                else if (currentMember.Expression is UnaryExpression unaryExpr &&
                         (unaryExpr.NodeType == ExpressionType.Convert || unaryExpr.NodeType == ExpressionType.ConvertChecked) &&
                         unaryExpr.Operand is ParameterExpression convertedParam)
                {
                    baseExpression = convertedParam;
                    break;
                }
                else
                {
                    current = null;
                }
            }

            if (baseExpression is ParameterExpression bpExpr)
            {
                // IGrouping chain: group.Key.FirstName
                if (bpExpr.Type.IsGenericType && bpExpr.Type.GetGenericTypeDefinition().Name.Contains("IGrouping"))
                {
                    if (propertyPath.Count > 0 && propertyPath[0] == "Key")
                    {
                        var groupByFragment = _context.FragmentSequence.OfType<GroupByFragment>().LastOrDefault();
                        if (groupByFragment == null)
                            throw new InvalidOperationException("GroupBy fragment not found for IGrouping.Key access");

                        var resolvedKey = groupByFragment.Expression;
                        var remainingPath = propertyPath.Skip(1).ToList();
                        var resolvedPath = remainingPath.Count > 0
                            ? $"{resolvedKey}.{string.Join(".", remainingPath)}"
                            : resolvedKey;
                        _logger.LogDebug("Mapped IGrouping chain access to {Path}", resolvedPath);
                        result = Expression.Constant(resolvedPath);
                        return true;
                    }
                    throw new NotSupportedException($"IGrouping property '{propertyPath.FirstOrDefault()}' is not supported. Use group.Key");
                }

                // Check if the first property in the chain (after the base alias) is a complex property
                // (INode or IRelationship). If so, replace the chain with the cp_ alias from the
                // OptionalMatchFragment to enable targeted complex property loading (§8.2.6).
                if (propertyPath.Count >= 2 && declaringTypes.Count >= 2)
                {
                    var firstProp = propertyPath[0]; // e.g., "HomeAddress"
                    var firstPropDeclaringType = declaringTypes[0]; // e.g., PersonWithAddressNode

                    // Check if firstProp is a complex property on its declaring type
                    var firstPropInfo = firstPropDeclaringType.GetProperty(
                        firstProp,
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (firstPropInfo != null)
                    {
                        var propType = firstPropInfo.PropertyType;
                        if (typeof(INode).IsAssignableFrom(propType) || typeof(IRelationship).IsAssignableFrom(propType))
                        {
                            // This is a complex property - use cp_ alias instead of src0.PropertyName
                            // Example: src0.HomeAddress.Street → cp_HomeAddress.Street
                            var remainingPath = string.Join(".", propertyPath.Skip(1));
                            var cpAlias = $"cp_{firstProp}";
                            var resolvedPath = string.IsNullOrEmpty(remainingPath)
                                ? cpAlias
                                : $"{cpAlias}.{remainingPath}";
                            _logger.LogDebug("Mapped complex property chain access to {Path}", resolvedPath);
                            result = Expression.Constant(resolvedPath);
                            return true;
                        }
                    }
                }

                var fp = $"{_alias}.{string.Join(".", propertyPath)}";
                _logger.LogDebug("Mapped nested property access to {Path}", fp);
                result = Expression.Constant(fp);
                return true;
            }
        }
        return false;
    }

    private Expression FallbackEvaluate(MemberExpression node)
    {
        try
        {
            var value = ExpressionCache.GetValue(node, static key =>
            {
                var objectMember = Expression.Convert(key, typeof(object));
                var getterLambda = Expression.Lambda<Func<object>>(objectMember);
                return getterLambda.Compile()();
            });

            var paramRef = _addParameter(value);
            return Expression.Constant(paramRef);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to evaluate member expression: {Expression}", node);
            throw new NotSupportedException($"Cannot evaluate member expression '{node}': {ex.Message}", ex);
        }
    }
}
