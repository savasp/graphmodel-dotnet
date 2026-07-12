// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Translation.Tests;

using System.Linq.Expressions;
using Cvoya.Graph.Neo4j.Querying.Cypher.Visitors.Core;
using Cvoya.Graph.Neo4j.Translation.Tests.Harness;
using Cvoya.Graph.Neo4j.Translation.Tests.Model;
using Microsoft.Extensions.Logging;

public class QueryPipelineRegressionTests
{
    [Fact]
    public void StreamingRoute_UsesCursorFetchLoop_NotBufferedQueryExecution()
    {
        var root = FindRepositoryRoot();

        var queryableSource = File.ReadAllText(Path.Join(
            root,
            "src",
            "Graph",
            "Querying",
            "Linq",
            "GraphQueryableBase.cs"));
        var getAsyncEnumerator = ExtractMember(
            queryableSource,
            "public IAsyncEnumerator<T> GetAsyncEnumerator",
            "#endregion");

        Assert.Contains("Provider.StreamAsync<T>", getAsyncEnumerator);
        Assert.DoesNotContain("Provider.ExecuteAsync<IEnumerable<T>>", getAsyncEnumerator);

        var executorSource = File.ReadAllText(Path.Join(
            root,
            "src",
            "Graph.Neo4j",
            "Querying",
            "Cypher",
            "Execution",
            "CypherExecutor.cs"));
        var streamAsync = ExtractBlockMember(
            executorSource,
            "public async IAsyncEnumerable<IRecord> StreamAsync");

        Assert.Contains("FetchAsync", streamAsync);
        Assert.DoesNotContain("ToListAsync", streamAsync);
    }

    [Fact]
    public void QueryTranslation_DebugLogs_DoNotIncludeParameterValues()
    {
        var secret = $"debug-secret-{Guid.NewGuid():N}";
        Expression<Func<Person, bool>> predicate = p => p.FirstName == secret;
        var query = Root.Nodes<Person>().Where(predicate);

        using var loggerFactory = new CapturingLoggerFactory(LogLevel.Debug);
        var visitor = new CypherQueryVisitor(typeof(Person), loggerFactory);
        visitor.Visit(query.Expression);

        var debugLog = string.Join(Environment.NewLine, loggerFactory.Messages(LogLevel.Debug));

        Assert.Contains("Added Cypher parameter", debugLog);
        Assert.DoesNotContain(secret, debugLog);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Join(directory.FullName, "AGENTS.md")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate repository root.");
    }

    private static string ExtractMember(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not find '{startMarker}'.");

        var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(end > start, $"Could not find end marker '{endMarker}'.");

        return source[start..end];
    }

    private static string ExtractBlockMember(string source, string startMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not find '{startMarker}'.");

        var bodyStart = source.IndexOf('{', start);
        Assert.True(bodyStart > start, $"Could not find body for '{startMarker}'.");

        var depth = 0;
        for (var index = bodyStart; index < source.Length; index++)
        {
            if (source[index] == '{')
            {
                depth++;
            }
            else if (source[index] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return source[start..(index + 1)];
                }
            }
        }

        throw new InvalidOperationException($"Could not find end of body for '{startMarker}'.");
    }

    private sealed class CapturingLoggerFactory(LogLevel minimumLevel) : ILoggerFactory
    {
        private readonly List<(LogLevel Level, string Message)> entries = [];

        public IEnumerable<string> Messages(LogLevel level) =>
            entries.Where(entry => entry.Level == level).Select(entry => entry.Message);

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(minimumLevel, entries);

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class CapturingLogger(
        LogLevel minimumLevel,
        List<(LogLevel Level, string Message)> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= minimumLevel;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
