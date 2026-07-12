// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

global using Cvoya.Graph.Logging;

using Microsoft.Extensions.Logging;

namespace Cvoya.Graph.Logging;

internal static partial class LogMessages
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Graph initialized for AGE graph '{GraphName}'")]
    internal static partial void LogInformationAgeGraph49(this ILogger logger, global::System.String graphName);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug, Message = "Beginning new transaction")]
    internal static partial void LogDebugAgeGraph61(this ILogger logger);

    [LoggerMessage(EventId = 3, Level = LogLevel.Debug, Message = "Successfully began transaction")]
    internal static partial void LogDebugAgeGraph66(this ILogger logger);

    [LoggerMessage(EventId = 4, Level = LogLevel.Error, Message = "Failed to begin transaction")]
    internal static partial void LogErrorAgeGraph80(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 5, Level = LogLevel.Debug, Message = "Building nodes queryable for type {NodeType}")]
    internal static partial void LogDebugAgeGraph89(this ILogger logger, global::System.String nodeType);

    [LoggerMessage(EventId = 6, Level = LogLevel.Debug, Message = "Building relationships queryable for type {RelationshipType}")]
    internal static partial void LogDebugAgeGraph104(this ILogger logger, global::System.String relationshipType);

    [LoggerMessage(EventId = 7, Level = LogLevel.Debug, Message = "Creating node of type {NodeType}")]
    internal static partial void LogDebugAgeGraph160(this ILogger logger, global::System.String nodeType);

    [LoggerMessage(EventId = 8, Level = LogLevel.Debug, Message = "Successfully created node {NodeId}")]
    internal static partial void LogDebugAgeGraph173(this ILogger logger, global::System.String nodeId);

    [LoggerMessage(EventId = 9, Level = LogLevel.Error, Message = "Failed to create node of type {NodeType}")]
    internal static partial void LogErrorAgeGraph182(this ILogger logger, Exception exception, global::System.String nodeType);

    [LoggerMessage(EventId = 10, Level = LogLevel.Debug, Message = "Creating relationship of type {RelationshipType}")]
    internal static partial void LogDebugAgeGraph208(this ILogger logger, global::System.String relationshipType);

    [LoggerMessage(EventId = 11, Level = LogLevel.Debug, Message = "Successfully created relationship {RelationshipId}")]
    internal static partial void LogDebugAgeGraph225(this ILogger logger, global::System.String relationshipId);

    [LoggerMessage(EventId = 12, Level = LogLevel.Error, Message = "Failed to create relationship of type {RelationshipType}")]
    internal static partial void LogErrorAgeGraph234(this ILogger logger, Exception exception, global::System.String relationshipType);

    [LoggerMessage(EventId = 13, Level = LogLevel.Debug, Message = "Updating node {NodeId} of type {NodeType}")]
    internal static partial void LogDebugAgeGraph262(this ILogger logger, global::System.String nodeId, global::System.String nodeType);

    [LoggerMessage(EventId = 14, Level = LogLevel.Debug, Message = "Successfully updated node {NodeId}")]
    internal static partial void LogDebugAgeGraph281(this ILogger logger, global::System.String nodeId);

    [LoggerMessage(EventId = 15, Level = LogLevel.Error, Message = "Failed to update node {NodeId} of type {NodeType}")]
    internal static partial void LogErrorAgeGraph290(this ILogger logger, Exception exception, global::System.String nodeId, global::System.String nodeType);

    [LoggerMessage(EventId = 16, Level = LogLevel.Debug, Message = "Updating relationship {RelationshipId} of type {RelationshipType}")]
    internal static partial void LogDebugAgeGraph318(this ILogger logger, global::System.String relationshipId, global::System.String relationshipType);

    [LoggerMessage(EventId = 17, Level = LogLevel.Debug, Message = "Successfully updated relationship {RelationshipId}")]
    internal static partial void LogDebugAgeGraph337(this ILogger logger, global::System.String relationshipId);

    [LoggerMessage(EventId = 18, Level = LogLevel.Error, Message = "Failed to update relationship {RelationshipId} of type {RelationshipType}")]
    internal static partial void LogErrorAgeGraph346(this ILogger logger, Exception exception, global::System.String relationshipId, global::System.String relationshipType);

    [LoggerMessage(EventId = 19, Level = LogLevel.Debug, Message = "Deleting node {NodeId}")]
    internal static partial void LogDebugAgeGraph368(this ILogger logger, global::System.String nodeId);

    [LoggerMessage(EventId = 20, Level = LogLevel.Debug, Message = "Successfully deleted node {NodeId}")]
    internal static partial void LogDebugAgeGraph387(this ILogger logger, global::System.String nodeId);

    [LoggerMessage(EventId = 21, Level = LogLevel.Error, Message = "Failed to delete node {NodeId}")]
    internal static partial void LogErrorAgeGraph397(this ILogger logger, Exception exception, global::System.String nodeId);

    [LoggerMessage(EventId = 22, Level = LogLevel.Debug, Message = "Deleting relationship {RelationshipId}")]
    internal static partial void LogDebugAgeGraph418(this ILogger logger, global::System.String relationshipId);

    [LoggerMessage(EventId = 23, Level = LogLevel.Debug, Message = "Successfully deleted relationship {RelationshipId}")]
    internal static partial void LogDebugAgeGraph432(this ILogger logger, global::System.String relationshipId);

    [LoggerMessage(EventId = 24, Level = LogLevel.Error, Message = "Failed to delete relationship {RelationshipId}")]
    internal static partial void LogErrorAgeGraph441(this ILogger logger, Exception exception, global::System.String relationshipId);

    [LoggerMessage(EventId = 25, Level = LogLevel.Debug, Message = "Building dynamic nodes queryable")]
    internal static partial void LogDebugAgeGraph457(this ILogger logger);

    [LoggerMessage(EventId = 26, Level = LogLevel.Debug, Message = "Building dynamic relationships queryable")]
    internal static partial void LogDebugAgeGraph467(this ILogger logger);

    [LoggerMessage(EventId = 27, Level = LogLevel.Information, Message = "Recreating indexes for Age graph")]
    internal static partial void LogInformationAgeGraph539(this ILogger logger);

    [LoggerMessage(EventId = 28, Level = LogLevel.Information, Message = "Index recreation completed successfully")]
    internal static partial void LogInformationAgeGraph541(this ILogger logger);

    [LoggerMessage(EventId = 29, Level = LogLevel.Error, Message = "Failed to recreate indexes")]
    internal static partial void LogErrorAgeGraph551(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 30, Level = LogLevel.Warning, Message = "Failed to roll back an uncommitted AGE transaction during disposal")]
    internal static partial void LogWarningAgeGraphTransaction70(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 31, Level = LogLevel.Warning, Message = "Failed to dispose an AGE transaction after a failed begin")]
    internal static partial void LogWarningAgeGraphTransaction127(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 32, Level = LogLevel.Warning, Message = "Failed to roll back cancelled transaction")]
    internal static partial void LogWarningTransactionHelpers54(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 33, Level = LogLevel.Warning, Message = "Failed to roll back cancelled transaction")]
    internal static partial void LogWarningTransactionHelpers58(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 34, Level = LogLevel.Warning, Message = "Failed to roll back cancelled transaction")]
    internal static partial void LogWarningTransactionHelpers62(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 35, Level = LogLevel.Error, Message = "{ErrorMessage}")]
    internal static partial void LogErrorTransactionHelpers70(this ILogger logger, Exception exception, global::System.String errorMessage);

    [LoggerMessage(EventId = 36, Level = LogLevel.Warning, Message = "Failed to roll back failed transaction")]
    internal static partial void LogWarningTransactionHelpers83(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 37, Level = LogLevel.Warning, Message = "Failed to roll back failed transaction")]
    internal static partial void LogWarningTransactionHelpers87(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 38, Level = LogLevel.Warning, Message = "Failed to roll back failed transaction")]
    internal static partial void LogWarningTransactionHelpers91(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 39, Level = LogLevel.Warning, Message = "Failed to dispose transaction after a failed operation")]
    internal static partial void LogWarningTransactionHelpers107(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 40, Level = LogLevel.Debug, Message = "Creating node of type {NodeType} with ID {NodeId}")]
    internal static partial void LogDebugAgeNodeManager44(this ILogger logger, global::System.String nodeType, global::System.String nodeId);

    [LoggerMessage(EventId = 41, Level = LogLevel.Information, Message = "Created node of type {NodeType} with ID {NodeId}")]
    internal static partial void LogInformationAgeNodeManager73(this ILogger logger, global::System.String nodeType, global::System.String nodeId);

    [LoggerMessage(EventId = 42, Level = LogLevel.Error, Message = "Error creating node of type {NodeType}")]
    internal static partial void LogErrorAgeNodeManager79(this ILogger logger, Exception exception, global::System.String nodeType);

    [LoggerMessage(EventId = 43, Level = LogLevel.Debug, Message = "Updating node of type {NodeType} with ID {NodeId}")]
    internal static partial void LogDebugAgeNodeManager92(this ILogger logger, global::System.String nodeType, global::System.String nodeId);

    [LoggerMessage(EventId = 44, Level = LogLevel.Warning, Message = "Node with ID {NodeId} not found for update")]
    internal static partial void LogWarningAgeNodeManager115(this ILogger logger, global::System.String nodeId);

    [LoggerMessage(EventId = 45, Level = LogLevel.Information, Message = "Updated node of type {NodeType} with ID {NodeId}")]
    internal static partial void LogInformationAgeNodeManager123(this ILogger logger, global::System.String nodeType, global::System.String nodeId);

    [LoggerMessage(EventId = 46, Level = LogLevel.Error, Message = "Error updating node {NodeId} of type {NodeType}")]
    internal static partial void LogErrorAgeNodeManager128(this ILogger logger, Exception exception, global::System.String nodeId, global::System.String nodeType);

    [LoggerMessage(EventId = 47, Level = LogLevel.Debug, Message = "Deleting node with ID: {NodeId}, cascade: {CascadeDelete}")]
    internal static partial void LogDebugAgeNodeManager141(this ILogger logger, global::System.String nodeId, global::System.Boolean cascadeDelete);

    [LoggerMessage(EventId = 48, Level = LogLevel.Warning, Message = "Node with ID {NodeId} not found for deletion")]
    internal static partial void LogWarningAgeNodeManager151(this ILogger logger, global::System.String nodeId);

    [LoggerMessage(EventId = 49, Level = LogLevel.Warning, Message = "Node with ID {NodeId} not found for deletion")]
    internal static partial void LogWarningAgeNodeManager224(this ILogger logger, global::System.String nodeId);

    [LoggerMessage(EventId = 50, Level = LogLevel.Information, Message = "Deleted node with ID {NodeId}")]
    internal static partial void LogInformationAgeNodeManager228(this ILogger logger, global::System.String nodeId);

    [LoggerMessage(EventId = 51, Level = LogLevel.Error, Message = "Error deleting node with ID: {NodeId}")]
    internal static partial void LogErrorAgeNodeManager233(this ILogger logger, Exception exception, global::System.String nodeId);

    [LoggerMessage(EventId = 52, Level = LogLevel.Debug, Message = "Creating relationship of type {RelationshipType} from {StartNodeId} to {EndNodeId}")]
    internal static partial void LogDebugAgeRelationshipManager40(this ILogger logger, global::System.String relationshipType, global::System.String startNodeId, global::System.String endNodeId);

    [LoggerMessage(EventId = 53, Level = LogLevel.Information, Message = "Created relationship of type {RelationshipType} with ID {RelationshipId}")]
    internal static partial void LogInformationAgeRelationshipManager86(this ILogger logger, global::System.String relationshipType, global::System.String relationshipId);

    [LoggerMessage(EventId = 54, Level = LogLevel.Error, Message = "Error creating relationship of type {RelationshipType}")]
    internal static partial void LogErrorAgeRelationshipManager93(this ILogger logger, Exception exception, global::System.String relationshipType);

    [LoggerMessage(EventId = 55, Level = LogLevel.Debug, Message = "Updating relationship of type {RelationshipType} with ID {RelationshipId}")]
    internal static partial void LogDebugAgeRelationshipManager106(this ILogger logger, global::System.String relationshipType, global::System.String relationshipId);

    [LoggerMessage(EventId = 56, Level = LogLevel.Warning, Message = "Relationship with ID {RelationshipId} not found for update")]
    internal static partial void LogWarningAgeRelationshipManager138(this ILogger logger, global::System.String relationshipId);

    [LoggerMessage(EventId = 57, Level = LogLevel.Information, Message = "Updated relationship of type {RelationshipType} with ID {RelationshipId}")]
    internal static partial void LogInformationAgeRelationshipManager153(this ILogger logger, global::System.String relationshipType, global::System.String relationshipId);

    [LoggerMessage(EventId = 58, Level = LogLevel.Error, Message = "Error updating relationship {RelationshipId} of type {RelationshipType}")]
    internal static partial void LogErrorAgeRelationshipManager160(this ILogger logger, Exception exception, global::System.String relationshipId, global::System.String relationshipType);

    [LoggerMessage(EventId = 59, Level = LogLevel.Debug, Message = "Deleting relationship with ID {RelationshipId}")]
    internal static partial void LogDebugAgeRelationshipManager173(this ILogger logger, global::System.String relationshipId);

    [LoggerMessage(EventId = 60, Level = LogLevel.Warning, Message = "Relationship with ID {RelationshipId} not found for deletion")]
    internal static partial void LogWarningAgeRelationshipManager188(this ILogger logger, global::System.String relationshipId);

    [LoggerMessage(EventId = 61, Level = LogLevel.Information, Message = "Deleted relationship with ID {RelationshipId}")]
    internal static partial void LogInformationAgeRelationshipManager192(this ILogger logger, global::System.String relationshipId);

    [LoggerMessage(EventId = 62, Level = LogLevel.Error, Message = "Error deleting relationship with ID {RelationshipId}")]
    internal static partial void LogErrorAgeRelationshipManager197(this ILogger logger, Exception exception, global::System.String relationshipId);

    [LoggerMessage(EventId = 63, Level = LogLevel.Debug, Message = "Skipping null complex property {PropertyName}")]
    internal static partial void LogDebugComplexPropertyManager88(this ILogger logger, global::System.String propertyName);

    [LoggerMessage(EventId = 64, Level = LogLevel.Warning, Message = "Unsupported complex property type: {PropertyType} for property {PropertyName}")]
    internal static partial void LogWarningComplexPropertyManager92(this ILogger logger, global::System.String propertyType, global::System.String propertyName);

    [LoggerMessage(EventId = 65, Level = LogLevel.Debug, Message = "Created {Count} complex property node(s) across {RelationshipTypeCount} semantic relationship type(s)")]
    internal static partial void LogDebugComplexPropertyManager202(this ILogger logger, global::System.Int32 count, global::System.Int32 relationshipTypeCount);

    [LoggerMessage(EventId = 66, Level = LogLevel.Debug, Message = "Deleted {DeletedCount} complex property relationships for parent {ParentId}")]
    internal static partial void LogDebugComplexPropertyManager270(this ILogger logger, global::System.Int32 deletedCount, global::System.String parentId);

    [LoggerMessage(EventId = 67, Level = LogLevel.Debug, Message = "AGE query returned {RecordCount} records")]
    internal static partial void LogDebugAgeQueryRunner87(this ILogger logger, global::System.Int32 recordCount);

    [LoggerMessage(EventId = 68, Level = LogLevel.Debug, Message = "Streaming AGE Cypher query with {ParameterCount} parameters and {ColumnCount} projected columns: {Query}")]
    internal static partial void LogDebugAgeQueryRunner159(this ILogger logger, global::System.Int32 parameterCount, global::System.Int32 columnCount, global::System.String query);

    [LoggerMessage(EventId = 69, Level = LogLevel.Debug, Message = "Executing AGE Cypher query with {ParameterCount} parameters and {ColumnCount} projected columns: {Query}")]
    internal static partial void LogDebugAgeQueryRunner167(this ILogger logger, global::System.Int32 parameterCount, global::System.Int32 columnCount, global::System.String query);

    [LoggerMessage(EventId = 70, Level = LogLevel.Error, Message = "AGE query execution failed; correlation ID {CorrelationId}")]
    internal static partial void LogErrorAgeQueryRunner192(this ILogger logger, Exception exception, global::System.String correlationId);

    [LoggerMessage(EventId = 71, Level = LogLevel.Debug, Message = "Executing query for type {Type}")]
    internal static partial void LogDebugCypherEngine60(this ILogger logger, global::System.String type);

    [LoggerMessage(EventId = 72, Level = LogLevel.Error, Message = "Failed to execute query for type {Type}")]
    internal static partial void LogErrorCypherEngine91(this ILogger logger, Exception exception, global::System.String type);

    [LoggerMessage(EventId = 73, Level = LogLevel.Debug, Message = "Streaming query for type {Type}")]
    internal static partial void LogDebugCypherEngine103(this ILogger logger, global::System.String type);

    [LoggerMessage(EventId = 74, Level = LogLevel.Debug, Message = "Generated Cypher: {Cypher}")]
    internal static partial void LogDebugCypherEngine274(this ILogger logger, global::System.String cypher);

    [LoggerMessage(EventId = 75, Level = LogLevel.Debug, Message = "Generated Cypher parameter names: {ParameterNames}; count: {ParameterCount}")]
    internal static partial void LogDebugCypherEngine275(this ILogger logger, global::System.String[] parameterNames, global::System.Int32 parameterCount);

    [LoggerMessage(EventId = 76, Level = LogLevel.Trace, Message = "Generated Cypher parameters: {Parameters}")]
    internal static partial void LogTraceCypherEngine283(this ILogger logger, global::System.Collections.Generic.IReadOnlyDictionary<global::System.String, global::System.Object?> parameters);

    [LoggerMessage(EventId = 77, Level = LogLevel.Debug, Message = "AGE query returned {Count} records")]
    internal static partial void LogDebugCypherExecutor35(this ILogger logger, global::System.Int32 count);

    [LoggerMessage(EventId = 78, Level = LogLevel.Debug, Message = "Planning graph query rooted at {RootType}")]
    internal static partial void LogDebugCypherQueryVisitor36(this ILogger logger, global::System.Type rootType);

    [LoggerMessage(EventId = 79, Level = LogLevel.Debug, Message = "Added Cypher parameter names: {ParameterNames}; count: {ParameterCount}")]
    internal static partial void LogDebugCypherQueryVisitor53(this ILogger logger, global::System.String[] parameterNames, global::System.Int32 parameterCount);

    [LoggerMessage(EventId = 80, Level = LogLevel.Debug, Message = "Streaming async query for result type: {ResultType}")]
    internal static partial void LogDebugGraphQueryProvider71(this ILogger logger, global::System.String resultType);

    [LoggerMessage(EventId = 81, Level = LogLevel.Debug, Message = "Executing async query for result type: {ResultType}")]
    internal static partial void LogDebugGraphQueryProvider75(this ILogger logger, global::System.String resultType);

    [LoggerMessage(EventId = 82, Level = LogLevel.Debug, Message = "Expression type: {ExpressionType}")]
    internal static partial void LogDebugGraphQueryProvider77(this ILogger logger, global::System.String expressionType);

    [LoggerMessage(EventId = 83, Level = LogLevel.Error, Message = "Error executing query")]
    internal static partial void LogErrorGraphQueryProvider86(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 84, Level = LogLevel.Warning, Message = "Failed to roll back abandoned streaming query transaction")]
    internal static partial void LogWarningGraphQueryProvider89(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 85, Level = LogLevel.Debug, Message = "{Indent}Method: {Method} from {DeclaringType}")]
    internal static partial void LogDebugGraphQueryProvider97(this ILogger logger, global::System.String indent, global::System.String method, global::System.String? declaringType);

    [LoggerMessage(EventId = 86, Level = LogLevel.Debug, Message = "{Indent}Constant: {Type}")]
    internal static partial void LogDebugGraphQueryProvider110(this ILogger logger, global::System.String indent, global::System.String type);

    [LoggerMessage(EventId = 87, Level = LogLevel.Debug, Message = "Initialized AGE schema metadata")]
    internal static partial void LogDebugAgeSchemaManager49(this ILogger logger);

    [LoggerMessage(EventId = 88, Level = LogLevel.Information, Message = "AGE property indexes are unavailable because entity properties are stored in agtype")]
    internal static partial void LogInformationAgeSchemaManager60(this ILogger logger);

}
