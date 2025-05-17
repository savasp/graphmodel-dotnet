using System;

namespace Cvoya.Graph.Client.Neo4j
{
    internal static class DiagnosticsHelper
    {
        public static void ReportUnsupported(string context, Exception ex, string? suggestion = null)
        {
            var message = $"[Cypher Diagnostics] {context}: {ex.Message}";
            if (!string.IsNullOrEmpty(suggestion))
                message += $"\nSuggestion: {suggestion}";
            throw new NotSupportedException(message, ex);
        }
    }
}
