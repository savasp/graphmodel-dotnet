// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Querying.Cypher.Execution;

using System.Collections;
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

    public Task<AgeResultCursor> RunAsync(string cypher, object? parameters = null)
    {
        var dictionary = ToParameterDictionary(parameters);
        return RunAsync(cypher, dictionary, InferProjectionColumns(cypher), CancellationToken.None);
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

        var temporalParameters = NormalizeTemporalParameterArithmetic(cypher, parameters);
        cypher = temporalParameters.Cypher;
        parameters = temporalParameters.Parameters;
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
        await using var commandLease = command.ConfigureAwait(false);
        logger.LogDebug(
            "Executing AGE Cypher query with {ParameterCount} parameters and {ColumnCount} projected columns: {Query}",
            parameters.Count,
            effectiveColumns.Count,
            cypher);

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
                records = records
                    .DistinctBy(record => string.Join(
                        '\u001f',
                        record.Values.Select(pair => $"{pair.Key}\u001e{pair.Value}")), StringComparer.Ordinal)
                    .ToList();
            }

            logger.LogDebug("AGE query returned {RecordCount} records", records.Count);
            return new AgeResultCursor(records);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            var correlationId = Guid.NewGuid().ToString("N");
            logger.LogError(exception, "AGE query execution failed; correlation ID {CorrelationId}", correlationId);
            throw new GraphException($"AGE query execution failed. Correlation ID: {correlationId}.", exception);
        }
    }

    private NpgsqlCommand CreateCommand(
        string cypher,
        IReadOnlyDictionary<string, object?> parameters,
        IReadOnlyList<string> projectionColumns)
    {
        cypher = NormalizeClauseOrder(cypher);
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

    private static string NormalizeClauseOrder(string cypher)
    {
        // The shared planner places paging immediately after the root MATCH to reduce work before
        // its entity-projection expansion. AGE requires WITH between MATCH and
        // LIMIT/SKIP. Moving paging after the final projection preserves the result semantics and
        // keeps the executor adaptation local to the dialect that needs it.
        var trailing = new List<string>();
        var retained = new List<string>();
        foreach (var line in cypher.Split('\n'))
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("LIMIT ", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("SKIP ", StringComparison.OrdinalIgnoreCase))
            {
                trailing.Add(trimmed);
            }
            else if (trimmed.StartsWith("ORDER BY ", StringComparison.OrdinalIgnoreCase))
            {
                trailing.Add(RewriteEntityOrdering(trimmed));
            }
            else
            {
                retained.Add(line);
            }
        }

        string reordered;
        if (trailing.Count == 0)
        {
            reordered = cypher;
        }
        else if (AggregateReturnRegex().IsMatch(cypher))
        {
            var returnIndex = retained.FindLastIndex(line =>
                line.TrimStart().StartsWith("RETURN ", StringComparison.OrdinalIgnoreCase));
            var withIndex = retained.FindLastIndex(returnIndex, line =>
                line.TrimStart().StartsWith("WITH ", StringComparison.OrdinalIgnoreCase));
            var insertionIndex = withIndex >= 0 ? withIndex + 1 : returnIndex;
            retained.InsertRange(insertionIndex, trailing);
            reordered = string.Join('\n', retained);
        }
        else
        {
            reordered = $"{string.Join('\n', retained)}\n{string.Join('\n', trailing)}";
        }
        return NormalizeEntityProjection(reordered);
    }

    private static string RewriteEntityOrdering(string orderBy)
    {
        var prefixLength = "ORDER BY ".Length;
        var items = SplitTopLevel(orderBy[prefixLength..]).Select(item =>
        {
            var trimmed = item.Trim();
            var suffix = string.Empty;
            if (trimmed.EndsWith(" DESC", StringComparison.OrdinalIgnoreCase))
            {
                suffix = trimmed[^5..];
                trimmed = trimmed[..^5].TrimEnd();
            }
            else if (trimmed.EndsWith(" ASC", StringComparison.OrdinalIgnoreCase))
            {
                suffix = trimmed[^4..];
                trimmed = trimmed[..^4].TrimEnd();
            }

            return trimmed is "src" or "tgt" or "r"
                ? $"{trimmed}.Id{suffix}"
                : $"{trimmed}{suffix}";
        });
        return $"ORDER BY {string.Join(", ", items)}";
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

        cypher = ComplexRelationshipAllPredicateRegex().Replace(
            cypher,
            "WHERE coalesce(${relationships}[toInteger(0)].${marker}, false) = true");
        cypher = ReduceCollectedPathsRegex().Replace(cypher, "collect(${path})");
        cypher = TemporalWrapperRegex().Replace(cypher, "${value}");
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

        var carry = new List<string>();
        var candidateAliases = new List<string> { "src", "r", "tgt" };
        for (var suffix = 2; suffix <= GraphDataModel.DefaultDepthAllowed; suffix++)
        {
            candidateAliases.Add($"r_{suffix}");
            candidateAliases.Add($"tgt_{suffix}");
        }

        foreach (var candidate in candidateAliases)
        {
            if (Regex.IsMatch(cypher, $@"\b{candidate}\b", RegexOptions.CultureInvariant))
            {
                carry.Add(candidate);
            }
        }

        var clauses = new StringBuilder();
        foreach (var alias in aliases)
        {
            var relationships = $"{alias}_inline_relationships";
            var property = $"{alias}_inline_property";
            var path = $"{alias}_inline_path";
            var properties = $"{alias}_inline_properties";
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
            var isMatchLine = trimmedOriginal.StartsWith("MATCH ", StringComparison.OrdinalIgnoreCase) ||
                trimmedOriginal.StartsWith("OPTIONAL MATCH ", StringComparison.OrdinalIgnoreCase);
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
                var propertyTarget = depth.Length == 0 ? alias : $"{alias}[toInteger(0)]";
                var types = SplitRenderedIdentifiers(match.Groups["identifiers"].Value);
                predicates.Add($"({string.Join(" OR ", types.Select(type =>
                    $"{RenderCypherString(type)} IN coalesce({propertyTarget}.inheritance_labels, [])"))})");
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

    private static IReadOnlyList<string> SplitRenderedIdentifiers(string rendered) => rendered
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

    private static object? ParseTextAgtype(string text)
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

    private static IReadOnlyDictionary<string, object?> NormalizeDictionary(IDictionary dictionary)
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

    internal static (string Cypher, IReadOnlyDictionary<string, object?> Parameters) NormalizeTemporalParameterArithmetic(
        string cypher,
        IReadOnlyDictionary<string, object?> parameters)
    {
        var mutable = new Dictionary<string, object?>(parameters, StringComparer.Ordinal);
        var index = 0;
        cypher = TemporalNowRegex().Replace(cypher, match =>
        {
            var parameterName = $"age_temporal_{index++}";
            mutable[parameterName] = match.Groups["function"].Value.ToLowerInvariant() switch
            {
                "localdatetime" => DateTime.Now,
                "date" => DateTime.Today.ToString(
                    "yyyy-MM-dd'T'00:00:00.0000000",
                    System.Globalization.CultureInfo.InvariantCulture),
                "time" => TimeOnly.FromDateTime(DateTime.Now),
                _ => DateTime.UtcNow,
            };
            return $"${parameterName}";
        });
        cypher = DurationMapRegex().Replace(cypher, "${value}");
        cypher = TemporalWrapperRegex().Replace(cypher, "${value}");
        cypher = TemporalParameterArithmeticRegex().Replace(cypher, match =>
        {
            if (!mutable.TryGetValue(match.Groups["value"].Value, out var rawValue) ||
                !mutable.TryGetValue(match.Groups["amount"].Value, out var rawAmount) ||
                rawValue is null || rawAmount is null)
            {
                return match.Value;
            }

            var amount = Convert.ToDouble(rawAmount, System.Globalization.CultureInfo.InvariantCulture);
            DateTimeOffset value;
            if (rawValue is DateTime dateTime)
            {
                value = new DateTimeOffset(dateTime);
            }
            else if (rawValue is DateTimeOffset dateTimeOffset)
            {
                value = dateTimeOffset;
            }
            else if (!DateTimeOffset.TryParse(
                Convert.ToString(rawValue, System.Globalization.CultureInfo.InvariantCulture),
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out value))
            {
                return match.Value;
            }

            value = match.Groups["unit"].Value.ToLowerInvariant() switch
            {
                "days" => value.AddDays(amount),
                "hours" => value.AddHours(amount),
                "minutes" => value.AddMinutes(amount),
                "seconds" => value.AddSeconds(amount),
                "milliseconds" => value.AddMilliseconds(amount),
                "months" => value.AddMonths(Convert.ToInt32(amount, System.Globalization.CultureInfo.InvariantCulture)),
                "years" => value.AddYears(Convert.ToInt32(amount, System.Globalization.CultureInfo.InvariantCulture)),
                _ => value,
            };
            var parameterName = $"age_temporal_{index++}";
            mutable[parameterName] = value.ToUniversalTime();
            return $"${parameterName}";
        });
        return (cypher, mutable);
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

    private static IReadOnlyList<string> InferProjectionColumns(string cypher)
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

    [GeneratedRegex(@"(?:datetime|localdatetime|date|time|duration)\((?<value>[^()]+)\)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TemporalWrapperRegex();

    [GeneratedRegex(@"(?<value>[A-Za-z_][A-Za-z0-9_]*\.[A-Za-z_][A-Za-z0-9_]*)\.(?<member>year|month|day|hour|minute|second)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TemporalMemberRegex();

    [GeneratedRegex(@"\$(?<value>[A-Za-z_][A-Za-z0-9_]*)\s*[+]\s*[{]\s*(?<unit>days|hours|minutes|seconds|milliseconds|months|years)\s*:\s*\$(?<amount>[A-Za-z_][A-Za-z0-9_]*)\s*[}]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TemporalParameterArithmeticRegex();

    [GeneratedRegex(@"(?<function>datetime|localdatetime|date|time)\(\)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TemporalNowRegex();

    [GeneratedRegex(@"duration\((?<value>\{[^()]+\})\)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DurationMapRegex();

    [GeneratedRegex(@"\[(?<index>i(?:\s*\+\s*1)?)\]", RegexOptions.CultureInvariant)]
    private static partial Regex PathIndexRegex();

    [GeneratedRegex(@"\bAS\s+(?<alias>exists|contains)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReservedProjectionAliasRegex();

    [GeneratedRegex(@"\bRETURN\s+DISTINCT\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReturnDistinctRegex();

    [GeneratedRegex(@"\bRETURN\s+(?:count|sum|min|max|avg)\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AggregateReturnRegex();

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

internal sealed class AgeResultCursor(IReadOnlyList<AgeRecord> records)
{
    private int currentIndex = -1;

    public AgeRecord Current => currentIndex >= 0 && currentIndex < records.Count
        ? records[currentIndex]
        : throw new InvalidOperationException("The cursor is not positioned on a record.");

    public int Count => records.Count;

    public Task ConsumeAsync() => Task.CompletedTask;

    public Task<List<AgeRecord>> ToListAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(records.ToList());
    }

    public Task<AgeRecord> SingleAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(records.Count == 1
            ? records[0]
            : throw new InvalidOperationException($"Expected one AGE record but received {records.Count}."));
    }

    public Task<AgeRecord> FirstAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(records.Count > 0
            ? records[0]
            : throw new InvalidOperationException("The AGE result contains no records."));
    }

    public Task<bool> FetchAsync()
    {
        currentIndex++;
        return Task.FromResult(currentIndex < records.Count);
    }
}
