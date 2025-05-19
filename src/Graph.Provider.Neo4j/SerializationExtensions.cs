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

using System.Reflection;
using Neo4j.Driver;

namespace Cvoya.Graph.Provider.Neo4j;

internal static class SerializationExtensions
{
    public static IDictionary<string, object?> ToDictionary<T>(this T obj)
    {
        var dictionary = new Dictionary<string, object?>();
        var properties = typeof(T).GetProperties();

        foreach (var property in properties)
        {
            dictionary[property.Name] = property.GetValue(obj);
        }

        return dictionary;
    }

    public static T FromDictionary<T>(this IReadOnlyDictionary<string, object?> dictionary)
        where T : new()
    {
        var obj = new T();
        var properties = typeof(T).GetProperties();

        foreach (var property in properties)
        {
            if (dictionary.TryGetValue(property.Name, out var value))
            {
                SetPropertyValue(property, obj, value);
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
        // TODO: Handle Point (spatial) types here as well
        prop.SetValue(obj, value);
    }
}