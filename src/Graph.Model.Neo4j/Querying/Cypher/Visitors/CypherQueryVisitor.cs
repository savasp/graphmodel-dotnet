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

namespace Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors;

using System.Linq.Expressions;
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Builders;
using Cvoya.Graph.Model.Neo4j.Querying.Linq.Queryables;
using Cvoya.Graph.Model.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

internal sealed class CypherQueryVisitor : ExpressionVisitor
{
    private readonly EntityFactory _entityFactory;
    private readonly CypherQueryBuilder _queryBuilder;
    private readonly CypherQueryScope _scope;
    private readonly ILogger<CypherQueryVisitor> _logger;
    private readonly ILoggerFactory? _loggerFactory;

    public CypherQueryVisitor(EntityFactory entityFactory, ILoggerFactory? loggerFactory = null)
    {
        _entityFactory = entityFactory ?? throw new ArgumentNullException(nameof(entityFactory));
        _queryBuilder = new CypherQueryBuilder();
        _scope = new CypherQueryScope();
        _loggerFactory = loggerFactory;
        _logger = loggerFactory?.CreateLogger<CypherQueryVisitor>() ?? NullLogger<CypherQueryVisitor>.Instance;
    }

    public CypherQuery Build()
    {
        // Only add a return clause if none exists and we're not dealing with a relationship query
        if (!_queryBuilder.HasReturnClause && !typeof(IRelationship).IsAssignableFrom(_scope.CurrentType))
        {
            var alias = _scope.CurrentAlias
                ?? throw new InvalidOperationException("No current alias set when building return clause");
            _queryBuilder.AddReturn(alias);
        }

        return _queryBuilder.Build();
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        _logger.LogDebug("Visiting method: {MethodName}", node.Method.Name);

        // Visit the source first (this processes the expression tree bottom-up)
        var source = node.Object ?? (node.Arguments.Count > 0 ? node.Arguments[0] : null);
        if (source != null)
        {
            Visit(source);
        }

        // Then handle this method
        var handled = node.Method.Name switch
        {
            "Where" => HandleWhere(node),
            "Select" => HandleSelect(node),
            "SelectMany" => HandleSelectMany(node),
            "OrderBy" or "OrderByDescending" => HandleOrderBy(node, isDescending: node.Method.Name.Contains("Descending")),
            "ThenBy" or "ThenByDescending" => HandleOrderBy(node, isDescending: node.Method.Name.Contains("Descending"), isThenBy: true),
            "Take" => HandleTake(node),
            "Skip" => HandleSkip(node),
            "First" or "FirstOrDefault" or "Single" or "SingleOrDefault" or "Last" or "LastOrDefault" => HandleFirst(node),
            "Count" or "LongCount" => HandleCount(node),
            "Any" => HandleAny(node),
            "All" => HandleAll(node),
            "Distinct" => HandleDistinct(node),
            "GroupBy" => HandleGroupBy(node),
            "Traverse" => HandleTraverse(node),
            "Relationships" => HandleRelationships(node),
            "PathSegments" => HandlePathSegments(node),
            _ => false
        };

        if (!handled)
        {
            _logger.LogWarning("Unhandled method: {MethodName}", node.Method.Name);
            return base.VisitMethodCall(node);
        }

        return node;
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        // This is typically the root of our query
        if (node.Value is IGraphQueryable queryable)
        {
            var elementType = queryable.ElementType;
            InitializeQuery(elementType);
        }

        return node;
    }

    private void InitializeQuery(Type rootType)
    {
        _logger.LogDebug("Initializing query for type: {TypeName}", rootType.Name);
        _scope.CurrentType = rootType;

        if (typeof(INode).IsAssignableFrom(rootType))
        {
            var alias = _scope.GetOrCreateAlias(rootType, "n");
            var label = Labels.GetLabelFromType(rootType);
            _queryBuilder.AddMatch(alias, label); // Pass alias and label separately!
            _scope.CurrentAlias = alias;

            // Check if we need to include complex properties
            AddComplexPropertyMatches(rootType, alias);
            _queryBuilder.EnableComplexPropertyLoading();
        }
        else if (typeof(IRelationship).IsAssignableFrom(rootType))
        {
            // For relationship queries, we'll use a single pattern
            var srcAlias = _scope.GetOrCreateAlias(typeof(IEntity), "src");
            var relAlias = _scope.GetOrCreateAlias(rootType, "r");
            var tgtAlias = _scope.GetOrCreateAlias(typeof(INode), "tgt");

            var relType = Labels.GetLabelFromType(rootType);

            // Add a single pattern match
            _queryBuilder.AddMatchPattern($"({srcAlias})-[{relAlias}:{relType}]->({tgtAlias})");

            // Add return clause with all three elements for proper materialization
            _queryBuilder.AddReturn($"{srcAlias}, {relAlias}, {tgtAlias}");

            // Set the current alias to the relationship for further operations
            _scope.CurrentAlias = relAlias;
        }
    }

    private void AddComplexPropertyMatches(Type nodeType, string nodeAlias)
    {
        if (!_entityFactory.CanDeserialize(nodeType))
            return;

        var schema = _entityFactory.GetSchema(nodeType);
        if (schema is null)
        {
            _logger.LogWarning("No schema found for type: {TypeName}", nodeType.Name);
            return;
        }

        if (!schema.ComplexProperties.Any())
            return;

        foreach (var (propName, propInfo) in schema.ComplexProperties)
        {
            var relType = GraphDataModel.PropertyNameToRelationshipTypeName(propName);
            _queryBuilder.AddOptionalMatch($"path = ({nodeAlias})-[:{relType}]->()");
        }
    }

    private bool HandleWhere(MethodCallExpression node)
    {
        if (node.Arguments.Count < 2) return false;

        var predicate = StripQuotes(node.Arguments[1]);
        if (predicate is LambdaExpression lambda)
        {
            // Clear any existing WHERE clauses before processing
            _queryBuilder.ClearWhere();

            var visitor = new WhereVisitor(_scope, _queryBuilder, _loggerFactory);
            visitor.ProcessWhereClause(lambda);
            return true;
        }

        return false;
    }

    private bool HandleSelect(MethodCallExpression node)
    {
        if (node.Arguments.Count < 2) return false;

        var selector = StripQuotes(node.Arguments[1]);
        if (selector is LambdaExpression lambda)
        {
            var visitor = new SelectVisitor(_scope, _queryBuilder, _loggerFactory);
            visitor.Visit(lambda);
            return true;
        }

        return false;
    }

    private bool HandleSelectMany(MethodCallExpression node)
    {
        if (node.Arguments.Count < 2) return false;

        var collectionSelector = StripQuotes(node.Arguments[1]) as LambdaExpression;
        LambdaExpression? resultSelector = null;

        if (node.Arguments.Count > 2)
        {
            resultSelector = StripQuotes(node.Arguments[2]) as LambdaExpression;
        }

        if (collectionSelector != null)
        {
            var visitor = new SelectManyVisitor(_scope, _queryBuilder, _loggerFactory);
            visitor.VisitSelectMany(collectionSelector, resultSelector);
            return true;
        }

        return false;
    }

    private bool HandleOrderBy(MethodCallExpression node, bool isDescending, bool isThenBy = false)
    {
        if (node.Arguments.Count < 2) return false;

        var selector = StripQuotes(node.Arguments[1]) as LambdaExpression;
        if (selector != null)
        {
            var visitor = new OrderByVisitor(_scope, _queryBuilder, _loggerFactory);
            visitor.VisitOrderBy(selector, isDescending, isThenBy);
            return true;
        }

        return false;
    }

    private bool HandleTake(MethodCallExpression node)
    {
        if (node.Arguments.Count < 2) return false;

        if (node.Arguments[1] is ConstantExpression constant && constant.Value is int limit)
        {
            _queryBuilder.AddLimit(limit);
            return true;
        }

        return false;
    }

    private bool HandleSkip(MethodCallExpression node)
    {
        if (node.Arguments.Count < 2) return false;

        if (node.Arguments[1] is ConstantExpression constant && constant.Value is int skip)
        {
            _queryBuilder.AddSkip(skip);
            return true;
        }

        return false;
    }

    private bool HandleFirst(MethodCallExpression node)
    {
        Expression? predicate = null;
        if (node.Arguments.Count > 1)
        {
            predicate = StripQuotes(node.Arguments[1]);
        }

        var orDefault = node.Method.Name.Contains("OrDefault");
        var isLast = node.Method.Name.StartsWith("Last");

        var visitor = new FirstVisitor(_scope, _queryBuilder, _loggerFactory);
        visitor.VisitFirst(predicate, orDefault, isLast);
        return true;
    }

    private bool HandleCount(MethodCallExpression node)
    {
        Expression? selector = null;
        if (node.Arguments.Count > 1)
        {
            selector = StripQuotes(node.Arguments[1]);
        }

        var visitor = new AggregateVisitor(_scope, _queryBuilder, _loggerFactory);
        visitor.VisitAggregate(node.Method.Name, selector);
        return true;
    }

    private bool HandleAny(MethodCallExpression node)
    {
        Expression? predicate = null;
        if (node.Arguments.Count > 1)
        {
            predicate = StripQuotes(node.Arguments[1]);
        }

        var visitor = new AnyVisitor(_scope, _queryBuilder, _loggerFactory);
        visitor.VisitAny(predicate);
        return true;
    }

    private bool HandleAll(MethodCallExpression node)
    {
        if (node.Arguments.Count < 2) return false;

        var predicate = StripQuotes(node.Arguments[1]) as LambdaExpression;
        if (predicate != null)
        {
            var visitor = new AllVisitor(_scope, _queryBuilder, _loggerFactory);
            visitor.VisitAll(predicate);
            return true;
        }

        return false;
    }

    private bool HandleDistinct(MethodCallExpression node)
    {
        var visitor = new DistinctVisitor(_scope, _queryBuilder, _loggerFactory);
        visitor.ApplyDistinct();
        return true;
    }

    private bool HandleGroupBy(MethodCallExpression node)
    {
        if (node.Arguments.Count < 2) return false;

        var keySelector = StripQuotes(node.Arguments[1]) as LambdaExpression;
        LambdaExpression? elementSelector = null;

        if (node.Arguments.Count > 2)
        {
            elementSelector = StripQuotes(node.Arguments[2]) as LambdaExpression;
        }

        if (keySelector != null)
        {
            var visitor = new GroupByVisitor(_scope, _queryBuilder, _loggerFactory);
            visitor.VisitGroupBy(keySelector, elementSelector);
            return true;
        }

        return false;
    }

    private bool HandleTraverse(MethodCallExpression node)
    {
        var genericArgs = node.Method.GetGenericArguments();
        if (genericArgs.Length >= 2)
        {
            var builder = new TraverseBuilder(_scope, _queryBuilder);
            builder.BuildTraversal(genericArgs[0], genericArgs[1]);
            return true;
        }

        return false;
    }

    private bool HandleRelationships(MethodCallExpression node)
    {
        Type? relationshipType = null;
        var direction = RelationshipDirection.Both;

        // Extract type from generic arguments if present
        if (node.Method.IsGenericMethod)
        {
            relationshipType = node.Method.GetGenericArguments()[0];
        }

        // Check for direction parameter
        if (node.Arguments.Count > 1 && node.Arguments[1] is ConstantExpression dirConstant)
        {
            direction = (RelationshipDirection)dirConstant.Value!;
        }

        var visitor = new RelationshipsVisitor(_scope, _queryBuilder, _loggerFactory);
        visitor.VisitRelationships(relationshipType, direction);
        return true;
    }

    private bool HandlePathSegments(MethodCallExpression node)
    {
        var genericArgs = node.Method.GetGenericArguments();
        if (genericArgs.Length >= 2)
        {
            // Get the actual entity type from the queryable, not the queryable type itself
            Type sourceType;

            if (node.Object?.Type.IsGenericType == true &&
                node.Object.Type.GetGenericTypeDefinition() == typeof(GraphNodeQueryable<>))
            {
                // Extract the entity type from GraphNodeQueryable<T>
                sourceType = node.Object.Type.GetGenericArguments()[0];
            }
            else if (node.Arguments.Count > 0 &&
                     node.Arguments[0].Type.IsGenericType &&
                     node.Arguments[0].Type.GetGenericTypeDefinition() == typeof(GraphNodeQueryable<>))
            {
                // Extract from the first argument if it's a queryable
                sourceType = node.Arguments[0].Type.GetGenericArguments()[0];
            }
            else
            {
                // Fallback to the original logic
                sourceType = node.Object?.Type ?? node.Arguments[0].Type;
            }

            var relationshipType = genericArgs[0]; // WorksFor
            var targetType = genericArgs[1];       // Company

            var visitor = new PathSegmentVisitor(_scope, _queryBuilder, _loggerFactory);
            visitor.BuildPathSegmentQuery(sourceType, relationshipType, targetType);
            return true;
        }

        return false;
    }

    private static Expression StripQuotes(Expression expression)
    {
        while (expression.NodeType == ExpressionType.Quote)
        {
            expression = ((UnaryExpression)expression).Operand;
        }
        return expression;
    }
}