// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

global using Cvoya.Graph.Logging;

using Microsoft.Extensions.Logging;

namespace Cvoya.Graph.Logging;

internal static partial class LogMessages
{
    // LoggerExtensions emitted numeric event ID 0; preserve it during this mechanical migration.
    [LoggerMessage(EventId = 0, Level = LogLevel.Error, Message = "Error executing scheduled task")]
    internal static partial void LogErrorBackgroundWorker30(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 0, Level = LogLevel.Error, Message = "Error executing scheduled task")]
    internal static partial void LogErrorBackgroundWorker51(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Disposing DatabasePool")]
    internal static partial void LogDebugDatabasePoolManager106(this ILogger logger);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Requesting an available database from the pool")]
    internal static partial void LogDebugDatabasePoolManager127(this ILogger logger);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "A database is available, acquiring it")]
    internal static partial void LogDebugDatabasePoolManager135(this ILogger logger);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Acquired database {DatabaseName} from the pool")]
    internal static partial void LogDebugDatabasePoolManager139(this ILogger logger, global::System.String databaseName);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Releasing database {DatabaseName} back to the pool")]
    internal static partial void LogDebugDatabasePoolManager147(this ILogger logger, global::System.String databaseName);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Database {DatabaseName} is now available in the pool")]
    internal static partial void LogDebugDatabasePoolManager151(this ILogger logger, global::System.String databaseName);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Cleaning database {DatabaseName}")]
    internal static partial void LogDebugDatabasePoolManager166(this ILogger logger, global::System.String databaseName);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Database {DatabaseName} cleaned successfully")]
    internal static partial void LogDebugDatabasePoolManager170(this ILogger logger, global::System.String databaseName);

    [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Setting up {Count} databases in the pool (max concurrency: {Concurrency})")]
    internal static partial void LogInformationDatabasePoolManager176(this ILogger logger, global::System.Int32 count, global::System.Int32 concurrency);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Creating database {DatabaseName} in the pool")]
    internal static partial void LogDebugDatabasePoolManager190(this ILogger logger, global::System.String databaseName);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Database {DatabaseName} is ready for use ({Count}/{Total}, {Elapsed:F1}s)")]
    internal static partial void LogDebugDatabasePoolManager196(this ILogger logger, global::System.String databaseName, global::System.Int32 count, global::System.Int32 total, global::System.Double elapsed);

    [LoggerMessage(EventId = 0, Level = LogLevel.Error, Message = "Failed to create database {DatabaseName}, skipping")]
    internal static partial void LogErrorDatabasePoolManager202(this ILogger logger, Exception exception, global::System.String databaseName);

    [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Database pool setup complete: {SuccessCount} ready, {FailCount} failed in {Elapsed:F1}s")]
    internal static partial void LogInformationDatabasePoolManager225(this ILogger logger, global::System.Int32 successCount, global::System.Int32 failCount, global::System.Double elapsed);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Creating database {DatabaseName} if it does not exist")]
    internal static partial void LogDebugDatabasePoolManager231(this ILogger logger, global::System.String databaseName);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Database {DatabaseName} is now ready")]
    internal static partial void LogDebugDatabasePoolManager236(this ILogger logger, global::System.String databaseName);

    [LoggerMessage(EventId = 0, Level = LogLevel.Warning, Message = "Neo4j database administration is unavailable; using shared default database {DatabaseName} for tests ({FailCount} database-create failures)")]
    internal static partial void LogWarningDatabasePoolManager242(this ILogger logger, global::System.String databaseName, global::System.Int32 failCount);

    [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Shared default database {DatabaseName} is ready in {Elapsed:F1}s")]
    internal static partial void LogInformationDatabasePoolManager251(this ILogger logger, global::System.String databaseName, global::System.Double elapsed);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Scheduling drop of database {DatabaseName}")]
    internal static partial void LogDebugDatabasePoolManager256(this ILogger logger, global::System.String databaseName);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Dropping database {DatabaseName}")]
    internal static partial void LogDebugDatabasePoolManager259(this ILogger logger, global::System.String databaseName);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Database {DatabaseName} dropped successfully")]
    internal static partial void LogDebugDatabasePoolManager263(this ILogger logger, global::System.String databaseName);

    [LoggerMessage(EventId = 0, Level = LogLevel.Warning, Message = "Failed to drop database {DatabaseName}. It may not exist or is already dropped.")]
    internal static partial void LogWarningDatabasePoolManager267(this ILogger logger, global::System.String databaseName);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Waiting for database {DatabaseName} to become available for driver connections")]
    internal static partial void LogDebugDatabasePoolManager274(this ILogger logger, global::System.String databaseName);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Database {DatabaseName} is now online")]
    internal static partial void LogDebugDatabasePoolManager283(this ILogger logger, global::System.String databaseName);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Database {DatabaseName} not yet available, attempt {Attempt}/{MaxAttempts}")]
    internal static partial void LogDebugDatabasePoolManager289(this ILogger logger, global::System.String databaseName, global::System.Int32 attempt, global::System.Int32 maxAttempts);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Error checking database {DatabaseName}, attempt {Attempt}/{MaxAttempts}: {Error}")]
    internal static partial void LogDebugDatabasePoolManager294(this ILogger logger, global::System.String databaseName, global::System.Int32 attempt, global::System.Int32 maxAttempts, global::System.String error);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Disposing test infrastructure fixture")]
    internal static partial void LogDebugTestInfrastructureFixture65(this ILogger logger);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Reusing existing database: {DatabaseName}")]
    internal static partial void LogDebugTestInfrastructureFixture94(this ILogger logger, global::System.String databaseName);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Getting new database for test")]
    internal static partial void LogDebugTestInfrastructureFixture99(this ILogger logger);

    [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Initializing test: {TestName}")]
    internal static partial void LogInformationNeo4jTest29(this ILogger logger, global::System.String testName);

    [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Test {TestName} initialized successfully")]
    internal static partial void LogInformationNeo4jTest37(this ILogger logger, global::System.String testName);

}
