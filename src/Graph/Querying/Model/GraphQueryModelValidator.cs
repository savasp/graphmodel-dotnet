// Copyright CVOYA. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.
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

namespace Cvoya.Graph.Querying;

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
        var possibleScopeTypes = new List<Type?> { currentType };

        for (var i = 0; i < model.Traversal.Count; i++)
        {
            var step = model.Traversal[i];

            ValidateDepthRange(step.Depth, i);

            if (!step.IsComplexPropertyTraversal &&
                currentType is not null &&
                !typeof(INode).IsAssignableFrom(currentType))
            {
                throw new GraphException($"Traversal step {i} requires a node input, but the current scope is '{currentType.FullName}'.");
            }

            ValidatePredicateList(step.RelationshipPredicates, typeof(IRelationship), $"Traversal step {i} relationship predicate");

            if (!step.IsComplexPropertyTraversal)
            {
                currentType = step.TargetType ?? typeof(INode);
                possibleScopeTypes.Add(currentType);
            }
        }

        for (var i = 0; i < model.Predicates.Count; i++)
        {
            var predicate = model.Predicates[i];
            var name = predicate.Alias is { Length: > 0 } alias
                ? $"Root predicate '{alias}'"
                : $"Root predicate {i}";
            ValidateLambdaReferences(predicate.Predicate, possibleScopeTypes, name);
        }

        if (model.Join is { } join)
        {
            ValidateLambdaReferences(join.OuterKeySelector, [ResolveRootType(model.Root)], "Join outer key selector");
            ValidateLambdaReferences(join.InnerKeySelector, [ResolveRootType(join.InnerRoot)], "Join inner key selector");
        }
        else if (model.Projection?.Selector is { } selector)
        {
            ValidateLambdaReferences(selector, possibleScopeTypes, "Projection selector");
            possibleScopeTypes.Add(selector.ReturnType);
        }

        for (var i = 0; i < model.Ordering.Count; i++)
        {
            ValidateLambdaReferences(model.Ordering[i].KeySelector, possibleScopeTypes, $"Ordering key {i}");
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
        ValidateLambdaReferences(lambda, [currentType], description);
    }

    private static void ValidateLambdaReferences(
        LambdaExpression lambda,
        IReadOnlyCollection<Type?> possibleScopeTypes,
        string description)
    {
        if (lambda.Parameters.Count == 0)
        {
            throw new GraphException($"{description} must declare at least one parameter.");
        }

        var definedScopeTypes = possibleScopeTypes.Where(type => type is not null).Cast<Type>().ToArray();
        if (definedScopeTypes.Length > 0 &&
            !lambda.Parameters.Any(parameter => typeof(IGraphPathSegment).IsAssignableFrom(parameter.Type)))
        {
            foreach (var parameter in lambda.Parameters.Where(parameter =>
                !definedScopeTypes.Any(scopeType =>
                    parameter.Type.IsAssignableFrom(scopeType) || scopeType.IsAssignableFrom(parameter.Type))))
            {
                throw new GraphException(
                    $"{description} parameter '{parameter.Name}' has type '{parameter.Type.FullName}', which is outside the current query scopes.");
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
