// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Querying.Cypher.Execution;

using System.Collections;
using System.Data.Common;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Cvoya.Graph.Age.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Npgsql.Age.Types;

internal sealed partial class AgeQueryRunner
{
    private readonly string graphName;
    private readonly NpgsqlConnection connection;
    private readonly NpgsqlTransaction transaction;
    private readonly ILogger<AgeQueryRunner> logger;

    public AgeQueryRunner(
        string graphName,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ILoggerFactory? loggerFactory)
    {
        this.graphName = AgeSqlIdentifier.Validate(graphName, "graph name");
        this.connection = connection ?? throw new ArgumentNullException(nameof(connection));
        this.transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
        logger = loggerFactory?.CreateLogger<AgeQueryRunner>() ?? NullLogger<AgeQueryRunner>.Instance;
    }

    public Task<AgeResultCursor> RunAsync(
        string cypher,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var dictionary = ToParameterDictionary(parameters);
        return RunAsync(cypher, dictionary, InferProjectionColumns(cypher), cancellationToken);
    }

    /// <summary>The AGE graph name, which is also the Postgres schema holding this graph's label tables.</summary>
    public string GraphName => graphName;

    /// <summary>
    /// Runs a plain (non-<c>cypher()</c>) SQL statement on this runner's connection and transaction,
    /// binding the caller-supplied search text to the <c>@query</c> parameter, and returns the string
    /// values of the first result column. This is the phase-1 seam for full-text search: AGE cannot
    /// express Postgres text search in its Cypher subset, so the provider runs it as SQL over the
    /// label tables and seeds the residual Cypher query with the matching ids (see
    /// <see cref="Querying.AgeFullTextSearch"/>). It executes on the SAME transaction as the residual
    /// query, so it observes that transaction's uncommitted writes.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The caller composes the statement from validated SQL identifiers (AgeSqlIdentifier) " +
            "and constant text; the only user-controlled value is the search text, which travels as the " +
            "@query bind parameter.")]
    internal async Task<List<string>> QueryScalarStringsAsync(
        string sql,
        string? queryParameter,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        cancellationToken.ThrowIfCancellationRequested();

        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        if (queryParameter is not null)
        {
            command.Parameters.Add(new NpgsqlParameter("query", queryParameter));
        }

        await using var commandLease = command.ConfigureAwait(false);

        try
        {
            var values = new List<string>();
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using var readerLease = reader.ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (!await reader.IsDBNullAsync(0, cancellationToken).ConfigureAwait(false))
                {
                    values.Add(reader.GetString(0));
                }
            }

            return values;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (IsQueryExecutionFailure(exception))
        {
            throw WrapQueryExecutionFailure(exception);
        }
    }

    public async Task<AgeResultCursor> RunAsync(
        string cypher,
        IReadOnlyDictionary<string, object?> parameters,
        IReadOnlyList<string> projectionColumns,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cypher);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(projectionColumns);
        cancellationToken.ThrowIfCancellationRequested();

        var (command, distinct) = PrepareCommand(cypher, parameters, projectionColumns, streaming: false);
        await using var commandLease = command.ConfigureAwait(false);

        try
        {
            var records = new List<AgeRecord>();
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using var readerLease = reader.ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var values = new Dictionary<string, object?>(reader.FieldCount, StringComparer.Ordinal);
                for (var index = 0; index < reader.FieldCount; index++)
                {
                    values.Add(
                        reader.GetName(index),
                        await reader.IsDBNullAsync(index, cancellationToken).ConfigureAwait(false)
                            ? null
                            : ParseTextAgtype(reader.GetString(index)));
                }

                records.Add(new AgeRecord(values));
            }

            if (distinct)
            {
                // The key embeds the value's runtime type so 1 (integer) and "1" (string) stay
                // distinct rows.
                records = records.DistinctBy(GetDistinctKey, StringComparer.Ordinal).ToList();
            }

            logger.LogDebugAgeQueryRunner87(records.Count);
            return new AgeResultCursor(records);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (IsQueryExecutionFailure(exception))
        {
            throw WrapQueryExecutionFailure(exception);
        }
    }

    public async Task<AgeResultCursor> RunStreamingAsync(
        string cypher,
        IReadOnlyDictionary<string, object?> parameters,
        IReadOnlyList<string> projectionColumns,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cypher);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(projectionColumns);
        cancellationToken.ThrowIfCancellationRequested();

        var (command, distinct) = PrepareCommand(cypher, parameters, projectionColumns, streaming: true);

        try
        {
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            return new AgeResultCursor(
                new AgeReaderRecordSource(command, reader, WrapQueryExecutionFailure),
                distinct);
        }
        catch (OperationCanceledException)
        {
            await command.DisposeAsync().ConfigureAwait(false);
            throw;
        }
        catch (Exception exception) when (IsQueryExecutionFailure(exception))
        {
            await command.DisposeAsync().ConfigureAwait(false);
            throw WrapQueryExecutionFailure(exception);
        }
        catch
        {
            await command.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private (NpgsqlCommand Command, bool Distinct) PrepareCommand(
        string cypher,
        IReadOnlyDictionary<string, object?> parameters,
        IReadOnlyList<string> projectionColumns,
        bool streaming)
    {
        if (projectionColumns.Count == 0)
        {
            projectionColumns = InferProjectionColumns(cypher);
        }

        var distinct = ReturnDistinctRegex().IsMatch(cypher);
        cypher = ReturnDistinctRegex().Replace(cypher, "RETURN");
        var normalizedProjection = NormalizeProjectionAliases(cypher, projectionColumns);
        cypher = normalizedProjection.Cypher;
        var effectiveColumns = normalizedProjection.Columns.Count == 0 ? ["result"] : normalizedProjection.Columns;
        var command = CreateCommand(cypher, parameters, effectiveColumns);
        if (streaming)
        {
            logger.LogDebugAgeQueryRunner159(parameters.Count, effectiveColumns.Count, cypher);
        }
        else
        {
            logger.LogDebugAgeQueryRunner167(parameters.Count, effectiveColumns.Count, cypher);
        }
        return (command, distinct);
    }

    /// <summary>
    /// True for the exceptions the reader/command execution and <see cref="ParseTextAgtype"/> raise
    /// on a broken connection, a malformed server response, or an unexpected column shape - the
    /// failures this correlation-id boundary is meant to catch. Anything else (e.g. a bug
    /// elsewhere) should propagate rather than be silently wrapped.
    /// </summary>
    internal static bool IsQueryExecutionFailure(Exception exception) =>
        exception is NpgsqlException or JsonException or FormatException or InvalidCastException;

    internal static string GetDistinctKey(AgeRecord record) => string.Join(
        '\u001f',
        record.Values.Select(pair => $"{pair.Key}\u001e{pair.Value?.GetType().Name}\u001e{pair.Value}"));

    private GraphException WrapQueryExecutionFailure(Exception exception)
    {
        var correlationId = Guid.NewGuid().ToString("N");
        logger.LogErrorAgeQueryRunner192(exception, correlationId);
        return new GraphException($"AGE query execution failed. Correlation ID: {correlationId}.", exception);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "graphName and projection columns are validated by AgeSqlIdentifier, the dollar-quote tag is collision-proof, and values travel in the agtype parameter.")]
    private NpgsqlCommand CreateCommand(
        string cypher,
        IReadOnlyDictionary<string, object?> parameters,
        IReadOnlyList<string> projectionColumns)
    {
        cypher = NormalizeEntityProjection(cypher);
        var quoteTag = ChooseDollarQuoteTag(cypher);
        var columnDefinitions = string.Join(", ", projectionColumns.Select(column =>
            $"{AgeSqlIdentifier.Quote(column, "projection column")} agtype"));
        var textColumns = string.Join(", ", projectionColumns.Select(column =>
        {
            var quoted = AgeSqlIdentifier.Quote(column, "projection column");
            return $"age_result.{quoted}::text AS {quoted}";
        }));
        var serializedParameters = JsonSerializer.Serialize(
            parameters.ToDictionary(pair => pair.Key, pair => NormalizeParameter(pair.Value), StringComparer.Ordinal));

        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            $"SELECT {textColumns} FROM ag_catalog.cypher('{graphName}', {quoteTag}{cypher}{quoteTag}, @agtypeParams) " +
            $"AS age_result ({columnDefinitions})";
        command.Parameters.Add(new NpgsqlParameter
        {
            ParameterName = "agtypeParams",
            Value = new Agtype(serializedParameters),
            DataTypeName = "ag_catalog.agtype",
        });
        return command;
    }

    private static (string Cypher, IReadOnlyList<string> Columns) NormalizeProjectionAliases(
        string cypher,
        IReadOnlyList<string> projectionColumns)
    {
        if (projectionColumns.Count == 0 || projectionColumns.All(IsSqlIdentifier))
        {
            return (cypher, projectionColumns);
        }

        var matches = ReturnRegex().Matches(cypher);
        if (matches.Count == 0)
        {
            return (cypher, projectionColumns);
        }

        var match = matches[^1];
        var items = SplitTopLevel(match.Groups[1].Value).ToArray();
        if (items.Length != projectionColumns.Count)
        {
            throw new GraphException("AGE projection metadata does not match the rendered RETURN shape.");
        }

        var normalizedColumns = projectionColumns.ToArray();
        for (var index = 0; index < normalizedColumns.Length; index++)
        {
            if (IsSqlIdentifier(normalizedColumns[index]))
            {
                continue;
            }

            normalizedColumns[index] = $"age_column_{index}";
            items[index] = $"{items[index].Trim()} AS {normalizedColumns[index]}";
        }

        var replacement = string.Join(", ", items);
        cypher = $"{cypher[..match.Groups[1].Index]}{replacement}{cypher[(match.Groups[1].Index + match.Groups[1].Length)..]}";
        return (cypher, normalizedColumns);
    }

    private static bool IsSqlIdentifier(string value)
    {
        if (string.IsNullOrEmpty(value) || (!char.IsAsciiLetter(value[0]) && value[0] != '_')) return false;
        return value.Skip(1).All(character => char.IsAsciiLetterOrDigit(character) || character == '_');
    }

    private static string NormalizeEntityProjection(string cypher)
    {
        // AGE 1.7 rejects a named path on OPTIONAL MATCH. The shared entity projection only uses
        // that path to navigate the already-bound relationship list, so startNode/endNode are an
        // equivalent AGE representation that avoids the unsupported named-path form.
        foreach (Match match in NamedOptionalPathRegex().Matches(cypher))
        {
            var path = match.Groups["path"].Value;
            var relationships = match.Groups["relationships"].Value;
            cypher = cypher.Replace(
                $"OPTIONAL MATCH {path} = (",
                "OPTIONAL MATCH (",
                StringComparison.Ordinal);
            cypher = cypher.Replace($"{path} IS NULL", $"{relationships} IS NULL", StringComparison.Ordinal);
            cypher = cypher.Replace(
                $"nodes({path})[i + 1]",
                $"endNode({relationships}[i])",
                StringComparison.Ordinal);
            cypher = cypher.Replace(
                $"nodes({path})[i]",
                $"startNode({relationships}[i])",
                StringComparison.Ordinal);
        }

        // AGE 1.7 supports neither ALL(...) nor reduce(), and a list comprehension inside an
        // OPTIONAL MATCH WHERE drops the unmatched row instead of yielding NULL bindings, so the
        // shared every-hop marker predicate cannot be expressed here — these hydration paths check
        // the first hop only. That is safe in the storage model: value nodes are reachable only
        // through marker relationships and never own business relationships, so a marker first hop
        // implies marker hops throughout. The delete paths, which would over-delete on that
        // assumption, run in plain MATCH and enforce every hop (see AgeNodeManager and
        // ComplexPropertyManager).
        cypher = ComplexRelationshipAllPredicateRegex().Replace(
            cypher,
            "WHERE coalesce(${relationships}[toInteger(0)].${marker}, false) = true");
        cypher = ReduceCollectedPathsRegex().Replace(cypher, "collect(${path})");
        cypher = TemporalMemberRegex().Replace(cypher, match => match.Groups["member"].Value.ToLowerInvariant() switch
        {
            "year" => $"toInteger(substring({match.Groups["value"].Value}, 0, 4))",
            "month" => $"toInteger(substring({match.Groups["value"].Value}, 5, 2))",
            "day" => $"toInteger(substring({match.Groups["value"].Value}, 8, 2))",
            "hour" => $"toInteger(substring({match.Groups["value"].Value}, 11, 2))",
            "minute" => $"toInteger(substring({match.Groups["value"].Value}, 14, 2))",
            "second" => $"toInteger(substring({match.Groups["value"].Value}, 17, 2))",
            _ => match.Value,
        });
        cypher = StringContainsRegex().Replace(
            cypher,
            "${left} =~ ('.*' + ${right} + '.*')");
        cypher = PathIndexRegex().Replace(cypher, "[toInteger(${index})]");
        cypher = ReservedProjectionAliasRegex().Replace(cypher, "AS `${alias}`");
        cypher = SumAggregateRegex().Replace(cypher, "coalesce(sum(${value}), 0)");
        cypher = NormalizeInlineComplexPropertyProjections(cypher);
        return NormalizeLabelPatterns(cypher);
    }

    private static string NormalizeInlineComplexPropertyProjections(string cypher)
    {
        var aliases = new List<string>();
        cypher = InlineComplexPropertyProjectionRegex().Replace(cypher, match =>
        {
            var alias = match.Groups["alias"].Value;
            if (!aliases.Contains(alias, StringComparer.Ordinal))
            {
                aliases.Add(alias);
            }

            return $"{alias}_inline_properties";
        });
        if (aliases.Count == 0)
        {
            return cypher;
        }

        var returnMatch = ReturnRegex().Matches(cypher).Cast<Match>().LastOrDefault();
        if (returnMatch is null)
        {
            return cypher;
        }

        // pathIndex/hopIndex are the planner's TraversePaths decomposition variables; they must
        // survive the generated WITH pipes or the final RETURN/ORDER BY loses them from scope.
        var candidateAliases = new List<string> { "pathIndex", "hopIndex", "src", "r", "tgt" };
        for (var suffix = 2; suffix <= GraphDataModel.DefaultDepthAllowed; suffix++)
        {
            candidateAliases.Add($"r_{suffix}");
            candidateAliases.Add($"tgt_{suffix}");
        }

        var carry = candidateAliases
            .Where(candidate => Regex.IsMatch(cypher, $@"\b{candidate}\b", RegexOptions.CultureInvariant))
            .ToList();

        var clauses = new StringBuilder();
        foreach (var alias in aliases)
        {
            var relationships = $"{alias}_inline_relationships";
            var property = $"{alias}_inline_property";
            var path = $"{alias}_inline_path";
            var properties = $"{alias}_inline_properties";
            // First-hop-only marker check: comprehensions in an OPTIONAL MATCH WHERE drop the
            // unmatched row in AGE 1.7, and the storage model guarantees marker-first-hop paths
            // stay inside the complex-property subtree (see NormalizeEntityProjection).
            clauses.Append("OPTIONAL MATCH (").Append(alias).Append(")-[")
                .Append(relationships).Append("*1..").Append(GraphDataModel.DefaultDepthAllowed)
                .Append("]->(").Append(property).AppendLine(")")
                .Append("WHERE coalesce(").Append(relationships)
                .Append("[toInteger(0)].").Append(ComplexPropertyStorage.RelationshipMarkerProperty)
                .AppendLine(", false) = true")
                .Append("WITH ").Append(string.Join(", ", carry)).Append(", CASE WHEN ")
                .Append(relationships).Append(" IS NULL THEN [] ELSE [i IN range(0, size(")
                .Append(relationships).Append(") - 1) | { ParentNode: startNode(")
                .Append(relationships).Append("[toInteger(i)]), Relationship: ")
                .Append(relationships).Append("[toInteger(i)], SequenceNumber: ")
                .Append(relationships).Append("[toInteger(i)].SequenceNumber, Property: endNode(")
                .Append(relationships).Append("[toInteger(i)]) }] END AS ").AppendLine(path)
                .Append("WITH ").Append(string.Join(", ", carry)).Append(", collect(")
                .Append(path).Append(") AS ").AppendLine(properties);
            carry.Add(properties);
        }

        var returnStart = returnMatch.Index;
        return $"{cypher[..returnStart]}{clauses}{cypher[returnStart..]}";
    }

    private static string NormalizeLabelPatterns(string cypher)
    {
        var output = new List<string>();
        var generatedRelationshipAlias = 0;
        foreach (var originalLine in cypher.Split('\n'))
        {
            var line = originalLine;
            var predicates = new List<string>();
            var trimmedOriginal = originalLine.TrimStart();
            var isOptionalMatchLine = trimmedOriginal.StartsWith("OPTIONAL MATCH ", StringComparison.OrdinalIgnoreCase);
            var isMatchLine = isOptionalMatchLine ||
                trimmedOriginal.StartsWith("MATCH ", StringComparison.OrdinalIgnoreCase);
            if (!isMatchLine)
            {
                if (line.TrimStart().StartsWith("WHERE ", StringComparison.OrdinalIgnoreCase) &&
                    output.Count > 0 &&
                    output[^1].TrimStart().StartsWith("WHERE ", StringComparison.OrdinalIgnoreCase))
                {
                    output[^1] = $"{output[^1]} AND ({line.Trim()[6..]})";
                }
                else
                {
                    output.Add(line);
                }

                continue;
            }

            line = NodeLabelPatternRegex().Replace(line, match =>
            {
                var alias = match.Groups["alias"].Value;
                var labels = SplitRenderedIdentifiers(match.Groups["identifiers"].Value);
                predicates.Add($"({string.Join(" OR ", labels.Select(label =>
                    $"{RenderCypherString(label)} IN coalesce({alias}.inheritance_labels, [])"))})");
                return $"({alias})";
            });
            line = RelationshipTypePatternRegex().Replace(line, match =>
            {
                var alias = match.Groups["alias"].Value.Length > 0
                    ? match.Groups["alias"].Value
                    : $"age_relationship_{generatedRelationshipAlias++}";
                var depth = match.Groups["depth"].Value;
                var types = SplitRenderedIdentifiers(match.Groups["identifiers"].Value);
                if (depth.Length == 0)
                {
                    predicates.Add($"({string.Join(" OR ", types.Select(type =>
                        $"{RenderCypherString(type)} IN coalesce({alias}.inheritance_labels, [])"))})");
                }
                else if (isOptionalMatchLine)
                {
                    // A comprehension in an OPTIONAL MATCH WHERE drops the unmatched row in
                    // AGE 1.7, so an optional variable-length pattern can only type-check its
                    // first hop.
                    var propertyTarget = $"{alias}[toInteger(0)]";
                    predicates.Add($"({string.Join(" OR ", types.Select(type =>
                        $"{RenderCypherString(type)} IN coalesce({propertyTarget}.inheritance_labels, [])"))})");
                }
                else
                {
                    // A variable-length pattern type-constrains every hop; AGE lacks ALL(...), so
                    // the equivalent index-based comprehension checks each relationship in the list.
                    var perHop = string.Join(" OR ", types.Select(type =>
                        $"{RenderCypherString(type)} IN coalesce({alias}[toInteger(age_hop)].inheritance_labels, [])"));
                    predicates.Add(
                        $"(size([age_hop IN range(0, size({alias}) - 1) WHERE {perHop}]) = size({alias}))");
                }

                return $"[{alias}{depth}]";
            });

            if (predicates.Count > 0)
            {
                output.Add(line);
                output.Add($"WHERE {string.Join(" AND ", predicates)}");
            }
            else
            {
                output.Add(line);
            }
        }

        return string.Join('\n', output);
    }

    private static string[] SplitRenderedIdentifiers(string rendered) => rendered
        .Split('|', StringSplitOptions.RemoveEmptyEntries)
        .Select(identifier => identifier.StartsWith('`')
            ? identifier[1..^1].Replace("``", "`", StringComparison.Ordinal)
            : identifier)
        .ToArray();

    private static string RenderCypherString(string value) => $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";

    internal static string ChooseDollarQuoteTag(string cypher)
    {
        for (var suffix = 0; ; suffix++)
        {
            var candidate = $"$cvoya_age_{suffix}$";
            if (!cypher.Contains(candidate, StringComparison.Ordinal))
            {
                return candidate;
            }
        }
    }

    private static object? NormalizeParameter(object? value)
    {
        return value switch
        {
            null => null,
            DateTime dateTime => dateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            DateOnly date => date.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            TimeOnly time => time.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            TimeSpan duration => duration.ToString("c", System.Globalization.CultureInfo.InvariantCulture),
            Point point => new Dictionary<string, object?>
            {
                [nameof(Point.Latitude)] = point.Latitude,
                [nameof(Point.Longitude)] = point.Longitude,
                [nameof(Point.Height)] = point.Height,
            },
            IDictionary dictionary => NormalizeDictionary(dictionary),
            IEnumerable sequence when value is not string and not byte[] =>
                sequence.Cast<object?>().Select(NormalizeParameter).ToArray(),
            _ => value,
        };
    }

    internal static object? ParseTextAgtype(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.StartsWith('"'))
        {
            return JsonSerializer.Deserialize<string>(trimmed);
        }

        if ((trimmed.StartsWith('{') || trimmed.StartsWith('[')) &&
            !trimmed.Contains("::vertex", StringComparison.Ordinal) &&
            !trimmed.Contains("::edge", StringComparison.Ordinal) &&
            !trimmed.Contains("::path", StringComparison.Ordinal))
        {
            return JsonDocument.Parse(trimmed).RootElement.Clone();
        }

        if (trimmed.StartsWith('{') ||
            trimmed.StartsWith('[') ||
            trimmed.EndsWith("::numeric", StringComparison.Ordinal) ||
            trimmed is "true" or "false" or "null" or "NaN" or "Infinity" or "-Infinity" ||
            decimal.TryParse(trimmed, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _))
        {
            return new Agtype(trimmed);
        }

        return trimmed;
    }

    private static Dictionary<string, object?> NormalizeDictionary(IDictionary dictionary)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        var enumerator = dictionary.GetEnumerator();
        while (enumerator.MoveNext())
        {
            result.Add(
                Convert.ToString(enumerator.Key, System.Globalization.CultureInfo.InvariantCulture)!,
                NormalizeParameter(enumerator.Value));
        }

        return result;
    }

    private static IReadOnlyDictionary<string, object?> ToParameterDictionary(object? parameters)
    {
        if (parameters is null)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal);
        }

        if (parameters is IReadOnlyDictionary<string, object?> readOnly)
        {
            return readOnly;
        }

        if (parameters is IDictionary<string, object?> dictionary)
        {
            return new Dictionary<string, object?>(dictionary, StringComparer.Ordinal);
        }

        return parameters.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .ToDictionary(property => property.Name, property => property.GetValue(parameters), StringComparer.Ordinal);
    }

    private static string[] InferProjectionColumns(string cypher)
    {
        var matches = ReturnRegex().Matches(cypher);
        if (matches.Count == 0)
        {
            return [];
        }

        var returnText = matches[^1].Groups[1].Value;
        return SplitTopLevel(returnText).Select(item =>
        {
            var alias = AliasRegex().Match(item);
            if (alias.Success)
            {
                return alias.Groups[1].Value.Trim('`');
            }

            var expression = item.Trim();
            var dot = expression.LastIndexOf('.');
            return (dot >= 0 ? expression[(dot + 1)..] : expression).Trim('`', ' ');
        }).ToArray();
    }

    private static IEnumerable<string> SplitTopLevel(string value)
    {
        var start = 0;
        var depth = 0;
        for (var index = 0; index < value.Length; index++)
        {
            depth += value[index] switch
            {
                '(' or '[' or '{' => 1,
                ')' or ']' or '}' => -1,
                _ => 0,
            };
            if (value[index] == ',' && depth == 0)
            {
                yield return value[start..index];
                start = index + 1;
            }
        }

        yield return value[start..];
    }

    [GeneratedRegex(@"\bRETURN\s+(.+?)(?=\b(?:ORDER\s+BY|SKIP|LIMIT)\b|$)", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex ReturnRegex();

    [GeneratedRegex(@"\bAS\s+(`?[A-Za-z_][A-Za-z0-9_]*`?)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AliasRegex();

    [GeneratedRegex(@"OPTIONAL\s+MATCH\s+(?<path>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*\([^\r\n]+?\)-\[(?<relationships>[A-Za-z_][A-Za-z0-9_]*)\*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NamedOptionalPathRegex();

    [GeneratedRegex(@"^\s*WHERE\s+ALL\(rel\s+IN\s+(?<relationships>[A-Za-z_][A-Za-z0-9_]*)\s+WHERE\s+rel\.(?<marker>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*true\)\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex ComplexRelationshipAllPredicateRegex();

    [GeneratedRegex(@"reduce\(flat\s*=\s*\[\],\s*path\s+IN\s+collect\((?<path>[A-Za-z_][A-Za-z0-9_]*)\)\s*\|\s*flat\s*\+\s*path\)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReduceCollectedPathsRegex();

    [GeneratedRegex(@"\((?<alias>[A-Za-z_][A-Za-z0-9_]*):(?<identifiers>(?:`(?:``|[^`])+`|[A-Za-z_][A-Za-z0-9_]*)(?:\|(?:`(?:``|[^`])+`|[A-Za-z_][A-Za-z0-9_]*))*)\)", RegexOptions.CultureInvariant)]
    private static partial Regex NodeLabelPatternRegex();

    [GeneratedRegex(@"\[(?<alias>[A-Za-z_][A-Za-z0-9_]*)?:(?<identifiers>(?:`(?:``|[^`])+`|[A-Za-z_][A-Za-z0-9_]*)(?:\|(?:`(?:``|[^`])+`|[A-Za-z_][A-Za-z0-9_]*))*)(?<depth>\*[^\]]+)?\]", RegexOptions.CultureInvariant)]
    private static partial Regex RelationshipTypePatternRegex();

    [GeneratedRegex(@"(?<value>[A-Za-z_][A-Za-z0-9_]*\.[A-Za-z_][A-Za-z0-9_]*)\.(?<member>year|month|day|hour|minute|second)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TemporalMemberRegex();

    [GeneratedRegex(@"\[(?<index>i(?:\s*\+\s*1)?)\]", RegexOptions.CultureInvariant)]
    private static partial Regex PathIndexRegex();

    [GeneratedRegex(@"\bAS\s+(?<alias>exists|contains)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReservedProjectionAliasRegex();

    [GeneratedRegex(@"\bRETURN\s+DISTINCT\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReturnDistinctRegex();

    [GeneratedRegex(@"\bsum\((?<value>[^()]+)\)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SumAggregateRegex();

    [GeneratedRegex(@"(?<left>[A-Za-z_][A-Za-z0-9_]*\.[A-Za-z_][A-Za-z0-9_]*|\$[A-Za-z_][A-Za-z0-9_]*)\s+CONTAINS\s+(?<right>[A-Za-z_][A-Za-z0-9_]*\.[A-Za-z_][A-Za-z0-9_]*|\$[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex StringContainsRegex();

    [GeneratedRegex(@"reduce\(flat\s*=\s*\[\],\s*propertyPath\s+IN\s*\[\s*\((?<alias>[A-Za-z_][A-Za-z0-9_]*)\)-\[propertyRelationships\*1\.\.[0-9]+\]->\(propertyNode\).*?\]\s*\|\s*flat\s*\+\s*propertyPath\)", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex InlineComplexPropertyProjectionRegex();
}

internal sealed record AgeRecord(IReadOnlyDictionary<string, object?> Values)
{
    public IReadOnlyList<string> Keys { get; } = [.. Values.Keys];

    public object? this[string key] => Values[key];
}

internal interface IAgeRecordSource : IAsyncDisposable
{
    AgeRecord Current { get; }

    ValueTask<bool> ReadAsync(CancellationToken cancellationToken);
}

internal sealed class AgeReaderRecordSource(
    DbCommand command,
    DbDataReader reader,
    Func<Exception, GraphException> wrapQueryExecutionFailure) : IAgeRecordSource
{
    public AgeRecord Current { get; private set; } = null!;

    public async ValueTask<bool> ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return false;
            }

            var values = new Dictionary<string, object?>(reader.FieldCount, StringComparer.Ordinal);
            for (var index = 0; index < reader.FieldCount; index++)
            {
                values.Add(
                    reader.GetName(index),
                    await reader.IsDBNullAsync(index, cancellationToken).ConfigureAwait(false)
                        ? null
                        : AgeQueryRunner.ParseTextAgtype(reader.GetString(index)));
            }

            Current = new AgeRecord(values);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (AgeQueryRunner.IsQueryExecutionFailure(exception))
        {
            throw wrapQueryExecutionFailure(exception);
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await reader.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            await command.DisposeAsync().ConfigureAwait(false);
        }
    }
}

internal sealed class AgeResultCursor : IAsyncDisposable
{
    private readonly IReadOnlyList<AgeRecord>? records;
    private readonly IAgeRecordSource? source;
    private readonly HashSet<string>? distinctKeys;
    private int currentIndex = -1;
    private AgeRecord? current;
    private bool disposed;

    public AgeResultCursor(IReadOnlyList<AgeRecord> records)
    {
        this.records = records ?? throw new ArgumentNullException(nameof(records));
    }

    public AgeResultCursor(IAgeRecordSource source, bool distinct)
    {
        this.source = source ?? throw new ArgumentNullException(nameof(source));
        distinctKeys = distinct ? new HashSet<string>(StringComparer.Ordinal) : null;
    }

    public AgeRecord Current => current
        ?? throw new InvalidOperationException("The cursor is not positioned on a record.");

    public int Count => records?.Count
        ?? throw new InvalidOperationException("A streaming AGE cursor does not have a buffered count.");

    public async Task ConsumeAsync(CancellationToken cancellationToken = default)
    {
        if (records is not null)
        {
            return;
        }

        try
        {
            while (await FetchAsync(cancellationToken).ConfigureAwait(false))
            {
                // Drain the remaining records so the source is fully consumed.
            }
        }
        finally
        {
            await DisposeAsync().ConfigureAwait(false);
        }
    }

    public async Task<List<AgeRecord>> ToListAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (records is not null)
        {
            return records.ToList();
        }

        var result = new List<AgeRecord>();
        try
        {
            while (await FetchAsync(cancellationToken).ConfigureAwait(false))
            {
                result.Add(Current);
            }

            return result;
        }
        finally
        {
            await DisposeAsync().ConfigureAwait(false);
        }
    }

    public async Task<AgeRecord> SingleAsync(CancellationToken cancellationToken = default)
    {
        var result = await ToListAsync(cancellationToken).ConfigureAwait(false);
        return result.Count == 1
            ? result[0]
            : throw new InvalidOperationException($"Expected one AGE record but received {result.Count}.");
    }

    public async Task<AgeRecord> FirstAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (records is not null)
        {
            return records.Count > 0
                ? records[0]
                : throw new InvalidOperationException("The AGE result contains no records.");
        }

        try
        {
            return await FetchAsync(cancellationToken).ConfigureAwait(false)
                ? Current
                : throw new InvalidOperationException("The AGE result contains no records.");
        }
        finally
        {
            await DisposeAsync().ConfigureAwait(false);
        }
    }

    public async ValueTask<bool> FetchAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        if (records is not null)
        {
            currentIndex++;
            if (currentIndex >= records.Count)
            {
                current = null;
                return false;
            }

            current = records[currentIndex];
            return true;
        }

        while (await source!.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var record = source.Current;
            if (distinctKeys is null || distinctKeys.Add(AgeQueryRunner.GetDistinctKey(record)))
            {
                current = record;
                return true;
            }
        }

        current = null;
        return false;
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (source is not null)
        {
            await source.DisposeAsync().ConfigureAwait(false);
        }
    }
}
