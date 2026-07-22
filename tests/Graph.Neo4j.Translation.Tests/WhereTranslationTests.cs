// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Translation.Tests;

using Cvoya.Graph.Neo4j.Translation.Tests.Harness;
using Cvoya.Graph.Neo4j.Translation.Tests.Model;

public class WhereTranslationTests : TranslationTestBase
{
    [Fact]
    public Task Where_SimplePredicate()
    {
        var query = Root.Nodes<Person>().Where(p => p.Age > 30);
        return VerifyTranslation(query);
    }

    [Fact]
    public Task Where_CustomPropertyLabel_UsesPhysicalKey()
    {
        var query = Root.Nodes<CustomPropertyLabelNode>().Where(p => p.LastName == "Smith");
        return VerifyTranslation(query);
    }

    [Fact]
    public Task Where_MultipleConditions_AndOr()
    {
        var query = Root.Nodes<Person>().Where(p => (p.Age > 30 && p.FirstName == "Alice") || p.LastName == "Smith");
        return VerifyTranslation(query);
    }

    [Fact]
    public Task Where_ChainedWhere()
    {
        var query = Root.Nodes<Person>().Where(p => p.Age > 18).Where(p => p.Age < 65);
        return VerifyTranslation(query);
    }

    [Fact]
    public Task Where_NullComparison()
    {
        var query = Root.Nodes<Person>().Where(p => p.HomeAddress != null);
        return VerifyTranslation(query);
    }

    [Fact]
    public Task Where_EnumComparison()
    {
        var query = Root.Nodes<Person>().Where(p => p.Status == EmploymentStatus.Active);
        return VerifyTranslation(query);
    }

    [Fact]
    public Task Where_ClosureCapturedVariable()
    {
        var minAge = 21;
        var query = Root.Nodes<Person>().Where(p => p.Age >= minAge);
        return VerifyTranslation(query);
    }

    private static readonly string[] PersonKeys = ["person-1", "person-2"];

    [Fact]
    public Task Where_CapturedQueryableConstantContainsEntityKey()
    {
        IQueryable<string> keys = PersonKeys.AsQueryable();
        var query = Root.Nodes<Person>().Where(p => keys.Contains(p.TestKey));
        return VerifyTranslation(query);
    }

    [Fact]
    public void Where_NullableSimpleCollectionContains_ReconstructsAndUsesNullAwareMembership()
    {
        var query = Root.Nodes<Person>().Where(person => person.NullableNicknames.Contains(null));

        var translation = CypherTranslator.Translate(query);

        Assert.Contains("__cvoya_sc:v1:n:", translation, StringComparison.Ordinal);
        Assert.Contains("size([__cvoya_collection_item", translation, StringComparison.Ordinal);
        Assert.DoesNotContain("client", translation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Where_ByteArrayContains_KeepsScalarAccessAndNativeMembership()
    {
        var query = Root.Nodes<Person>().Where(person => person.Fingerprint.Contains((byte)1));

        var translation = CypherTranslator.Translate(query);

        Assert.Contains("IN src.Fingerprint", translation, StringComparison.Ordinal);
        Assert.DoesNotContain("__cvoya_sc", translation, StringComparison.Ordinal);
        Assert.DoesNotContain("__cvoya_collection_item", translation, StringComparison.Ordinal);
    }

    [Fact]
    public Task Where_ComplexPropertyNavigation()
    {
        var query = Root.Nodes<Person>().Where(p => p.HomeAddress!.City == "Seattle");
        return VerifyTranslation(query);
    }

    /// <summary>
    /// Pins the #221 decision: complex-property navigation is null-propagating. The navigation
    /// hoists as OPTIONAL MATCH with a WITH * barrier before WHERE, so rows without the complex
    /// property stay in play for the other OR branch instead of being dropped by a required MATCH.
    /// </summary>
    [Fact]
    public Task Where_NavigationInOrPredicate_KeepsRowsWithoutComplexProperty()
    {
        var query = Root.Nodes<Person>().Where(p => p.HomeAddress!.City == "Seattle" || p.FirstName == "Alice");
        return VerifyTranslation(query);
    }

    [Fact]
    public Task Where_ComplexPropertyNavigation_ArbitraryDepth()
    {
        var query = Root.Nodes<Person>().Where(p => p.HomeAddress!.Region!.Name == "Northwest");
        return VerifyTranslation(query);
    }

    [Fact]
    public Task Where_ComplexPropertyNavigation_UsesRelationshipOverride()
    {
        var query = Root.Nodes<Person>().Where(p => p.MailingAddress!.City == "Seattle");
        return VerifyTranslation(query);
    }

    [Fact]
    public Task Where_ComplexCollectionAny()
    {
        var query = Root.Nodes<Person>().Where(p => p.Offices.Any(office => office.City == "Seattle"));
        return VerifyTranslation(query);
    }

    [Fact]
    public Task Where_ComplexCollectionAll()
    {
        var query = Root.Nodes<Person>().Where(p => p.Offices.All(office => office.City != "Closed"));
        return VerifyTranslation(query);
    }

    [Fact]
    public Task Where_ComplexCollectionCount()
    {
        var query = Root.Nodes<Person>().Where(p => p.Offices.Count > 1);
        return VerifyTranslation(query);
    }

    [Fact]
    public Task Where_OnRelationshipQueryable()
    {
        var query = Root.Relationships<Knows>().Where(k => k.Since > 2020);
        return VerifyTranslation(query);
    }
}
