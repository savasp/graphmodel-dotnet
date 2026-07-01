// Copyright 2025 Savas Parastatidis

namespace Cvoya.Graph.Model.Age.Querying.Cypher.Visitors.Core.Modular;

using System.Collections.Immutable;
using System.Linq.Expressions;
using Cvoya.Graph.Model;
using Cvoya.Graph.Model.Cypher.Querying.Cypher.Visitors.Core;
using Microsoft.Extensions.Logging;

/// <summary>
/// Detects and handles degree query patterns in Where predicates:
///   .Where(p => p.NavigationProperty.Count(lambda) comparison_op value)
/// Translates to:
///   MATCH (src)-[r:REL]->(tgt)
///   [WHERE tgt.predicate]
///   WITH src, count(r) AS degree
///   WHERE degree comparison_op value
///   RETURN src
///
/// This handler fixes three limitations of the older ClosureCaptureHandler:
/// 1. Avoids invalid AGE size() with pattern expressions
/// 2. Generates query-level fragments (not inline Cypher strings)
/// 3. Extracts relationship type from expression tree metadata (no runtime Compile()())
/// </summary>
internal sealed class DegreeQueryHandler
{
    private readonly CypherQueryContext _context;
    private readonly ILogger _logger;

    public DegreeQueryHandler(CypherQueryContext context, ILogger logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Attempts to handle a Where clause that contains a degree query pattern.
    /// The source expression must already be visited before calling this method
    /// (so that the initial MatchRootFragment is in the fragment sequence).
    /// Returns true if the pattern was detected and fragments were emitted.
    /// </summary>
    public bool TryHandleDegreeWhereClause(MethodCallExpression whereNode)
    {
        ArgumentNullException.ThrowIfNull(whereNode);

        if (whereNode.Arguments.Count < 2)
            return false;

        // Step 1: Extract the predicate lambda
        var lambda = ExtractLambda(whereNode.Arguments[1]);
        if (lambda == null || lambda.Body is not BinaryExpression comparison)
            return false;

        // Step 2: Verify the lambda body is a comparison (>, >=, <, <=, ==, !=)
        if (!IsComparisonOperator(comparison.NodeType))
            return false;

        // Step 3: Identify which side is the Count() call and which is the threshold
        MethodCallExpression? countCall = null;
        Expression? thresholdExpr = null;

        if (comparison.Left is MethodCallExpression leftCall && IsCountMethod(leftCall))
        {
            countCall = leftCall;
            thresholdExpr = comparison.Right;
        }
        else if (comparison.Right is MethodCallExpression rightCall && IsCountMethod(rightCall))
        {
            countCall = rightCall;
            thresholdExpr = comparison.Left;
        }
        else
        {
            return false;
        }

        // Step 4: Extract the source expression and optional predicate from the Count call
        Expression? sourceExpr;
        Expression? countPredicateExpr;

        if (countCall.Object != null && countCall.Arguments.Count == 1)
        {
            // Instance: obj.Count(lambda)
            sourceExpr = countCall.Object;
            countPredicateExpr = countCall.Arguments[0];
        }
        else if (countCall.Object == null && countCall.Arguments.Count == 2)
        {
            // Static: Enumerable.Count(obj, lambda)
            sourceExpr = countCall.Arguments[0];
            countPredicateExpr = countCall.Arguments[1];
        }
        else if (countCall.Object != null && countCall.Arguments.Count == 0)
        {
            // Instance: obj.Count() (no predicate)
            sourceExpr = countCall.Object;
            countPredicateExpr = null;
        }
        else if (countCall.Object == null && countCall.Arguments.Count == 1)
        {
            // Static: Enumerable.Count(obj) (no predicate)
            sourceExpr = countCall.Arguments[0];
            countPredicateExpr = null;
        }
        else
        {
            return false;
        }

        // Step 5: Verify the source is a MemberExpression on the lambda parameter
        // e.g., p.Friends where p is the Where lambda parameter
        if (sourceExpr is not MemberExpression memberExpr)
            return false;

        if (memberExpr.Expression != lambda.Parameters.FirstOrDefault())
            return false;

        // Step 6: Extract the relationship type from the navigation property's type
        // e.g., IEnumerable<KnowsRelationship> → KnowsRelationship
        var relationshipType = ExtractRelationshipType(memberExpr.Type);
        if (relationshipType == null || !typeof(IRelationship).IsAssignableFrom(relationshipType))
            return false;

        // Step 7: Resolve the relationship label
        var relationshipLabel = ResolveRelationshipLabel(relationshipType);
        if (string.IsNullOrWhiteSpace(relationshipLabel))
        {
            _logger.LogWarning("Could not resolve relationship label for {Type}", relationshipType.Name);
            relationshipLabel = relationshipType.Name.Replace("Relationship", "", StringComparison.Ordinal);
        }

        // Step 8: Generate the fragment sequence
        return EmitDegreeQueryFragments(
            relationshipType,
            relationshipLabel,
            comparison.NodeType,
            thresholdExpr,
            countPredicateExpr);
    }

    /// <summary>
    /// Emits the fragment sequence for a degree query:
    /// MatchSegmentFragment → [WhereFragment for Count predicate] → WithFragment → WhereFragment for degree → ProjectionFragment
    /// </summary>
    private bool EmitDegreeQueryFragments(
        Type relationshipType,
        string relationshipLabel,
        ExpressionType comparisonOperator,
        Expression? thresholdExpr,
        Expression? countPredicateExpr)
    {
        var sourceAlias = _context.Scope.CurrentAlias ?? "src0";
        var relAlias = $"r{GetHopFromAlias(sourceAlias)}";
        var tgtAlias = $"tgt{GetHopFromAlias(sourceAlias)}";

        // Step 8a: Emit MatchSegmentFragment for the relationship traversal pattern
        // Build pattern like: (src0:PersonNode)-[r0:KnowsRelationship]->(tgt0)
        var rootType = _context.Scope.RootType;
        var srcLabel = Labels.GetLabelFromType(rootType);
        var pathPattern = BuildPathPattern(srcLabel, relationshipLabel, sourceAlias, relAlias, tgtAlias);

        var createdAliases = ImmutableArray.Create(sourceAlias, relAlias, tgtAlias);
        var segment = new MatchSegmentFragment(
            pathPattern,
            rootType,
            relationshipType,
            relationshipType,
            GraphTraversalDirection.Outgoing,
            createdAliases,
            tgtAlias);
        _context.AddFragment(segment);

        _logger.LogDebug("DegreeQuery: emitted MatchSegmentFragment: {Pattern}", pathPattern);

        // Step 8b: If Count has a predicate, translate it to a WHERE clause on the target node
        // e.g., friend => friend.Age > 30 becomes WHERE tgt0.Age > 30
        if (countPredicateExpr != null)
        {
            var countLambda = ExtractLambda(countPredicateExpr);
            if (countLambda != null)
            {
                try
                {
                    var expressionVisitor = new AgeExpressionToCypherVisitor(
                        _context, _logger, tgtAlias);
                    var predicateCypher = expressionVisitor.VisitAndReturnCypher(countLambda.Body);
                    var whereFragment = new WhereFragment(
                        predicateCypher,
                        ImmutableArray.Create(tgtAlias),
                        tgtAlias);
                    _context.AddFragment(whereFragment);
                    _logger.LogDebug("DegreeQuery: emitted WHERE for Count predicate: {Predicate}", predicateCypher);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "DegreeQuery: Failed to translate Count predicate, proceeding without filter");
                }
            }
        }

        // Step 8c: Emit WithFragment for "WITH src, count(r) AS degree"
        var degreeAlias = "degree";
        var withExpression = $"{sourceAlias}, count({relAlias}) AS {degreeAlias}";
        var withFragment = new WithFragment(
            withExpression,
            ImmutableArray.Create(sourceAlias, relAlias),
            degreeAlias);
        _context.AddFragment(withFragment);

        _logger.LogDebug("DegreeQuery: emitted WithFragment: {WithExpr}", withExpression);

        // Step 8d: Emit WhereFragment for the degree comparison
        // e.g., "degree > 5" or "degree >= 3"
        var cypherOperator = comparisonOperator switch
        {
            ExpressionType.GreaterThan => ">",
            ExpressionType.GreaterThanOrEqual => ">=",
            ExpressionType.LessThan => "<",
            ExpressionType.LessThanOrEqual => "<=",
            ExpressionType.Equal => "=",
            ExpressionType.NotEqual => "<>",
            _ => ">"
        };

        var thresholdCypher = ResolveThreshold(thresholdExpr);
        var degreeWhere = $"{degreeAlias} {cypherOperator} {thresholdCypher}";
        var degreeWhereFragment = new WhereFragment(
            degreeWhere,
            ImmutableArray.Create(degreeAlias),
            degreeAlias);
        _context.AddFragment(degreeWhereFragment);

        _logger.LogDebug("DegreeQuery: emitted WHERE for degree: {DegreeWhere}", degreeWhere);

        // Step 8e: Emit ProjectionFragment for RETURN
        // "RETURN src0"
        var projectionFragment = new ProjectionFragment(
            ImmutableArray.Create(sourceAlias),
            sourceAlias);
        _context.AddFragment(projectionFragment);

        _logger.LogDebug(
            "DegreeQuery complete: MATCH {Pattern} {PredicateWhere}WITH {WithExpr} WHERE {DegreeWhere} RETURN {Return}",
            pathPattern,
            countPredicateExpr != null ? "WHERE predicate " : "",
            withExpression,
            degreeWhere,
            sourceAlias);

        return true;
    }

    /// <summary>
    /// Builds the MATCH path pattern string.
    /// </summary>
    private static string BuildPathPattern(
        string sourceLabel,
        string relationshipLabel,
        string sourceAlias,
        string relAlias,
        string targetAlias)
    {
        // For interface relationship types, omit the label
        var relLabel = string.IsNullOrWhiteSpace(relationshipLabel)
            ? ""
            : $":{relationshipLabel}";

        return $"({sourceAlias}:{sourceLabel})-[{relAlias}{relLabel}]->({targetAlias})";
    }

    /// <summary>
    /// Extracts the relationship element type from a collection type.
    /// Handles IEnumerable&lt;T&gt;, IList&lt;T&gt;, List&lt;T&gt;, ICollection&lt;T&gt; etc.
    /// </summary>
    private static Type? ExtractRelationshipType(Type collectionType)
    {
        if (collectionType.IsGenericType)
        {
            var genDef = collectionType.GetGenericTypeDefinition();
            if (genDef == typeof(IEnumerable<>) || genDef == typeof(IList<>)
                || genDef == typeof(List<>) || genDef == typeof(ICollection<>))
                return collectionType.GetGenericArguments()[0];
        }

        // Check interfaces for IEnumerable<T>
        foreach (var iface in collectionType.GetInterfaces())
        {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                return iface.GetGenericArguments()[0];
        }

        return null;
    }

    /// <summary>
    /// Resolves the relationship label from the CLR type, using Labels infrastructure.
    /// </summary>
    private static string ResolveRelationshipLabel(Type relationshipType)
    {
        try { return Labels.GetLabelFromType(relationshipType); }
        catch
        {
            // Fallback: strip "Relationship" suffix from type name
            var name = relationshipType.Name;
            return name.EndsWith("Relationship", StringComparison.Ordinal)
                ? name[..^"Relationship".Length]
                : name;
        }
    }

    /// <summary>
    /// Resolves the threshold expression to a Cypher parameter reference.
    /// e.g., constant 5 → "$param_0"
    /// </summary>
    private string ResolveThreshold(Expression? thresholdExpr)
    {
        if (thresholdExpr == null)
            return "0";

        // Direct constant
        if (thresholdExpr is ConstantExpression constant)
            return _context.ParameterStore.Add(constant.Value);

        // Try compile-time evaluation
        try
        {
            var lambda = Expression.Lambda<Func<object>>(
                Expression.Convert(thresholdExpr, typeof(object)));
            var value = lambda.Compile()();
            return _context.ParameterStore.Add(value);
        }
        catch
        {
            // Fallback: visit as Cypher expression
            try
            {
                var visitor = new AgeExpressionToCypherVisitor(_context, _logger);
                return visitor.VisitAndReturnCypher(thresholdExpr);
            }
            catch
            {
                return thresholdExpr.ToString() ?? "0";
            }
        }
    }

    /// <summary>
    /// Extracts the hop number from an alias like "src0" → 0, "src1" → 1.
    /// </summary>
    private static int GetHopFromAlias(string alias)
    {
        if (alias.Length > 3 && int.TryParse(alias[3..], out var hop))
            return hop;
        return 0;
    }

    private static LambdaExpression? ExtractLambda(Expression expression)
    {
        if (expression is UnaryExpression { NodeType: ExpressionType.Quote, Operand: LambdaExpression lambda })
            return lambda;
        return expression as LambdaExpression;
    }

    private static bool IsComparisonOperator(ExpressionType type)
        => type is ExpressionType.GreaterThan or ExpressionType.GreaterThanOrEqual
            or ExpressionType.LessThan or ExpressionType.LessThanOrEqual
            or ExpressionType.Equal or ExpressionType.NotEqual;

    private static bool IsCountMethod(MethodCallExpression node)
        => node.Method.Name == "Count" || node.Method.Name == "LongCount";
}
