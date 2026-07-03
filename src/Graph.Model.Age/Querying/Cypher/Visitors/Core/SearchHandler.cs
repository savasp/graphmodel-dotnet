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
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using Cvoya.Graph.Model.Age.Querying.Linq.Queryables;
using Microsoft.Extensions.Logging;

/// <summary>
/// Handles full-text search operations for AGE Cypher queries.
/// Supports Search() LINQ method and AgeFullTextSearchExpression.
/// </summary>
internal sealed class SearchHandler
{
    private readonly CypherQueryContext _context;
    private readonly ILogger _logger;
    private readonly Func<Expression, Expression> _visit;
    private readonly Action<Type> _setupInitialMatch;
    private readonly Action<string, string?, ImmutableArray<string>> _emitWhereFragment;

    public SearchHandler(
        CypherQueryContext context,
        ILogger logger,
        Func<Expression, Expression> visit,
        Action<Type> setupInitialMatch,
        Action<string, string?, ImmutableArray<string>> emitWhereFragment)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _visit = visit ?? throw new ArgumentNullException(nameof(visit));
        _setupInitialMatch = setupInitialMatch ?? throw new ArgumentNullException(nameof(setupInitialMatch));
        _emitWhereFragment = emitWhereFragment ?? throw new ArgumentNullException(nameof(emitWhereFragment));
    }

    /// <summary>
    /// Handles the Search() LINQ method call, building a regex-based WHERE clause
    /// over string properties of the queried entity type.
    /// </summary>
    public Expression HandleSearch(MethodCallExpression node)
    {
        // Visit source FIRST to ensure CurrentAlias is set and MATCH pattern is created
        _visit(node.Arguments[0]);

        // Search(query) method has the search term as the second argument
        if (node.Arguments.Count >= 2)
        {
            // Try to extract the search query string
            string? searchQuery = null;
            if (node.Arguments[1] is ConstantExpression constExpr)
                searchQuery = constExpr.Value as string;

            if (searchQuery != null)
            {
                var alias = _context.Scope.CurrentAlias ?? "src0";
                var searchQueryParam = _context.ParameterStore.Add(searchQuery);
                var elementType = _context.Scope.RootType;
                var stringProperties = elementType.GetProperties()
                    .Where(p => p.PropertyType == typeof(string) && p.CanRead)
                    .Where(p => IsIncludedInFullTextSearch(elementType, p.Name))
                    .Select(p => p.Name)
                    .ToList();

                if (stringProperties.Count > 0)
                {
                    var conditions = stringProperties
                        .Select(prop => $"{alias}.{prop} =~ '(?i)\\\\m' + {searchQueryParam} + '\\\\M'")
                        .ToList();
                    var whereCondition = string.Join(" OR ", conditions);
                    _emitWhereFragment(whereCondition, alias, ImmutableArray.Create(alias));
                    _logger.LogDebug("Emitted Search WHERE: {Condition}", whereCondition);
                }
                else
                {
                    _emitWhereFragment($"toString({alias}) =~ '(?i)\\\\m' + {searchQueryParam} + '\\\\M'", alias, ImmutableArray.Create(alias));
                }
            }
        }

        return node;
    }

    /// <summary>
    /// Handles an AgeFullTextSearchExpression by building a WHERE clause
    /// over string properties matching the search query.
    /// </summary>
    public void HandleAgeFullTextSearch(AgeFullTextSearchExpression searchExpr)
    {
        var entityType = searchExpr.EntityType;
        var searchQuery = searchExpr.SearchQuery;

        // For IEntity (all entities search), search both nodes and relationships
        if (entityType == typeof(IEntity))
        {
            HandleAllEntitiesTextSearch(searchQuery);
            return;
        }

        // Set up the initial MATCH pattern for the entity type
        _setupInitialMatch(entityType);

        // Get the alias set up by SetupInitialMatch
        var alias = _context.Scope.CurrentAlias ?? "src0";

        // Build a WHERE condition checking string properties with IncludeInFullTextSearch != false.
        var stringProperties = entityType.GetProperties()
            .Where(p => p.PropertyType == typeof(string) && p.CanRead)
            .Where(p => IsIncludedInFullTextSearch(entityType, p.Name))
            .Select(p => p.Name)
            .ToList();

        var searchQueryParam = _context.ParameterStore.Add(searchQuery);

        if (stringProperties.Count > 0)
        {
            var conditions = stringProperties
                .Select(prop => $"{alias}.{prop} =~ '(?i)\\\\m' + {searchQueryParam} + '\\\\M'")
                .ToList();

            var whereCondition = string.Join(" OR ", conditions);
            _emitWhereFragment(whereCondition, alias, ImmutableArray.Create(alias));
            _logger.LogDebug("Emitted full text search WHERE: {Condition}", whereCondition);
        }
        else
        {
            _emitWhereFragment($"toString({alias}) =~ '(?i)\\\\m' + {searchQueryParam} + '\\\\M'", alias, ImmutableArray.Create(alias));
            _logger.LogDebug("Emitted fallback full text search WHERE for alias {Alias}", alias);
        }
    }

    /// <summary>
    /// Handles full-text search across all entities (both nodes and relationships).
    /// Creates MATCH patterns to cover both entity types.
    /// </summary>
    private void HandleAllEntitiesTextSearch(string searchQuery)
    {
        // For all-entities search, we search nodes
        _setupInitialMatch(typeof(INode));
        var alias = _context.Scope.CurrentAlias ?? "src0";
        var searchQueryParam = _context.ParameterStore.Add(searchQuery);
        _emitWhereFragment($"toString({alias}) =~ '(?i).*' + {searchQueryParam} + '.*'", alias, ImmutableArray.Create(alias));
    }

    /// <summary>
    /// Checks whether a property should be included in full-text search queries.
    /// If IncludeInFullTextSearch is explicitly set to false (in the SchemaRegistry),
    /// the property is excluded.
    /// </summary>
    private bool IsIncludedInFullTextSearch(Type entityType, string propertyName)
    {
        var schemaRegistry = _context.SchemaRegistry;
        if (schemaRegistry == null)
            return true; // No schema available, include by default

        var label = Labels.GetLabelFromType(entityType);
        var schema = schemaRegistry.GetNodeSchema(label)
                  ?? schemaRegistry.GetRelationshipSchema(label) as EntitySchemaInfo;
        if (schema?.Properties == null)
            return true;

        // Look up by C# property name (schema dictionary key)
        if (schema.Properties.TryGetValue(propertyName, out var propSchema))
        {
            return propSchema.IncludeInFullTextSearch != false;
        }

        return true; // Property not in schema, include by default
    }
}
