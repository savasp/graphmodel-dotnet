// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Translation.Tests;

using Cvoya.Graph.Neo4j.Translation.Tests.Harness;
using Cvoya.Graph.Neo4j.Translation.Tests.Model;

public sealed class LabelFilterTranslationTests : TranslationTestBase
{
    [Fact]
    public Task TypedLabelFilters_RenderAnyAndAllPredicates()
    {
        var query = Root.Nodes<Person>()
            .OfLabels(GraphLabelMatch.Any, "Manager", "Contractor")
            .OfLabels(GraphLabelMatch.All, "Active", "Verified")
            .Where(person => person.Age >= 18)
            .OrderBy(person => person.FirstName)
            .Take(5);

        return VerifyTranslation(query);
    }

    [Fact]
    public Task DynamicLabelFilter_TreatsHostileLabelAsAValue()
    {
        var query = Root.Nodes<DynamicNode>()
            .OfLabel("Manager') OR true //");

        return VerifyTranslation(query);
    }

    [Fact]
    public void DynamicHasLabel_UsesThePublicLabelsProperty()
    {
        var query = Root.Nodes<DynamicNode>()
            .Where(node => node.HasLabel("Manager"));

        var translation = CypherTranslator.Translate(query);

        Assert.Contains("$p0 IN src.Labels", translation, StringComparison.Ordinal);
        Assert.DoesNotContain("labels(src)", translation, StringComparison.Ordinal);
    }
}
