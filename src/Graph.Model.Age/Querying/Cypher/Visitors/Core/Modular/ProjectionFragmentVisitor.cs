// Copyright 2025 Savas Parastatidis

namespace Cvoya.Graph.Model.Age.Querying.Cypher.Visitors.Core.Modular;

using System.Collections.Immutable;
using System.Linq.Expressions;
using Cvoya.Graph.Model;
using Cvoya.Graph.Model.Cypher.Querying.Cypher.Visitors.Core;
using Microsoft.Extensions.Logging;

/// <summary>
/// Specialized visitor for handling projection operations (Select).
/// </summary>
internal sealed class ProjectionFragmentVisitor : FragmentEmittingVisitorBase
{
    private readonly NestedCollectHandler _nestedCollectHandler;

    public ProjectionFragmentVisitor(CypherQueryContext context, ILogger logger)
        : base(context, logger)
    {
        _nestedCollectHandler = new NestedCollectHandler(context, logger);
    }

    /// <summary>
    /// Handles Select projection and emits ComplexPropertyLoadingFragment (disabled) to 
    /// indicate that entity-level properties should not be expanded into separate columns.
    /// </summary>
    public Expression HandleSelect(MethodCallExpression node)
    {
        Logger.LogDebug("Processing SELECT clause");
        var sourceExpression = node.Arguments[0];
        var lambda = ExtractLambda(node.Arguments[1]);
        if (lambda == null) return sourceExpression;

        // For projections, disable complex property loading so entity properties are 
        // inlined in the RETURN clause rather than expanded as OPTIONAL MATCH.
        Context.AddFragment(new ComplexPropertyLoadingFragment(false, Context.Scope.CurrentAlias));
        Logger.LogDebug("Emitted ComplexPropertyLoadingFragment (disabled)");

        // Simple parameter projection (x => x) — return entire entity
        if (lambda.Body is ParameterExpression)
        {
            var alias = Context.Scope.CurrentAlias ?? "src0";
            Context.AddFragment(new ProjectionFragment(ImmutableArray.Create(alias), alias));
            Logger.LogDebug("Simple parameter projection: {Alias}", alias);
            return sourceExpression;
        }

        // Anonymous type or member projection
        if (lambda.Body is NewExpression newExpr)
        {
            var returns = new List<string>();
            for (int i = 0; i < newExpr.Arguments.Count; i++)
            {
                var propertyExpr = newExpr.Arguments[i];
                // When Members is null (named record types like `new Foo(x, y)`),
                // extract parameter names from the constructor via reflection so column
                // aliases match the record's constructor parameter names (e.g., "Since", "EndNode")
                // instead of falling back to "Prop0", "Prop1" etc.
                var propertyName = newExpr.Members?[i].Name ?? GetNewExpressionParameterName(newExpr, i) ?? $"Prop{i}";
                var cypherAlias = $"c_{propertyName}";

                if (propertyExpr is MemberExpression member && member.Expression is ParameterExpression)
                {
                    // Simple parameter member access (e.g., p.FirstName) — use ResolveMemberExpression
                    returns.Add($"{ResolveMemberExpression(member)} AS {cypherAlias}");
                }
                else if (propertyExpr is ParameterExpression pathParam &&
                         typeof(IGraphPathSegment).IsAssignableFrom(pathParam.Type))
                {
                    // PathSegment parameter projection (e.g., ps => new { PathSegment = ps })
                    // Need to expand to all 3 components: source, relationship, target
                    var hop = Context.Scope.LastPathSegmentHop >= 0
                        ? Context.Scope.LastPathSegmentHop
                        : 0;
                    var aliases = Context.Scope.GetHopAliases(hop);
                    if (aliases != null)
                    {
                        returns.Add($"{aliases.SourceAlias} AS {cypherAlias}_{aliases.SourceAlias}");
                        returns.Add($"{aliases.RelationshipAlias} AS {cypherAlias}_{aliases.RelationshipAlias}");
                        returns.Add($"{aliases.TargetAlias} AS {cypherAlias}_{aliases.TargetAlias}");
                    }
                    else
                    {
                        // Fallback if no hop aliases available
                        returns.Add($"{Context.Scope.CurrentAlias ?? "src0"} AS {cypherAlias}");
                    }
                }
                else
                {
                    // Check for nested .Select().ToList() on IGrouping parameters
                    if (_nestedCollectHandler.TryHandleNestedCollect(propertyExpr, propertyName, cypherAlias, returns))
                        continue;

                    // Complex expression — use expression visitor with collect fallback
                    var expr = TryResolveWithCollectFallback(propertyExpr, propertyName);
                    returns.Add($"{expr} AS {cypherAlias}");
                }
            }

            var currentAlias = Context.Scope.CurrentAlias ?? "src0";
            Context.AddFragment(new ProjectionFragment(ImmutableArray.CreateRange(returns), currentAlias));
            Logger.LogDebug("Anonymous type projection: {Returns}", string.Join(", ", returns));
            return sourceExpression;
        }

        // Member expression (e.g., Select(p => p.Name))
        if (lambda.Body is MemberExpression memberExpr)
        {
            var visitor = CreateExpressionVisitor();
            var cypherExpr = visitor.VisitAndReturnCypher(lambda.Body);
            Context.Scope.LastProjectedExpression = cypherExpr;

            // Update the scope alias to match the projected member's alias.
            // For example, after .Select(s => s.StartNode) on a path segment where
            // s.StartNode resolves to "src0", set CurrentAlias to "src0".
            var alias = Context.Scope.CurrentAlias ?? "src0";
            var cypherAlias = cypherExpr.Split('.')[0].Trim();
            if (cypherAlias != alias)
            {
                Context.Scope.CurrentAlias = cypherAlias;
                Logger.LogDebug("Updated CurrentAlias from '{OldAlias}' to '{NewAlias}' after member projection",
                    alias, cypherAlias);
            }

            Context.AddFragment(new ProjectionFragment(ImmutableArray.Create(cypherExpr), cypherAlias));
            Logger.LogDebug("Member projection: {Expression} -> alias {Alias}", cypherExpr, cypherAlias);
            return sourceExpression;
        }

        // Fallback: use expression visitor
        var fallbackVisitor = CreateExpressionVisitor();
        var cypherResult = fallbackVisitor.VisitAndReturnCypher(lambda.Body);
        Context.Scope.LastProjectedExpression = cypherResult;
        var currentAlias2 = Context.Scope.CurrentAlias ?? "src0";
        Context.AddFragment(new ProjectionFragment(ImmutableArray.Create(cypherResult), currentAlias2));
        return sourceExpression;
    }

    public Expression HandleGroupBy(MethodCallExpression node)
    {
        Logger.LogDebug("Processing GROUP BY clause");
        var sourceExpression = node.Arguments[0];

        if (node.Arguments.Count >= 2)
        {
            var lambda = ExtractLambda(node.Arguments[1]);
            if (lambda != null)
            {
                var visitor = CreateExpressionVisitor();
                var groupExpr = visitor.VisitAndReturnCypher(lambda.Body);
                var groupAlias = Context.Scope.CurrentAlias ?? "src0";

                // Fix for Traverse(): identity GroupBy on target entities must group by source
                if (lambda.Body is ParameterExpression && groupExpr == "tgt0")
                {
                    groupExpr = "src0";
                    groupAlias = "src0";
                    Context.Scope.IdentityGroupByRewritten = true;
                    Logger.LogDebug("Rewriting identity GroupBy on Traverse: tgt0 -> src0");
                }

                Context.AddFragment(new GroupByFragment(groupExpr, groupAlias));
                Logger.LogDebug("GROUP BY expression: {Expression}", groupExpr);
            }
        }

        return sourceExpression;
    }

    private string ResolveMemberExpression(MemberExpression member)
    {
        // Handle IGrouping.Key — map to the GROUP BY expression from the last GroupByFragment
        if (member.Member.Name == "Key" && member.Expression is ParameterExpression groupParam
            && groupParam.Type.IsGenericType
            && groupParam.Type.GetGenericTypeDefinition().Name.Contains("IGrouping"))
        {
            var groupByFragment = Context.FragmentSequence.OfType<GroupByFragment>().LastOrDefault();
            if (groupByFragment != null)
                return groupByFragment.Expression;
        }

        if (member.Expression is ParameterExpression && typeof(IGraphPathSegment).IsAssignableFrom(member.Expression.Type))
        {
            // Use the stored hop aliases from scope to resolve path segment members
            // For chained patterns: hop 0 = (src0, r0, tgt0), hop 1 = (tgt0, r1, tgt1), etc.
            var lastPathSegmentHop = Context.Scope.LastPathSegmentHop;
            var hopAliases = lastPathSegmentHop >= 0
                ? Context.Scope.GetHopAliases(lastPathSegmentHop)
                : null;

            if (hopAliases != null)
            {
                return member.Member.Name switch
                {
                    nameof(IGraphPathSegment.StartNode) => hopAliases.SourceAlias,
                    nameof(IGraphPathSegment.EndNode) => hopAliases.TargetAlias,
                    nameof(IGraphPathSegment.Relationship) => hopAliases.RelationshipAlias,
                    _ => $"{hopAliases.TargetAlias}.{member.Member.Name}"
                };
            }

            // Fallback: use CurrentHop-1 as the path segment hop if LastPathSegmentHop not set
            var hop = Context.Scope.CurrentHop > 0 ? Context.Scope.CurrentHop - 1 : 0;
            var fallbackAliases = Context.Scope.GetHopAliases(hop);
            if (fallbackAliases != null)
            {
                return member.Member.Name switch
                {
                    nameof(IGraphPathSegment.StartNode) => fallbackAliases.SourceAlias,
                    nameof(IGraphPathSegment.EndNode) => fallbackAliases.TargetAlias,
                    nameof(IGraphPathSegment.Relationship) => fallbackAliases.RelationshipAlias,
                    _ => $"{fallbackAliases.TargetAlias}.{member.Member.Name}"
                };
            }

            return member.Member.Name;
        }
        return $"{Context.Scope.CurrentAlias ?? "src0"}.{member.Member.Name}";
    }

    // MapPropertyName imported via `using static ExpressionTranslationHelper`.

    private string TryResolveExpression(Expression expr, AgeExpressionToCypherVisitor visitor)
    {
        try { return visitor.VisitAndReturnCypher(expr); }
        catch (Exception ex)
        {
            Logger?.LogWarning(ex, "Failed to process projection fragment for expression: {Expression}", expr);
            throw new InvalidOperationException(
                $"Failed to process projection fragment for expression '{expr}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Attempts to resolve a complex expression with fallback to nested collect detection.
    /// Used as a second-chance handler for expressions that fail the primary expression visitor.
    /// </summary>
    private string TryResolveWithCollectFallback(Expression propertyExpr, string propertyName)
    {
        try
        {
            var visitor = CreateExpressionVisitor();
            return visitor.VisitAndReturnCypher(propertyExpr);
        }
        catch (Exception ex)
        {
            Logger?.LogWarning(ex, "Failed to process projection fragment for expression: {Expression}", propertyExpr);
            throw new InvalidOperationException(
                $"Failed to process projection fragment for expression '{propertyExpr}': {ex.Message}", ex);
        }
    }

    private static string? GetNewExpressionParameterName(NewExpression newExpr, int parameterIndex)
    {
        var constructor = newExpr.Constructor;
        if (constructor == null)
            return null;
        var parameters = constructor.GetParameters();
        if (parameterIndex < 0 || parameterIndex >= parameters.Length)
            return null;
        return parameters[parameterIndex].Name;
    }
}
