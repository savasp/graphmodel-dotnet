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

namespace Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Handlers;

using System.Linq.Expressions;
using System.Reflection;
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Builders;
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Core;
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Expressions;
using Microsoft.Extensions.Logging;

/// <summary>
/// Handles the Select LINQ method by generating appropriate RETURN clauses.
/// </summary>
internal record SelectMethodHandler : MethodHandlerBase
{
    public override bool Handle(CypherQueryContext context, MethodCallExpression node, Expression result)
    {
        var logger = context.LoggerFactory?.CreateLogger(nameof(SelectMethodHandler));
        logger?.LogDebug("SelectMethodHandler called");

        if (node.Method.Name != "Select" || node.Arguments.Count != 2)
        {
            logger?.LogDebug("SelectMethodHandler: not a Select method or wrong arguments");
            return false;
        }

        var selectorExpression = node.Arguments[1];

        // Extract the lambda expression
        var lambda = selectorExpression switch
        {
            LambdaExpression directLambda => directLambda,
            UnaryExpression { Operand: LambdaExpression unaryLambda } => unaryLambda,
            _ => null
        };

        if (lambda is null)
        {
            logger?.LogDebug("Could not extract lambda expression from selector");
            return false;
        }

        // Check if this is selecting the full entity (identity function like x => x)
        if (IsIdentitySelection(lambda))
        {
            var rootType = context.Scope.RootType;
            if (context.Builder.NeedsComplexProperties(rootType))
            {
                logger?.LogDebug("Identity selection detected - enabling complex property loading");
                context.Builder.EnableComplexPropertyLoading();
            }
        }

        // Handle the actual projection using our dedicated method
        return HandleProjection(context, lambda, result);
    }

    private static bool IsIdentitySelection(Expression selectorExpression)
    {
        // Check if selector is like x => x (parameter expression)
        if (selectorExpression is LambdaExpression lambda &&
            lambda.Body is ParameterExpression param &&
            lambda.Parameters.Contains(param))
        {
            return true;
        }

        return false;
    }

    private static bool HandleProjection(CypherQueryContext context, LambdaExpression lambda, Expression? result)
    {
        var alias = context.Scope.CurrentAlias
            ?? throw new InvalidOperationException("No current alias set in the scope for Select method");

        var expressionVisitorFactory = new ExpressionVisitorChainFactory(context);
        var expressionVisitor = expressionVisitorFactory.CreateSelectClauseChain(alias);

        return lambda.Body switch
        {
            NewExpression newExpression when IsAnonymousType(newExpression.Type) =>
                HandleAnonymousTypeProjection(context, newExpression, expressionVisitor),

            _ => HandleSimpleProjection(context, lambda.Body, expressionVisitor)
        };
    }

    private static bool HandleAnonymousTypeProjection(
        CypherQueryContext context,
        NewExpression newExpression,
        ICypherExpressionVisitor expressionVisitor)
    {
        if (newExpression.Arguments.Count == 0)
        {
            return true; // Empty anonymous type, nothing to project
        }

        for (var i = 0; i < newExpression.Arguments.Count; i++)
        {
            var argument = newExpression.Arguments[i];
            var memberName = newExpression.Members?[i]?.Name ?? $"Item{i}";

            var expression = expressionVisitor.Visit(argument);
            context.Builder.AddUserProjection($"{expression} AS {memberName}");
        }

        return true;
    }

    private static bool HandleSimpleProjection(
        CypherQueryContext context,
        Expression projectionExpression,
        ICypherExpressionVisitor expressionVisitor)
    {
        var projection = expressionVisitor.Visit(projectionExpression);

        // Check if we're projecting from a path segment to a single node
        if (context.Scope.IsPathSegmentContext &&
            projectionExpression is MemberExpression member)
        {
            var pathSegmentProjection = member.Member.Name switch
            {
                nameof(IGraphPathSegment.EndNode) => CypherQueryBuilder.PathSegmentProjection.EndNode,
                nameof(IGraphPathSegment.StartNode) => CypherQueryBuilder.PathSegmentProjection.StartNode,
                nameof(IGraphPathSegment.Relationship) => CypherQueryBuilder.PathSegmentProjection.Relationship,
                _ => CypherQueryBuilder.PathSegmentProjection.Full
            };

            if (pathSegmentProjection != CypherQueryBuilder.PathSegmentProjection.Full)
            {
                // Set the projection type on the builder
                context.Builder.SetPathSegmentProjection(pathSegmentProjection);

                // Update the scope to reflect what we're now returning
                var targetType = pathSegmentProjection switch
                {
                    CypherQueryBuilder.PathSegmentProjection.EndNode => context.Scope.TraversalInfo?.TargetNodeType,
                    CypherQueryBuilder.PathSegmentProjection.StartNode => context.Scope.TraversalInfo?.SourceNodeType,
                    CypherQueryBuilder.PathSegmentProjection.Relationship => context.Scope.TraversalInfo?.RelationshipType,
                    _ => null
                };

                if (targetType != null)
                {
                    context.Scope.CurrentType = targetType;
                    context.Scope.IsPathSegmentContext = false;
                }

                return true;
            }
        }

        context.Builder.AddUserProjection(projection);
        return true;
    }

    private static bool IsAnonymousType(Type type)
    {
        return type.IsGenericType
            && type.IsClass
            && type.IsSealed
            && type.Attributes.HasFlag(TypeAttributes.NotPublic)
            && type.Name.StartsWith("<>", StringComparison.Ordinal)
            && type.Name.Contains("AnonymousType");
    }
}
