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

namespace Cvoya.Graph.Model.Age.Querying.Cypher.Visitors.Core;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Cvoya.Graph.Model;
using Microsoft.Extensions.Logging;

/// <summary>
/// Handles query initialization for AGE Cypher queries.
/// Sets up MATCH patterns, complex properties, and relationship type filters
/// based on the entity type being queried.
/// </summary>
internal sealed class QueryInitializationHandler
{
    private readonly CypherQueryContext _context;
    private readonly ILogger _logger;
    private readonly Func<string> _getContextualAlias;
    private readonly Action<string, string?, ImmutableArray<string>> _emitWhereFragment;

    public QueryInitializationHandler(
        CypherQueryContext context,
        ILogger logger,
        Func<string> getContextualAlias,
        Action<string, string?, ImmutableArray<string>> emitWhereFragment)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _getContextualAlias = getContextualAlias ?? throw new ArgumentNullException(nameof(getContextualAlias));
        _emitWhereFragment = emitWhereFragment ?? throw new ArgumentNullException(nameof(emitWhereFragment));
    }

    /// <summary>
    /// Sets up the initial MATCH clause based on the element type.
    /// Skips if match fragments already exist (use <see cref="SetupAdditionalMatch"/> to force).
    /// </summary>
    public void SetupInitialMatch(Type elementType)
    {
        // Skip setup if we already have path patterns (e.g., from PathSegments)
        if (_context.HasMatchFragments())
        {
            _logger.LogDebug("Skipping initial match setup - match fragments already exist");
            return;
        }

        SetupMatchInternal(elementType);
    }

    /// <summary>
    /// Sets up an additional MATCH clause for JOIN operations, bypassing the
    /// "has match fragments" guard. Visits the inner queryable to emit its
    /// MatchRootFragment independently of any existing match fragments.
    /// </summary>
    public void SetupAdditionalMatch(Type elementType)
    {
        _logger.LogDebug("Setting up additional match for {Type}", elementType.Name);
        SetupMatchInternal(elementType);
    }

    /// <summary>
    /// Core match setup logic shared by <see cref="SetupInitialMatch"/> and <see cref="SetupAdditionalMatch"/>.
    /// </summary>
    private void SetupMatchInternal(Type elementType)
    {
        if (typeof(INode).IsAssignableFrom(elementType))
        {
            // Node query: MATCH (n:BaseLabel) 
            // For AGE inheritance support, always use base type label
            var baseLabel = Labels.GetBaseTypeLabel(elementType);
            var alias = _context.Scope.CurrentAlias ?? _getContextualAlias();
            // Set CurrentAlias so subsequent operations (WHERE, SELECT, etc.) use the correct alias
            _context.Scope.CurrentAlias = alias;

            // Emit MatchRootFragment for root node queries
            var pattern = $"({alias}:{baseLabel})";
            var fragment = new MatchRootFragment(pattern, baseLabel, elementType, ImmutableArray.Create(alias), alias);
            _context.AddFragment(fragment);
            _logger.LogDebug("Emitted MatchRootFragment for {Type} with alias {Alias}", elementType.Name, alias);

            // Add inheritance filter if querying for a derived type
            var actualLabel = Labels.GetLabelFromType(elementType);
            if (baseLabel != actualLabel)
            {
                // Add WHERE clause to filter by inheritance hierarchy
                var inheritanceFilter = $"'{actualLabel}' IN {alias}.inheritance_labels";
                _emitWhereFragment(inheritanceFilter, alias, ImmutableArray.Create(alias));
                _logger.LogDebug("Emitted inheritance filter: {Filter}", inheritanceFilter);
            }

            // Set up complex properties for node types
            SetupComplexProperties(elementType, alias);

            _logger.LogDebug("Set up node match for type {Type} with base label {BaseLabel}", elementType.Name, baseLabel);
        }
        else if (typeof(IRelationship).IsAssignableFrom(elementType))
        {
            var alias = _context.Scope.CurrentAlias ?? _context.Scope.GetNumberedAlias("r");
            var srcAlias = _context.Scope.GetNumberedAlias("src");
            var tgtAlias = _context.Scope.GetNumberedAlias("tgt");
            var relationshipLabel = GetRelationshipLabel(elementType);
            var relationshipPattern = string.IsNullOrEmpty(relationshipLabel)
                ? alias
                : $"{alias}:{relationshipLabel}";
            var pattern = $"({srcAlias})-[{relationshipPattern}]->({tgtAlias})";
            // Set CurrentAlias so subsequent operations use the correct alias
            _context.Scope.CurrentAlias = alias;

            // Emit MatchRootFragment for root relationship queries
            var fragment = new MatchRootFragment(pattern, relationshipLabel, elementType, ImmutableArray.Create(srcAlias, alias, tgtAlias), alias);
            _context.AddFragment(fragment);
            _logger.LogDebug("Emitted MatchRootFragment for {Type} with alias {Alias}", elementType.Name, alias);

            AddRelationshipTypeFilter(elementType, alias);

            var labelForLog = string.IsNullOrEmpty(relationshipLabel) ? "*" : relationshipLabel;
            _logger.LogDebug("Set up relationship match for type {Type} with label {Label} using aliases: {SrcAlias}-[{RelAlias}]->{TgtAlias}",
                elementType.Name, labelForLog, srcAlias, alias, tgtAlias);
        }
        else
        {
            throw new NotSupportedException($"Query type {elementType.Name} is not supported. Only INode and IRelationship types are supported.");
        }
    }

    private void SetupComplexProperties(Type nodeType, string alias)
    {
        var complexProps = GetComplexProperties(nodeType);
        if (complexProps.Count == 0) return;

        _logger.LogDebug("Setting up {Count} complex properties for {Type}", complexProps.Count, nodeType.Name);

        foreach (var prop in complexProps)
        {
            var relType = GraphDataModel.PropertyNameToRelationshipTypeName(prop.Name);
            // Pattern does NOT include "OPTIONAL MATCH " prefix — the renderer adds it automatically.
            // Including it would cause a doubled prefix in the final output.
            var optionalPattern = $"({alias})-[r_{prop.Name}:{relType}]->(cp_{prop.Name})";
            var optionalFragment = new OptionalMatchFragment(
                optionalPattern,
                ImmutableArray.Create($"r_{prop.Name}", $"cp_{prop.Name}"),
                ImmutableArray.Create(alias),
                alias);
            _context.AddFragment(optionalFragment);
            _logger.LogDebug("Emitted optional match fragment for complex property '{Property}': {Match}", prop.Name, optionalPattern);
        }
    }

    /// <summary>
    /// Thread-safe cache of complex properties per type. Once computed for a type,
    /// the result is reused across all queries, avoiding repeated reflection overhead.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, IReadOnlyList<PropertyInfo>> ComplexPropertiesCache = new();

    private static IReadOnlyList<PropertyInfo> GetComplexProperties(Type type)
    {
        return ComplexPropertiesCache.GetOrAdd(type, static t =>
        {
            return t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => typeof(INode).IsAssignableFrom(p.PropertyType) ||
                           typeof(IRelationship).IsAssignableFrom(p.PropertyType) ||
                           (p.PropertyType.IsGenericType &&
                            p.PropertyType.GetGenericTypeDefinition() == typeof(ICollection<>) &&
                            (typeof(INode).IsAssignableFrom(p.PropertyType.GetGenericArguments()[0]) ||
                             typeof(IRelationship).IsAssignableFrom(p.PropertyType.GetGenericArguments()[0]))))
                .ToList();
        });
    }

    /// <summary>
    /// Gets the appropriate relationship label for Cypher queries.
    /// For interface types, returns empty string (no type filtering in pattern - will use WHERE clause instead).
    /// For concrete types, returns the base type label.
    /// </summary>
    private string GetRelationshipLabel(Type relationshipType)
    {
        if (IsUnspecifiedRelationshipType(relationshipType))
        {
            _logger.LogDebug("Relationship type {TypeName} requests unspecified pattern", relationshipType.Name);
            return string.Empty;
        }

        // For interface types, we don't specify the type in the pattern
        // Instead, we'll add a WHERE clause later to filter by inheritance_labels property
        if (relationshipType.IsInterface)
        {
            var interfaceLabel = Labels.GetLabelFromType(relationshipType);
            _logger.LogDebug("Interface relationship type {TypeName} mapped to label: {Label}",
                relationshipType.Name, interfaceLabel);

            // Store the interface label for later use in WHERE clause
            // We'll use the more efficient inheritance_labels property approach
            return "";
        }

        // For concrete types, use the base type label
        return Labels.GetBaseTypeLabel(relationshipType);
    }

    /// <summary>
    /// Adds a WHERE clause to filter relationships by type when dealing with interface types.
    /// </summary>
    private void AddRelationshipTypeFilter(Type relationshipType, string relationshipAlias)
    {
        if (!relationshipType.IsInterface || IsUnspecifiedRelationshipType(relationshipType))
        {
            return;
        }

        var interfaceLabel = Labels.GetLabelFromType(relationshipType);

        // Use standard Cypher IN syntax for AGE compatibility
        // Generate: WHERE 'IRelationship' IN r0.inheritance_labels
        var whereClause = $"'{interfaceLabel}' IN {relationshipAlias}.inheritance_labels";
        _emitWhereFragment(whereClause, relationshipAlias, ImmutableArray.Create(relationshipAlias));
        _logger.LogDebug("Emitted inheritance-based relationship filter: {WhereClause}", whereClause);
    }

    private static bool IsUnspecifiedRelationshipType(Type relationshipType)
    {
        return relationshipType == typeof(IRelationship) || relationshipType == typeof(Relationship);
    }
}
