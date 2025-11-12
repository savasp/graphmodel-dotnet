// Copyright 2025 Savas Parastatidis

namespace Cvoya.Graph.Model.Age.Querying.Cypher.Visitors.Core.Modular;

using System.Collections.Immutable;
using System.Linq.Expressions;
using Cvoya.Graph.Model;
using Cvoya.Graph.Model.Cypher.Querying.Cypher.Visitors.Core;
using Microsoft.Extensions.Logging;

/// <summary>
/// Specialized visitor for handling graph traversal operations
/// (PathSegments, WithDepth, Direction, MATCH pattern generation).
/// Emits MatchSegmentFragment and MatchRootFragment instances.
/// </summary>
internal sealed class TraversalFragmentVisitor : FragmentEmittingVisitorBase
{
    public TraversalFragmentVisitor(CypherQueryContext context, ILogger logger)
        : base(context, logger)
    {
    }

    /// <summary>
    /// Processes PathSegments() operation and emits MatchSegmentFragment or MatchRootFragment.
    /// Handles chained hops and multi-hop traversals.
    /// </summary>
    public Expression HandlePathSegments(MethodCallExpression node)
    {
        Logger.LogDebug("Processing PathSegments method call, CurrentHop={CurrentHop} BEFORE processing", Context.Scope.CurrentHop);

        // Detect if this is a chained PathSegments call
        bool isChainedPathSegments = ContainsPathSegmentsInChain(node.Arguments[0]);
        bool hasExistingPatterns = Context.Builder.HasMatchPatterns && Context.Scope.CurrentHop > 0;
        isChainedPathSegments = isChainedPathSegments || hasExistingPatterns;

        Logger.LogDebug(
            "Chain detection for hop {Hop}: isChained={IsChained}",
            Context.Scope.CurrentHop,
            isChainedPathSegments
        );

        // Extract generic type arguments (TSource, TRelationship, TTarget)
        var method = node.Method;
        if (!method.IsGenericMethod)
        {
            Logger.LogWarning("PathSegments is not a generic method");
            return node.Arguments[0];
        }

        var genericArgs = method.GetGenericArguments();
        if (genericArgs.Length != 3)
        {
            Logger.LogWarning("PathSegments does not have exactly 3 generic arguments");
            return node.Arguments[0];
        }

        var (sourceType, relationshipType, targetType) = (genericArgs[0], genericArgs[1], genericArgs[2]);
        Logger.LogDebug(
            "PathSegments: Source={Source}, Relationship={Relationship}, Target={Target}",
            sourceType.Name,
            relationshipType.Name,
            targetType.Name
        );

        // Set up traversal context
        Context.Scope.SetTraversalInfo(sourceType, relationshipType, targetType);
        Context.Scope.IsPathSegmentContext = true;

        // Generate aliases
        var (sourceAlias, relAlias, targetAlias) = GenerateAliases(sourceType, targetType, isChainedPathSegments);

        // Register type-to-alias mappings
        RegisterTypeAliases(sourceType, relationshipType, targetType, sourceAlias, relAlias, targetAlias, isChainedPathSegments);

        // Store hop information for WHERE clause resolution and Select projections
        Context.Scope.StoreHopAliases(Context.Scope.CurrentHop, sourceAlias, relAlias, targetAlias);
        Context.Scope.StoreHopTypes(Context.Scope.CurrentHop, sourceType, relationshipType, targetType);

        // Build and emit MATCH pattern
        var pathPattern = BuildMatchPattern(
            sourceType,
            relationshipType,
            targetType,
            sourceAlias,
            relAlias,
            targetAlias,
            isChainedPathSegments
        );

        Context.Builder.AddMatchPattern(pathPattern);
        Logger.LogDebug("Added path segment pattern: {Pattern}", pathPattern);
        Console.WriteLine($"[PathSegments] hop={Context.Scope.CurrentHop}, isChained={isChainedPathSegments}, pattern={pathPattern}");

        // Emit fragment
        EmitMatchFragment(
            pathPattern,
            sourceType,
            relationshipType,
            targetType,
            sourceAlias,
            relAlias,
            targetAlias
        );

        // Add relationship type filter for interface relationships
        AddRelationshipTypeFilter(relationshipType, relAlias);

        // Update current alias to target for subsequent operations
        Context.Scope.CurrentAlias = targetAlias;
        Logger.LogDebug("Updated CurrentAlias to target: {TargetAlias}", targetAlias);

        // Advance hop counter for chaining
        Context.Scope.AdvanceHop();

        // Reset traversal direction after consuming it for this PathSegments call so subsequent traversals default appropriately.
        Context.Builder.ClearTraversalDirection();

        // Return the source expression (caller will process it)
        return node.Arguments[0];
    }

    /// <summary>
    /// Processes WithDepth() operation to set variable-length path depth.
    /// </summary>
    public Expression HandleWithDepth(MethodCallExpression node)
    {
        Logger.LogDebug("Processing WithDepth method call");

        if (node.Arguments.Count == 2)
        {
            // WithDepth(maxDepth)
            var maxDepth = EvaluateConstantExpression<int>(node.Arguments[1]);
            Context.Scope.SetTraversalDepth(1, maxDepth);
            Logger.LogDebug("Set traversal max depth: {MaxDepth}", maxDepth);
        }
        else if (node.Arguments.Count == 3)
        {
            // WithDepth(minDepth, maxDepth)
            var minDepth = EvaluateConstantExpression<int>(node.Arguments[1]);
            var maxDepth = EvaluateConstantExpression<int>(node.Arguments[2]);
            Context.Scope.SetTraversalDepth(minDepth, maxDepth);
            Logger.LogDebug("Set traversal depth range: {MinDepth}-{MaxDepth}", minDepth, maxDepth);
        }

        // Return the source expression (caller will process it)
        return node.Arguments[0];
    }

    /// <summary>
    /// Processes Direction() operation to set traversal direction (Incoming/Outgoing/Both).
    /// </summary>
    public Expression HandleDirection(MethodCallExpression node)
    {
        Logger.LogDebug("Processing Direction method call");

        if (node.Arguments.Count >= 2)
        {
            var direction = EvaluateConstantExpression<GraphTraversalDirection>(node.Arguments[1]);
            Context.Builder.SetTraversalDirection(direction);
            Logger.LogDebug("Set traversal direction: {Direction}", direction);
        }

        // Return the source expression (caller will process it)
        return node.Arguments[0];
    }

    // --- Private Helpers ---

    private (string sourceAlias, string relAlias, string targetAlias) GenerateAliases(
        Type sourceType,
        Type targetType,
        bool isChained
    )
    {
        string sourceAlias;
        
        // For chained patterns after the first hop, the source is the previous hop's target
        if (isChained && Context.Scope.CurrentHop > 0)
        {
            var previousHop = Context.Scope.CurrentHop - 1;
            var previousHopAliases = Context.Scope.GetHopAliases(previousHop);
            var currentAlias = Context.Scope.CurrentAlias;

            if (!string.IsNullOrWhiteSpace(currentAlias))
            {
                var currentAliasType = Context.Scope.GetTypeForAlias(currentAlias);
                if (currentAliasType != null &&
                    (currentAliasType == sourceType || sourceType.IsAssignableFrom(currentAliasType) || currentAliasType.IsAssignableFrom(sourceType)))
                {
                    sourceAlias = currentAlias;
                    Logger.LogDebug(
                        "Chained hop {Hop}: Using current alias '{Alias}' as source",
                        Context.Scope.CurrentHop,
                        sourceAlias
                    );
                }
                else if (previousHopAliases.HasValue)
                {
                    sourceAlias = previousHopAliases.Value.tgt;
                    Logger.LogDebug(
                        "Chained hop {Hop}: Current alias type mismatch, using previous hop {PrevHop} target '{Alias}' as source",
                        Context.Scope.CurrentHop,
                        previousHop,
                        sourceAlias
                    );
                }
                else
                {
                    sourceAlias = Context.Scope.GetNumberedAlias("src");
                    Logger.LogWarning(
                        "Chained hop {Hop}: Could not resolve compatible alias, falling back to numbered alias",
                        Context.Scope.CurrentHop
                    );
                }
            }
            else if (previousHopAliases.HasValue)
            {
                sourceAlias = previousHopAliases.Value.tgt; // Use previous target as this hop's source
                Logger.LogDebug(
                    "Chained hop {Hop}: Using previous hop {PrevHop} target '{Alias}' as source",
                    Context.Scope.CurrentHop,
                    previousHop,
                    sourceAlias
                );
            }
            else
            {
                // Fallback to numbered alias (shouldn't happen)
                sourceAlias = Context.Scope.GetNumberedAlias("src");
                Logger.LogWarning(
                    "Chained hop {Hop}: Previous hop {PrevHop} aliases not found, falling back to numbered alias",
                    Context.Scope.CurrentHop,
                    previousHop
                );
            }
        }
        else
        {
            // First hop or non-chained: use standard numbered src alias
            sourceAlias = Context.Scope.GetNumberedAlias("src");
        }
        
        var relAlias = Context.Scope.GetNumberedAlias("r");
        var targetAlias = Context.Scope.GetNumberedAlias("tgt");

        Logger.LogDebug(
            "Generated hop {Hop} aliases: src={SrcAlias}, r={RelAlias}, tgt={TgtAlias}",
            Context.Scope.CurrentHop,
            sourceAlias,
            relAlias,
            targetAlias
        );

        return (sourceAlias, relAlias, targetAlias);
    }

    private void RegisterTypeAliases(
        Type sourceType,
        Type relationshipType,
        Type targetType,
        string sourceAlias,
        string relAlias,
        string targetAlias,
        bool isChained
    )
    {
        Context.Scope.RegisterTypeAlias(sourceType, sourceAlias);
        Context.Scope.RegisterTypeAlias(relationshipType, relAlias);

        // Only register targetType if not chained or first hop
        if (!isChained || Context.Scope.CurrentHop == 0)
        {
            Context.Scope.RegisterTypeAlias(targetType, targetAlias);
        }

        Logger.LogDebug(
            "Registered type aliases: {SourceType}={SourceAlias}, {RelType}={RelAlias}, {TargetType}={TargetAlias}",
            sourceType.Name,
            sourceAlias,
            relationshipType.Name,
            relAlias,
            targetType.Name,
            targetAlias
        );
    }

    private string BuildMatchPattern(
        Type sourceType,
        Type relationshipType,
        Type targetType,
        string sourceAlias,
        string relAlias,
        string targetAlias,
        bool isChained
    )
    {
        var sourceLabel = Labels.GetBaseTypeLabel(sourceType);
        var relationshipLabel = GetRelationshipLabel(relationshipType);
        var targetLabel = Labels.GetBaseTypeLabel(targetType);

        // Determine relationship direction arrows
        var direction = Context.Builder.TraversalDirection ?? GraphTraversalDirection.Outgoing;
        var (leftArrow, rightArrow) = GetDirectionArrows(direction);

        // Build relationship pattern with optional depth range
        var relPattern = BuildRelationshipPattern(relAlias, relationshipLabel);

        // Build path pattern based on hop position and chaining
        string pathPattern;
        if (Context.Scope.CurrentHop == 0)
        {
            // First hop: include both source and target nodes
            pathPattern = $"({sourceAlias}:{sourceLabel}){leftArrow}[{relPattern}]{rightArrow}({targetAlias}:{targetLabel})";
            Logger.LogDebug("First hop pattern: {Pattern} (direction: {Direction})", pathPattern, direction);
        }
        else if (isChained)
        {
            // Determine the alias that represents the current source for this chained hop.
            var chainSourceAlias = Context.Scope.CurrentAlias;
            if (string.IsNullOrWhiteSpace(chainSourceAlias))
            {
                var previousHop = Context.Scope.CurrentHop - 1;
                var previousHopAliases = Context.Scope.GetHopAliases(previousHop);
                if (previousHopAliases.HasValue)
                {
                    chainSourceAlias = previousHopAliases.Value.tgt;
                    Console.WriteLine($"[BuildMatchPattern] Chaining: using previous hop {previousHop} target '{chainSourceAlias}' as source for hop {Context.Scope.CurrentHop}");
                }
                else
                {
                    chainSourceAlias = sourceAlias;
                    Logger.LogWarning("Could not find previous hop aliases, using generated source alias");
                }
            }

            var chainSourceLabel = Labels.GetBaseTypeLabel(sourceType);
            pathPattern = $"({chainSourceAlias}:{chainSourceLabel}){leftArrow}[{relPattern}]{rightArrow}({targetAlias}:{targetLabel})";
            Logger.LogDebug("Chained hop {Hop} pattern: {Pattern}", Context.Scope.CurrentHop, pathPattern);
        }
        else
        {
            // Independent hop: standalone pattern
            pathPattern = $"({sourceAlias}:{sourceLabel}){leftArrow}[{relPattern}]{rightArrow}({targetAlias}:{targetLabel})";
            Logger.LogDebug("Independent hop {Hop} pattern: {Pattern}", Context.Scope.CurrentHop, pathPattern);
        }

        return pathPattern;
    }

    private string BuildRelationshipPattern(string relAlias, string relationshipLabel)
    {
        var relPattern = string.IsNullOrEmpty(relationshipLabel) ? relAlias : $"{relAlias}:{relationshipLabel}";

        // Add depth range if specified (for variable-length paths)
        if (Context.Scope.TraversalMinDepth.HasValue || Context.Scope.TraversalMaxDepth.HasValue)
        {
            var minDepth = Context.Scope.TraversalMinDepth ?? 1;
            var maxDepth = Context.Scope.TraversalMaxDepth ?? minDepth;
            relPattern = $"{relPattern}*{minDepth}..{maxDepth}";
            Logger.LogDebug("Added depth range to relationship pattern: {Pattern}", relPattern);
        }

        return relPattern;
    }

    private (string leftArrow, string rightArrow) GetDirectionArrows(GraphTraversalDirection direction)
    {
        return direction switch
        {
            GraphTraversalDirection.Incoming => ("<-", "-"),
            GraphTraversalDirection.Both => ("-", "-"),
            _ => ("-", "->") // Outgoing
        };
    }

    private void EmitMatchFragment(
        string pathPattern,
        Type sourceType,
        Type relationshipType,
        Type targetType,
        string sourceAlias,
        string relAlias,
        string targetAlias
    )
    {
        try
        {
            var direction = Context.Builder.TraversalDirection ?? GraphTraversalDirection.Outgoing;
            var created = ImmutableArray.Create(sourceAlias, relAlias, targetAlias);
            var fragment = new MatchSegmentFragment(
                pathPattern,
                sourceType,
                relationshipType,
                targetType,
                direction,
                created,
                targetAlias
            );
            EmitFragment(fragment, "MatchSegmentFragment");
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Failed to emit MatchSegmentFragment (non-fatal)");
        }
    }

    private bool ContainsPathSegmentsInChain(Expression expression)
    {
        return expression switch
        {
            MethodCallExpression methodCall when methodCall.Method.Name == nameof(GraphTraversalExtensions.PathSegments)
                => true,
            MethodCallExpression methodCall when methodCall.Method.Name is "Where" or "Select"
                => methodCall.Arguments.Any(ContainsPathSegmentsInChain),
            MethodCallExpression methodCall
                => methodCall.Arguments.Any(ContainsPathSegmentsInChain),
            UnaryExpression unary
                => ContainsPathSegmentsInChain(unary.Operand),
            _
                => false
        };
    }

    private T EvaluateConstantExpression<T>(Expression expression)
    {
        if (expression is ConstantExpression constantExpr && constantExpr.Value is T value)
        {
            return value;
        }

        Logger.LogWarning("Could not evaluate constant expression of type {Type}", typeof(T).Name);
        return default!;
    }

    private string GetRelationshipLabel(Type relationshipType)
    {
        if (IsUnspecifiedRelationshipType(relationshipType))
        {
            Logger.LogDebug("Relationship type {TypeName} uses unspecified pattern", relationshipType.Name);
            return string.Empty;
        }

        if (relationshipType.IsInterface)
        {
            Logger.LogDebug("Interface relationship type {TypeName} uses inheritance filter", relationshipType.Name);
            return string.Empty;
        }

        return Labels.GetBaseTypeLabel(relationshipType);
    }

    private void AddRelationshipTypeFilter(Type relationshipType, string relAlias)
    {
        if (!relationshipType.IsInterface || IsUnspecifiedRelationshipType(relationshipType))
        {
            return;
        }

        Logger.LogDebug("Relationship interface {TypeName} requires inheritance filter", relationshipType.Name);

        var interfaceLabel = Labels.GetLabelFromType(relationshipType);
        var typeFilter = $"'{interfaceLabel}' IN {relAlias}.inheritance_labels";
        Context.Builder.AddWhere(typeFilter);
        Logger.LogDebug("Added relationship type filter: {Filter}", typeFilter);
    }

    private static bool IsUnspecifiedRelationshipType(Type relationshipType)
    {
        return relationshipType == typeof(IRelationship) || relationshipType == typeof(Relationship);
    }
}
