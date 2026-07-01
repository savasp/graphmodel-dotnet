// Copyright 2025 Savas Parastatidis

namespace Cvoya.Graph.Model.Age.Querying.Cypher.Visitors.Core.Modular;

using System.Collections.Immutable;
using System.Linq.Expressions;
using Cvoya.Graph.Model;
using Microsoft.Extensions.Logging;

/// <summary>
/// Specialized visitor for handling graph traversal operations
/// (PathSegments, WithDepth, Direction, MATCH pattern generation).
/// </summary>
internal sealed class TraversalFragmentVisitor : FragmentEmittingVisitorBase
{
    public TraversalFragmentVisitor(CypherQueryContext context, ILogger logger)
        : base(context, logger)
    {
    }

    public Expression HandlePathSegments(MethodCallExpression node)
    {
        Logger.LogDebug("Processing PathSegments method call, CurrentHop={CurrentHop} BEFORE processing", Context.Scope.CurrentHop);

        bool isChainedPathSegments = ContainsPathSegmentsInChain(node.Arguments[0]);
        bool hasExistingPatterns = Context.Scope.CurrentHop > 0 && Context.HasMatchFragments();
        isChainedPathSegments = isChainedPathSegments || hasExistingPatterns;

        var method = node.Method;
        if (!method.IsGenericMethod) return node.Arguments[0];
        var genericArgs = method.GetGenericArguments();
        if (genericArgs.Length != 3) return node.Arguments[0];

        var (sourceType, relationshipType, targetType) = (genericArgs[0], genericArgs[1], genericArgs[2]);
        var (sourceAlias, relAlias, targetAlias) = GenerateAliases(sourceType, targetType, isChainedPathSegments);

        Context.Scope.StoreHopAliases(Context.Scope.CurrentHop, sourceAlias, relAlias, targetAlias);
        Context.Scope.StoreHopTypes(Context.Scope.CurrentHop, sourceType, relationshipType, targetType);
        Context.Scope.LastPathSegmentHop = Context.Scope.CurrentHop;

        var traversalDirection = Context.Scope.TraversalDirection ?? GraphTraversalDirection.Outgoing;
        var minDepth = Context.Scope.TraversalMinDepth;
        var maxDepth = Context.Scope.TraversalMaxDepth;

        var pathPattern = BuildMatchPattern(sourceType, relationshipType, targetType,
            sourceAlias, relAlias, targetAlias, traversalDirection, minDepth, maxDepth, isChainedPathSegments);

        var createdAliases = ImmutableArray.Create(sourceAlias, relAlias, targetAlias);
        Context.Scope.CurrentAlias = targetAlias;

        if (isChainedPathSegments)
        {
            var matchSegment = new MatchSegmentFragment(pathPattern, sourceType, relationshipType, targetType, traversalDirection, createdAliases, targetAlias);
            Context.AddFragment(matchSegment);
            Logger.LogDebug("Emitted chained MatchSegmentFragment: {Pattern}", pathPattern);
        }
        else
        {
            var matchRoot = new MatchRootFragment(pathPattern, Labels.GetLabelFromType(sourceType), sourceType, createdAliases, targetAlias);
            Context.AddFragment(matchRoot);
            Logger.LogDebug("Emitted MatchRootFragment: {Pattern}", pathPattern);
        }

        Context.Scope.AdvanceHop();
        // Clear traversal direction and depth so subsequent path segments
        // without explicit Direction/WithDepth use defaults (Outgoing, no limit).
        Context.Scope.ClearTraversalDirection();
        Context.Scope.ClearTraversalDepth();
        Logger.LogDebug("PathSegments complete, CurrentHop={CurrentHop}", Context.Scope.CurrentHop);

        return node.Arguments[0];
    }

    public Expression HandleWithDepth(MethodCallExpression node)
    {
        if (node.Arguments.Count >= 1 && node.Arguments[^1] is ConstantExpression constant)
        {
            Context.Scope.SetTraversalDepth(1, (int)constant.Value!);
            Logger.LogDebug("Set traversal depth: max={MaxDepth}", constant.Value);
        }
        return node.Arguments[0];
    }

    public Expression HandleDirection(MethodCallExpression node)
    {
        if (node.Arguments.Count >= 1 && node.Arguments[^1] is ConstantExpression constant)
        {
            var direction = (GraphTraversalDirection)constant.Value!;
            Context.Scope.SetTraversalDirection(direction);
            Logger.LogDebug("Set traversal direction: {Direction}", direction);
        }
        return node.Arguments[0];
    }

    private (string src, string rel, string tgt) GenerateAliases(Type sourceType, Type targetType, bool isChained)
    {
        var hop = Context.Scope.CurrentHop;
        var aliasManager = Context.AliasManager;

        // Use AliasManager for hop-based alias generation (preserves src0/r0/tgt0 naming)
        var rel = aliasManager.GetHopAlias("r", hop);
        var tgt = aliasManager.GetHopAlias("tgt", hop);

        if (isChained && hop > 0)
        {
            // In chained mode, the source of this hop is normally the target of the previous hop.
            // However, if there was an intermediate Select (e.g., Select(ps => ps.StartNode))
            // that changed the current alias, use the current alias instead.
            var prevTgt = aliasManager.GetHopAlias("tgt", hop - 1);
            var currentAlias = Context.Scope.CurrentAlias;

            if (!string.IsNullOrWhiteSpace(currentAlias) && currentAlias != prevTgt)
            {
                Logger.LogDebug("Chained PathSegments: using CurrentAlias '{CurrentAlias}' instead of '{PrevTgt}'",
                    currentAlias, prevTgt);
                return (currentAlias, rel, tgt);
            }
            return (prevTgt, rel, tgt);
        }

        var src = aliasManager.GetHopAlias("src", hop);
        return (src, rel, tgt);
    }

    private string BuildMatchPattern(Type srcType, Type relType, Type tgtType,
        string srcAlias, string relAlias, string tgtAlias,
        GraphTraversalDirection direction, int? minDepth, int? maxDepth, bool isChained)
    {
        var srcLabel = Labels.GetLabelFromType(srcType);
        var tgtLabel = Labels.GetLabelFromType(tgtType);

        // For interface relationship types (e.g., IRelationship), omit the label entirely
        var relLabel = relType.IsInterface ? "" : $":{Labels.GetLabelFromType(relType)}";

        var (leftArrow, rightArrow) = direction switch
        {
            GraphTraversalDirection.Incoming => ("<-", "-"),
            GraphTraversalDirection.Outgoing => ("-", "->"),
            GraphTraversalDirection.Both => ("-", "-"),
            _ => ("-", "->")
        };

        var depthPattern = (minDepth, maxDepth) switch
        {
            (null, int max) => $"{relLabel}*1..{max}",
            (int min, int max) => $"{relLabel}*{min}..{max}",
            (int min, null) => $"{relLabel}*{min}..",
            _ => relLabel
        };

        var fullRel = $"{leftArrow}[{relAlias}{depthPattern}]{rightArrow}";

        if (isChained)
        {
            // For chained path segments, we normally use the shorthand without the source
            // node (it's assumed to be the previous hop's target). However, if there was
            // an intermediate Select (e.g., Select(ps => ps.StartNode)) that changed the
            // current alias to a different variable, we must emit the full pattern with
            // the explicit source.
            var prevTgt = Context.AliasManager.GetHopAlias("tgt", Context.Scope.CurrentHop - 1);
            if (srcAlias != prevTgt)
            {
                Logger.LogDebug("Chained PathSegments: using full pattern with srcAlias '{SrcAlias}' (expected '{PrevTgt}')",
                    srcAlias, prevTgt);
                return $"({srcAlias}:{srcLabel}){fullRel}({tgtAlias}:{tgtLabel})";
            }
            return $"{fullRel}({tgtAlias}:{tgtLabel})";
        }

        return $"({srcAlias}:{srcLabel}){fullRel}({tgtAlias}:{tgtLabel})";
    }

    private static bool ContainsPathSegmentsInChain(Expression expression)
    {
        if (expression is MethodCallExpression methodCall &&
            (methodCall.Method.Name == "PathSegments" ||
             methodCall.Method.Name == "PathSegmentsIncoming" ||
             methodCall.Method.Name == "PathSegmentsOutgoing"))
            return true;
        return false;
    }
}
