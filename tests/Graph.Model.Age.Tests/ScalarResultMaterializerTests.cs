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

namespace Cvoya.Graph.Model.Age.Tests;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Threading;
using Cvoya.Graph.Model.Age.Querying.Cypher.Execution;
using Npgsql.Age.Types;
using Xunit;

/// <summary>
/// Tests for ScalarResultMaterializer AgtypeConverters.
/// Uses reflection to access the private converter dictionary for unit testing.
/// </summary>
public sealed class ScalarResultMaterializerTests
{
    private static readonly Dictionary<Type, Func<Agtype, object?>> Converters = GetConverters();

    private static Dictionary<Type, Func<Agtype, object?>> GetConverters()
    {
        var field = typeof(ScalarResultMaterializer).GetField(
            "AgtypeConverters",
            BindingFlags.Static | BindingFlags.NonPublic);
        return (Dictionary<Type, Func<Agtype, object?>>)field!.GetValue(null)!;
    }

    private static Agtype Agtype(string value) => new(value);

    // ── DateTime Tests ──────────────────────────────────────────────

    [Fact]
    public void DateTime_ParseIso8601WithT()
    {
        var converter = Converters[typeof(DateTime)];
        var result = converter(Agtype("2024-06-15T14:30:00"));
        var dt = Assert.IsType<DateTime>(result);
        Assert.Equal(2024, dt.Year);
        Assert.Equal(6, dt.Month);
        Assert.Equal(15, dt.Day);
        Assert.Equal(14, dt.Hour);
        Assert.Equal(30, dt.Minute);
        Assert.Equal(0, dt.Second);
    }

    [Fact]
    public void DateTime_ParseDateOnly()
    {
        var converter = Converters[typeof(DateTime)];
        var result = converter(Agtype("2024-01-15"));
        var dt = Assert.IsType<DateTime>(result);
        Assert.Equal(2024, dt.Year);
        Assert.Equal(1, dt.Month);
        Assert.Equal(15, dt.Day);
    }

    [Fact]
    public void DateTime_ParseWithSpaceSeparator()
    {
        var converter = Converters[typeof(DateTime)];
        var result = converter(Agtype("2024-06-15 14:30:00"));
        var dt = Assert.IsType<DateTime>(result);
        Assert.Equal(2024, dt.Year);
        Assert.Equal(6, dt.Month);
        Assert.Equal(15, dt.Day);
        Assert.Equal(14, dt.Hour);
        Assert.Equal(30, dt.Minute);
    }

    [Fact]
    public void DateTime_PreservesUtcKind()
    {
        var converter = Converters[typeof(DateTime)];
        var result = converter(Agtype("2024-06-15T14:30:00Z"));
        var dt = Assert.IsType<DateTime>(result);
        Assert.Equal(DateTimeKind.Utc, dt.Kind);
    }

    [Fact]
    public void DateTime_PreservesUtcKindWithPositiveOffset()
    {
        // +00:00 offset uses RoundtripKind which yields Local kind adjusted for offset.
        // To preserve Utc kind, values should use Z suffix.
        var converter = Converters[typeof(DateTime)];
        var result = converter(Agtype("2024-06-15T14:30:00+00:00"));
        var dt = Assert.IsType<DateTime>(result);
        Assert.Equal(DateTimeKind.Local, dt.Kind);
    }

    [Fact]
    public void DateTime_UnspecifiedKind_BecomesLocal()
    {
        var converter = Converters[typeof(DateTime)];
        var result = converter(Agtype("2024-06-15T14:30:00"));
        var dt = Assert.IsType<DateTime>(result);
        Assert.Equal(DateTimeKind.Local, dt.Kind);
    }

    [Fact]
    public void DateTime_ParseWithFractionalSeconds()
    {
        var converter = Converters[typeof(DateTime)];
        var result = converter(Agtype("2024-06-15T14:30:00.1234567"));
        var dt = Assert.IsType<DateTime>(result);
        Assert.Equal(2024, dt.Year);
        Assert.Equal(6, dt.Month);
        Assert.Equal(15, dt.Day);
        Assert.Equal(14, dt.Hour);
        Assert.Equal(30, dt.Minute);
        Assert.Equal(0, dt.Second);
    }

    [Fact]
    public void DateTime_ParseWithExtraQuotes()
    {
        var converter = Converters[typeof(DateTime)];
        var result = converter(Agtype("\"2024-06-15T14:30:00\""));
        var dt = Assert.IsType<DateTime>(result);
        Assert.Equal(2024, dt.Year);
        Assert.Equal(6, dt.Month);
        Assert.Equal(15, dt.Day);
    }

    [Fact]
    public void DateTime_Nullable_ReturnsDateTime()
    {
        var converter = Converters[typeof(DateTime?)];
        var result = converter(Agtype("2024-06-15T14:30:00"));
        var dt = Assert.IsType<DateTime>(result);
        Assert.Equal(2024, dt.Year);
    }

    // ── Culture Invariant Tests ─────────────────────────────────────

    [Fact]
    public void DateTime_ParsesWithInvariantCulture_UnderGermanCulture()
    {
        // Save original culture and switch to de-DE
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUICulture = CultureInfo.CurrentUICulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("de-DE");

            var converter = Converters[typeof(DateTime)];
            var result = converter(Agtype("2024-06-15T14:30:00"));
            var dt = Assert.IsType<DateTime>(result);
            Assert.Equal(2024, dt.Year);
            Assert.Equal(6, dt.Month);
            Assert.Equal(15, dt.Day);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = originalCulture;
            Thread.CurrentThread.CurrentUICulture = originalUICulture;
        }
    }

    [Fact]
    public void DateTime_ParsesDateOnly_UnderGermanCulture()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUICulture = CultureInfo.CurrentUICulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("de-DE");

            var converter = Converters[typeof(DateTime)];
            var result = converter(Agtype("2024-01-15"));
            var dt = Assert.IsType<DateTime>(result);
            Assert.Equal(2024, dt.Year);
            Assert.Equal(1, dt.Month);
            Assert.Equal(15, dt.Day);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = originalCulture;
            Thread.CurrentThread.CurrentUICulture = originalUICulture;
        }
    }

    // ── DateTimeOffset Tests ──────────────────────────────────────

    [Fact]
    public void DateTimeOffset_ParseWithPositiveOffset()
    {
        var converter = Converters[typeof(DateTimeOffset)];
        var result = converter(Agtype("2024-06-15T14:30:00+02:00"));
        var dto = Assert.IsType<DateTimeOffset>(result);
        Assert.Equal(2024, dto.Year);
        Assert.Equal(6, dto.Month);
        Assert.Equal(15, dto.Day);
        Assert.Equal(14, dto.Hour);
        Assert.Equal(30, dto.Minute);
        Assert.Equal(new TimeSpan(2, 0, 0), dto.Offset);
    }

    [Fact]
    public void DateTimeOffset_ParseUtc()
    {
        var converter = Converters[typeof(DateTimeOffset)];
        var result = converter(Agtype("2024-06-15T12:30:00Z"));
        var dto = Assert.IsType<DateTimeOffset>(result);
        Assert.Equal(TimeSpan.Zero, dto.Offset);
    }

    [Fact]
    public void DateTimeOffset_ParseWithNegativeOffset()
    {
        var converter = Converters[typeof(DateTimeOffset)];
        var result = converter(Agtype("2024-06-15T14:30:00-05:00"));
        var dto = Assert.IsType<DateTimeOffset>(result);
        Assert.Equal(new TimeSpan(-5, 0, 0), dto.Offset);
    }

    [Fact]
    public void DateTimeOffset_Nullable_ReturnsDateTimeOffset()
    {
        var converter = Converters[typeof(DateTimeOffset?)];
        var result = converter(Agtype("2024-06-15T14:30:00+02:00"));
        var dto = Assert.IsType<DateTimeOffset>(result);
        Assert.Equal(new TimeSpan(2, 0, 0), dto.Offset);
    }

    // ── DateOnly Tests ───────────────────────────────────────────

    [Fact]
    public void DateOnly_Parse()
    {
        var converter = Converters[typeof(DateOnly)];
        var result = converter(Agtype("2024-01-15"));
        var date = Assert.IsType<DateOnly>(result);
        Assert.Equal(2024, date.Year);
        Assert.Equal(1, date.Month);
        Assert.Equal(15, date.Day);
    }

    [Fact]
    public void DateOnly_Nullable_ReturnsDateOnly()
    {
        var converter = Converters[typeof(DateOnly?)];
        var result = converter(Agtype("2024-01-15"));
        var date = Assert.IsType<DateOnly>(result);
        Assert.Equal(2024, date.Year);
    }

    // ── TimeOnly Tests ───────────────────────────────────────────

    [Fact]
    public void TimeOnly_Parse()
    {
        var converter = Converters[typeof(TimeOnly)];
        var result = converter(Agtype("14:30:00"));
        var time = Assert.IsType<TimeOnly>(result);
        Assert.Equal(14, time.Hour);
        Assert.Equal(30, time.Minute);
        Assert.Equal(0, time.Second);
    }

    [Fact]
    public void TimeOnly_ParseWithFractionalSeconds()
    {
        var converter = Converters[typeof(TimeOnly)];
        var result = converter(Agtype("14:30:00.1234567"));
        var time = Assert.IsType<TimeOnly>(result);
        Assert.Equal(14, time.Hour);
        Assert.Equal(30, time.Minute);
        Assert.Equal(0, time.Second);
    }

    [Fact]
    public void TimeOnly_Nullable_ReturnsTimeOnly()
    {
        var converter = Converters[typeof(TimeOnly?)];
        var result = converter(Agtype("14:30:00"));
        var time = Assert.IsType<TimeOnly>(result);
        Assert.Equal(14, time.Hour);
    }

    // ── TimeSpan Tests ───────────────────────────────────────────

    [Fact]
    public void TimeSpan_Parse_Days()
    {
        var converter = Converters[typeof(TimeSpan)];
        var result = converter(Agtype("1.02:03:00"));
        var ts = Assert.IsType<TimeSpan>(result);
        Assert.Equal(1, ts.Days);
        Assert.Equal(2, ts.Hours);
        Assert.Equal(3, ts.Minutes);
    }

    [Fact]
    public void TimeSpan_Parse_HoursMinutes()
    {
        var converter = Converters[typeof(TimeSpan)];
        var result = converter(Agtype("02:03:00"));
        var ts = Assert.IsType<TimeSpan>(result);
        Assert.Equal(2, ts.Hours);
        Assert.Equal(3, ts.Minutes);
    }

    [Fact]
    public void TimeSpan_Nullable_ReturnsTimeSpan()
    {
        var converter = Converters[typeof(TimeSpan?)];
        var result = converter(Agtype("02:03:00"));
        var ts = Assert.IsType<TimeSpan>(result);
        Assert.Equal(2, ts.Hours);
    }

    // ── Invalid Input Tests ───────────────────────────────────────

    [Fact]
    public void DateTime_InvalidInput_ReturnsNull()
    {
        var converter = Converters[typeof(DateTime)];
        var result = converter(Agtype("not-a-date"));
        Assert.Null(result);
    }

    [Fact]
    public void DateOnly_InvalidInput_ReturnsNull()
    {
        var converter = Converters[typeof(DateOnly)];
        var result = converter(Agtype("not-a-date"));
        Assert.Null(result);
    }

    [Fact]
    public void TimeOnly_InvalidInput_ReturnsNull()
    {
        var converter = Converters[typeof(TimeOnly)];
        var result = converter(Agtype("not-a-time"));
        Assert.Null(result);
    }

    [Fact]
    public void TimeSpan_InvalidInput_ReturnsNull()
    {
        var converter = Converters[typeof(TimeSpan)];
        var result = converter(Agtype("not-a-span"));
        Assert.Null(result);
    }

    [Fact]
    public void DateTimeOffset_InvalidInput_ReturnsNull()
    {
        var converter = Converters[typeof(DateTimeOffset)];
        var result = converter(Agtype("not-a-date"));
        Assert.Null(result);
    }
}
