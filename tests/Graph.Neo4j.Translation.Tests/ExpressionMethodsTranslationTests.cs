// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Translation.Tests;

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
        var query = Root.Nodes<Person>().Where(p => p.FirstName.StartsWith("A"));
        return VerifyTranslation(query);
    }

    [Fact]
    public Task StringEndsWith()
    {
        var query = Root.Nodes<Person>().Where(p => p.FirstName.EndsWith("e"));
        return VerifyTranslation(query);
    }

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
}
