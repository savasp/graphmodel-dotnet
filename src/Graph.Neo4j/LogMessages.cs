// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

global using Cvoya.Graph.Logging;

using Microsoft.Extensions.Logging;

namespace Cvoya.Graph.Logging;

internal static partial class LogMessages
{
    // LoggerExtensions emitted numeric event ID 0; preserve it during this mechanical migration.
    [LoggerMessage(EventId = 0, Level = LogLevel.Warning, Message = "Closing session timed out. The session may not have been closed properly.")]
    internal static partial void LogWarningGraphTransaction111(this ILogger logger);

    [LoggerMessage(EventId = 0, Level = LogLevel.Warning, Message = "Failed to roll back an uncommitted Neo4j transaction during disposal")]
    internal static partial void LogWarningGraphTransactionRollbackFailure(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 0, Level = LogLevel.Warning, Message = "Failed to dispose the underlying Neo4j driver transaction")]
    internal static partial void LogWarningGraphTransactionDriverDisposalFailure(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 0, Level = LogLevel.Error, Message = "Failed to close the Neo4j session")]
    internal static partial void LogErrorGraphTransactionSessionCloseFailure(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 0, Level = LogLevel.Warning, Message = "Failed to clean up the Neo4j session after transaction begin failed")]
    internal static partial void LogWarningGraphTransactionBeginCleanupFailure(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Beginning new transaction")]
    internal static partial void LogDebugGraphTransaction123(this ILogger logger);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Successfully began transaction")]
    internal static partial void LogDebugGraphTransaction125(this ILogger logger);

    [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Graph initialized for database '{DatabaseName}'")]
    internal static partial void LogInformationNeo4jGraph51(this ILogger logger, global::System.String databaseName);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Beginning new transaction")]
    internal static partial void LogDebugNeo4jGraph63(this ILogger logger);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Successfully began transaction")]
    internal static partial void LogDebugNeo4jGraph68(this ILogger logger);

    [LoggerMessage(EventId = 0, Level = LogLevel.Error, Message = "Failed to begin transaction")]
    internal static partial void LogErrorNeo4jGraph82(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Building nodes queryable for type {NodeType}")]
    internal static partial void LogDebugNeo4jGraph91(this ILogger logger, global::System.String nodeType);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Building relationships queryable for type {RelationshipType}")]
    internal static partial void LogDebugNeo4jGraph106(this ILogger logger, global::System.String relationshipType);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Creating node of type {NodeType}")]
    internal static partial void LogDebugNeo4jGraph162(this ILogger logger, global::System.String nodeType);

    [LoggerMessage(EventId = 0, Level = LogLevel.Error, Message = "Failed to create node of type {NodeType}")]
    internal static partial void LogErrorNeo4jGraph183(this ILogger logger, Exception exception, global::System.String nodeType);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Building dynamic nodes queryable")]
    internal static partial void LogDebugNeo4jGraph454(this ILogger logger);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Building dynamic relationships queryable")]
    internal static partial void LogDebugNeo4jGraph464(this ILogger logger);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Building full text search queryable; query length: {QueryLength}")]
    internal static partial void LogDebugNeo4jGraph496(this ILogger logger, global::System.Int32 queryLength);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Building full text search queryable for nodes; query length: {QueryLength}")]
    internal static partial void LogDebugNeo4jGraph508(this ILogger logger, global::System.Int32 queryLength);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Building full text search queryable for relationships; query length: {QueryLength}")]
    internal static partial void LogDebugNeo4jGraph520(this ILogger logger, global::System.Int32 queryLength);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Building full text search queryable for nodes of type {NodeType}; query length: {QueryLength}")]
    internal static partial void LogDebugNeo4jGraph532(this ILogger logger, global::System.String nodeType, global::System.Int32 queryLength);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Building full text search queryable for relationships of type {RelationshipType}; query length: {QueryLength}")]
    internal static partial void LogDebugNeo4jGraph547(this ILogger logger, global::System.String relationshipType, global::System.Int32 queryLength);

    [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Recreating managed indexes for Neo4j graph")]
    internal static partial void LogInformationNeo4jGraph562(this ILogger logger);

    [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Managed index recreation completed successfully")]
    internal static partial void LogInformationNeo4jGraph564(this ILogger logger);

    [LoggerMessage(EventId = 0, Level = LogLevel.Error, Message = "Failed to recreate managed indexes")]
    internal static partial void LogErrorNeo4jGraph572(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 0, Level = LogLevel.Error, Message = "{ErrorMessage}")]
    internal static partial void LogErrorTransactionHelpers69(this ILogger logger, Exception exception, global::System.String errorMessage);

    [LoggerMessage(EventId = 0, Level = LogLevel.Warning, Message = "Failed to roll back cancelled transaction")]
    internal static partial void LogWarningTransactionHelpersCancelledRollbackFailure(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 0, Level = LogLevel.Warning, Message = "Failed to roll back failed transaction")]
    internal static partial void LogWarningTransactionHelpersFailedRollbackFailure(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 0, Level = LogLevel.Warning, Message = "Failed to dispose transaction after a failed operation")]
    internal static partial void LogWarningTransactionHelpersDisposalFailure(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Skipping null complex property {PropertyName}")]
    internal static partial void LogDebugComplexPropertyManager88(this ILogger logger, global::System.String propertyName);

    [LoggerMessage(EventId = 0, Level = LogLevel.Warning, Message = "Unsupported complex property type: {PropertyType} for property {PropertyName}")]
    internal static partial void LogWarningComplexPropertyManager92(this ILogger logger, global::System.String propertyType, global::System.String propertyName);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Created {Count} complex property node(s) of type {RelationshipType} to {Label}")]
    internal static partial void LogDebugComplexPropertyManager182(this ILogger logger, global::System.Int32 count, global::System.String relationshipType, global::System.String label);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Deleted {DeletedCount} complex property relationships for parent {ParentId}")]
    internal static partial void LogDebugComplexPropertyManager221(this ILogger logger, global::System.Int32 deletedCount, global::System.String parentId);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Creating subgraph {SourceType}-[{RelationshipType}]->{TargetType} in a single statement")]
    internal static partial void LogDebugNeo4jSubgraphManager44(this ILogger logger, global::System.String sourceType, global::System.String relationshipType, global::System.String targetType);

    [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Created subgraph for relationship {RelationshipId}")]
    internal static partial void LogInformationNeo4jSubgraphManager54(this ILogger logger, global::System.String relationshipId);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Creating node of type {NodeType}")]
    internal static partial void LogDebugNeo4jNodeManager54(this ILogger logger, global::System.String nodeType);

    [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Created node of type {NodeType}")]
    internal static partial void LogInformationNeo4jNodeManager75(this ILogger logger, global::System.String nodeType);

    [LoggerMessage(EventId = 0, Level = LogLevel.Error, Message = "Error creating node of type {NodeType}")]
    internal static partial void LogErrorNeo4jNodeManager85(this ILogger logger, Exception exception, global::System.String nodeType);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Executing query for type {Type}")]
    internal static partial void LogDebugCypherEngine60(this ILogger logger, global::System.String type);

    [LoggerMessage(EventId = 0, Level = LogLevel.Error, Message = "Failed to execute query for type {Type}")]
    internal static partial void LogErrorCypherEngine90(this ILogger logger, Exception exception, global::System.String type);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Streaming query for type {Type}")]
    internal static partial void LogDebugCypherEngine102(this ILogger logger, global::System.String type);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Generated Cypher: {Cypher}")]
    internal static partial void LogDebugCypherEngine272(this ILogger logger, global::System.String cypher);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Generated Cypher parameter names: {ParameterNames}; count: {ParameterCount}")]
    internal static partial void LogDebugCypherEngine273(this ILogger logger, global::System.String[] parameterNames, global::System.Int32 parameterCount);

    [LoggerMessage(EventId = 0, Level = LogLevel.Trace, Message = "Generated Cypher parameters: {Parameters}")]
    internal static partial void LogTraceCypherEngine281(this ILogger logger, global::System.Collections.Generic.IReadOnlyDictionary<global::System.String, global::System.Object?> parameters);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Executing Cypher query: {Query}")]
    internal static partial void LogDebugCypherExecutor31(this ILogger logger, global::System.String query);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Query returned {Count} records")]
    internal static partial void LogDebugCypherExecutor36(this ILogger logger, global::System.Int32 count);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Streaming Cypher query: {Query}")]
    internal static partial void LogDebugCypherExecutor49(this ILogger logger, global::System.String query);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Query streamed {Count} records")]
    internal static partial void LogDebugCypherExecutor61(this ILogger logger, global::System.Int32 count);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Planning graph query rooted at {RootType}")]
    internal static partial void LogDebugCypherQueryVisitor36(this ILogger logger, global::System.Type rootType);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Added Cypher parameter names: {ParameterNames}; count: {ParameterCount}")]
    internal static partial void LogDebugCypherQueryVisitor53(this ILogger logger, global::System.String[] parameterNames, global::System.Int32 parameterCount);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Streaming async query for result type: {ResultType}")]
    internal static partial void LogDebugGraphQueryProvider65(this ILogger logger, global::System.String resultType);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Executing async query for result type: {ResultType}")]
    internal static partial void LogDebugGraphQueryProvider69(this ILogger logger, global::System.String resultType);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Expression type: {ExpressionType}")]
    internal static partial void LogDebugGraphQueryProvider71(this ILogger logger, global::System.String expressionType);

    [LoggerMessage(EventId = 0, Level = LogLevel.Error, Message = "Error executing query")]
    internal static partial void LogErrorGraphQueryProvider80(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 0, Level = LogLevel.Warning, Message = "Failed to roll back streaming query transaction during cleanup")]
    internal static partial void LogWarningGraphQueryProvider83(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 0, Level = LogLevel.Warning, Message = "Failed to dispose streaming query enumerator during cleanup")]
    internal static partial void LogWarningGraphQueryProviderEnumeratorDisposalFailure(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 0, Level = LogLevel.Warning, Message = "Failed to dispose streaming query transaction during cleanup")]
    internal static partial void LogWarningGraphQueryProviderTransactionDisposalFailure(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "{Indent}Method: {Method} from {DeclaringType}")]
    internal static partial void LogDebugGraphQueryProvider91(this ILogger logger, global::System.String indent, global::System.String method, global::System.String? declaringType);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "{Indent}Constant: {Type}")]
    internal static partial void LogDebugGraphQueryProvider104(this ILogger logger, global::System.String indent, global::System.String type);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Schema already initialized, skipping initialization")]
    internal static partial void LogDebugNeo4jSchemaManager45(this ILogger logger);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Schema already initialized, skipping initialization")]
    internal static partial void LogDebugNeo4jSchemaManager56(this ILogger logger);

    [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Initializing Neo4j schema...")]
    internal static partial void LogInformationNeo4jSchemaManager60(this ILogger logger);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Schema registry initialized with {NodeCount} node types and {RelationshipCount} relationship types")]
    internal static partial void LogDebugNeo4jSchemaManager68(this ILogger logger, global::System.Int32 nodeCount, global::System.Int32 relationshipCount);

    [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Neo4j schema initialization completed successfully")]
    internal static partial void LogInformationNeo4jSchemaManager90(this ILogger logger);

    [LoggerMessage(EventId = 0, Level = LogLevel.Error, Message = "Failed to initialize Neo4j schema")]
    internal static partial void LogErrorNeo4jSchemaManager98(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Recreating managed Neo4j indexes...")]
    internal static partial void LogInformationNeo4jSchemaManager116(this ILogger logger);

    [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Managed Neo4j indexes recreated successfully")]
    internal static partial void LogInformationNeo4jSchemaManager140(this ILogger logger);

    [LoggerMessage(EventId = 0, Level = LogLevel.Error, Message = "Failed to recreate managed Neo4j indexes")]
    internal static partial void LogErrorNeo4jSchemaManager148(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Node schema already processed for label: {Label}")]
    internal static partial void LogDebugNeo4jSchemaManager160(this ILogger logger, global::System.String label);

    [LoggerMessage(EventId = 0, Level = LogLevel.Warning, Message = "No schema found for node label: {Label}")]
    internal static partial void LogWarningNeo4jSchemaManager168(this ILogger logger, global::System.String label);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Successfully processed node schema for label: {Label}")]
    internal static partial void LogDebugNeo4jSchemaManager183(this ILogger logger, global::System.String label);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Relationship schema already processed for type: {Type}")]
    internal static partial void LogDebugNeo4jSchemaManager193(this ILogger logger, global::System.String type);

    [LoggerMessage(EventId = 0, Level = LogLevel.Warning, Message = "No schema found for relationship type: {Type}")]
    internal static partial void LogWarningNeo4jSchemaManager201(this ILogger logger, global::System.String type);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Successfully processed relationship schema for type: {Type}")]
    internal static partial void LogDebugNeo4jSchemaManager216(this ILogger logger, global::System.String type);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Created key tuple constraint for properties {Properties} on label {Label}")]
    internal static partial void LogDebugNeo4jSchemaManager261(this ILogger logger, global::System.String properties, global::System.String label);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Created unique constraint for property {Property} on label {Label}")]
    internal static partial void LogDebugNeo4jSchemaManager283(this ILogger logger, global::System.String property, global::System.String label);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Skipping NOT NULL constraint for complex property {Property} on label {Label} - complex properties are modeled as separate nodes")]
    internal static partial void LogDebugNeo4jSchemaManager293(this ILogger logger, global::System.String property, global::System.String label);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Skipping NOT NULL constraint for property {Property} on label {Label} because Neo4j Community does not support property existence constraints")]
    internal static partial void LogDebugNeo4jSchemaManager299(this ILogger logger, global::System.String property, global::System.String label);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Created not null constraint for property {Property} on label {Label}")]
    internal static partial void LogDebugNeo4jSchemaManager313(this ILogger logger, global::System.String property, global::System.String label);

    [LoggerMessage(EventId = 0, Level = LogLevel.Error, Message = "Failed to create constraints for node label: {Label}")]
    internal static partial void LogErrorNeo4jSchemaManager323(this ILogger logger, Exception exception, global::System.String label);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Created key tuple constraint for properties {Properties} on relationship type {Type}")]
    internal static partial void LogDebugNeo4jSchemaManager370(this ILogger logger, global::System.String properties, global::System.String type);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Created unique constraint for property {Property} on relationship type {Type}")]
    internal static partial void LogDebugNeo4jSchemaManager392(this ILogger logger, global::System.String property, global::System.String type);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Skipping NOT NULL constraint for property {Property} on relationship type {Type} because Neo4j Community does not support property existence constraints")]
    internal static partial void LogDebugNeo4jSchemaManager400(this ILogger logger, global::System.String property, global::System.String type);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Created not null constraint for property {Property} on relationship type {Type}")]
    internal static partial void LogDebugNeo4jSchemaManager414(this ILogger logger, global::System.String property, global::System.String type);

    [LoggerMessage(EventId = 0, Level = LogLevel.Error, Message = "Failed to create constraints for relationship type: {Type}")]
    internal static partial void LogErrorNeo4jSchemaManager424(this ILogger logger, Exception exception, global::System.String type);

    [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Neo4j Community detected; required-property constraints will be skipped because they require Enterprise Edition")]
    internal static partial void LogInformationNeo4jSchemaManager447(this ILogger logger);

    [LoggerMessage(EventId = 0, Level = LogLevel.Warning, Message = "Failed to detect Neo4j edition; assuming property existence constraints are supported")]
    internal static partial void LogWarningNeo4jSchemaManager454(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 0, Level = LogLevel.Warning, Message = "Failed to detect Neo4j edition; assuming property existence constraints are supported")]
    internal static partial void LogWarningNeo4jSchemaManager459(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 0, Level = LogLevel.Warning, Message = "Failed to read the existing schema snapshot; proceeding with creation attempts")]
    internal static partial void LogWarningNeo4jSchemaManager466(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Schema object {Name} already exists with an equivalent definition; skipping creation")]
    internal static partial void LogDebugNeo4jSchemaManager478(this ILogger logger, global::System.String name);

    [LoggerMessage(EventId = 0, Level = LogLevel.Warning, Message = "Failed to retrieve existing constraints for {LabelOrType}, proceeding with creation attempts")]
    internal static partial void LogWarningNeo4jSchemaManager491(this ILogger logger, Exception exception, global::System.String labelOrType);

    [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Recreating full-text index {Name} because its installed definition does not match the current model")]
    internal static partial void LogInformationNeo4jSchemaManager502(this ILogger logger, global::System.String name);

    [LoggerMessage(EventId = 0, Level = LogLevel.Warning, Message = "Failed to recreate full-text index {Name}; preserving the original schema conflict")]
    internal static partial void LogWarningNeo4jSchemaManager512(this ILogger logger, Exception exception, global::System.String name);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Created index {Index} for property {Property} on label {Label}")]
    internal static partial void LogDebugNeo4jSchemaManager521(this ILogger logger, global::System.String index, global::System.String property, global::System.String label);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Created index {Index} for property {Property} on relationship type {Type}")]
    internal static partial void LogDebugNeo4jSchemaManager557(this ILogger logger, global::System.String index, global::System.String property, global::System.String type);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Creating global full text indexes")]
    internal static partial void LogDebugNeo4jSchemaManager572(this ILogger logger);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Created global node full-text index with {LabelCount} labels and {PropertyCount} properties")]
    internal static partial void LogDebugNeo4jSchemaManager609(this ILogger logger, global::System.Int32 labelCount, global::System.Int32 propertyCount);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Skipped node full-text index creation - no labels or string properties found")]
    internal static partial void LogDebugNeo4jSchemaManager613(this ILogger logger);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Created global relationship full-text index with {TypeCount} types and {PropertyCount} properties")]
    internal static partial void LogDebugNeo4jSchemaManager644(this ILogger logger, global::System.Int32 typeCount, global::System.Int32 propertyCount);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Skipped relationship full-text index creation - no types or string properties found")]
    internal static partial void LogDebugNeo4jSchemaManager648(this ILogger logger);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Global full-text index creation completed")]
    internal static partial void LogDebugNeo4jSchemaManager652(this ILogger logger);

    [LoggerMessage(EventId = 0, Level = LogLevel.Error, Message = "Failed to create global full-text indexes")]
    internal static partial void LogErrorNeo4jSchemaManager657(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Dropped index: {IndexName}")]
    internal static partial void LogDebugNeo4jSchemaManager690(this ILogger logger, global::System.String indexName);

    [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Dropped {Count} positively owned indexes")]
    internal static partial void LogInformationNeo4jSchemaManager695(this ILogger logger, global::System.Int32 count);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Cleared schema cache and reset initialization state")]
    internal static partial void LogDebugNeo4jSchemaManager722(this ILogger logger);

}
