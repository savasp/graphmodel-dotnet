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

internal sealed class SelectManyVisitor : ExpressionVisitor
{
    private readonly QueryScope _scope;
    private readonly CypherQueryBuilder _builder;

    public SelectManyVisitor(QueryScope scope, CypherQueryBuilder builder)
    {
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        _builder = builder ?? throw new ArgumentNullException(nameof(builder));
    }

    public void VisitSelectMany(LambdaExpression collectionSelector, LambdaExpression? resultSelector = null)
    {
        // SelectMany in graph context usually means traversing relationships
        // or expanding collections
        var body = collectionSelector.Body;

        switch (body)
        {
            case MethodCallExpression methodCall when methodCall.Method.Name == "Traverse":
                // Handle traversal pattern
                HandleTraversePattern(methodCall, resultSelector);
                break;

            case MemberExpression member:
                // Handle collection property expansion
                HandleCollectionExpansion(member, resultSelector);
                break;

            default:
                throw new NotSupportedException($"SelectMany pattern {body.NodeType} not supported");
        }
    }

    private void HandleTraversePattern(MethodCallExpression traverseCall, LambdaExpression? resultSelector)
    {
        // Extract relationship and target types from the Traverse call
        var genericArgs = traverseCall.Method.GetGenericArguments();
        var relationshipType = genericArgs[0];
        var targetType = genericArgs[1];

        var sourceAlias = _scope.CurrentAlias ?? "n";
        var relAlias = _scope.GetOrCreateAlias(relationshipType, "r");
        var targetAlias = _scope.GetOrCreateAlias(targetType, "t");

        var relLabel = Labels.GetLabelFromType(relationshipType);
        var targetLabel = Labels.GetLabelFromType(targetType);

        // Build the traversal
        _builder.AddMatch($"({sourceAlias})-[{relAlias}:{relLabel}]->({targetAlias}:{targetLabel})");

        // Handle result selector if provided
        if (resultSelector != null)
        {
            // Result selector typically combines source and target
            var selectVisitor = new SelectVisitor(_scope, _builder);
            selectVisitor.Visit(resultSelector);
        }
        else
        {
            // Default to returning the target nodes
            _builder.AddReturn(targetAlias);
            _scope.CurrentAlias = targetAlias;
        }
    }

    private void HandleCollectionExpansion(MemberExpression member, LambdaExpression? resultSelector)
    {
        var alias = member.Expression switch
        {
            ParameterExpression param => _scope.GetAliasForType(param.Type)
                ?? param.Name
                ?? throw new InvalidOperationException($"No alias found for parameter of type {param.Type.Name}"),
            _ => _scope.CurrentAlias ?? "n"
        };

        var propertyName = member.Member.Name;

        // UNWIND the collection
        _builder.AddUnwind($"{alias}.{propertyName} AS item");

        if (resultSelector != null)
        {
            var selectVisitor = new SelectVisitor(_scope, _builder);
            selectVisitor.Visit(resultSelector);
        }
        else
        {
            _builder.AddReturn("item");
        }
    }
}