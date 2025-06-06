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

using System.Linq.Expressions;
using System.Text;
using Cvoya.Graph.Model;
using Cvoya.Graph.Provider.Neo4j.Linq.Builders;
using Cvoya.Graph.Provider.Neo4j.Linq.Processors;
using Cvoya.Graph.Provider.Neo4j.Schema;

namespace Cvoya.Graph.Provider.Neo4j.Linq;

/// <summary>
/// Coordinates the entire query building process by orchestrating expression builders and processors
/// </summary>
internal class QueryBuildCoordinator
{
    private readonly ExpressionBuilderDispatcher _expressionDispatcher;

    public QueryBuildCoordinator()
    {
        _expressionDispatcher = new ExpressionBuilderDispatcher();
    }

    /// <summary>
    /// Builds a complete Cypher query from a LINQ expression tree
    /// </summary>
    public (string cypher, Dictionary<string, object?> parameters, CypherBuildContext context) BuildQuery(
        Expression expression,
        Type elementType,
        Neo4jGraphProvider provider,
        GraphQueryContext? queryContext = null)
    {
        var context = new CypherBuildContext();

        // Preprocess to detect Last operations
        context.IsLastOperation = DetectLastOperation(expression);

        // Set context information from queryContext if available
        if (queryContext != null)
        {
            context.QueryRootType = queryContext.RootType;
        }

        // Set the root type from the element type
        context.RootType = elementType;

        // Detect aggregate/scalar operations
        if (expression is MethodCallExpression methodCall)
        {
            var methodName = methodCall.Method.Name;
            context.IsScalarResult = methodName switch
            {
                "Count" or "LongCount" or "Any" or "All" or "First" or "FirstOrDefault" or
                "Last" or "LastOrDefault" or "Single" or "SingleOrDefault" or
                "Sum" or "Average" or "Min" or "Max" => true,
                _ => false
            };
        }

        // Process the expression tree
        ProcessExpression(expression, elementType, context, provider);

        // Handle inheritance labels for polymorphic queries
        if (context.RootType != null && !context.IsScalarResult && !context.IsPathResult)
        {
            var inheritanceLabels = Neo4jTypeManager.GetLabelsForAssignableTypes(context.RootType).ToList();
            if (inheritanceLabels.Count > 1)
            {
                context.InheritanceLabels = inheritanceLabels;
            }
        }

        // Find the source entity type by walking up the expression tree if needed
        if (context.RootType == null || context.RootType == typeof(object))
        {
            var sourceEntityType = FindSourceEntityType(expression);
            if (sourceEntityType != null)
            {
                context.RootType = sourceEntityType;
            }
        }

        // Build the final Cypher query
        var cypher = BuildCypherQuery(context);

        return (cypher, context.Parameters, context);
    }

    /// <summary>
    /// Processes an expression node recursively
    /// </summary>
    private void ProcessExpression(
        Expression expression,
        Type elementType,
        CypherBuildContext context,
        Neo4jGraphProvider provider)
    {
        switch (expression)
        {
            case MethodCallExpression methodCall:
                ProcessMethodCall(methodCall, elementType, context, provider);
                break;
            case UnaryExpression unary when unary.NodeType == ExpressionType.Convert:
                // Handle Convert expressions (like casting from IQueryable<T> to IGraphQueryable<T>)
                ProcessExpression(unary.Operand, elementType, context, provider);
                break;
            case ConstantExpression constant when constant.Type.IsGenericType &&
                constant.Type.GetGenericTypeDefinition() == typeof(GraphQueryable<>):

                // Root queryable - extract context from the queryable instance
                context.RootType = elementType;

                // Try to extract the GraphQueryContext from the queryable instance
                if (constant.Value != null)
                {
                    var contextProperty = constant.Type.GetProperty("Context");
                    if (contextProperty?.GetValue(constant.Value) is GraphQueryContext queryContext)
                    {
                        context.QueryRootType = queryContext.RootType;
                    }
                }

                // Clear any existing match pattern to avoid duplicate MATCH clauses
                context.Match.Clear();

                // Generate different patterns based on query root type
                if (context.QueryRootType == GraphQueryContext.QueryRootType.Relationship)
                {
                    // For relationship queries, use n1 and n2 for nodes
                    context.CurrentAlias = "r1"; // Change current alias to relationship

                    var labels = Neo4jTypeManager.GetLabelsForAssignableTypes(elementType).ToList();

                    if (labels.Count == 1)
                    {
                        // Single label case: (n1)-[r1:type]->(n2)
                        context.Match.Append($"(n1)-[r1:{labels.First()}]->(n2)");
                    }
                    else if (labels.Count > 1)
                    {
                        // Multiple labels case: (n1)-[r1]->(n2) WHERE r1:Label1 OR r1:Label2
                        context.Match.Append("(n1)-[r1]->(n2)");
                        var labelConditions = labels.Select(l => $"r1:{l}");
                        if (context.Where.Length > 0) context.Where.Append(" AND ");
                        context.Where.Append($"({string.Join(" OR ", labelConditions)})");
                    }

                    context.Return = "r1";
                }
                else
                {
                    // Regular node queries
                    context.CurrentAlias = "n";
                    context.Return = "n";
                }
                break;
        }
    }

    /// <summary>
    /// Processes method call expressions
    /// </summary>
    private void ProcessMethodCall(
        MethodCallExpression methodCall,
        Type elementType,
        CypherBuildContext context,
        Neo4jGraphProvider provider)
    {
        // Debug output to see what methods are being called
        System.Diagnostics.Debug.WriteLine($"DEBUG: Processing method call: {methodCall.Method.Name}, DeclaringType: {methodCall.Method.DeclaringType?.Name}");

        // For instance methods, process the object (this) as the source
        if (methodCall.Object != null)
        {
            ProcessExpression(methodCall.Object, GetSourceElementType(methodCall), context, provider);
        }
        // For static extension methods, process the first argument as the source
        else if (methodCall.Arguments.Count > 0 && methodCall.Arguments[0] is Expression source)
        {
            ProcessExpression(source, GetSourceElementType(methodCall), context, provider);
        }

        // Handle different types of method calls
        if (methodCall.Method.DeclaringType == typeof(GraphQueryExtensions))
        {
            ProcessGraphExtensionMethod(methodCall, elementType, context, provider);
        }
        else if (methodCall.Method.DeclaringType?.FullName == "Cvoya.Graph.Model.GraphQueryableExtensions")
        {
            ProcessGraphQueryableExtensionMethod(methodCall, elementType, context, provider);
        }
        else if (methodCall.Method.DeclaringType == typeof(Queryable))
        {
            ProcessStandardLinqMethod(methodCall, elementType, context, provider);
        }
        else if (IsGraphTraversalMethod(methodCall))
        {
            System.Diagnostics.Debug.WriteLine($"DEBUG: Detected graph traversal method: {methodCall.Method.Name}");
            ProcessGraphTraversalMethod(methodCall, elementType, context, provider);
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"DEBUG: Unhandled method call: {methodCall.Method.Name} from {methodCall.Method.DeclaringType?.Name}");
        }
    }

    /// <summary>
    /// Processes standard LINQ methods (Where, Select, OrderBy, etc.)
    /// </summary>
    private void ProcessStandardLinqMethod(
        MethodCallExpression methodCall,
        Type elementType,
        CypherBuildContext context,
        Neo4jGraphProvider provider)
    {
        switch (methodCall.Method.Name)
        {
            case "Where":
                var predicate = ExtractLambdaFromQuote(methodCall.Arguments[1]);
                if (predicate != null)
                {
                    WhereProcessor.ProcessWhere(predicate, context);
                }
                break;

            case "Select":
                var selector = ExtractLambdaFromQuote(methodCall.Arguments[1]);
                if (selector != null)
                {
                    if (IsSelectAfterGroupBy(methodCall))
                    {
                        SelectProcessor.ProcessSelect(selector, context);
                    }
                    else
                    {
                        SelectProcessor.ProcessSelect(selector, context);
                    }
                }
                break;

            case "OrderBy":
                var keySelector = ExtractLambdaFromQuote(methodCall.Arguments[1]);
                if (keySelector != null)
                {
                    OrderByProcessor.ProcessOrderBy(keySelector, true, context);
                }
                break;

            case "OrderByDescending":
                var descKeySelector = ExtractLambdaFromQuote(methodCall.Arguments[1]);
                if (descKeySelector != null)
                {
                    OrderByProcessor.ProcessOrderBy(descKeySelector, false, context);
                }
                break;

            case "ThenBy":
                var thenKeySelector = ExtractLambdaFromQuote(methodCall.Arguments[1]);
                if (thenKeySelector != null)
                {
                    OrderByProcessor.ProcessOrderBy(thenKeySelector, true, context);
                }
                break;

            case "ThenByDescending":
                var thenDescKeySelector = ExtractLambdaFromQuote(methodCall.Arguments[1]);
                if (thenDescKeySelector != null)
                {
                    OrderByProcessor.ProcessOrderBy(thenDescKeySelector, false, context);
                }
                break;

            case "GroupBy":
                GroupByProcessor.ProcessGroupBy(methodCall, context);
                break;

            case "Take":
                if (methodCall.Arguments[1] is ConstantExpression takeCount)
                {
                    context.Limit = (int)takeCount.Value!;
                }
                break;

            case "Skip":
                if (methodCall.Arguments[1] is ConstantExpression skipCount)
                {
                    context.Skip = (int)skipCount.Value!;
                }
                break;

            case "Distinct":
                context.IsDistinct = true;
                break;

            case "Count":
            case "LongCount":
                context.IsScalarResult = true;
                context.Return = $"COUNT({context.CurrentAlias})";
                break;

            case "Any":
                context.IsScalarResult = true;
                context.Return = $"COUNT({context.CurrentAlias}) > 0";
                break;

            case "All":
                if (methodCall.Arguments.Count > 1)
                {
                    var allPredicate = ExtractLambdaFromQuote(methodCall.Arguments[1]);
                    if (allPredicate != null)
                    {
                        var negatedPredicate = Expression.Lambda(
                            Expression.Not(allPredicate.Body),
                            allPredicate.Parameters);
                        WhereProcessor.ProcessWhere(negatedPredicate, context);
                        context.IsScalarResult = true;
                        context.Return = $"COUNT({context.CurrentAlias}) = 0";
                    }
                }
                break;

            case "First":
            case "FirstOrDefault":
                context.Limit = 1;
                if (methodCall.Arguments.Count > 1)
                {
                    var firstPredicate = ExtractLambdaFromQuote(methodCall.Arguments[1]);
                    if (firstPredicate != null)
                    {
                        WhereProcessor.ProcessWhere(firstPredicate, context);
                    }
                }
                break;

            case "Last":
            case "LastOrDefault":
                context.Limit = 1;
                context.IsLastOperation = true;
                if (methodCall.Arguments.Count > 1)
                {
                    var lastPredicate = ExtractLambdaFromQuote(methodCall.Arguments[1]);
                    if (lastPredicate != null)
                    {
                        WhereProcessor.ProcessWhere(lastPredicate, context);
                    }
                }
                break;

            case "Single":
            case "SingleOrDefault":
                context.Limit = 2; // Limit 2 to detect if more than one result
                if (methodCall.Arguments.Count > 1)
                {
                    var singlePredicate = ExtractLambdaFromQuote(methodCall.Arguments[1]);
                    if (singlePredicate != null)
                    {
                        WhereProcessor.ProcessWhere(singlePredicate, context);
                    }
                }
                break;
        }
    }

    /// <summary>
    /// Processes graph traversal methods
    /// </summary>
    private void ProcessGraphTraversalMethod(
        MethodCallExpression methodCall,
        Type elementType,
        CypherBuildContext context,
        Neo4jGraphProvider provider)
    {
        switch (methodCall.Method.Name)
        {
            case "TraversalToInternal":
                TraversalProcessor.ProcessTraversalTo(methodCall, context);
                break;

            case "TraversalRelationshipsInternal":
                TraversalProcessor.ProcessTraversalRelationships(methodCall, context);
                break;

            case "TraversalPathsInternal":
                TraversalProcessor.ProcessTraversalPaths(methodCall, context);
                break;
        }
    }

    /// <summary>
    /// Processes graph extension methods
    /// </summary>
    private void ProcessGraphExtensionMethod(
        MethodCallExpression methodCall,
        Type elementType,
        CypherBuildContext context,
        Neo4jGraphProvider provider)
    {
        // Handle graph-specific extension methods
        // This would include methods like ConnectedBy, WithDepth, etc.
        // For now, delegate to the original implementation
    }

    /// <summary>
    /// Processes graph queryable extension methods
    /// </summary>
    private void ProcessGraphQueryableExtensionMethod(
        MethodCallExpression methodCall,
        Type elementType,
        CypherBuildContext context,
        Neo4jGraphProvider provider)
    {
        // Handle graph queryable extension methods
        switch (methodCall.Method.Name)
        {
            case "TraversePath":
                ProcessTraversePath(methodCall, elementType, context, provider);
                break;
            default:
                // For other graph queryable methods, delegate to the original implementation
                System.Diagnostics.Debug.WriteLine($"DEBUG: Unhandled GraphQueryableExtension method: {methodCall.Method.Name}");
                break;
        }
    }

    /// <summary>
    /// Processes TraversePath method calls
    /// </summary>
    private void ProcessTraversePath(
        MethodCallExpression methodCall,
        Type elementType,
        CypherBuildContext context,
        Neo4jGraphProvider provider)
    {
        // Extract the generic arguments: TSource, TRelationship, TTarget
        var genericArgs = methodCall.Method.GetGenericArguments();
        var sourceType = genericArgs[0]; // TSource
        var relationshipType = genericArgs[1]; // TRelationship
        var targetType = genericArgs[2]; // TTarget

        // Get labels
        var relationshipLabel = Neo4jTypeManager.GetLabel(relationshipType);
        var targetLabel = Neo4jTypeManager.GetLabel(targetType);
        var sourceLabel = Neo4jTypeManager.GetLabel(sourceType);

        // Get current source alias and create new aliases
        var sourceAlias = context.CurrentAlias;
        var relationshipAlias = context.GetNextAlias("r");
        var targetAlias = context.GetNextAlias("t");

        // For TraversePath, we want to replace the existing match with a complete traversal pattern
        // Clear any existing MATCH content and build the complete pattern
        context.Match.Clear();

        // Determine the relationship pattern based on traversal depth
        string relationshipPattern;
        if (context.TraversalDepth.HasValue)
        {
            // Single depth specified
            var depth = context.TraversalDepth.Value;
            if (depth == 1)
            {
                relationshipPattern = $"[{relationshipAlias}:{relationshipLabel}]";
            }
            else
            {
                relationshipPattern = $"[{relationshipAlias}:{relationshipLabel}*{depth}]";
            }
        }
        else if (context.MinTraversalDepth.HasValue && context.MaxTraversalDepth.HasValue)
        {
            // Depth range specified
            relationshipPattern = $"[{relationshipAlias}:{relationshipLabel}*{context.MinTraversalDepth}..{context.MaxTraversalDepth}]";
        }
        else
        {
            // Default to single hop
            relationshipPattern = $"[{relationshipAlias}:{relationshipLabel}]";
        }

        var matchClause = $"({sourceAlias}:{sourceLabel})-{relationshipPattern}->({targetAlias}:{targetLabel})";
        context.Match.Append(matchClause);

        // Update the current alias to the target for further operations
        context.CurrentAlias = targetAlias;

        // Return separate columns instead of a map
        context.Return = $"{sourceAlias} AS source, {relationshipAlias} AS relationship, {targetAlias} AS target";

        // Mark as path result for proper result handling
        context.IsPathResult = true;
    }

    /// <summary>
    /// Builds the final Cypher query from the context
    /// </summary>
    private string BuildCypherQuery(CypherBuildContext context)
    {
        var query = new StringBuilder();

        // MATCH clause - only add if we have content
        if (context.Match.Length > 0)
        {
            query.Append("MATCH ").AppendLine(context.Match.ToString());
        }
        else
        {
            // If no explicit MATCH clause was built, create a basic one from the root type
            if (context.RootType != null)
            {
                // Generate different patterns based on query root type
                if (context.QueryRootType == GraphQueryContext.QueryRootType.Relationship)
                {
                    // For relationship queries, generate: (n1)-[r1:type]->(n2)
                    var labels = Neo4jTypeManager.GetLabelsForAssignableTypes(context.RootType).ToList();

                    if (labels.Count == 1)
                    {
                        // Single label case
                        query.Append("MATCH ").AppendLine($"(n1)-[r1:{labels.First()}]->(n2)");
                    }
                    else if (labels.Count > 1)
                    {
                        // Multiple labels case
                        query.Append("MATCH ").AppendLine("(n1)-[r1]->(n2)");
                        var labelConditions = labels.Select(l => $"r1:{l}");
                        query.Append("WHERE ").AppendLine($"({string.Join(" OR ", labelConditions)})");
                    }
                }
                else
                {
                    // Regular node queries
                    var labels = Neo4jTypeManager.GetLabelsForAssignableTypes(context.RootType).ToList();
                    if (labels.Count == 1)
                    {
                        query.Append("MATCH ").AppendLine($"(n:{labels.First()})");
                    }
                    else if (labels.Count > 1)
                    {
                        query.Append("MATCH ").AppendLine("(n)");
                    }
                }
            }
        }

        // WHERE clause
        var whereConditions = new List<string>();

        // Add inheritance label conditions for polymorphic queries
        if (context.InheritanceLabels != null && context.InheritanceLabels.Count > 1)
        {
            if (!context.IsScalarResult && !context.IsPathResult && context.QueryRootType != GraphQueryContext.QueryRootType.Relationship)
            {
                var labelConditions = context.InheritanceLabels.Select(l => $"{context.CurrentAlias}:{l}");
                whereConditions.Add($"({string.Join(" OR ", labelConditions)})");
            }
        }

        // Add existing WHERE conditions
        if (context.Where.Length > 0)
        {
            whereConditions.Add(context.Where.ToString());
        }

        // Combine all WHERE conditions
        if (whereConditions.Count > 0)
        {
            query.Append("WHERE ").AppendLine(string.Join(" AND ", whereConditions));
        }

        // WITH clause for grouping and aggregations
        if (!string.IsNullOrEmpty(context.With))
        {
            query.AppendLine(context.With);
        }

        // RETURN clause
        query.Append("RETURN ");
        if (context.IsDistinct)
        {
            query.Append("DISTINCT ");
        }
        query.AppendLine(context.Return ?? context.CurrentAlias);

        // ORDER BY clause
        if (context.OrderBy.Length > 0)
        {
            query.Append("ORDER BY ").AppendLine(context.OrderBy.ToString());
        }

        // SKIP clause
        if (context.Skip > 0)
        {
            query.AppendLine($"SKIP {context.Skip}");
        }

        // LIMIT clause
        if (context.Limit > 0)
        {
            query.AppendLine($"LIMIT {context.Limit}");
        }

        return query.ToString().Trim();
    }

    // Helper methods
    private static bool DetectLastOperation(Expression expression)
    {
        // Walk the expression tree to detect Last operations
        return expression.ToString().Contains(".Last");
    }

    private static Type? FindSourceEntityType(Expression expression)
    {
        // Walk the expression tree to find the source entity type
        // This is a simplified version - the full implementation would be more complex
        return null;
    }

    private static LambdaExpression? ExtractLambdaFromQuote(Expression expression)
    {
        if (expression is UnaryExpression { NodeType: ExpressionType.Quote } ue && ue.Operand is LambdaExpression lambda)
        {
            return lambda;
        }
        return expression as LambdaExpression;
    }

    private static Type GetSourceElementType(MethodCallExpression methodCall)
    {
        // Get the source type from the method call
        if (methodCall.Object != null)
        {
            var sourceType = methodCall.Object.Type;
            if (sourceType.IsGenericType)
            {
                return sourceType.GetGenericArguments()[0];
            }
        }
        else if (methodCall.Arguments.Count > 0)
        {
            var sourceType = methodCall.Arguments[0].Type;
            if (sourceType.IsGenericType)
            {
                return sourceType.GetGenericArguments()[0];
            }
        }

        return typeof(object);
    }

    private static bool IsSelectAfterGroupBy(MethodCallExpression methodCall)
    {
        // Check if this Select comes after a GroupBy
        if (methodCall.Object is MethodCallExpression objectCall)
        {
            return objectCall.Method.Name == "GroupBy";
        }
        if (methodCall.Arguments.Count > 0 && methodCall.Arguments[0] is MethodCallExpression argCall)
        {
            return argCall.Method.Name == "GroupBy";
        }
        return false;
    }

    private static bool IsGraphTraversalMethod(MethodCallExpression methodCall)
    {
        return methodCall.Method.DeclaringType?.IsGenericType == true &&
               methodCall.Method.DeclaringType.GetGenericTypeDefinition() == typeof(GraphTraversal<,>) &&
               methodCall.Method.Name is "TraversalToInternal" or "TraversalRelationshipsInternal" or "TraversalPathsInternal";
    }
}
