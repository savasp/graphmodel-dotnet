// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Translation.Tests;

using System.Globalization;
using Cvoya.Graph.Neo4j.Translation.Tests.Harness;
using Cvoya.Graph.Neo4j.Translation.Tests.Model;

/// <summary>
/// Covers expression-level constructs lowered through the shared Cypher planner: string
/// methods, DateTime members/arithmetic, Math methods, closures, and null comparisons. (Enum
/// comparisons and complex-property navigation in predicates are covered in
/// <see cref="WhereTranslationTests"/>.)
/// </summary>
public class ExpressionMethodsTranslationTests : TranslationTestBase
{
    [Fact]
    public Task StringContains()
    {
        var query = Root.Nodes<Person>().Where(p => p.FirstName.Contains("li"));
        return VerifyTranslation(query);
    }

    [Fact]
    public Task StringStartsWith()
    {
#pragma warning disable CA1866 // Preserve the string overload whose translation is snapshotted.
        var query = Root.Nodes<Person>().Where(p => p.FirstName.StartsWith("A"));
#pragma warning restore CA1866
        return VerifyTranslation(query);
    }

    [Fact]
    public Task StringEndsWith()
    {
#pragma warning disable CA1866 // Preserve the string overload whose translation is snapshotted.
        var query = Root.Nodes<Person>().Where(p => p.FirstName.EndsWith("e"));
#pragma warning restore CA1866
        return VerifyTranslation(query);
    }

    [Fact]
    public Task StringContains_Ordinal()
    {
        var query = Root.Nodes<Person>().Where(p => p.FirstName.Contains("li", StringComparison.Ordinal));
        return VerifyTranslation(query);
    }

    [Fact]
    public Task StringContains_OrdinalIgnoreCase_ThrowsNotSupported()
    {
        var query = Root.Nodes<Person>().Where(p => p.FirstName.Contains("ς", StringComparison.OrdinalIgnoreCase));
        return VerifyTranslationThrows(query);
    }

    [Fact]
    public Task StringStartsWith_OrdinalIgnoreCase_ThrowsNotSupported()
    {
        var query = Root.Nodes<Person>().Where(p => p.FirstName.StartsWith("a", StringComparison.OrdinalIgnoreCase));
        return VerifyTranslationThrows(query);
    }

    [Fact]
    public Task StringEndsWith_OrdinalIgnoreCase_ThrowsNotSupported()
    {
        var query = Root.Nodes<Person>().Where(p => p.FirstName.EndsWith("E", StringComparison.OrdinalIgnoreCase));
        return VerifyTranslationThrows(query);
    }

    // The culture-sensitive ToLower()/ToUpper() calls below deliberately exercise the
    // lowerer's mapping of those overloads to Cypher toLower()/toUpper(); they never
    // execute in-process, so no culture applies.
#pragma warning disable CA1304, CA1311, CA1862
    [Fact]
    public Task StringToLower()
    {
        var query = Root.Nodes<Person>().Where(p => p.FirstName.ToLower() == "alice");
        return VerifyTranslation(query);
    }

    [Fact]
    public Task StringToUpper()
    {
        var query = Root.Nodes<Person>().Where(p => p.FirstName.ToUpper() == "ALICE");
        return VerifyTranslation(query);
    }
#pragma warning restore CA1304, CA1311, CA1862

    [Fact]
    public Task StringToLowerInvariant()
    {
#pragma warning disable CA1862 // Preserve the normalization expression whose translation is snapshotted.
        var query = Root.Nodes<Person>().Where(p => p.FirstName.ToLowerInvariant() == "alice");
#pragma warning restore CA1862
        return VerifyTranslation(query);
    }

    [Fact]
    public Task StringToUpperInvariant()
    {
#pragma warning disable CA1862 // Preserve the normalization expression whose translation is snapshotted.
        var query = Root.Nodes<Person>().Where(p => p.FirstName.ToUpperInvariant() == "ALICE");
#pragma warning restore CA1862
        return VerifyTranslation(query);
    }

    [Fact]
    public Task StringToLower_WithCulture_ThrowsNotSupported()
    {
#pragma warning disable CA1862 // Preserve the unsupported normalization overload under test.
        var query = Root.Nodes<Person>().Where(
            p => p.FirstName.ToLower(CultureInfo.GetCultureInfo("tr-TR")) == "alice");
#pragma warning restore CA1862
        return VerifyTranslationThrows(query);
    }

    [Fact]
    public Task StringToUpper_WithCulture_ThrowsNotSupported()
    {
#pragma warning disable CA1862 // Preserve the unsupported normalization overload under test.
        var query = Root.Nodes<Person>().Where(
            p => p.FirstName.ToUpper(CultureInfo.GetCultureInfo("tr-TR")) == "ALICE");
#pragma warning restore CA1862
        return VerifyTranslationThrows(query);
    }

    [Fact]
    public Task StringSubstring_StartOnly()
    {
        var query = Root.Nodes<Person>().Select(p => p.FirstName.Substring(1));
        return VerifyTranslation(query);
    }

    [Fact]
    public Task StringSubstring_StartAndLength()
    {
        var query = Root.Nodes<Person>().Select(p => p.FirstName.Substring(0, 3));
        return VerifyTranslation(query);
    }

    [Fact]
    public Task StringReplace()
    {
        var query = Root.Nodes<Person>().Select(p => p.FirstName.Replace("a", "e"));
        return VerifyTranslation(query);
    }

    [Fact]
    public Task StringReplace_Ordinal()
    {
        var query = Root.Nodes<Person>().Select(p => p.FirstName.Replace("a", "e", StringComparison.Ordinal));
        return VerifyTranslation(query);
    }

    [Fact]
    public Task StringReplace_OrdinalIgnoreCase_ThrowsNotSupported()
    {
        var query = Root.Nodes<Person>().Select(p => p.FirstName.Replace("a", "e", StringComparison.OrdinalIgnoreCase));
        return VerifyTranslationThrows(query);
    }

    [Fact]
    public Task StringTrim_WithCharacters_ThrowsNotSupported()
    {
        var query = Root.Nodes<Person>().Select(p => p.FirstName.Trim('A'));
        return VerifyTranslationThrows(query);
    }

    [Fact]
    public Task StringTrimStart_WithCharacters_ThrowsNotSupported()
    {
        var query = Root.Nodes<Person>().Select(p => p.FirstName.TrimStart('A'));
        return VerifyTranslationThrows(query);
    }

    [Fact]
    public Task StringTrimEnd_WithCharacters_ThrowsNotSupported()
    {
        var query = Root.Nodes<Person>().Select(p => p.FirstName.TrimEnd('A'));
        return VerifyTranslationThrows(query);
    }

    [Fact]
    public Task StringLength()
    {
        var query = Root.Nodes<Person>().Where(p => p.FirstName.Length > 3);
        return VerifyTranslation(query);
    }

    [Fact]
    public Task StringIsNullOrEmpty()
    {
        var query = Root.Nodes<Person>().Where(p => !string.IsNullOrEmpty(p.FirstName));
        return VerifyTranslation(query);
    }

    [Fact]
    public Task DateTimeNow_AddDays()
    {
        var query = Root.Nodes<Person>().Where(p => p.CreatedAt < DateTime.Now.AddDays(-7));
        return VerifyTranslation(query);
    }

    [Fact]
    public Task DateTimeUtcNow_Comparison()
    {
        var query = Root.Nodes<Person>().Where(p => p.CreatedAt < DateTime.UtcNow);
        return VerifyTranslation(query);
    }

    [Fact]
    public Task DateTimeYearProperty()
    {
        var query = Root.Nodes<Person>().Where(p => p.CreatedAt.Year == 2024);
        return VerifyTranslation(query);
    }

    [Fact]
    public Task DateTimeMonthAndDayProperties()
    {
        var query = Root.Nodes<Person>().Where(p => p.CreatedAt.Month == 7 && p.CreatedAt.Day == 4);
        return VerifyTranslation(query);
    }

    [Fact]
    public Task DateOnlyYearProperty()
    {
        var query = Root.Nodes<Person>().Where(p => p.HiredOn.Year == 2024);
        return VerifyTranslation(query);
    }

    [Fact]
    public Task TimeOnlyHourProperty()
    {
        var query = Root.Nodes<Person>().Where(p => p.ShiftStartsAt.Hour == 9);
        return VerifyTranslation(query);
    }

    [Fact]
    public Task TimeSpanTotalDaysProperty()
    {
        var query = Root.Nodes<Person>().Where(p => p.Tenure.TotalDays > 30);
        return VerifyTranslation(query);
    }

    [Fact]
    public Task MathAbs()
    {
        var query = Root.Nodes<Person>().Where(p => Math.Abs(p.Age - 30) < 5);
        return VerifyTranslation(query);
    }

    [Fact]
    public Task MathRound()
    {
        var query = Root.Nodes<Person>().Select(p => Math.Round(p.Height));
        return VerifyTranslation(query);
    }

    [Fact]
    public Task MathSqrt()
    {
        var query = Root.Nodes<Person>().Select(p => Math.Sqrt(p.Height));
        return VerifyTranslation(query);
    }

    [Fact]
    public Task ClosureCapturedVariable_UsedTwice()
    {
        var name = "Alice";
        var query = Root.Nodes<Person>().Where(p => p.FirstName == name || p.LastName == name);
        return VerifyTranslation(query);
    }

    [Fact]
    public Task NullCoalescing_OnComplexProperty()
    {
        var query = Root.Nodes<Person>().Where(p => p.HomeAddress == null);
        return VerifyTranslation(query);
    }

    [Fact]
    public Task ComposedPredicateProjectionOrderingAndPaging()
    {
        var minimumAge = 18;
        var allowedNames = new[] { "Alice", "Grace" };
        var prefix = "a";
        var targetAge = 30;
        var tolerance = 12;
        var year = 2024;
        var bonus = 2;
        var fallback = "(missing)";

        var query = Root.Nodes<Person>()
            .Where(person =>
                person.Age >= minimumAge &&
                allowedNames.Contains(person.FirstName) &&
                (person.MiddleName == null || person.MiddleName.StartsWith(prefix)) &&
                !person.NullableNicknames.Contains("blocked") &&
                Math.Abs(person.Age - targetAge) <= tolerance &&
                person.CreatedAt.Year == year)
            .OrderBy(person => person.LastName)
            .ThenByDescending(person => person.Age)
            .Skip(1)
            .Take(3)
            .Select(person => new
            {
                person.FirstName,
                AdjustedAge = person.Age + bonus,
                Display = person.MiddleName == null ? fallback : person.MiddleName.ToUpperInvariant(),
            });

        return VerifyTranslation(query);
    }
}
