// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Tests;

using System.Threading.Tasks;
using Serilog.Core;
using Serilog.Events;

public class WithCustomInfoEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var taskId = Task.CurrentId?.ToString() ?? "none";
        logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("TaskId", taskId));
    }
}