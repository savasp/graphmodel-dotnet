// Copyright 2025 Savas Parastatidis

namespace Cvoya.Graph.Model.Age.Querying.Cypher.Visitors.Core.Modular;

using System.Collections.Immutable;
using System.Linq.Expressions;
using Cvoya.Graph.Model.Age.Querying.Cypher.Visitors;
using Cvoya.Graph.Model.Cypher.Querying.Cypher.Visitors.Core;
using Microsoft.Extensions.Logging;

/// <summary>
/// Specialized visitor for handling projection operations (Select, GroupBy, projected properties).
/// Emits ProjectionFragment and GroupByFragment instances.
/// </summary>
internal sealed class ProjectionFragmentVisitor : FragmentEmittingVisitorBase
{
    public ProjectionFragmentVisitor(CypherQueryContext context, ILogger logger)
        : base(context, logger)
    {
    }

    /// <summary>
    /// Processes a Select() operation and emits the corresponding ProjectionFragment.
    /// Handles identity projections, anonymous types, single properties, and calculated projections.
    /// </summary>
    public Expression HandleSelect(MethodCallExpression node)
    {
        Logger.LogDebug("Processing SELECT projection, CurrentHop={CurrentHop}", Context.Scope.CurrentHop);

        var sourceExpression = node.Arguments[0];
        var lambda = ExtractLambda(node.Arguments[1]);

        if (lambda == null)
        {
            Logger.LogWarning("Where lambda could not be extracted");
            return sourceExpression;
        }

        var body = lambda.Body;
        Console.WriteLine($"[HandleSelect] Lambda body: {body.ToString().Substring(0, Math.Min(200, body.ToString().Length))}");

        // Identity projection: x => x
        if (body == lambda.Parameters[0])
        {
            Logger.LogDebug("Identity projection detected, no fragment emitted");
            return sourceExpression;
        }

        // Check if lambda parameter is PathSegment type (not source expression!)
        var parameter = lambda.Parameters.FirstOrDefault();
        bool isPathSegmentsSource = parameter != null && 
                                    typeof(IGraphPathSegment).IsAssignableFrom(parameter.Type);

        // CRITICAL: PathSegments are visited depth-first BEFORE their Selects.
        // When we have nested PathSegments + Select chains like:
        //   Select_outer(PathSegments_outer(Select_inner(PathSegments_inner(...))))
        // Visiting order is:
        //   1. PathSegments_inner (uses hop 1, increments to 2)
        //   2. PathSegments_outer (uses hop 0, increments to 1 wait no...)
        // Actually visiting order for depth-first is:
        //   1. Deepest first: PathSegments_inner processes at hop 0
        //   2. Select_inner processes
        //   3. PathSegments_outer processes at hop 1
        //   4. Select_outer processes
        // When Select_outer runs, CurrentHop is 2, but it needs hop 1 (PathSegments_outer used hop 1)
        // When Select_inner runs earlier, CurrentHop was 1, and it needs hop 0 (PathSegments_inner used hop 0)
        // Pattern: Select following PathSegments should use CurrentHop - 1!
        if (isPathSegmentsSource && sourceExpression is MethodCallExpression methodCall && 
            methodCall.Method.Name == "PathSegments")
        {
            // The PathSegments that this Select follows has already run and incremented CurrentHop.
            // It used CurrentHop - 1, so that's what we should store.
            Context.Scope.LastPathSegmentHop = Context.Scope.CurrentHop - 1;
            var sourcePreview = sourceExpression.ToString().Substring(0, Math.Min(150, sourceExpression.ToString().Length));
            Console.WriteLine($"[HandleSelect] Setting LastPathSegmentHop = {Context.Scope.LastPathSegmentHop} (CurrentHop={Context.Scope.CurrentHop}), source: {sourcePreview}");
            Logger.LogDebug("Select follows PathSegments directly, will use hop {Hop} (CurrentHop - 1)", 
                Context.Scope.LastPathSegmentHop);
        }
        else if (isPathSegmentsSource)
        {
            Logger.LogDebug("Select has PathSegment parameter but source is NOT direct PathSegments: {Source}", 
                sourceExpression.ToString().Substring(0, Math.Min(100, sourceExpression.ToString().Length)));
        }

        // Anonymous type projection: x => new { ... }
        if (body is NewExpression newExpr)
        {
            HandleAnonymousTypeProjection(newExpr, isPathSegmentsSource, parameter);
        }
        // Single member projection: x => x.Property or ps => ps.EndNode
        else if (body is MemberExpression memberExpr)
        {
            HandleSingleMemberProjection(memberExpr, isPathSegmentsSource);
        }
        // Calculated projection: x => x.Property * 2
        else
        {
            HandleCalculatedProjection(body);
        }

        return sourceExpression;
    }

    /// <summary>
    /// Checks if an expression is a PathSegments method call.
    /// </summary>
    private bool IsPathSegmentsExpression(Expression expression)
    {
        if (expression is MethodCallExpression methodCall)
        {
            var methodName = methodCall.Method.Name;
            return methodName == "PathSegments" || 
                   methodName == "PathSegmentsIncoming" || 
                   methodName == "PathSegmentsOutgoing";
        }
        return false;
    }

    /// <summary>
    /// Processes a GroupBy() operation and emits GroupByFragment + ProjectionFragment.
    /// </summary>
    public Expression HandleGroupBy(MethodCallExpression node)
    {
        Logger.LogDebug("Processing GROUP BY aggregation");

        var sourceExpression = node.Arguments[0];
        var keyLambda = ExtractLambda(node.Arguments[1]);

        if (keyLambda == null)
        {
            Logger.LogWarning("GroupBy key lambda could not be extracted");
            return sourceExpression;
        }

        var expressionVisitor = CreateExpressionVisitor();
        var groupByKey = expressionVisitor.VisitAndReturnCypher(keyLambda.Body);

        // Store the GROUP BY expression in the scope for later use
        Context.Scope.SetGroupByExpression(groupByKey);
        Logger.LogDebug("Set GROUP BY expression: {Key}", groupByKey);

        // Emit GroupByFragment
        var groupingAlias = Context.Scope.CurrentAlias ?? "src0";
        var groupByFragment = new GroupByFragment(groupByKey, groupingAlias);
        EmitFragment(groupByFragment, "GroupByFragment");

        // If there's a result selector (3 arguments), handle projection
        if (node.Arguments.Count == 3)
        {
            var resultLambda = ExtractLambda(node.Arguments[2]);
            if (resultLambda?.Body is NewExpression newExpr)
            {
                HandleGroupByProjection(newExpr, groupByKey);
            }
        }

        return sourceExpression;
    }

    /// <summary>
    /// Handles anonymous type projection: new { Name = x.Name, Age = x.Age }
    /// </summary>
    private void HandleAnonymousTypeProjection(
        NewExpression newExpr,
        bool isPathSegmentsSource,
        ParameterExpression? pathSegmentParameter)
    {
        Logger.LogDebug("Anonymous type projection with {Count} members", newExpr.Arguments.Count);

        // Select projections should override any RETURNS emitted by earlier operations
        Context.Builder.ClearReturn();

        var returnsBuilder = ImmutableArray.CreateBuilder<string>();
        var effectiveParameter = isPathSegmentsSource ? pathSegmentParameter : null;
        var hopNumber = isPathSegmentsSource ? DeterminePathSegmentHop() : -1;

        for (int i = 0; i < newExpr.Arguments.Count; i++)
        {
            var memberName = newExpr.Members?[i]?.Name ?? $"Item{i + 1}";
            var argument = newExpr.Arguments[i];

            if (isPathSegmentsSource && argument == effectiveParameter)
            {
                AddPathSegmentParameterProjection(memberName, hopNumber, returnsBuilder);
                continue;
            }

            string memberExpression;

            if (isPathSegmentsSource && argument is MemberExpression memberExpr && memberExpr.Expression is ParameterExpression paramExpr)
            {
                var propertyName = memberExpr.Member.Name;
                var paramType = paramExpr.Type;

                bool isPathSegmentType = paramType.IsGenericType &&
                                         paramType.GetGenericTypeDefinition().Name.Contains("GraphPathSegment");

                if (isPathSegmentType && (propertyName is "StartNode" or "EndNode" or "Relationship"))
                {
                    var hopAliases = Context.Scope.GetHopAliases(hopNumber);

                    if (hopAliases.HasValue)
                    {
                        var (srcAlias, relAlias, tgtAlias) = hopAliases.Value;

                        memberExpression = propertyName switch
                        {
                            "StartNode" => srcAlias,
                            "EndNode" => tgtAlias,
                            _ => relAlias
                        };
                    }
                    else
                    {
                        Logger.LogWarning("Hop {Hop} aliases not found in storage, falling back to numbered aliases", hopNumber);
                        memberExpression = propertyName switch
                        {
                            "StartNode" => Context.Scope.GetNumberedAliasForHop("src", hopNumber),
                            "EndNode" => Context.Scope.GetNumberedAliasForHop("tgt", hopNumber),
                            _ => Context.Scope.GetNumberedAliasForHop("r", hopNumber)
                        };
                    }
                }
                else
                {
                    var visitor = ShouldUsePathSegmentVisitor(argument, effectiveParameter)
                        ? CreatePathSegmentExpressionVisitor(effectiveParameter!, hopNumber)
                        : CreateExpressionVisitor();
                    memberExpression = visitor.VisitAndReturnCypher(argument);
                }
            }
            else
            {
                var visitor = ShouldUsePathSegmentVisitor(argument, effectiveParameter)
                    ? CreatePathSegmentExpressionVisitor(effectiveParameter!, hopNumber)
                    : CreateExpressionVisitor();
                memberExpression = visitor.VisitAndReturnCypher(argument);
            }

            var returnClause = $"{memberExpression} AS {EscapeIdentifier(memberName)}";
            returnsBuilder.Add(returnClause);

            Context.Builder.AddReturn(returnClause);
            Logger.LogDebug("Projected member: {Name} = {Expression}", memberName, memberExpression);
        }

        var returns = returnsBuilder.ToImmutable();
        var currentAlias = Context.Scope.CurrentAlias ?? "src0";
        var fragment = new ProjectionFragment(returns, currentAlias);
        EmitFragment(fragment, "ProjectionFragment");
    }

    private void AddPathSegmentParameterProjection(string memberName, int hopNumber, ImmutableArray<string>.Builder returnsBuilder)
    {
        hopNumber = Math.Max(0, hopNumber);

        var hopAliases = Context.Scope.GetHopAliases(hopNumber);
        string sourceAlias;
        string relationshipAlias;
        string targetAlias;

        if (hopAliases.HasValue)
        {
            (sourceAlias, relationshipAlias, targetAlias) = hopAliases.Value;
        }
        else
        {
            Logger.LogDebug("Hop {Hop} aliases missing for PathSegment parameter projection; using numbered aliases", hopNumber);
            sourceAlias = Context.Scope.GetNumberedAliasForHop("src", hopNumber);
            relationshipAlias = Context.Scope.GetNumberedAliasForHop("r", hopNumber);
            targetAlias = Context.Scope.GetNumberedAliasForHop("tgt", hopNumber);
        }

        var aliasPrefix = $"c_{EscapeIdentifier(memberName)}";

        void AddReturn(string expressionAlias, string columnAliasSuffix)
        {
            var columnAlias = $"{aliasPrefix}_{columnAliasSuffix}";
            var returnClause = $"{expressionAlias} AS {columnAlias}";
            returnsBuilder.Add(returnClause);
            Context.Builder.AddReturn(returnClause);
            Logger.LogDebug("Projected PathSegment parameter component: {Clause}", returnClause);
        }

        AddReturn(sourceAlias, "src");
        AddReturn(relationshipAlias, "r");
        AddReturn(targetAlias, "tgt");
    }

    private int DeterminePathSegmentHop()
    {
        var currentHop = Math.Max(0, Context.Scope.CurrentHop - 1);
        var lastHop = Context.Scope.LastPathSegmentHop;

        if (lastHop >= currentHop)
        {
            return lastHop;
        }

        return currentHop;
    }

    private bool ShouldUsePathSegmentVisitor(Expression expression, ParameterExpression? pathSegmentParameter)
    {
        if (pathSegmentParameter is null)
        {
            return false;
        }

        var finder = new ParameterReferenceFinder(pathSegmentParameter);
        finder.Visit(expression);
        return finder.Found;
    }

    private AgeExpressionToCypherVisitor CreatePathSegmentExpressionVisitor(ParameterExpression pathSegmentParameter, int hopNumber)
    {
        hopNumber = Math.Max(0, hopNumber);

        var hopAliases = Context.Scope.GetHopAliases(hopNumber);
        if (hopAliases.HasValue)
        {
            var (src, rel, tgt) = hopAliases.Value;
            return new AgeExpressionToCypherVisitor(Context.Builder, Logger, "ps", pathSegmentParameter, src, rel, tgt);
        }

        Logger.LogWarning("Hop {Hop} aliases missing, using numbered aliases", hopNumber);
        var fallbackSrc = Context.Scope.GetNumberedAliasForHop("src", hopNumber);
        var fallbackRel = Context.Scope.GetNumberedAliasForHop("r", hopNumber);
        var fallbackTgt = Context.Scope.GetNumberedAliasForHop("tgt", hopNumber);

        return new AgeExpressionToCypherVisitor(Context.Builder, Logger, "ps", pathSegmentParameter, fallbackSrc, fallbackRel, fallbackTgt);
    }

    private sealed class ParameterReferenceFinder : ExpressionVisitor
    {
        private readonly ParameterExpression target;

        public ParameterReferenceFinder(ParameterExpression targetParameter)
        {
            target = targetParameter;
        }

        public bool Found { get; private set; }

        public override Expression? Visit(Expression? node)
        {
            if (Found || node is null)
            {
                return node;
            }

            if (node == target)
            {
                Found = true;
                return node;
            }

            return base.Visit(node);
        }
    }

    /// <summary>
    /// Handles single member projection: x => x.Name or ps => ps.EndNode
    /// </summary>
    private void HandleSingleMemberProjection(MemberExpression memberExpr, bool isPathSegmentsSource)
    {
        // Each Select should override prior RETURN clauses
        Context.Builder.ClearReturn();

        // Check if this is a PathSegment property projection (StartNode, EndNode, Relationship)
        if (isPathSegmentsSource && memberExpr.Expression is ParameterExpression paramExpr)
        {
            var propertyName = memberExpr.Member.Name;
            var paramType = paramExpr.Type;
            
            // Check if parameter is IGraphPathSegment<,,>
            bool isPathSegmentType = paramType.IsGenericType && 
                                     paramType.GetGenericTypeDefinition().Name.Contains("GraphPathSegment");
            
            if (isPathSegmentType)
            {
                Logger.LogDebug("PathSegment property projection detected: {Property}", propertyName);

                var hopNumber = Context.Scope.LastPathSegmentHop >= 0
                    ? Context.Scope.LastPathSegmentHop
                    : Math.Max(0, Context.Scope.CurrentHop - 1);

                var hopAliases = Context.Scope.GetHopAliases(hopNumber);
                var hopTypes = Context.Scope.GetHopTypes(hopNumber);

                string ResolveAlias(string baseAlias, Func<(string src, string rel, string tgt), string> selector)
                {
                    if (hopAliases.HasValue)
                    {
                        return selector(hopAliases.Value);
                    }

                    return Context.Scope.GetNumberedAliasForHop(baseAlias, hopNumber);
                }

                string aliasToUse;
                Type? projectedType = null;

                switch (propertyName)
                {
                    case "StartNode":
                        aliasToUse = ResolveAlias("src", aliases => aliases.src);
                        projectedType = hopTypes?.src;
                        break;
                    case "EndNode":
                        aliasToUse = ResolveAlias("tgt", aliases => aliases.tgt);
                        projectedType = hopTypes?.tgt;
                        break;
                    case "Relationship":
                        aliasToUse = ResolveAlias("r", aliases => aliases.rel);
                        projectedType = hopTypes?.rel;
                        break;
                    default:
                        aliasToUse = ResolveAlias("tgt", aliases => aliases.tgt);
                        break;
                }

                Context.Scope.CurrentAlias = aliasToUse;
                Context.Scope.CurrentType = projectedType ?? Context.Scope.CurrentType;
                Context.Scope.IsPathSegmentContext = false;
                Context.Scope.LastProjectedExpression = aliasToUse;

                Context.Builder.AddReturn(aliasToUse);
                Logger.LogDebug("Projected PathSegment member: {Property} -> {Alias} (hop {Hop})", propertyName, aliasToUse, hopNumber);

                var pathSegmentReturns = ImmutableArray.Create(aliasToUse);
                var pathSegmentFragment = new ProjectionFragment(pathSegmentReturns, aliasToUse);
                EmitFragment(pathSegmentFragment, "ProjectionFragment");
                return;
            }
        }
        
        // Regular property projection
        var expressionVisitor2 = CreateExpressionVisitor();
        var propertyExpression = expressionVisitor2.VisitAndReturnCypher(memberExpr);

        // Check if this is a complex property (INode type) that requires OPTIONAL MATCH
        // Only emit OptionalMatchFragment when using fragment renderer
        var memberType = memberExpr.Type;
        if (Context.UseFragmentRenderer && typeof(INode).IsAssignableFrom(memberType))
        {
            Logger.LogDebug("Complex property projection detected for type: {Type}", memberType.Name);
            
            // Emit OptionalMatchFragment for complex property traversal
            var sourceAlias = Context.Scope.CurrentAlias ?? "src0";
            var optionalPattern = $"({sourceAlias})-[prop_rel]->(prop_node)";
            var optionalFragment = new OptionalMatchFragment(
                optionalPattern,
                ImmutableArray.Create("prop_rel", "prop_node"),
                ImmutableArray.Create(sourceAlias),
                sourceAlias);
            EmitFragment(optionalFragment, "OptionalMatchFragment for complex property");
            Logger.LogDebug("Emitted OptionalMatchFragment for complex property traversal");
        }

    Context.Builder.AddReturn(propertyExpression);
        Logger.LogDebug("Projected single member: {Expression}", propertyExpression);

        // Emit ProjectionFragment with single field
        var returns = ImmutableArray.Create(propertyExpression);
        var currentAlias = Context.Scope.CurrentAlias ?? "src0";
        var fragment = new ProjectionFragment(returns, currentAlias);
        EmitFragment(fragment, "ProjectionFragment");
    }

    /// <summary>
    /// Handles calculated projection: x => x.Age * 2 or x => x.FirstName + " " + x.LastName
    /// </summary>
    private void HandleCalculatedProjection(Expression expression)
    {
        // Calculated projections also replace prior RETURN clauses
        Context.Builder.ClearReturn();

        var expressionVisitor = CreateExpressionVisitor();
        var calculatedExpression = expressionVisitor.VisitAndReturnCypher(expression);

        Context.Builder.AddReturn(calculatedExpression);
        Logger.LogDebug("Projected calculated expression: {Expression}", calculatedExpression);

        // Emit ProjectionFragment with single calculated field
        var returns = ImmutableArray.Create(calculatedExpression);
        var currentAlias = Context.Scope.CurrentAlias ?? "src0";
        var fragment = new ProjectionFragment(returns, currentAlias);
        EmitFragment(fragment, "ProjectionFragment");
    }

    /// <summary>
    /// Handles projection in GroupBy result selector: .GroupBy(x => x.Category, (key, group) => new { ... })
    /// </summary>
    private void HandleGroupByProjection(NewExpression newExpr, string groupByKey)
    {
        Logger.LogDebug("GroupBy projection with {Count} members", newExpr.Arguments.Count);

        var expressionVisitor = CreateExpressionVisitor();
        var projectionBuilder = ImmutableDictionary.CreateBuilder<string, string>();

        for (int i = 0; i < newExpr.Arguments.Count; i++)
        {
            var memberName = newExpr.Members?[i]?.Name ?? $"Item{i + 1}";
            var argument = newExpr.Arguments[i];

            // Detect aggregations in the projection
            if (argument is MethodCallExpression methodCall && IsAggregationMethod(methodCall.Method.Name))
            {
                var aggregation = methodCall.Method.Name.ToLowerInvariant();
                var targetExpression = methodCall.Arguments.Count > 1
                    ? expressionVisitor.VisitAndReturnCypher(ExtractLambda(methodCall.Arguments[1])?.Body ?? methodCall.Arguments[1])
                    : "group";

                var aggregationExpression = $"{aggregation}({targetExpression})";
                projectionBuilder.Add(memberName, aggregationExpression);
                Context.Builder.AddReturn($"{aggregationExpression} AS {EscapeIdentifier(memberName)}");
            }
            else
            {
                var memberExpression = expressionVisitor.VisitAndReturnCypher(argument);
                projectionBuilder.Add(memberName, memberExpression);
                Context.Builder.AddReturn($"{memberExpression} AS {EscapeIdentifier(memberName)}");
            }
        }

        // Emit ProjectionFragment
        var returns = projectionBuilder.Values.ToImmutableArray();
        var currentAlias = Context.Scope.CurrentAlias ?? "src0";
        var fragment = new ProjectionFragment(returns, currentAlias);
        EmitFragment(fragment, "ProjectionFragment");
    }

    /// <summary>
    /// Checks if a method name represents an aggregation function.
    /// </summary>
    private static bool IsAggregationMethod(string methodName)
    {
        return methodName is "Count" or "Sum" or "Average" or "Min" or "Max" or "Any" or "All";
    }

    /// <summary>
    /// Escapes an identifier for use in Cypher, adding a suffix if it's a reserved word.
    /// </summary>
    private static string EscapeIdentifier(string identifier)
    {
        // List of known reserved words in AGE/Cypher that need escaping
        var reservedWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CONTAINS", "STARTS", "ENDS", "WITH", "IN", "AS", "WHERE", "MATCH", 
            "RETURN", "CREATE", "DELETE", "SET", "REMOVE", "MERGE", "ON", "AND", 
            "OR", "NOT", "XOR", "NULL", "TRUE", "FALSE", "CASE", "WHEN", "THEN", 
            "ELSE", "END", "DISTINCT", "ORDER", "BY", "SKIP", "LIMIT", "OPTIONAL",
            "UNION", "UNWIND", "ALL", "ANY", "NONE", "SINGLE"
        };

        if (reservedWords.Contains(identifier))
        {
            // AGE doesn't support quoted identifiers well, so we add a suffix instead
            return $"{identifier}_";
        }

        return identifier;
    }
}
