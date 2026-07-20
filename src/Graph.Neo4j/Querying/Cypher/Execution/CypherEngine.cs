// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Querying.Cypher.Execution;

using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Cvoya.Graph.Neo4j.Core;
using Cvoya.Graph.Neo4j.Entities;
using Cvoya.Graph.Neo4j.Querying.Cypher.Builders;
using Cvoya.Graph.Neo4j.Querying.Cypher.Visitors.Core;
using Cvoya.Graph.Cypher;
using Cvoya.Graph.Cypher.Ast;
using Cvoya.Graph.Cypher.Planning;
using Cvoya.Graph.Querying;
using Cvoya.Graph.Querying.Commands;
using Cvoya.Graph.Serialization;
using Cvoya.Graph.Serialization.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using global::Neo4j.Driver;


internal sealed class CypherEngine
{
    private enum ElementTerminal
    {
        None,
        First,
        FirstOrDefault,
        Last,
        Single,
        SingleOrDefault,
    }

    private static readonly Dictionary<MethodInfo, ElementTerminal> ElementTerminalMethods = CreateElementTerminalMethods();

    private static readonly HashSet<MethodInfo> NonEmptyAggregateTerminalMethods = CreateNonEmptyAggregateTerminalMethods();

    private readonly EntityFactory _entityFactory;
    private readonly ILogger<CypherEngine> _logger;
    private readonly CypherExecutor _executor;
    private readonly GraphResultMaterializer _materializer;
    private readonly Neo4jRecordAdapter _recordAdapter = new();
    private readonly ILoggerFactory? _loggerFactory;

    public CypherEngine(EntityFactory entityFactory, ILoggerFactory? loggerFactory)
    {
        _entityFactory = entityFactory ?? throw new ArgumentNullException(nameof(entityFactory));
        _logger = loggerFactory?.CreateLogger<CypherEngine>() ?? NullLogger<CypherEngine>.Instance;

        // Create our internal components
        _executor = new CypherExecutor(loggerFactory);
        _materializer = new GraphResultMaterializer(entityFactory, loggerFactory);
        _loggerFactory = loggerFactory;

    }

    internal static void ValidateMutation(GraphMutationModel mutation)
    {
        _ = new CypherQueryPlanner(Neo4jDialect.Instance).Plan(mutation.Selection);
        if (mutation.Kind == GraphMutationKind.Update ||
            mutation.Selection.ElementKind == GraphElementKind.Relationship)
        {
            _ = new CypherMutationPlanner(Neo4jDialect.Instance).Plan(mutation, []);
        }
    }

    internal async Task<IReadOnlyList<SelectedGraphElement>> SelectNativeAsync(
        GraphElementSelectionModel selection,
        GraphTransaction transaction,
        CancellationToken cancellationToken)
    {
        var statement = new CypherQueryPlanner(Neo4jDialect.Instance).Plan(selection);
        var records = await ExecuteStatementAsync(statement, transaction, cancellationToken).ConfigureAwait(false);
        return records.Select(record => new SelectedGraphElement(
            selection.ElementKind,
            record["__nativeId"].As<string>())).ToArray();
    }

    internal async Task<int> ApplyMutationAsync(
        GraphMutationModel mutation,
        GraphTransaction transaction,
        CancellationToken cancellationToken)
    {
        var selected = await SelectNativeAsync(mutation.Selection, transaction, cancellationToken)
            .ConfigureAwait(false);
        if (selected.Count == 0)
        {
            return 0;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var nativeIdentities = selected.Select(item => item.NativeIdentity).ToArray();
        if (mutation.Kind == GraphMutationKind.Delete &&
            mutation.Selection.ElementKind == GraphElementKind.Node)
        {
            await DeleteNodesAsync(
                nativeIdentities.Cast<string>().ToArray(),
                mutation.CascadeDelete,
                transaction,
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var statement = new CypherMutationPlanner(Neo4jDialect.Instance)
                .Plan(mutation, nativeIdentities);
            _ = await ExecuteStatementAsync(statement, transaction, cancellationToken).ConfigureAwait(false);
        }

        return selected.Count;
    }

    private async Task DeleteNodesAsync(
        IReadOnlyList<string> nativeIdentities,
        bool cascadeDelete,
        GraphTransaction transaction,
        CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["targetIds"] = nativeIdentities,
        };
        if (!cascadeDelete)
        {
            var preflight = $"""
                MATCH (target)
                WHERE elementId(target) IN $targetIds
                OPTIONAL MATCH (target)-[relationship]-()
                WHERE coalesce(relationship.{ComplexPropertyStorage.RelationshipMarkerProperty}, false) = false
                RETURN count(relationship) AS relationshipCount
                """;
            var records = await _executor.ExecuteAsync(
                preflight,
                parameters,
                transaction,
                cancellationToken).ConfigureAwait(false);
            var relationshipCount = records.Single()["relationshipCount"].As<long>();
            if (relationshipCount > 0)
            {
                throw new GraphException(
                    $"Cannot delete the selected nodes because the frozen target set has {relationshipCount} incident user relationship(s). " +
                    "Delete those relationships first or use cascade delete.");
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        var delete = $"""
            MATCH (target)
            WHERE elementId(target) IN $targetIds
            OPTIONAL MATCH propertyPath = (target)-[propertyRelationships*1..{GraphDataModel.DefaultDepthAllowed}]->(propertyNode)
            WHERE ALL(relationship IN propertyRelationships WHERE relationship.{ComplexPropertyStorage.RelationshipMarkerProperty} = true)
            WITH target, [node IN collect(DISTINCT propertyNode) WHERE node IS NOT NULL] AS propertyNodes
            FOREACH (propertyNode IN propertyNodes | DETACH DELETE propertyNode)
            DETACH DELETE target
            RETURN count(*) AS affectedCount
            """;
        _ = await _executor.ExecuteAsync(
            delete,
            parameters,
            transaction,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<List<global::Neo4j.Driver.IRecord>> ExecuteStatementAsync(
        CypherStatement statement,
        GraphTransaction transaction,
        CancellationToken cancellationToken)
    {
        var rendered = new CypherRenderer(Neo4jDialect.Instance).Render(statement);
        var parameters = CypherQueryVisitor.RewriteFullTextSearchParameters(statement, rendered.Parameters);
        var parameterBuilder = new CypherParameterBuilder(_entityFactory, _loggerFactory);
        var converted = parameters.ToDictionary(
            pair => pair.Key,
            pair => parameterBuilder.BuildParameterValue(pair.Value),
            StringComparer.Ordinal);
        return await _executor.ExecuteAsync(
            rendered.Text,
            converted,
            transaction,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<T?> ExecuteAsync<T>(
            Expression expression,
            GraphTransaction transaction,
            CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebugCypherEngine60(typeof(T).Name);

            // Build the Cypher query from the expression
            var cypherQuery = BuildCypherQuery(typeof(T), expression, _loggerFactory);

            LogCypherQuery(cypherQuery);

            // Execute the query
            var records = await _executor.ExecuteAsync(
                cypherQuery.Text,
                cypherQuery.Parameters,
                transaction,
                cancellationToken).ConfigureAwait(false);

            ValidateElementTerminalRecordCount(GetElementTerminal(expression), records.Count);
            ValidateNonEmptyAggregateValue<T>(expression, records);

            // Let the materializer handle everything - no need to duplicate logic here
            var result = await _materializer.MaterializeAsync<T>(
                _recordAdapter.Adapt(records),
                cypherQuery.GraphPathTypes,
                cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogErrorCypherEngine90(ex, typeof(T).Name);
            throw;
        }
    }

    public async IAsyncEnumerable<T> StreamAsync<T>(
        Expression expression,
        GraphTransaction transaction,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogDebugCypherEngine102(typeof(T).Name);

        var cypherQuery = BuildCypherQuery(typeof(T), expression, _loggerFactory);
        LogCypherQuery(cypherQuery);

        var records = _executor.StreamAsync(
            cypherQuery.Text,
            cypherQuery.Parameters,
            transaction,
            cancellationToken);

        if (cypherQuery.GraphPathTypes is { } graphPathTypes && typeof(T) == typeof(IGraphPath))
        {
            await foreach (var path in StreamGraphPathsAsync(records, graphPathTypes, cancellationToken)
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
            {
                yield return (T)path;
            }

            yield break;
        }

        await foreach (var record in records.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var item = await _materializer.MaterializeRecordAsync<T>(
                _recordAdapter.Adapt(record),
                cancellationToken).ConfigureAwait(false);

            if (item is not null)
            {
                yield return item;
            }
        }
    }

    private async IAsyncEnumerable<IGraphPath> StreamGraphPathsAsync(
        IAsyncEnumerable<global::Neo4j.Driver.IRecord> records,
        (Type Source, Type Relationship, Type Target) graphPathTypes,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        List<GraphResultProcessor.GraphPathHop>? currentHops = null;
        int? currentPathIndex = null;

        await foreach (var record in records.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();

            // TraversePaths orders rows by pathIndex, then hopIndex, so a path can be emitted once its index changes.
            var hop = _materializer.ProcessGraphPathHop(
                _recordAdapter.Adapt(record),
                graphPathTypes,
                cancellationToken);
            if (currentPathIndex is int pathIndex && hop.PathIndex != pathIndex)
            {
                yield return _materializer.MaterializeGraphPath(currentHops!, graphPathTypes);
                currentHops = [];
            }

            currentPathIndex = hop.PathIndex;
            currentHops ??= [];
            currentHops.Add(hop);
        }

        if (currentHops is { Count: > 0 })
        {
            yield return _materializer.MaterializeGraphPath(currentHops, graphPathTypes);
        }
    }

    private static Dictionary<MethodInfo, ElementTerminal> CreateElementTerminalMethods()
    {
        var methods = new Dictionary<MethodInfo, ElementTerminal>();

        AddAll(nameof(QueryTerminals.FirstAsyncMarker), ElementTerminal.First);
        AddAll(nameof(QueryTerminals.FirstOrDefaultAsyncMarker), ElementTerminal.FirstOrDefault);
        AddAll(nameof(QueryTerminals.LastAsyncMarker), ElementTerminal.Last);
        AddAll(nameof(QueryTerminals.SingleAsyncMarker), ElementTerminal.Single);
        AddAll(nameof(QueryTerminals.SingleOrDefaultAsyncMarker), ElementTerminal.SingleOrDefault);

        return methods;

        void AddAll(string name, ElementTerminal terminal)
        {
            foreach (var method in typeof(QueryTerminals).GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
                .Where(m => m.Name == name))
            {
                methods[method] = terminal;
            }
        }
    }

    private static HashSet<MethodInfo> CreateNonEmptyAggregateTerminalMethods()
    {
        // Min, Max, and non-nullable Average require a non-empty sequence: each returns a single
        // record whose aggregate value is null over zero rows, which must surface as an empty-
        // sequence error rather than default(T). Sum and Count are excluded — they return 0.
        return typeof(QueryTerminals)
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Where(m => m.Name is nameof(QueryTerminals.MinAsyncMarker) or nameof(QueryTerminals.MaxAsyncMarker)
                or nameof(QueryTerminals.AverageAsyncMarker))
            .ToHashSet();
    }

    private static ElementTerminal GetElementTerminal(Expression expression)
    {
        if (expression is not MethodCallExpression methodCall)
            return ElementTerminal.None;

        var key = methodCall.Method.IsGenericMethod
            ? methodCall.Method.GetGenericMethodDefinition()
            : methodCall.Method;

        return ElementTerminalMethods.TryGetValue(key, out var terminal)
            ? terminal
            : ElementTerminal.None;
    }

    private static void ValidateNonEmptyAggregateValue<T>(Expression expression, List<global::Neo4j.Driver.IRecord> records)
    {
        if (!typeof(T).IsValueType || Nullable.GetUnderlyingType(typeof(T)) is not null)
            return;

        if (!IsNonEmptyAggregateTerminal(expression))
            return;

        // min()/max()/avg() over zero rows yields a single record holding a null aggregate value,
        // not zero records, so the empty-sequence signal cannot come from the record count. A
        // nullable result type (handled above) legitimately materializes that null; a non-nullable
        // one must raise the standard empty-sequence error instead of returning default(T).
        if (records is [var record] && record.Values.Count == 1 && record.Values.Values.First() is null)
            throw new InvalidOperationException("Sequence contains no elements");
    }

    private static bool IsNonEmptyAggregateTerminal(Expression expression)
    {
        if (expression is not MethodCallExpression methodCall)
            return false;

        var key = methodCall.Method.IsGenericMethod
            ? methodCall.Method.GetGenericMethodDefinition()
            : methodCall.Method;

        return NonEmptyAggregateTerminalMethods.Contains(key);
    }

    private static void ValidateElementTerminalRecordCount(ElementTerminal terminal, int recordCount)
    {
        switch (terminal)
        {
            case ElementTerminal.First when recordCount == 0:
            case ElementTerminal.Last when recordCount == 0:
            case ElementTerminal.Single when recordCount == 0:
                throw new InvalidOperationException("Sequence contains no elements");

            case ElementTerminal.Single when recordCount > 1:
            case ElementTerminal.SingleOrDefault when recordCount > 1:
                throw new InvalidOperationException("Sequence contains more than one element");

            case ElementTerminal.None:
            case ElementTerminal.First:
            case ElementTerminal.FirstOrDefault:
            case ElementTerminal.Last:
            case ElementTerminal.Single:
            case ElementTerminal.SingleOrDefault:
                return;
        }
    }

    private void LogCypherQuery(CypherQuery cypherQuery)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            var parameterNames = cypherQuery.Parameters.Keys.ToArray();
            _logger.LogDebugCypherEngine272(cypherQuery.Text);
            _logger.LogDebugCypherEngine273(parameterNames, parameterNames.Length);
        }

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTraceCypherEngine281(cypherQuery.Parameters);
        }
    }

    private CypherQuery BuildCypherQuery(Type type, Expression expression, ILoggerFactory? loggerFactory = null)
    {
        // Extract the element type from the expression if the type is a collection
        var elementType = ExtractElementType(type, expression);

        var visitor = new CypherQueryVisitor(elementType, loggerFactory);
        visitor.Visit(expression);

        var query = visitor.Query;

        var paramBuilder = new CypherParameterBuilder(_entityFactory);
        var convertedParams = new Dictionary<string, object?>();

        foreach (var (key, value) in query.Parameters)
        {
            convertedParams[key] = paramBuilder.BuildParameterValue(value);
        }

        return new CypherQuery(query.Text, convertedParams, query.GraphPathTypes);
    }

    private static Type ExtractElementType(Type resultType, Expression expression)
    {
        // If the result type is a collection (List<T>, IEnumerable<T>, etc.), 
        // extract the element type
        if (resultType.IsGenericType)
        {
            var genericArgs = resultType.GetGenericArguments();
            if (genericArgs.Length == 1)
            {
                return genericArgs[0];
            }
        }

        // Try to extract from the expression tree by finding the first queryable constant
        return ExtractElementTypeFromExpression(expression) ?? resultType;
    }

    private static Type? ExtractElementTypeFromExpression(Expression expression)
    {
        if (expression is ConstantExpression constant &&
            constant.Value is IQueryable queryable)
        {
            return queryable.ElementType;
        }

        if (expression is MethodCallExpression methodCall)
        {
            // Check the object of the method call
            if (methodCall.Object != null)
            {
                var objType = ExtractElementTypeFromExpression(methodCall.Object);
                if (objType != null) return objType;
            }

            // Check the arguments
            foreach (var arg in methodCall.Arguments)
            {
                var argType = ExtractElementTypeFromExpression(arg);
                if (argType != null) return argType;
            }
        }

        if (expression is UnaryExpression unary)
        {
            return ExtractElementTypeFromExpression(unary.Operand);
        }

        return null;
    }
}
