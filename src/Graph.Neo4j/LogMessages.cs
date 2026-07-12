// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

global using Cvoya.Graph.Logging;

using Microsoft.Extensions.Logging;

namespace Cvoya.Graph.Logging;

internal static partial class LogMessages
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Closing session timed out. The session may not have been closed properly.")]
    internal static partial void LogWarningGraphTransaction111(this ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "An error occurred while closing the session: {Message}")]
    internal static partial void LogErrorGraphTransaction115(this ILogger logger, global::System.String message);

    [LoggerMessage(EventId = 3, Level = LogLevel.Debug, Message = "Beginning new transaction")]
    internal static partial void LogDebugGraphTransaction123(this ILogger logger);

    [LoggerMessage(EventId = 4, Level = LogLevel.Debug, Message = "Successfully began transaction")]
    internal static partial void LogDebugGraphTransaction125(this ILogger logger);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information, Message = "Graph initialized for database '{DatabaseName}'")]
    internal static partial void LogInformationNeo4jGraph51(this ILogger logger, global::System.String databaseName);

    [LoggerMessage(EventId = 6, Level = LogLevel.Debug, Message = "Beginning new transaction")]
    internal static partial void LogDebugNeo4jGraph63(this ILogger logger);

    [LoggerMessage(EventId = 7, Level = LogLevel.Debug, Message = "Successfully began transaction")]
    internal static partial void LogDebugNeo4jGraph68(this ILogger logger);

    [LoggerMessage(EventId = 8, Level = LogLevel.Error, Message = "Failed to begin transaction")]
    internal static partial void LogErrorNeo4jGraph82(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 9, Level = LogLevel.Debug, Message = "Building nodes queryable for type {NodeType}")]
    internal static partial void LogDebugNeo4jGraph91(this ILogger logger, global::System.String nodeType);

    [LoggerMessage(EventId = 10, Level = LogLevel.Debug, Message = "Building relationships queryable for type {RelationshipType}")]
    internal static partial void LogDebugNeo4jGraph106(this ILogger logger, global::System.String relationshipType);

    [LoggerMessage(EventId = 11, Level = LogLevel.Debug, Message = "Creating node of type {NodeType}")]
    internal static partial void LogDebugNeo4jGraph162(this ILogger logger, global::System.String nodeType);

    [LoggerMessage(EventId = 12, Level = LogLevel.Debug, Message = "Successfully created node {NodeId}")]
    internal static partial void LogDebugNeo4jGraph174(this ILogger logger, global::System.String nodeId);

    [LoggerMessage(EventId = 13, Level = LogLevel.Error, Message = "Failed to create node of type {NodeType}")]
    internal static partial void LogErrorNeo4jGraph183(this ILogger logger, Exception exception, global::System.String nodeType);

    [LoggerMessage(EventId = 14, Level = LogLevel.Debug, Message = "Creating relationship of type {RelationshipType}")]
    internal static partial void LogDebugNeo4jGraph209(this ILogger logger, global::System.String relationshipType);

    [LoggerMessage(EventId = 15, Level = LogLevel.Debug, Message = "Successfully created relationship {RelationshipId}")]
    internal static partial void LogDebugNeo4jGraph225(this ILogger logger, global::System.String relationshipId);

    [LoggerMessage(EventId = 16, Level = LogLevel.Error, Message = "Failed to create relationship of type {RelationshipType}")]
    internal static partial void LogErrorNeo4jGraph234(this ILogger logger, Exception exception, global::System.String relationshipType);

    [LoggerMessage(EventId = 17, Level = LogLevel.Debug, Message = "Updating node {NodeId} of type {NodeType}")]
    internal static partial void LogDebugNeo4jGraph262(this ILogger logger, global::System.String nodeId, global::System.String nodeType);

    [LoggerMessage(EventId = 18, Level = LogLevel.Debug, Message = "Successfully updated node {NodeId}")]
    internal static partial void LogDebugNeo4jGraph280(this ILogger logger, global::System.String nodeId);

    [LoggerMessage(EventId = 19, Level = LogLevel.Error, Message = "Failed to update node {NodeId} of type {NodeType}")]
    internal static partial void LogErrorNeo4jGraph289(this ILogger logger, Exception exception, global::System.String nodeId, global::System.String nodeType);

    [LoggerMessage(EventId = 20, Level = LogLevel.Debug, Message = "Updating relationship {RelationshipId} of type {RelationshipType}")]
    internal static partial void LogDebugNeo4jGraph317(this ILogger logger, global::System.String relationshipId, global::System.String relationshipType);

    [LoggerMessage(EventId = 21, Level = LogLevel.Debug, Message = "Successfully updated relationship {RelationshipId}")]
    internal static partial void LogDebugNeo4jGraph335(this ILogger logger, global::System.String relationshipId);

    [LoggerMessage(EventId = 22, Level = LogLevel.Error, Message = "Failed to update relationship {RelationshipId} of type {RelationshipType}")]
    internal static partial void LogErrorNeo4jGraph344(this ILogger logger, Exception exception, global::System.String relationshipId, global::System.String relationshipType);

    [LoggerMessage(EventId = 23, Level = LogLevel.Debug, Message = "Deleting node {NodeId}")]
    internal static partial void LogDebugNeo4jGraph366(this ILogger logger, global::System.String nodeId);

    [LoggerMessage(EventId = 24, Level = LogLevel.Debug, Message = "Successfully deleted node {NodeId}")]
    internal static partial void LogDebugNeo4jGraph384(this ILogger logger, global::System.String nodeId);

    [LoggerMessage(EventId = 25, Level = LogLevel.Error, Message = "Failed to delete node {NodeId}")]
    internal static partial void LogErrorNeo4jGraph394(this ILogger logger, Exception exception, global::System.String nodeId);

    [LoggerMessage(EventId = 26, Level = LogLevel.Debug, Message = "Deleting relationship {RelationshipId}")]
    internal static partial void LogDebugNeo4jGraph415(this ILogger logger, global::System.String relationshipId);

    [LoggerMessage(EventId = 27, Level = LogLevel.Debug, Message = "Successfully deleted relationship {RelationshipId}")]
    internal static partial void LogDebugNeo4jGraph429(this ILogger logger, global::System.String relationshipId);

    [LoggerMessage(EventId = 28, Level = LogLevel.Error, Message = "Failed to delete relationship {RelationshipId}")]
    internal static partial void LogErrorNeo4jGraph438(this ILogger logger, Exception exception, global::System.String relationshipId);

    [LoggerMessage(EventId = 29, Level = LogLevel.Debug, Message = "Building dynamic nodes queryable")]
    internal static partial void LogDebugNeo4jGraph454(this ILogger logger);

    [LoggerMessage(EventId = 30, Level = LogLevel.Debug, Message = "Building dynamic relationships queryable")]
    internal static partial void LogDebugNeo4jGraph464(this ILogger logger);

    [LoggerMessage(EventId = 31, Level = LogLevel.Debug, Message = "Building full text search queryable; query length: {QueryLength}")]
    internal static partial void LogDebugNeo4jGraph496(this ILogger logger, global::System.Int32 queryLength);

    [LoggerMessage(EventId = 32, Level = LogLevel.Debug, Message = "Building full text search queryable for nodes; query length: {QueryLength}")]
    internal static partial void LogDebugNeo4jGraph508(this ILogger logger, global::System.Int32 queryLength);

    [LoggerMessage(EventId = 33, Level = LogLevel.Debug, Message = "Building full text search queryable for relationships; query length: {QueryLength}")]
    internal static partial void LogDebugNeo4jGraph520(this ILogger logger, global::System.Int32 queryLength);

    [LoggerMessage(EventId = 34, Level = LogLevel.Debug, Message = "Building full text search queryable for nodes of type {NodeType}; query length: {QueryLength}")]
    internal static partial void LogDebugNeo4jGraph532(this ILogger logger, global::System.String nodeType, global::System.Int32 queryLength);

    [LoggerMessage(EventId = 35, Level = LogLevel.Debug, Message = "Building full text search queryable for relationships of type {RelationshipType}; query length: {QueryLength}")]
    internal static partial void LogDebugNeo4jGraph547(this ILogger logger, global::System.String relationshipType, global::System.Int32 queryLength);

    [LoggerMessage(EventId = 36, Level = LogLevel.Information, Message = "Recreating indexes for Neo4j graph")]
    internal static partial void LogInformationNeo4jGraph562(this ILogger logger);

    [LoggerMessage(EventId = 37, Level = LogLevel.Information, Message = "Index recreation completed successfully")]
    internal static partial void LogInformationNeo4jGraph564(this ILogger logger);

    [LoggerMessage(EventId = 38, Level = LogLevel.Error, Message = "Failed to recreate indexes")]
    internal static partial void LogErrorNeo4jGraph572(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 39, Level = LogLevel.Warning, Message = "Failed to roll back cancelled transaction")]
    internal static partial void LogWarningTransactionHelpers53(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 40, Level = LogLevel.Warning, Message = "Failed to roll back cancelled transaction")]
    internal static partial void LogWarningTransactionHelpers57(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 41, Level = LogLevel.Warning, Message = "Failed to roll back cancelled transaction")]
    internal static partial void LogWarningTransactionHelpers61(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 42, Level = LogLevel.Error, Message = "{ErrorMessage}")]
    internal static partial void LogErrorTransactionHelpers69(this ILogger logger, Exception exception, global::System.String errorMessage);

    [LoggerMessage(EventId = 43, Level = LogLevel.Debug, Message = "Skipping null complex property {PropertyName}")]
    internal static partial void LogDebugComplexPropertyManager88(this ILogger logger, global::System.String propertyName);

    [LoggerMessage(EventId = 44, Level = LogLevel.Warning, Message = "Unsupported complex property type: {PropertyType} for property {PropertyName}")]
    internal static partial void LogWarningComplexPropertyManager92(this ILogger logger, global::System.String propertyType, global::System.String propertyName);

    [LoggerMessage(EventId = 45, Level = LogLevel.Debug, Message = "Created {Count} complex property node(s) of type {RelationshipType} to {Label}")]
    internal static partial void LogDebugComplexPropertyManager182(this ILogger logger, global::System.Int32 count, global::System.String relationshipType, global::System.String label);

    [LoggerMessage(EventId = 46, Level = LogLevel.Debug, Message = "Deleted {DeletedCount} complex property relationships for parent {ParentId}")]
    internal static partial void LogDebugComplexPropertyManager221(this ILogger logger, global::System.Int32 deletedCount, global::System.String parentId);

    [LoggerMessage(EventId = 47, Level = LogLevel.Debug, Message = "Creating node of type {NodeType} with ID {NodeId}")]
    internal static partial void LogDebugNeo4jNodeManager54(this ILogger logger, global::System.String nodeType, global::System.String nodeId);

    [LoggerMessage(EventId = 48, Level = LogLevel.Information, Message = "Created node of type {NodeType} with ID {NodeId}")]
    internal static partial void LogInformationNeo4jNodeManager75(this ILogger logger, global::System.String nodeType, global::System.String nodeId);

    [LoggerMessage(EventId = 49, Level = LogLevel.Error, Message = "Error creating node of type {NodeType}")]
    internal static partial void LogErrorNeo4jNodeManager85(this ILogger logger, Exception exception, global::System.String nodeType);

    [LoggerMessage(EventId = 50, Level = LogLevel.Debug, Message = "Updating node of type {NodeType} with ID {NodeId}")]
    internal static partial void LogDebugNeo4jNodeManager98(this ILogger logger, global::System.String nodeType, global::System.String nodeId);

    [LoggerMessage(EventId = 51, Level = LogLevel.Warning, Message = "Node with ID {NodeId} not found for update")]
    internal static partial void LogWarningNeo4jNodeManager118(this ILogger logger, global::System.String nodeId);

    [LoggerMessage(EventId = 52, Level = LogLevel.Information, Message = "Updated node of type {NodeType} with ID {NodeId}")]
    internal static partial void LogInformationNeo4jNodeManager126(this ILogger logger, global::System.String nodeType, global::System.String nodeId);

    [LoggerMessage(EventId = 53, Level = LogLevel.Error, Message = "Error updating node {NodeId} of type {NodeType}")]
    internal static partial void LogErrorNeo4jNodeManager131(this ILogger logger, Exception exception, global::System.String nodeId, global::System.String nodeType);

    [LoggerMessage(EventId = 54, Level = LogLevel.Debug, Message = "Deleting node with ID: {NodeId}, cascade: {CascadeDelete}")]
    internal static partial void LogDebugNeo4jNodeManager144(this ILogger logger, global::System.String nodeId, global::System.Boolean cascadeDelete);

    [LoggerMessage(EventId = 55, Level = LogLevel.Warning, Message = "Node with ID {NodeId} not found for deletion")]
    internal static partial void LogWarningNeo4jNodeManager156(this ILogger logger, global::System.String nodeId);

    [LoggerMessage(EventId = 56, Level = LogLevel.Warning, Message = "Node with ID {NodeId} not found for deletion")]
    internal static partial void LogWarningNeo4jNodeManager216(this ILogger logger, global::System.String nodeId);

    [LoggerMessage(EventId = 57, Level = LogLevel.Information, Message = "Deleted node with ID {NodeId}")]
    internal static partial void LogInformationNeo4jNodeManager220(this ILogger logger, global::System.String nodeId);

    [LoggerMessage(EventId = 58, Level = LogLevel.Error, Message = "Error deleting node with ID: {NodeId}")]
    internal static partial void LogErrorNeo4jNodeManager225(this ILogger logger, Exception exception, global::System.String nodeId);

    [LoggerMessage(EventId = 59, Level = LogLevel.Debug, Message = "Creating relationship of type {RelationshipType} from {StartNodeId} to {EndNodeId}")]
    internal static partial void LogDebugNeo4jRelationshipManager39(this ILogger logger, global::System.String relationshipType, global::System.String startNodeId, global::System.String endNodeId);

    [LoggerMessage(EventId = 60, Level = LogLevel.Information, Message = "Created relationship of type {RelationshipType} with ID {RelationshipId}")]
    internal static partial void LogInformationNeo4jRelationshipManager77(this ILogger logger, global::System.String relationshipType, global::System.String relationshipId);

    [LoggerMessage(EventId = 61, Level = LogLevel.Error, Message = "Error creating relationship of type {RelationshipType}")]
    internal static partial void LogErrorNeo4jRelationshipManager84(this ILogger logger, Exception exception, global::System.String relationshipType);

    [LoggerMessage(EventId = 62, Level = LogLevel.Debug, Message = "Updating relationship of type {RelationshipType} with ID {RelationshipId}")]
    internal static partial void LogDebugNeo4jRelationshipManager97(this ILogger logger, global::System.String relationshipType, global::System.String relationshipId);

    [LoggerMessage(EventId = 63, Level = LogLevel.Warning, Message = "Relationship with ID {RelationshipId} not found for update")]
    internal static partial void LogWarningNeo4jRelationshipManager126(this ILogger logger, global::System.String relationshipId);

    [LoggerMessage(EventId = 64, Level = LogLevel.Information, Message = "Updated relationship of type {RelationshipType} with ID {RelationshipId}")]
    internal static partial void LogInformationNeo4jRelationshipManager141(this ILogger logger, global::System.String relationshipType, global::System.String relationshipId);

    [LoggerMessage(EventId = 65, Level = LogLevel.Error, Message = "Error updating relationship {RelationshipId} of type {RelationshipType}")]
    internal static partial void LogErrorNeo4jRelationshipManager148(this ILogger logger, Exception exception, global::System.String relationshipId, global::System.String relationshipType);

    [LoggerMessage(EventId = 66, Level = LogLevel.Debug, Message = "Deleting relationship with ID {RelationshipId}")]
    internal static partial void LogDebugNeo4jRelationshipManager161(this ILogger logger, global::System.String relationshipId);

    [LoggerMessage(EventId = 67, Level = LogLevel.Warning, Message = "Relationship with ID {RelationshipId} not found for deletion")]
    internal static partial void LogWarningNeo4jRelationshipManager176(this ILogger logger, global::System.String relationshipId);

    [LoggerMessage(EventId = 68, Level = LogLevel.Information, Message = "Deleted relationship with ID {RelationshipId}")]
    internal static partial void LogInformationNeo4jRelationshipManager180(this ILogger logger, global::System.String relationshipId);

    [LoggerMessage(EventId = 69, Level = LogLevel.Error, Message = "Error deleting relationship with ID {RelationshipId}")]
    internal static partial void LogErrorNeo4jRelationshipManager185(this ILogger logger, Exception exception, global::System.String relationshipId);

    [LoggerMessage(EventId = 70, Level = LogLevel.Debug, Message = "Executing query for type {Type}")]
    internal static partial void LogDebugCypherEngine60(this ILogger logger, global::System.String type);

    [LoggerMessage(EventId = 71, Level = LogLevel.Error, Message = "Failed to execute query for type {Type}")]
    internal static partial void LogErrorCypherEngine90(this ILogger logger, Exception exception, global::System.String type);

    [LoggerMessage(EventId = 72, Level = LogLevel.Debug, Message = "Streaming query for type {Type}")]
    internal static partial void LogDebugCypherEngine102(this ILogger logger, global::System.String type);

    [LoggerMessage(EventId = 73, Level = LogLevel.Debug, Message = "Generated Cypher: {Cypher}")]
    internal static partial void LogDebugCypherEngine272(this ILogger logger, global::System.String cypher);

    [LoggerMessage(EventId = 74, Level = LogLevel.Debug, Message = "Generated Cypher parameter names: {ParameterNames}; count: {ParameterCount}")]
    internal static partial void LogDebugCypherEngine273(this ILogger logger, global::System.String[] parameterNames, global::System.Int32 parameterCount);

    [LoggerMessage(EventId = 75, Level = LogLevel.Trace, Message = "Generated Cypher parameters: {Parameters}")]
    internal static partial void LogTraceCypherEngine281(this ILogger logger, global::System.Collections.Generic.IReadOnlyDictionary<global::System.String, global::System.Object?> parameters);

    [LoggerMessage(EventId = 76, Level = LogLevel.Debug, Message = "Executing Cypher query: {Query}")]
    internal static partial void LogDebugCypherExecutor31(this ILogger logger, global::System.String query);

    [LoggerMessage(EventId = 77, Level = LogLevel.Debug, Message = "Query returned {Count} records")]
    internal static partial void LogDebugCypherExecutor36(this ILogger logger, global::System.Int32 count);

    [LoggerMessage(EventId = 78, Level = LogLevel.Debug, Message = "Streaming Cypher query: {Query}")]
    internal static partial void LogDebugCypherExecutor49(this ILogger logger, global::System.String query);

    [LoggerMessage(EventId = 79, Level = LogLevel.Debug, Message = "Query streamed {Count} records")]
    internal static partial void LogDebugCypherExecutor61(this ILogger logger, global::System.Int32 count);

    [LoggerMessage(EventId = 80, Level = LogLevel.Debug, Message = "Planning graph query rooted at {RootType}")]
    internal static partial void LogDebugCypherQueryVisitor36(this ILogger logger, global::System.Type rootType);

    [LoggerMessage(EventId = 81, Level = LogLevel.Debug, Message = "Added Cypher parameter names: {ParameterNames}; count: {ParameterCount}")]
    internal static partial void LogDebugCypherQueryVisitor53(this ILogger logger, global::System.String[] parameterNames, global::System.Int32 parameterCount);

    [LoggerMessage(EventId = 82, Level = LogLevel.Debug, Message = "Streaming async query for result type: {ResultType}")]
    internal static partial void LogDebugGraphQueryProvider65(this ILogger logger, global::System.String resultType);

    [LoggerMessage(EventId = 83, Level = LogLevel.Debug, Message = "Executing async query for result type: {ResultType}")]
    internal static partial void LogDebugGraphQueryProvider69(this ILogger logger, global::System.String resultType);

    [LoggerMessage(EventId = 84, Level = LogLevel.Debug, Message = "Expression type: {ExpressionType}")]
    internal static partial void LogDebugGraphQueryProvider71(this ILogger logger, global::System.String expressionType);

    [LoggerMessage(EventId = 85, Level = LogLevel.Error, Message = "Error executing query")]
    internal static partial void LogErrorGraphQueryProvider80(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 86, Level = LogLevel.Warning, Message = "Failed to roll back abandoned streaming query transaction")]
    internal static partial void LogWarningGraphQueryProvider83(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 87, Level = LogLevel.Debug, Message = "{Indent}Method: {Method} from {DeclaringType}")]
    internal static partial void LogDebugGraphQueryProvider91(this ILogger logger, global::System.String indent, global::System.String method, global::System.String? declaringType);

    [LoggerMessage(EventId = 88, Level = LogLevel.Debug, Message = "{Indent}Constant: {Type}")]
    internal static partial void LogDebugGraphQueryProvider104(this ILogger logger, global::System.String indent, global::System.String type);

    [LoggerMessage(EventId = 89, Level = LogLevel.Debug, Message = "Schema already initialized, skipping initialization")]
    internal static partial void LogDebugNeo4jSchemaManager45(this ILogger logger);

    [LoggerMessage(EventId = 90, Level = LogLevel.Debug, Message = "Schema already initialized, skipping initialization")]
    internal static partial void LogDebugNeo4jSchemaManager56(this ILogger logger);

    [LoggerMessage(EventId = 91, Level = LogLevel.Information, Message = "Initializing Neo4j schema...")]
    internal static partial void LogInformationNeo4jSchemaManager60(this ILogger logger);

    [LoggerMessage(EventId = 92, Level = LogLevel.Debug, Message = "Schema registry initialized with {NodeCount} node types and {RelationshipCount} relationship types")]
    internal static partial void LogDebugNeo4jSchemaManager68(this ILogger logger, global::System.Int32 nodeCount, global::System.Int32 relationshipCount);

    [LoggerMessage(EventId = 93, Level = LogLevel.Information, Message = "Neo4j schema initialization completed successfully")]
    internal static partial void LogInformationNeo4jSchemaManager90(this ILogger logger);

    [LoggerMessage(EventId = 94, Level = LogLevel.Error, Message = "Failed to initialize Neo4j schema")]
    internal static partial void LogErrorNeo4jSchemaManager98(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 95, Level = LogLevel.Information, Message = "Recreating Neo4j indexes...")]
    internal static partial void LogInformationNeo4jSchemaManager116(this ILogger logger);

    [LoggerMessage(EventId = 96, Level = LogLevel.Information, Message = "Neo4j indexes recreated successfully")]
    internal static partial void LogInformationNeo4jSchemaManager140(this ILogger logger);

    [LoggerMessage(EventId = 97, Level = LogLevel.Error, Message = "Failed to recreate Neo4j indexes")]
    internal static partial void LogErrorNeo4jSchemaManager148(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 98, Level = LogLevel.Debug, Message = "Node schema already processed for label: {Label}")]
    internal static partial void LogDebugNeo4jSchemaManager160(this ILogger logger, global::System.String label);

    [LoggerMessage(EventId = 99, Level = LogLevel.Warning, Message = "No schema found for node label: {Label}")]
    internal static partial void LogWarningNeo4jSchemaManager168(this ILogger logger, global::System.String label);

    [LoggerMessage(EventId = 100, Level = LogLevel.Debug, Message = "Successfully processed node schema for label: {Label}")]
    internal static partial void LogDebugNeo4jSchemaManager183(this ILogger logger, global::System.String label);

    [LoggerMessage(EventId = 101, Level = LogLevel.Debug, Message = "Relationship schema already processed for type: {Type}")]
    internal static partial void LogDebugNeo4jSchemaManager193(this ILogger logger, global::System.String type);

    [LoggerMessage(EventId = 102, Level = LogLevel.Warning, Message = "No schema found for relationship type: {Type}")]
    internal static partial void LogWarningNeo4jSchemaManager201(this ILogger logger, global::System.String type);

    [LoggerMessage(EventId = 103, Level = LogLevel.Debug, Message = "Successfully processed relationship schema for type: {Type}")]
    internal static partial void LogDebugNeo4jSchemaManager216(this ILogger logger, global::System.String type);

    [LoggerMessage(EventId = 104, Level = LogLevel.Debug, Message = "Created unique Id constraint for label {Label}")]
    internal static partial void LogDebugNeo4jSchemaManager239(this ILogger logger, global::System.String label);

    [LoggerMessage(EventId = 105, Level = LogLevel.Debug, Message = "Created composite key constraint for properties {Properties} on label {Label}")]
    internal static partial void LogDebugNeo4jSchemaManager261(this ILogger logger, global::System.String properties, global::System.String label);

    [LoggerMessage(EventId = 106, Level = LogLevel.Debug, Message = "Created unique constraint for property {Property} on label {Label}")]
    internal static partial void LogDebugNeo4jSchemaManager283(this ILogger logger, global::System.String property, global::System.String label);

    [LoggerMessage(EventId = 107, Level = LogLevel.Debug, Message = "Skipping NOT NULL constraint for complex property {Property} on label {Label} - complex properties are modeled as separate nodes")]
    internal static partial void LogDebugNeo4jSchemaManager293(this ILogger logger, global::System.String property, global::System.String label);

    [LoggerMessage(EventId = 108, Level = LogLevel.Debug, Message = "Skipping NOT NULL constraint for property {Property} on label {Label} because Neo4j Community does not support property existence constraints")]
    internal static partial void LogDebugNeo4jSchemaManager299(this ILogger logger, global::System.String property, global::System.String label);

    [LoggerMessage(EventId = 109, Level = LogLevel.Debug, Message = "Created not null constraint for property {Property} on label {Label}")]
    internal static partial void LogDebugNeo4jSchemaManager313(this ILogger logger, global::System.String property, global::System.String label);

    [LoggerMessage(EventId = 110, Level = LogLevel.Error, Message = "Failed to create constraints for node label: {Label}")]
    internal static partial void LogErrorNeo4jSchemaManager323(this ILogger logger, Exception exception, global::System.String label);

    [LoggerMessage(EventId = 111, Level = LogLevel.Debug, Message = "Created unique Id constraint for relationship type {Type}")]
    internal static partial void LogDebugNeo4jSchemaManager348(this ILogger logger, global::System.String type);

    [LoggerMessage(EventId = 112, Level = LogLevel.Debug, Message = "Created composite key constraint for properties {Properties} on relationship type {Type}")]
    internal static partial void LogDebugNeo4jSchemaManager370(this ILogger logger, global::System.String properties, global::System.String type);

    [LoggerMessage(EventId = 113, Level = LogLevel.Debug, Message = "Created unique constraint for property {Property} on relationship type {Type}")]
    internal static partial void LogDebugNeo4jSchemaManager392(this ILogger logger, global::System.String property, global::System.String type);

    [LoggerMessage(EventId = 114, Level = LogLevel.Debug, Message = "Skipping NOT NULL constraint for property {Property} on relationship type {Type} because Neo4j Community does not support property existence constraints")]
    internal static partial void LogDebugNeo4jSchemaManager400(this ILogger logger, global::System.String property, global::System.String type);

    [LoggerMessage(EventId = 115, Level = LogLevel.Debug, Message = "Created not null constraint for property {Property} on relationship type {Type}")]
    internal static partial void LogDebugNeo4jSchemaManager414(this ILogger logger, global::System.String property, global::System.String type);

    [LoggerMessage(EventId = 116, Level = LogLevel.Error, Message = "Failed to create constraints for relationship type: {Type}")]
    internal static partial void LogErrorNeo4jSchemaManager424(this ILogger logger, Exception exception, global::System.String type);

    [LoggerMessage(EventId = 117, Level = LogLevel.Information, Message = "Neo4j Community detected; required-property constraints will be skipped because they require Enterprise Edition")]
    internal static partial void LogInformationNeo4jSchemaManager447(this ILogger logger);

    [LoggerMessage(EventId = 118, Level = LogLevel.Warning, Message = "Failed to detect Neo4j edition; assuming property existence constraints are supported")]
    internal static partial void LogWarningNeo4jSchemaManager454(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 119, Level = LogLevel.Warning, Message = "Failed to detect Neo4j edition; assuming property existence constraints are supported")]
    internal static partial void LogWarningNeo4jSchemaManager459(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 120, Level = LogLevel.Warning, Message = "Failed to retrieve existing constraints for {LabelOrType}, proceeding with creation attempts")]
    internal static partial void LogWarningNeo4jSchemaManager491(this ILogger logger, Exception exception, global::System.String labelOrType);

    [LoggerMessage(EventId = 121, Level = LogLevel.Debug, Message = "Created index {Index} for property {Property} on label {Label}")]
    internal static partial void LogDebugNeo4jSchemaManager521(this ILogger logger, global::System.String index, global::System.String property, global::System.String label);

    [LoggerMessage(EventId = 122, Level = LogLevel.Debug, Message = "Created index {Index} for property {Property} on relationship type {Type}")]
    internal static partial void LogDebugNeo4jSchemaManager557(this ILogger logger, global::System.String index, global::System.String property, global::System.String type);

    [LoggerMessage(EventId = 123, Level = LogLevel.Debug, Message = "Creating global full text indexes")]
    internal static partial void LogDebugNeo4jSchemaManager572(this ILogger logger);

    [LoggerMessage(EventId = 124, Level = LogLevel.Debug, Message = "Created global node full-text index with {LabelCount} labels and {PropertyCount} properties")]
    internal static partial void LogDebugNeo4jSchemaManager609(this ILogger logger, global::System.Int32 labelCount, global::System.Int32 propertyCount);

    [LoggerMessage(EventId = 125, Level = LogLevel.Debug, Message = "Skipped node full-text index creation - no labels or string properties found")]
    internal static partial void LogDebugNeo4jSchemaManager613(this ILogger logger);

    [LoggerMessage(EventId = 126, Level = LogLevel.Debug, Message = "Created global relationship full-text index with {TypeCount} types and {PropertyCount} properties")]
    internal static partial void LogDebugNeo4jSchemaManager644(this ILogger logger, global::System.Int32 typeCount, global::System.Int32 propertyCount);

    [LoggerMessage(EventId = 127, Level = LogLevel.Debug, Message = "Skipped relationship full-text index creation - no types or string properties found")]
    internal static partial void LogDebugNeo4jSchemaManager648(this ILogger logger);

    [LoggerMessage(EventId = 128, Level = LogLevel.Debug, Message = "Global full-text index creation completed")]
    internal static partial void LogDebugNeo4jSchemaManager652(this ILogger logger);

    [LoggerMessage(EventId = 129, Level = LogLevel.Error, Message = "Failed to create global full-text indexes")]
    internal static partial void LogErrorNeo4jSchemaManager657(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 130, Level = LogLevel.Debug, Message = "Dropped index: {IndexName}")]
    internal static partial void LogDebugNeo4jSchemaManager690(this ILogger logger, global::System.String indexName);

    [LoggerMessage(EventId = 131, Level = LogLevel.Information, Message = "Dropped {Count} indexes")]
    internal static partial void LogInformationNeo4jSchemaManager695(this ILogger logger, global::System.Int32 count);

    [LoggerMessage(EventId = 132, Level = LogLevel.Debug, Message = "Cleared schema cache and reset initialization state")]
    internal static partial void LogDebugNeo4jSchemaManager722(this ILogger logger);

}
