// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

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

        ValidateTerminalOperand(model);
        ValidateAliasBindings(model);

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

        if (model.PathShape is not null)
        {
            possibleScopeTypes.Add(typeof(IGraphPath));
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

        if (model.PostPaging is { } postPaging)
        {
            if (model.Paging.Skip is null && model.Paging.Take is null)
            {
                throw new GraphException("A post-paging sequence stage requires a primary Skip or Take operation.");
            }

            for (var i = 0; i < postPaging.Predicates.Count; i++)
            {
                ValidateLambdaReferences(
                    postPaging.Predicates[i].Predicate,
                    possibleScopeTypes,
                    $"Post-paging predicate {i}");
            }

            for (var i = 0; i < postPaging.Ordering.Count; i++)
            {
                ValidateLambdaReferences(
                    postPaging.Ordering[i].KeySelector,
                    possibleScopeTypes,
                    $"Post-paging ordering key {i}");
            }
        }

        if (model.GroupBy is { } groupBy)
        {
            ValidateLambdaReferences(groupBy.KeySelector, possibleScopeTypes, "GroupBy key selector");
            if (groupBy.ElementSelector is { } elementSelector)
            {
                ValidateLambdaReferences(elementSelector, possibleScopeTypes, "GroupBy element selector");
            }

            if (groupBy.ResultSelector is { } groupResultSelector)
            {
                // The result selector ranges over the key and the group, which are not query scopes;
                // only parameter containment is checkable here.
                ValidateParameterContainment(groupResultSelector, "GroupBy result selector");
            }
        }

        if (model.SelectMany is { } selectMany)
        {
            ValidateLambdaReferences(selectMany.CollectionSelector, possibleScopeTypes, "SelectMany collection selector");
            if (selectMany.ResultSelector is { } flattenResultSelector)
            {
                // The result selector's second parameter ranges over flattened elements, which are
                // not query scopes; only parameter containment is checkable here.
                ValidateParameterContainment(flattenResultSelector, "SelectMany result selector");
            }
        }

        if (model.Union is { } union)
        {
            Validate(union.First);
            Validate(union.Second);
            ValidateUnionOperandType(union.First, union.ElementType, "first");
            ValidateUnionOperandType(union.Second, union.ElementType, "second");
        }
    }

    private static void ValidateUnionOperandType(GraphQueryModel operand, Type elementType, string description)
    {
        var operandType = ResolveOutputType(operand);
        if (operandType is not null &&
            !elementType.IsAssignableFrom(operandType) && !operandType.IsAssignableFrom(elementType))
        {
            throw new GraphException(
                $"Union {description} source produces '{operandType.FullName}', which is incompatible with " +
                $"the union element type '{elementType.FullName}'.");
        }
    }

    private static Type? ResolveOutputType(GraphQueryModel model)
    {
        if (model.GroupBy is not null || model.SelectMany is not null || model.Union is not null)
        {
            // These fragments carry their own operator boundary types; the containing root and
            // projection do not necessarily describe the sequence element at that boundary.
            return null;
        }

        return model.Projection?.Selector?.ReturnType ?? ResolveRootType(model.Root);
    }

    private static void ValidateTerminalOperand(GraphQueryModel model)
    {
        if (model.Terminal is not (TerminalOperation.ElementAt or TerminalOperation.ElementAtOrDefault))
        {
            return;
        }

        if (model.TerminalOperand is not int index)
        {
            throw new GraphException(
                $"Terminal operation '{model.Terminal}' requires an integer index in {nameof(GraphQueryModel.TerminalOperand)}.");
        }

        if (index < 0)
        {
            throw new GraphException($"Terminal operation '{model.Terminal}' requires a non-negative index.");
        }
    }

    private static void ValidateAliasBindings(GraphQueryModel model)
    {
        var bound = CollectRootAliases(model);
        if (model.Join is not null)
        {
            bound.Add("joined");
        }

        var explicitIndex = 0;
        foreach (var step in model.Traversal)
        {
            if (step.IsComplexPropertyTraversal)
            {
                continue;
            }

            if (step.SourceAlias is { } sourceAlias && !bound.Contains(sourceAlias))
            {
                throw new GraphException(
                    $"Traversal step source alias '{sourceAlias}' is not bound by the root scope or a preceding traversal target.");
            }

            // Providers bind a relationship and target scope per explicit step; steps that do not
            // declare a target alias fall back to the same positional convention.
            bound.Add(explicitIndex == 0 ? "r" : $"r_{explicitIndex + 1}");
            bound.Add(step.TargetAlias ?? (explicitIndex == 0 ? "tgt" : $"tgt_{explicitIndex + 1}"));
            explicitIndex++;
        }

        if (model.PathShape is not null)
        {
            bound.Add("p");
        }

        for (var i = 0; i < model.Predicates.Count; i++)
        {
            if (model.Predicates[i].Alias is { } alias && !bound.Contains(alias))
            {
                throw new GraphException(
                    $"Root predicate {i} alias '{alias}' is not bound by the root scope or a traversal target.");
            }
        }

        for (var i = 0; i < model.Ordering.Count; i++)
        {
            if (model.Ordering[i].Alias is { } alias && !bound.Contains(alias))
            {
                throw new GraphException(
                    $"Ordering key {i} alias '{alias}' is not bound by the root scope or a traversal target.");
            }
        }
    }

    private static HashSet<string> CollectRootAliases(GraphQueryModel model)
    {
        HashSet<string> aliases = new(StringComparer.Ordinal);
        switch (model.Root)
        {
            case RelationshipRoot:
            case DynamicRoot { ElementType: { } dynamicType } when typeof(IRelationship).IsAssignableFrom(dynamicType):
            case SearchRoot { Target: SearchRootTarget.Relationships }:
                aliases.Add("src");
                aliases.Add("r");
                aliases.Add("tgt");
                break;
            case SearchRoot { Target: SearchRootTarget.Nodes }:
                // Predicates recognized before a Search replaced the root keep the original root
                // scope alias; planners remap it onto the search scope.
                aliases.Add("n");
                aliases.Add("src");
                break;
            case SearchRoot { Target: SearchRootTarget.Entities }:
                aliases.Add("entity");
                break;
            default:
                aliases.Add("src");
                if (model.Root is NodeRoot { ElementType: { IsInterface: true } elementType } &&
                    typeof(INode).IsAssignableFrom(elementType) &&
                    model.Traversal.Any(step => !step.IsComplexPropertyTraversal))
                {
                    // Interface traversal roots render as src_1 to avoid colliding with the
                    // concrete source alias inside the traversal pattern. Builder-produced
                    // predicates can still retain src and are remapped by the planner.
                    aliases.Add("src_1");
                }

                break;
        }

        return aliases;
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

        ValidateParameterContainment(lambda, description);
    }

    private static void ValidateParameterContainment(LambdaExpression lambda, string description)
    {
        if (lambda.Parameters.Count == 0)
        {
            throw new GraphException($"{description} must declare at least one parameter.");
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

        public static HashSet<ParameterExpression> Collect(Expression expression)
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
