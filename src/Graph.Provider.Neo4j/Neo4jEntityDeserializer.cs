// Copyright 2025 Savas Parastatidis
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      https://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Neo4j.Driver;

namespace Cvoya.Graph.Client.Neo4j
{
    /// <summary>
    /// Provides deserialization logic for Neo4j nodes and relationships.
    /// </summary>
    public static class Neo4jEntityDeserializer
    {
        public static T DeserializeNode<T>(INode n) where T : new()
        {
            return (T)DeserializeNode(typeof(T), n);
        }

        public static object DeserializeNode(Type type, INode n)
        {
            var obj = Activator.CreateInstance(type)!;
            foreach (var prop in type.GetProperties())
            {
                if (n.Properties.TryGetValue(prop.Name, out var value))
                {
                    SetPropertyValue(prop, obj, value);
                }
            }
            return obj;
        }

        public static R DeserializeRelationship<R>(IRelationship rel) where R : new()
        {
            return (R)DeserializeRelationship(typeof(R), rel);
        }

        public static object DeserializeRelationship(Type type, IRelationship rel)
        {
            var obj = Activator.CreateInstance(type)!;
            foreach (var prop in type.GetProperties())
            {
                if (rel.Properties.TryGetValue(prop.Name, out var value))
                {
                    SetPropertyValue(prop, obj, value);
                }
            }
            return obj;
        }

        /// <summary>
        /// Asynchronously hydrate complex properties for a node by traversing relationships named after the property.
        /// </summary>
        public static async Task<object> DeserializeNodeWithComplexPropertiesAsync(Type type, INode n, IDriver driver, int depth = 1)
        {
            var obj = Activator.CreateInstance(type)!;
            foreach (var prop in type.GetProperties())
            {
                if (n.Properties.TryGetValue(prop.Name, out var value))
                {
                    SetPropertyValue(prop, obj, value);
                }
                else if (!prop.PropertyType.IsValueType && prop.PropertyType != typeof(string) && !typeof(System.Collections.IEnumerable).IsAssignableFrom(prop.PropertyType))
                {
                    // Complex property: look for a node related via a relationship named after the property
                    var label = type.FullName ?? type.Name;
                    var propertyLabel = prop.PropertyType.FullName ?? prop.PropertyType.Name;
                    var relType = prop.Name;
                    var cypher = $@"MATCH (a:`{label}`)-[r:`{relType}`]->(b:`{propertyLabel}`) WHERE a.Id = $id RETURN b LIMIT 1";
                    await using var newSession = driver.AsyncSession();
                    var cursor = await newSession.RunAsync(cypher, new { id = n.Properties["Identifier"] });
                    if (await cursor.FetchAsync())
                    {
                        var relatedNode = cursor.Current["b"] as INode;
                        if (relatedNode != null && depth > 0)
                        {
                            var relatedObj = await DeserializeNodeWithComplexPropertiesAsync(prop.PropertyType, relatedNode, driver, depth - 1);
                            prop.SetValue(obj, relatedObj);
                        }
                    }
                }
            }
            return obj;
        }

        private static void SetPropertyValue(PropertyInfo prop, object obj, object? value)
        {
            if (value == null) return;
            // Handle Neo4j temporal and spatial types
            if (prop.PropertyType == typeof(DateTime))
            {
                if (value is ZonedDateTime zonedDateTime)
                {
                    prop.SetValue(obj, zonedDateTime.ToDateTimeOffset().DateTime);
                    return;
                }
                if (value is LocalDateTime localDateTime)
                {
                    prop.SetValue(obj, localDateTime.ToDateTime());
                    return;
                }
                if (value is LocalDate localDate)
                {
                    prop.SetValue(obj, localDate.ToDateTime());
                    return;
                }
            }
            if (prop.PropertyType == typeof(DateTimeOffset))
            {
                if (value is ZonedDateTime zonedDateTime)
                {
                    prop.SetValue(obj, zonedDateTime.ToDateTimeOffset());
                    return;
                }
            }
            if (prop.PropertyType == typeof(TimeSpan))
            {
                if (value is LocalTime localTime)
                {
                    prop.SetValue(obj, localTime.ToTimeSpan());
                    return;
                }
                if (value is OffsetTime offsetTime)
                {
                    var ts = new TimeSpan(
                        0,
                        offsetTime.Hour,
                        offsetTime.Minute,
                        offsetTime.Second,
                        offsetTime.Nanosecond / 1000000
                    );
                    prop.SetValue(obj, ts);
                    return;
                }
                if (value is Duration duration)
                {
                    var totalDays = (int)(duration.Months * 30 + duration.Days);
                    int milliseconds = 0;
                    var nanoProp = duration.GetType().GetProperty("Nanosecond");
                    if (nanoProp != null)
                    {
                        var nanosObj = nanoProp.GetValue(duration);
                        if (nanosObj != null)
                            milliseconds = (int)((long)nanosObj / 1000000);
                    }
                    var ts = new TimeSpan(
                        totalDays,
                        0,
                        0,
                        (int)duration.Seconds,
                        milliseconds
                    );
                    prop.SetValue(obj, ts);
                    return;
                }
            }
            // You may want to handle Point (spatial) types here as well
            prop.SetValue(obj, value);
        }
    }
}
