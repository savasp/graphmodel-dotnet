// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using System.Collections;
using Cvoya.Graph.Serialization.Results;
using global::Neo4j.Driver;
using Neo4jNode = global::Neo4j.Driver.INode;
using Neo4jRelationship = global::Neo4j.Driver.IRelationship;

namespace Cvoya.Graph.Neo4j.Querying.Cypher.Execution;

internal sealed class Neo4jRecordAdapter
{
    public GraphRecord Adapt(IRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return new GraphRecord(record.Keys.ToDictionary(
            key => key,
            key => AdaptValue(record[key]),
            StringComparer.Ordinal));
    }

    public List<GraphRecord> Adapt(IReadOnlyList<IRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        return records.Select(Adapt).ToList();
    }

    private GraphValue AdaptValue(object? value)
    {
        return value switch
        {
            null => GraphValue.Scalar(null),
            Neo4jNode node => AdaptNode(node),
            Neo4jRelationship relationship => AdaptRelationship(relationship),
            IPath path => AdaptPath(path),
            IReadOnlyDictionary<string, object> map => AdaptMap(map),
            IDictionary<string, object> map => AdaptMap(map),
            IDictionary map => AdaptMap(map),
            IEnumerable sequence when value is not string and not byte[] =>
                GraphValue.List(sequence.Cast<object?>().Select(AdaptValue).ToArray()),
            _ => GraphValue.Scalar(AdaptScalar(value)),
        };
    }

    private GraphValue AdaptNode(Neo4jNode node) => GraphValue.Node(
        node.ElementId,
        node.Labels,
        node.Properties.ToDictionary(
            pair => pair.Key,
            pair => AdaptValue(pair.Value),
            StringComparer.Ordinal));

    private GraphValue AdaptRelationship(Neo4jRelationship relationship) => GraphValue.Relationship(
        relationship.ElementId,
        relationship.Type,
        relationship.StartNodeElementId,
        relationship.EndNodeElementId,
        relationship.Properties.ToDictionary(
            pair => pair.Key,
            pair => AdaptValue(pair.Value),
            StringComparer.Ordinal));

    private GraphValue AdaptPath(IPath path)
    {
        var items = new List<GraphValue>(path.Relationships.Count * 2 + 1);
        for (var index = 0; index < path.Relationships.Count; index++)
        {
            items.Add(AdaptNode(path.Nodes[index]));
            items.Add(AdaptRelationship(path.Relationships[index]));
        }

        items.Add(AdaptNode(path.Nodes[^1]));
        return GraphValue.Path(items);
    }

    private GraphValue AdaptMap(IEnumerable<KeyValuePair<string, object>> map) => GraphValue.Map(
        map.ToDictionary(pair => pair.Key, pair => AdaptValue(pair.Value), StringComparer.Ordinal));

    private GraphValue AdaptMap(IDictionary map)
    {
        var entries = new Dictionary<string, GraphValue>(StringComparer.Ordinal);
        foreach (DictionaryEntry entry in map)
        {
            if (entry.Key is not string key)
            {
                throw new GraphException("Neo4j result maps must use string keys.");
            }

            entries.Add(key, AdaptValue(entry.Value));
        }

        return GraphValue.Map(entries);
    }

    private static DateTimeOffset ToDateTimeOffset(ZonedDateTime dateTime)
    {
        try
        {
            return dateTime.ToDateTimeOffset();
        }
        catch (ValueTruncationException)
        {
            // Sub-tick nanosecond precision (written by another client) cannot convert directly;
            // parse the textual form instead, truncating to DateTimeOffset resolution.
            return DateTimeOffset.Parse(dateTime.ToString(), System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    private static object AdaptScalar(object value)
    {
        return value switch
        {
            global::Neo4j.Driver.Point point => new Graph.Point
            {
                Longitude = point.X,
                Latitude = point.Y,
                Height = point.Z,
            },
            LocalDate date => date.ToDateOnly(),
            LocalDateTime dateTime => dateTime.ToDateTime(),
            LocalTime time => time.ToTimeOnly(),
            OffsetTime time => new DateTimeOffset(
                1970,
                1,
                1,
                time.Hour,
                time.Minute,
                time.Second,
                TimeSpan.FromSeconds(time.OffsetSeconds)).AddTicks(time.Nanosecond / 100),
            ZonedDateTime dateTime => ToDateTimeOffset(dateTime),
            Duration { Months: 0 } duration =>
                TimeSpan.FromDays(duration.Days) +
                TimeSpan.FromSeconds(duration.Seconds) +
                TimeSpan.FromTicks(duration.Nanos / 100),
            Duration => throw new GraphException(
                "Neo4j calendar durations containing months cannot be represented as a provider-neutral TimeSpan."),
            _ => value,
        };
    }
}
