// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Translation.Tests.Harness;

using System.Globalization;
using System.Linq.Expressions;
using System.Text;
using Cvoya.Graph.Neo4j.Querying.Cypher;
using Cvoya.Graph.Neo4j.Querying.Cypher.Visitors.Core;

/// <summary>
/// Drives <c>CypherQueryVisitor</c> directly against an <see cref="Expression"/> built via the
/// public <see cref="IGraphQueryable{T}"/> LINQ surface (rooted at <see cref="Root"/>), with no
/// Neo4j driver, transaction, or live graph involved. Produces a deterministic, human-readable
/// rendering of the generated Cypher text and parameters for snapshotting.
/// </summary>
internal static class CypherTranslator
{
    /// <summary>
    /// Translates the expression behind <paramref name="queryable"/> to Cypher and renders the
    /// result (query text + parameters) as a snapshot-friendly string.
    /// </summary>
    public static string Translate<T>(IQueryable<T> queryable) => Translate(typeof(T), queryable.Expression);

    /// <summary>
    /// Translates an arbitrary expression, with an explicit root element type (needed for
    /// terminal operations such as <c>Count</c>/<c>Sum</c> whose queryable result type no
    /// longer reflects the entity being queried).
    /// </summary>
    public static string Translate(Type rootType, Expression expression)
    {
        var visitor = new CypherQueryVisitor(rootType, null);
        visitor.Visit(expression);
        return Render(visitor.Query);
    }

    /// <summary>
    /// Runs the translation and captures any exception instead of letting it propagate, so
    /// that "this construct currently throws" can be snapshotted as the characterization
    /// itself.
    /// </summary>
    public static string TranslateExpectingException<T>(IQueryable<T> queryable) =>
        TranslateExpectingException(typeof(T), queryable.Expression);

    public static string TranslateExpectingException(Type rootType, Expression expression)
    {
        try
        {
            var result = Translate(rootType, expression);
            return "NO EXCEPTION THROWN. Result:" + Environment.NewLine + result;
        }
        catch (Exception ex) when (IsNonFatal(ex))
        {
            return $"{ex.GetType().FullName}: {ex.Message}";
        }
    }

    private static bool IsNonFatal(Exception ex) =>
        ex is not (
            OutOfMemoryException
            or StackOverflowException
            or AccessViolationException
            or AppDomainUnloadedException
            or BadImageFormatException
            or CannotUnloadAppDomainException
            or InvalidProgramException
            or ThreadAbortException);

    private static string Render(CypherQuery query)
    {
        var text = query.Text;
        var parameters = query.Parameters;

        var sb = new StringBuilder();
        sb.AppendLine("== Cypher ==");
        sb.AppendLine(text);
        sb.AppendLine();
        sb.AppendLine("== Parameters ==");

        if (parameters.Count == 0)
        {
            sb.AppendLine("(none)");
        }
        else
        {
            foreach (var kvp in parameters.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                sb.AppendLine(FormattableString.Invariant($"{kvp.Key} = {FormatValue(kvp.Value)}"));
            }
        }

        return sb.ToString();
    }

    private static string FormatValue(object? value) => value switch
    {
        null => "null",
        string s => $"\"{s}\"",
        bool b => b ? "true" : "false",
        DateTime dt => dt.ToString("O", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        System.Collections.IEnumerable enumerable and not string =>
            "[" + string.Join(", ", enumerable.Cast<object?>().Select(FormatValue)) + "]",
        _ => value.ToString() ?? "null"
    };
}
