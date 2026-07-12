// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

global using Cvoya.Graph.Logging;

using Microsoft.Extensions.Logging;

namespace Cvoya.Graph.Logging;

internal static partial class LogMessages
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "Created node {NodeId} of type {NodeType}")]
    internal static partial void LogDebugInMemoryGraph178(this ILogger logger, global::System.String nodeId, global::System.String nodeType);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug, Message = "Created relationship {RelationshipId} of type {RelationshipType}")]
    internal static partial void LogDebugInMemoryGraph225(this ILogger logger, global::System.String relationshipId, global::System.String relationshipType);

}
