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

namespace Cvoya.Graph.Model.Querying;

using System.Linq.Expressions;

/// <summary>
/// Validates semantic invariants for <see cref="GraphQueryModel"/>.
/// </summary>
public static class GraphQueryModelValidator
{
    /// <summary>
    /// Validates the specified query model.
    /// </summary>
    /// <param name="model">The query model to validate.</param>
    /// <exception cref="GraphException">Thrown when the model violates a semantic invariant.</exception>
    public static void Validate(GraphQueryModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var currentType = ResolveRootType(model.Root);
        ValidatePredicateList(model.Predicates, currentType, "Root predicate");

        for (var i = 0; i < model.Traversal.Count; i++)
        {
            var step = model.Traversal[i];

            ValidateDepthRange(step.Depth, i);

            if (currentType is not null && !typeof(INode).IsAssignableFrom(currentType))
            {
                throw new GraphException($"Traversal step {i} requires a node input, but the current scope is '{currentType.FullName}'.");
            }

            ValidatePredicateList(step.RelationshipPredicates, typeof(IRelationship), $"Traversal step {i} relationship predicate");

            currentType = step.TargetType ?? typeof(INode);
        }

        if (model.Projection?.Selector is { } selector)
        {
            var projectionScope = model.Projection.Kind == ProjectionKind.PathSegment
                ? typeof(IGraphPathSegment)
                : currentType;

            ValidateLambdaReferences(selector, projectionScope, "Projection selector");
        }

        for (var i = 0; i < model.Ordering.Count; i++)
        {
            ValidateLambdaReferences(model.Ordering[i].KeySelector, currentType, $"Ordering key {i}");
        }
    }

    private static Type? ResolveRootType(QueryRoot root)
    {
        return root switch
        {
            NodeRoot nodeRoot => nodeRoot.ElementType,
            RelationshipRoot relationshipRoot => relationshipRoot.ElementType,
            SearchRoot { Target: SearchRootTarget.Nodes, ElementType: { } elementType } => elementType,
            SearchRoot { Target: SearchRootTarget.Nodes } => typeof(INode),
            SearchRoot { Target: SearchRootTarget.Relationships, ElementType: { } elementType } => elementType,
            SearchRoot { Target: SearchRootTarget.Relationships } => typeof(IRelationship),
            SearchRoot { Target: SearchRootTarget.Entities, ElementType: { } elementType } => elementType,
            SearchRoot { Target: SearchRootTarget.Entities } => typeof(IEntity),
            DynamicRoot => null,
            _ => throw new GraphException($"Unknown query root '{root.GetType().FullName}'.")
        };
    }

    private static void ValidatePredicateList(IReadOnlyList<PredicateFragment> predicates, Type? currentType, string description)
    {
        for (var i = 0; i < predicates.Count; i++)
        {
            var name = predicates[i].Alias is { Length: > 0 } alias
                ? $"{description} '{alias}'"
                : $"{description} {i}";

            ValidateLambdaReferences(predicates[i].Predicate, currentType, name);
        }
    }

    private static void ValidateDepthRange(DepthRange depth, int stepIndex)
    {
        if (depth.Min < 0)
        {
            throw new GraphException($"Traversal step {stepIndex} depth range minimum must be non-negative.");
        }

        if (depth.Max < depth.Min)
        {
            throw new GraphException($"Traversal step {stepIndex} depth range maximum must be greater than or equal to minimum.");
        }
    }

    private static void ValidateLambdaReferences(LambdaExpression lambda, Type? currentType, string description)
    {
        if (lambda.Parameters.Count == 0)
        {
            throw new GraphException($"{description} must declare at least one parameter.");
        }

        if (currentType is not null)
        {
            foreach (var parameter in lambda.Parameters.Where(parameter =>
                !parameter.Type.IsAssignableFrom(currentType) && !currentType.IsAssignableFrom(parameter.Type)))
            {
                throw new GraphException(
                    $"{description} parameter '{parameter.Name}' has type '{parameter.Type.FullName}', which is outside the current scope '{currentType.FullName}'.");
            }
        }

        var referencedParameters = ParameterReferenceCollector.Collect(lambda.Body);
        foreach (var referencedParameter in referencedParameters.Where(referencedParameter => !lambda.Parameters.Contains(referencedParameter)))
        {
            throw new GraphException(
                $"{description} references parameter '{referencedParameter.Name}' that is outside the lambda scope.");
        }
    }

    private sealed class ParameterReferenceCollector : ExpressionVisitor
    {
        private readonly HashSet<ParameterExpression> _parameters = [];
        private readonly Stack<IReadOnlyList<ParameterExpression>> _nestedLambdaScopes = new();

        public static IReadOnlyCollection<ParameterExpression> Collect(Expression expression)
        {
            var collector = new ParameterReferenceCollector();
            collector.Visit(expression);
            return collector._parameters;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (!_nestedLambdaScopes.Any(scope => scope.Contains(node)))
            {
                _parameters.Add(node);
            }

            return node;
        }

        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            _nestedLambdaScopes.Push(node.Parameters);
            try
            {
                return base.VisitLambda(node);
            }
            finally
            {
                _nestedLambdaScopes.Pop();
            }
        }
    }
}
