// Copyright 2025 Savas Parastatidis

namespace Cvoya.Graph.Model.Age.Tests;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Cvoya.Graph.Model;
using Cvoya.Graph.Model.Age.Querying.Cypher.Visitors.Core;
using Cvoya.Graph.Model.Cypher.Querying.Cypher.Visitors.Core;
using Xunit;

public sealed class FragmentRendererTests
{
    /// <summary>
    /// Tests the specific case where Traverse (which internally uses PathSegments + Select)
    /// is followed by another explicit PathSegments + Select with projection.
    /// This tests that the projection uses the correct hop aliases.
    /// Expected: RETURN src0 AS StartNode, tgt0 AS EndNode (using hop 0 from outer PathSegments)
    /// </summary>
    [Fact]
    public void TraverseFollowedByPathSegmentsProjection_UsesCorrectAliases()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonNode>(provider);

        // This mimics: Traverse<Person, Knows, Person>().PathSegments<Person, Knows, Person>().Select(...)
        // Traverse internally does PathSegments + Select, so we have:
        // - First PathSegments (hop 0): Person->Person
        // - First Select: ps.EndNode (intermediate, returns Person node)
        // - Second PathSegments (hop 1): Person->Person  
        // - Second Select: new { ps.StartNode, ps.EndNode } (final, should use hop 1 aliases)
        var query = source
            .PathSegments<PersonNode, KnowsRelationship, PersonNode>()  // hop 0
            .Select(ps => ps.EndNode)                                     // intermediate Select
            .PathSegments<PersonNode, KnowsRelationship, PersonNode>()  // hop 1
            .Select(ps => new { ps.StartNode, ps.EndNode });            // final Select - should use hop 1

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);

        var cypher = context.GetQuery();
        
        // Debug: print the query to see what we're generating
        Console.WriteLine($"Generated Cypher:\n{cypher}");
        
        // The final projection should use hop 1 aliases (from second PathSegments)
        // In a CHAINED pattern: hop 1 starts at tgt0 (end of hop 0), NOT src1
        // Pattern: (src0)-[r0]->(tgt0)-[r1]->(tgt1)
        // StartNode of hop 1 = tgt0, EndNode of hop 1 = tgt1
        Assert.Contains("tgt0", cypher); // StartNode for hop 1 in chained pattern
        Assert.Contains("tgt1", cypher); // EndNode for hop 1
        Assert.Contains("StartNode", cypher);
        Assert.Contains("EndNode", cypher);
        
        // Should NOT use src1 (doesn't exist in chained pattern)
        Assert.DoesNotContain("src1", cypher);
        // Should NOT use hop 2 aliases (CurrentHop after both PathSegments, but no hop 2 exists)
        Assert.DoesNotContain("src2", cypher);
        Assert.DoesNotContain("tgt2", cypher);
        
        // Verify we have a proper chained pattern, not separate patterns
        // Should be: (src0)-[r0]->(tgt0)-[r1]->(tgt1) not separate MATCH clauses
        var matchCount = System.Text.RegularExpressions.Regex.Matches(cypher, @"\bMATCH\b").Count;
        Assert.Equal(1, matchCount); // Should only have one MATCH
        
        // The RETURN clause should NOT include intermediate Selects
        // It should only have the final projection: RETURN tgt0 AS StartNode, tgt1 AS EndNode
        // (tgt0 because that's the StartNode of the chained hop 1)
        Assert.Contains("RETURN tgt0 AS StartNode, tgt1 AS EndNode", cypher);
    }

    /// <summary>
    /// Focused test for pattern chaining: two consecutive PathSegments should create
    /// a single chained MATCH pattern, not two separate patterns.
    /// </summary>
    [Fact]
    public void ChainedPathSegments_CreatesChainedPattern()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonNode>(provider);

        // Two chained PathSegments should create: (src0)-[r0]->(tgt0)-[r1]->(tgt1)
        var query = source
            .PathSegments<PersonNode, KnowsRelationship, PersonNode>()  // hop 0
            .Select(ps => ps.EndNode)                                     // returns target of hop 0
            .PathSegments<PersonNode, KnowsRelationship, PersonNode>()  // hop 1 - should chain to tgt0
            .Select(ps => ps.EndNode);                                    // returns target of hop 1

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);

        var cypher = context.GetQuery();
        Console.WriteLine($"Generated Cypher:\n{cypher}");
        
        // Should have exactly ONE MATCH clause
        var matchCount = System.Text.RegularExpressions.Regex.Matches(cypher, @"\bMATCH\b").Count;
        Assert.Equal(1, matchCount);
        
        // Should NOT have a comma separating patterns (indicates separate patterns)
        var matchClause = cypher.Substring(cypher.IndexOf("MATCH"), 
            cypher.IndexOf("RETURN") - cypher.IndexOf("MATCH"));
        
        // Count commas in MATCH clause - should be 0 for chained pattern
        // (commas separate independent patterns)
        var commaCount = matchClause.Count(c => c == ',');
        Assert.Equal(0, commaCount);
        
        // Should contain the chained pattern structure
        // Pattern should flow: src0 -> r0 -> tgt0 -> r1 -> tgt1
        // Note: In a chained pattern, there's no src1 - the chain continues from tgt0
        Assert.Contains("src0", cypher);
        Assert.Contains("tgt0", cypher);
        Assert.Contains("r0", cypher);
        // src1 should NOT be in a properly chained pattern
        Assert.DoesNotContain("src1", cypher);
        Assert.Contains("tgt1", cypher);
        Assert.Contains("r1", cypher);
        
        // Verify the chain structure: tgt0 should connect to the second relationship
        // Expected pattern: (src0)-[r0]->(tgt0)-[r1]->(tgt1)
        // The key is that tgt0 appears before the second relationship
        var tgt0Index = cypher.IndexOf("tgt0");
        var r1Index = cypher.IndexOf("r1");
        Assert.True(tgt0Index < r1Index, "tgt0 should appear before r1 in a chained pattern");
    }

    /// <summary>
    /// Focused test for intermediate Select behavior: intermediate Select operations
    /// should not add entries to the final RETURN clause, only the terminal Select should.
    /// </summary>
    [Fact]
    public void IntermediateSelect_DoesNotAppearInFinalReturn()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonNode>(provider);

        // Chain of Selects: only the FINAL one should appear in RETURN
        var query = source
            .PathSegments<PersonNode, KnowsRelationship, PersonNode>()
            .Select(ps => ps.EndNode)                                // intermediate - should NOT be in RETURN
            .PathSegments<PersonNode, KnowsRelationship, PersonNode>()
            .Select(ps => new { Name = ps.EndNode.Name });          // final - SHOULD be in RETURN

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);

        var cypher = context.GetQuery();
        Console.WriteLine($"Generated Cypher:\n{cypher}");
        
        // Extract RETURN clause
        var returnIndex = cypher.IndexOf("RETURN");
        var returnClause = cypher.Substring(returnIndex);
        Console.WriteLine($"RETURN clause: {returnClause}");
        
        // The RETURN should only have the final projection (Name alias)
        Assert.Contains("Name", returnClause);
        
        // Count the number of items in RETURN by splitting on comma
        // Should only have 1 item: the Name projection
        var returnItems = returnClause
            .Replace("RETURN ", "")
            .Trim()
            .Split(',')
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();
        
        Console.WriteLine($"RETURN items count: {returnItems.Count}");
        foreach (var item in returnItems)
        {
            Console.WriteLine($"  - {item}");
        }
        
        // Should only have 1 return item (the final Name projection)
        // Currently failing because intermediate Select adds tgt1
        var singleItem = Assert.Single(returnItems);
        
        // The single return item should be the Name projection
        Assert.Contains("Name", singleItem);
    }

    [Fact]
    public void RendererMatchesBuilderForSingleHopWhereClause()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonNode>(provider);

        var query = source
            .PathSegments<PersonNode, KnowsRelationship, PersonNode>()
            .Where(ps => ps.EndNode.Age > 30)
            .Select(ps => ps.EndNode);

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);

        var builderOutput = context.GetQuery();
        var fragmentOutput = AgeFragmentRenderer.Render(context.FragmentSequence);

        Assert.NotEmpty(context.FragmentSequence);
    Assert.Equal(builderOutput, fragmentOutput);
    }

    [Fact]
    public void RendererMatchesBuilderForSimpleProjection()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonNode>(provider);

        var query = source
            .Select(p => new { Name = p.Name, Age = p.Age });

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);

        Assert.NotEmpty(context.FragmentSequence);
        Assert.Contains(context.FragmentSequence, fragment => fragment is MatchRootFragment);
        Assert.Contains(context.FragmentSequence, fragment => fragment is ProjectionFragment);
    Assert.Contains(context.FragmentSequence, fragment => fragment is ComplexPropertyLoadingFragment toggle && !toggle.IsEnabled);

        var builderOutput = context.GetQuery();
        var fragmentOutput = AgeFragmentRenderer.Render(context.FragmentSequence);

        Assert.Equal(builderOutput, fragmentOutput);
    }

    [Fact]
    public void RendererMatchesBuilderForMultiHopTraversal()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonNode>(provider);

        var query = source
            .PathSegments<PersonNode, KnowsRelationship, PersonNode>()
            .Select(ps => ps.EndNode)
            .PathSegments<PersonNode, KnowsRelationship, PersonNode>()
            .Select(ps => new { From = ps.StartNode.Name, To = ps.EndNode.Name, Relationship = ps.Relationship.Type });

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);

        var fragments = context.FragmentSequence.ToList();

        Assert.NotEmpty(fragments);
        Assert.True(fragments.Count(fragment => fragment is MatchSegmentFragment) >= 2);
        Assert.NotEmpty(fragments.OfType<ProjectionFragment>());

        var builderOutput = context.GetQuery();
        var fragmentOutput = AgeFragmentRenderer.Render(fragments);

        Assert.Equal(builderOutput, fragmentOutput);
    }

    [Fact]
    public void PathSegmentsWithInterfaceRelationship_UsesUnspecifiedRelationshipPattern()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonNode>(provider);

        var query = source
            .PathSegments<PersonNode, IRelationship, PersonNode>()
            .Select(ps => ps.Relationship);

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);

        var fragments = context.FragmentSequence.ToList();
        var builderOutput = context.GetQuery();
        var fragmentOutput = AgeFragmentRenderer.Render(fragments);

        Assert.Equal(builderOutput, fragmentOutput);
        Assert.Contains("MATCH (src0:PersonNode)-[r0]->(tgt0:PersonNode)", builderOutput);
        Assert.DoesNotContain(":IRelationship", builderOutput);
        Assert.DoesNotContain("'IRelationship' IN r0.inheritance_labels", builderOutput);
    }

    [Fact]
    public void RelationshipFirstOrDefault_IncludesReturnClause()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphRelationshipQueryable<KnowsRelationship>(provider);

        var context = new CypherQueryContext(typeof(KnowsRelationship));
        var visitor = new AgeCypherQueryVisitor(context);
        var firstOrDefaultExpression = Expression.Call(
            typeof(Queryable),
            nameof(Queryable.FirstOrDefault),
            new[] { typeof(KnowsRelationship) },
            source.Expression);

        visitor.Visit(firstOrDefaultExpression);

    var fragments = context.FragmentSequence.ToList();
    var builderOutput = context.GetQuery();
        var fragmentOutput = AgeFragmentRenderer.Render(fragments);

        Assert.Contains("RETURN r0", builderOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LIMIT 1", builderOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(builderOutput, fragmentOutput);
    }

    [Fact]
    public void TraverseThenPathSegments_EmitsOrderedMatchPattern()
    {
        var provider = new TestGraphQueryProvider();
        var users = new TestGraphNodeQueryable<PersonNode>(provider);

        var query = users
            .Where(u => u.Name == "Alice")
            .Traverse<PersonNode, KnowsRelationship, PersonNode>()
            .Where(friend => friend.Age >= 25)
            .Where(friend => friend.Age <= 40)
            .OrderByDescending(friend => friend.Age)
            .Take(1)
            .PathSegments<PersonNode, KnowsRelationship, PersonNode>()
            .Select(ps => new { ps.StartNode, ps.EndNode });

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);

        var fragments = context.FragmentSequence.ToList();
        var builderOutput = context.GetQuery();
        var fragmentOutput = AgeFragmentRenderer.Render(fragments);

        Assert.Contains(
            "MATCH (src0:PersonNode)-[r0:KnowsRelationship]->(tgt0:PersonNode)-[r1:KnowsRelationship]->(tgt1:PersonNode)",
            builderOutput,
            StringComparison.Ordinal);
        Assert.Contains("RETURN", builderOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ORDER BY", builderOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LIMIT 1", builderOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(builderOutput, fragmentOutput);
    }

    [Fact]
    public void RendererMatchesBuilderForAggregationWithOrdering()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonNode>(provider);

        var query = source
            .PathSegments<PersonNode, KnowsRelationship, PersonNode>()
            .GroupBy(ps => ps.StartNode.Name)
            .Select(group => new
            {
                Name = group.Key,
                FriendCount = group.Count(),
            })
            .OrderByDescending(result => result.FriendCount);

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);

        var fragments = context.FragmentSequence.ToList();

        Assert.NotEmpty(fragments);
        Assert.Contains(fragments, fragment => fragment is ProjectionFragment projection && projection.Returns.Any(returnClause => returnClause.Contains("count", StringComparison.OrdinalIgnoreCase)));
        Assert.Contains(fragments, fragment => fragment is OrderFragment order && order.Descending);

        var builderOutput = context.GetQuery();
        var fragmentOutput = AgeFragmentRenderer.Render(fragments);

        Assert.Equal(builderOutput, fragmentOutput);
    }

    [Fact]
    public void RendererMatchesBuilderForCountIgnoresOrdering()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonNode>(provider);

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);

        var ordered = source.OrderBy(person => person.Name);
        var countMethod = typeof(Queryable)
            .GetMethods()
            .Single(method => method.Name == nameof(Queryable.Count) && method.GetParameters().Length == 1)
            .MakeGenericMethod(typeof(PersonNode));

        var countExpression = Expression.Call(null, countMethod, ordered.Expression);

        visitor.Visit(countExpression);

        var fragments = context.FragmentSequence.ToList();

        Assert.NotEmpty(fragments);
        Assert.Contains(fragments, fragment => fragment is AggregationFragment);

        var builderOutput = context.GetQuery();
        var fragmentOutput = AgeFragmentRenderer.Render(fragments);

        Assert.DoesNotContain("ORDER BY", builderOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(builderOutput, fragmentOutput);
    }

    [Fact]
    public void RendererMatchesBuilderForSumAggregationWithSelector()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonNode>(provider);

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);

        var ordered = source.OrderBy(person => person.Age);
        Expression<Func<PersonNode, int>> selector = person => person.Age;
        var sumMethod = typeof(Queryable)
            .GetMethods()
            .Where(method => method.Name == nameof(Queryable.Sum))
            .Where(method =>
            {
                var parameters = method.GetParameters();
                if (parameters.Length != 2)
                {
                    return false;
                }

                var sourceParameter = parameters[0].ParameterType;
                var selectorParameter = parameters[1].ParameterType;

                if (!sourceParameter.IsGenericType || !selectorParameter.IsGenericType)
                {
                    return false;
                }

                if (sourceParameter.GetGenericTypeDefinition() != typeof(IQueryable<>) ||
                    selectorParameter.GetGenericTypeDefinition() != typeof(Expression<>))
                {
                    return false;
                }

                var selectorLambdaType = selectorParameter.GetGenericArguments()[0];
                if (!selectorLambdaType.IsGenericType || selectorLambdaType.GetGenericTypeDefinition() != typeof(Func<,>))
                {
                    return false;
                }

                var lambdaGenericArguments = selectorLambdaType.GetGenericArguments();
                return lambdaGenericArguments.Length == 2 && lambdaGenericArguments[1] == typeof(int);
            })
            .Single()
            .MakeGenericMethod(typeof(PersonNode));

        var sumExpression = Expression.Call(null, sumMethod, ordered.Expression, Expression.Quote(selector));

        visitor.Visit(sumExpression);

        var fragments = context.FragmentSequence.ToList();

        Assert.NotEmpty(fragments);
        Assert.Contains(fragments, fragment => fragment is AggregationFragment agg && agg.AggregationType.Equals("sum", StringComparison.OrdinalIgnoreCase));

        var builderOutput = context.GetQuery();
        var fragmentOutput = AgeFragmentRenderer.Render(fragments);

        Assert.DoesNotContain("ORDER BY", builderOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(builderOutput, fragmentOutput);
    }

    [Fact]
    public void RendererMatchesBuilderForGroupByWithMultipleAggregates()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonNode>(provider);

        var query = source
            .Where(p => p.Age > 18)
            .GroupBy(p => p.Name)
            .Select(group => new
            {
                Name = group.Key,
                Count = group.Count()
            });

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);

        var fragments = context.FragmentSequence.ToList();

        Assert.NotEmpty(fragments);
        Assert.Contains(fragments, fragment => fragment is GroupByFragment);
        Assert.Contains(fragments, fragment => fragment is ProjectionFragment);

        var builderOutput = context.GetQuery();
        var fragmentOutput = AgeFragmentRenderer.Render(fragments);

        Assert.Equal(builderOutput, fragmentOutput);
    }

    [Fact]
    public void RendererMatchesBuilderForSkipAndLimit()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonNode>(provider);

        var query = source
            .PathSegments<PersonNode, KnowsRelationship, PersonNode>()
            .Select(ps => ps.EndNode)
            .OrderBy(node => node.Name)
            .Skip(5)
            .Take(10);

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);

        var fragments = context.FragmentSequence.ToList();

        Assert.NotEmpty(fragments);
        Assert.NotNull(fragments.OfType<SkipFragment>().LastOrDefault());
        Assert.NotNull(fragments.OfType<LimitFragment>().LastOrDefault());

        var builderOutput = context.GetQuery();
        var fragmentOutput = AgeFragmentRenderer.Render(fragments);

        Assert.Equal(builderOutput, fragmentOutput);
    }

    [Fact]
    public void RendererMatchesBuilderForDistinctPagination()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonNode>(provider);

        var query = source
            .Select(person => person.Name)
            .Distinct()
            .Skip(3)
            .Take(5);

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);

        var fragments = context.FragmentSequence.ToList();

        Assert.NotEmpty(fragments);
        Assert.Contains(fragments, fragment => fragment is DistinctFragment);
        Assert.NotNull(fragments.OfType<SkipFragment>().LastOrDefault());
        Assert.NotNull(fragments.OfType<LimitFragment>().LastOrDefault());

        var builderOutput = context.GetQuery();
        var fragmentOutput = AgeFragmentRenderer.Render(fragments);

        Assert.Equal(builderOutput, fragmentOutput);
    }

    [Fact]
    public void RendererMatchesBuilderForOptionalComplexPropertyPagination()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonWithAddressNode>(provider);

        var query = source
            .Select(person => person.HomeAddress)
            .Distinct()
            .Skip(2)
            .Take(3);

        var context = new CypherQueryContext(typeof(PersonWithAddressNode), useFragmentRenderer: true);
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);

        var fragments = context.FragmentSequence.ToList();

        Assert.NotEmpty(fragments);
        Assert.Contains(fragments, fragment => fragment is OptionalMatchFragment);
        Assert.Contains(fragments, fragment => fragment is DistinctFragment);
        Assert.NotNull(fragments.OfType<SkipFragment>().LastOrDefault());
        Assert.NotNull(fragments.OfType<LimitFragment>().LastOrDefault());

        var builderOutput = context.GetQuery();
        var fragmentOutput = AgeFragmentRenderer.Render(fragments);

            Assert.Contains("ORDER BY", builderOutput);
        Assert.Equal(builderOutput, fragmentOutput);
    }

    [Fact]
    public void RendererMatchesBuilderForComplexPropertyHydration()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonWithAddressNode>(provider);

        var toListMethod = typeof(Enumerable)
            .GetMethods()
            .Single(method => method.Name == nameof(Enumerable.ToList) && method.GetParameters().Length == 1)
            .MakeGenericMethod(typeof(PersonWithAddressNode));

        var toListExpression = Expression.Call(null, toListMethod, source.Expression);

        var context = new CypherQueryContext(typeof(PersonWithAddressNode), useFragmentRenderer: true);
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(toListExpression);

        var fragments = context.FragmentSequence.ToList();

        Assert.NotEmpty(fragments);
        Assert.Contains(fragments, fragment => fragment is OptionalMatchFragment);
        Assert.Contains(fragments, fragment => fragment is ComplexPropertyLoadingFragment toggle && toggle.IsEnabled);

        var builderOutput = context.GetQuery();
        var fragmentOutput = AgeFragmentRenderer.Render(fragments);

        Assert.Contains("OPTIONAL MATCH", builderOutput);
        Assert.Contains("RETURN {", builderOutput);
        Assert.Equal(builderOutput, fragmentOutput);
    }

    [Fact(DisplayName = "RendererMatchesBuilderForComplexPropertyOrderingAndPagination")]
    public void RendererMatchesBuilderForComplexPropertyOrderingAndPagination()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonWithAddressNode>(provider);

        var query = source
            .OrderBy(person => person.Name)
            .Skip(1)
            .Take(2);

        var toListMethod = typeof(Enumerable)
            .GetMethods()
            .Single(method => method.Name == nameof(Enumerable.ToList) && method.GetParameters().Length == 1)
            .MakeGenericMethod(typeof(PersonWithAddressNode));

        var toListExpression = Expression.Call(null, toListMethod, query.Expression);

        var context = new CypherQueryContext(typeof(PersonWithAddressNode), useFragmentRenderer: true);
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(toListExpression);

        var fragments = context.FragmentSequence.ToList();

        Assert.NotEmpty(fragments);
        Assert.Contains(fragments, fragment => fragment is ComplexPropertyLoadingFragment toggle && toggle.IsEnabled);
        Assert.Contains(fragments, fragment => fragment is SkipFragment);
        Assert.Contains(fragments, fragment => fragment is LimitFragment);

        var builderOutput = context.GetQuery();
        var fragmentOutput = AgeFragmentRenderer.Render(fragments);

        Assert.Contains("ORDER BY", builderOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SKIP", builderOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LIMIT", builderOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(builderOutput, fragmentOutput);
    }

    [Fact]
    public void RendererMatchesBuilderForComplexPropertyDistinct()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonWithAddressNode>(provider);

        var query = source.Distinct();

        var toListMethod = typeof(Enumerable)
            .GetMethods()
            .Single(method => method.Name == nameof(Enumerable.ToList) && method.GetParameters().Length == 1)
            .MakeGenericMethod(typeof(PersonWithAddressNode));

        var toListExpression = Expression.Call(null, toListMethod, query.Expression);

        var context = new CypherQueryContext(typeof(PersonWithAddressNode), useFragmentRenderer: true);
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(toListExpression);

        var fragments = context.FragmentSequence.ToList();

        Assert.NotEmpty(fragments);
        Assert.Contains(fragments, fragment => fragment is DistinctFragment);
        Assert.Contains(fragments, fragment => fragment is ComplexPropertyLoadingFragment toggle && toggle.IsEnabled);

        var builderOutput = context.GetQuery();
        var fragmentOutput = AgeFragmentRenderer.Render(fragments);

        Assert.Equal(builderOutput, fragmentOutput);
    }

    [Fact]
    public void RendererMatchesBuilderForLastWithOrdering()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonNode>(provider);

        var ordered = source.OrderBy(person => person.Name);
        var lastMethod = typeof(Queryable)
            .GetMethods()
            .Single(method => method.Name == nameof(Queryable.Last) && method.GetParameters().Length == 1)
            .MakeGenericMethod(typeof(PersonNode));

        var lastExpression = Expression.Call(null, lastMethod, ordered.Expression);

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(lastExpression);

        var fragments = context.FragmentSequence.ToList();

        Assert.Contains(fragments, fragment => fragment is ReverseOrderFragment);
        Assert.NotNull(fragments.OfType<LimitFragment>().LastOrDefault());

        var builderOutput = context.GetQuery();
        var fragmentOutput = AgeFragmentRenderer.Render(fragments);

        Assert.Equal(builderOutput, fragmentOutput);
        Assert.Contains("ORDER BY", fragmentOutput);
        Assert.Contains("DESC", fragmentOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LIMIT 1", fragmentOutput, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RendererMatchesBuilderForLastWithDistinctSkip()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonNode>(provider);

        var sequence = source
            .Select(person => person.Name)
            .Distinct()
            .Skip(1);

        var lastMethod = typeof(Queryable)
            .GetMethods()
            .Single(method => method.Name == nameof(Queryable.Last) && method.GetParameters().Length == 1)
            .MakeGenericMethod(typeof(string));

        var lastExpression = Expression.Call(null, lastMethod, sequence.Expression);

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(lastExpression);

        var fragments = context.FragmentSequence.ToList();

        Assert.Contains(fragments, fragment => fragment is ReverseOrderFragment);
        Assert.Contains(fragments, fragment => fragment is DistinctFragment);
        Assert.NotNull(fragments.OfType<SkipFragment>().LastOrDefault());
        Assert.NotNull(fragments.OfType<LimitFragment>().LastOrDefault());

        var builderOutput = context.GetQuery();
        var fragmentOutput = AgeFragmentRenderer.Render(fragments);

        Assert.Equal(builderOutput, fragmentOutput);
        Assert.Contains("ORDER BY", fragmentOutput);
        Assert.Contains("DESC", fragmentOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LIMIT 1", fragmentOutput, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RendererMatchesBuilderForCalculatedFieldProjection()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonNode>(provider);

        var query = source
            .Select(p => new { 
                FullName = p.Name + " (Age: " + p.Age + ")",
                AgePlusTen = p.Age + 10,
                IsAdult = p.Age >= 18
            });

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);

        var fragments = context.FragmentSequence.ToList();

        Assert.NotEmpty(fragments);
        Assert.Contains(fragments, fragment => fragment is ProjectionFragment);
        Assert.Contains(fragments, fragment => fragment is ComplexPropertyLoadingFragment toggle && !toggle.IsEnabled);

        var builderOutput = context.GetQuery();
        var fragmentOutput = AgeFragmentRenderer.Render(fragments);

        Assert.Equal(builderOutput, fragmentOutput);
    }

    [Fact]
    public void RendererMatchesBuilderForNestedObjectProjection()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonNode>(provider);

        // Test projecting path segments with nested structure
        var query = source
            .PathSegments<PersonNode, KnowsRelationship, PersonNode>()
            .Select(ps => new { 
                Start = ps.StartNode, 
                End = ps.EndNode,
                RelType = ps.Relationship.Type
            });

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);

        var fragments = context.FragmentSequence.ToList();

        Assert.NotEmpty(fragments);
        Assert.Contains(fragments, fragment => fragment is ProjectionFragment);

        var builderOutput = context.GetQuery();
        var fragmentOutput = AgeFragmentRenderer.Render(fragments);

        Assert.Equal(builderOutput, fragmentOutput);
    }

    [Fact]
    public void FragmentRendererCanBeUsedAsQuerySource()
    {
        // This test validates that we can use the fragment renderer as the primary query generation path
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonNode>(provider);

        var query = source
            .Where(p => p.Age > 25)
            .Select(p => new { p.Name, p.Age })
            .OrderBy(p => p.Name)
            .Skip(10)
            .Take(20);

        // Use fragment renderer as the query source
        var context = new CypherQueryContext(typeof(PersonNode), useFragmentRenderer: true);
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);

        var fragmentQuery = context.GetQuery(); // Should use fragment renderer
        
        Assert.NotEmpty(fragmentQuery);
        Assert.Contains("MATCH", fragmentQuery);
        Assert.Contains("WHERE", fragmentQuery);
        Assert.Contains("RETURN", fragmentQuery);
        Assert.Contains("ORDER BY", fragmentQuery);
        Assert.Contains("SKIP 10", fragmentQuery);
        Assert.Contains("LIMIT 20", fragmentQuery);
        
        // Now compare with builder output
        var builderContext = new CypherQueryContext(typeof(PersonNode), useFragmentRenderer: false);
        var builderVisitor = new AgeCypherQueryVisitor(builderContext);
        builderVisitor.Visit(query.Expression);
        var builderQuery = builderContext.GetQuery();
        
        // They should produce identical queries
        Assert.Equal(builderQuery, fragmentQuery);
    }

    public sealed record PersonNode : Node
    {
        public override IReadOnlyList<string> Labels { get; set; } = new List<string> { nameof(PersonNode) };
        public int Age { get; init; }
        public string Name { get; init; } = string.Empty;
    }

    public sealed record PersonWithAddressNode : Node
    {
        public override IReadOnlyList<string> Labels { get; set; } = new List<string> { nameof(PersonWithAddressNode) };
        public string Name { get; init; } = string.Empty;
#pragma warning disable GM003 // Allow complex node property for optional match parity testing
        public AddressNode HomeAddress { get; init; } = new AddressNode();
#pragma warning restore GM003
    }

    public sealed record AddressNode : Node
    {
        public override IReadOnlyList<string> Labels { get; set; } = new List<string> { nameof(AddressNode) };
        public string Street { get; init; } = string.Empty;
        public string City { get; init; } = string.Empty;
    }

    public sealed record KnowsRelationship(string StartNodeId, string EndNodeId, RelationshipDirection Direction = RelationshipDirection.Outgoing) : Relationship(StartNodeId, EndNodeId, Direction)
    {
        public override string Type { get; set; } = nameof(KnowsRelationship);
    }

    internal sealed class TestGraphQueryProvider : IGraphQueryProvider
    {
        public IGraph Graph { get; } = new TestGraph();

        public IQueryable CreateQuery(Expression expression)
        {
            var elementType = expression.Type.GetGenericArguments().FirstOrDefault() ?? expression.Type;
            var method = typeof(TestGraphQueryProvider).GetMethod(nameof(CreateUntypedQuery), BindingFlags.Instance | BindingFlags.NonPublic);
            return (IQueryable)(method?.MakeGenericMethod(elementType).Invoke(this, new object[] { expression }) ?? throw new InvalidOperationException("Unable to create query"));
        }

        public IGraphQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            if (typeof(INode).IsAssignableFrom(typeof(TElement)))
            {
                var nodeQueryableType = typeof(TestGraphNodeQueryable<>).MakeGenericType(typeof(TElement));
                return (IGraphQueryable<TElement>)Activator.CreateInstance(nodeQueryableType, this, expression)!;
            }

            if (typeof(IRelationship).IsAssignableFrom(typeof(TElement)))
            {
                var relationshipQueryableType = typeof(TestGraphRelationshipQueryable<>).MakeGenericType(typeof(TElement));
                return (IGraphQueryable<TElement>)Activator.CreateInstance(relationshipQueryableType, this, expression)!;
            }

            var queryableType = typeof(TestGraphQueryable<>).MakeGenericType(typeof(TElement));
            return (IGraphQueryable<TElement>)Activator.CreateInstance(queryableType, this, expression)!;
        }

        IQueryable<TElement> IQueryProvider.CreateQuery<TElement>(Expression expression)
            => (IQueryable<TElement>)CreateQuery<TElement>(expression);

        private IQueryable<TElement> CreateUntypedQuery<TElement>(Expression expression)
        {
            return (IQueryable<TElement>)CreateQuery<TElement>(expression);
        }

        public object? Execute(Expression expression) => throw new NotSupportedException("Execute is not supported in test provider");

        public TResult Execute<TResult>(Expression expression) => throw new NotSupportedException("Execute is not supported in test provider");

        public Task<TResult> ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default) => throw new NotSupportedException("ExecuteAsync is not supported in test provider");

        public Task<object?> ExecuteAsync(Expression expression, CancellationToken cancellationToken = default) => throw new NotSupportedException("ExecuteAsync is not supported in test provider");
    }

    internal sealed class TestGraph : IGraph
    {
        public SchemaRegistry SchemaRegistry { get; } = new();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public IGraphNodeQueryable<DynamicNode> DynamicNodes(IGraphTransaction? transaction = null) => throw new NotSupportedException();

        public IGraphRelationshipQueryable<DynamicRelationship> DynamicRelationships(IGraphTransaction? transaction = null) => throw new NotSupportedException();

        public Task<DynamicNode> GetDynamicNodeAsync(string id, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<DynamicRelationship> GetDynamicRelationshipAsync(string id, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public IGraphNodeQueryable<N> Nodes<N>(IGraphTransaction? transaction = null)
            where N : INode => throw new NotSupportedException();

        public IGraphRelationshipQueryable<R> Relationships<R>(IGraphTransaction? transaction = null)
            where R : IRelationship => throw new NotSupportedException();

        public Task<N> GetNodeAsync<N>(string id, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) where N : INode => throw new NotSupportedException();

        public Task<R> GetRelationshipAsync<R>(string id, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) where R : IRelationship => throw new NotSupportedException();

        public Task CreateNodeAsync<N>(N node, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) where N : INode => throw new NotSupportedException();

        public Task CreateRelationshipAsync<R>(R relationship, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) where R : IRelationship => throw new NotSupportedException();

        public Task UpdateNodeAsync<N>(N node, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) where N : INode => throw new NotSupportedException();

        public Task UpdateRelationshipAsync<R>(R relationship, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) where R : IRelationship => throw new NotSupportedException();

        public Task DeleteNodeAsync(string id, bool cascadeDelete = false, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task DeleteRelationshipAsync(string id, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public IGraphQueryable<IEntity> Search(string query, IGraphTransaction? transaction = null) => throw new NotSupportedException();

        public IGraphNodeQueryable<INode> SearchNodes(string query, IGraphTransaction? transaction = null) => throw new NotSupportedException();

        public IGraphRelationshipQueryable<IRelationship> SearchRelationships(string query, IGraphTransaction? transaction = null) => throw new NotSupportedException();

        public IGraphNodeQueryable<T> SearchNodes<T>(string query, IGraphTransaction? transaction = null) where T : INode => throw new NotSupportedException();

        public IGraphRelationshipQueryable<T> SearchRelationships<T>(string query, IGraphTransaction? transaction = null) where T : IRelationship => throw new NotSupportedException();

        public Task RecreateIndexesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IGraphTransaction> GetTransactionAsync() => throw new NotSupportedException();
    }

    internal class TestGraphQueryable<TElement> : IOrderedGraphQueryable<TElement>
    {
        private readonly TestGraphQueryProvider provider;

        public TestGraphQueryable(TestGraphQueryProvider provider)
        {
            this.provider = provider;
            Provider = provider;
            Expression = Expression.Constant(this);
        }

        public TestGraphQueryable(TestGraphQueryProvider provider, Expression expression)
        {
            this.provider = provider;
            Provider = provider;
            Expression = expression;
        }

        public Type ElementType => typeof(TElement);

        public Expression Expression { get; }

        public IGraph Graph => provider.Graph;

        public IGraphQueryProvider Provider { get; }

        IQueryProvider IQueryable.Provider => Provider;

        public IEnumerator<TElement> GetEnumerator() => throw new NotSupportedException();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    internal sealed class TestGraphNodeQueryable<TElement> : TestGraphQueryable<TElement>, IGraphNodeQueryable<TElement>
        where TElement : INode
    {
        public TestGraphNodeQueryable(TestGraphQueryProvider provider) : base(provider)
        {
        }

        public TestGraphNodeQueryable(TestGraphQueryProvider provider, Expression expression) : base(provider, expression)
        {
        }
    }

    internal sealed class TestGraphRelationshipQueryable<TElement> : TestGraphQueryable<TElement>, IGraphRelationshipQueryable<TElement>
        where TElement : IRelationship
    {
        public TestGraphRelationshipQueryable(TestGraphQueryProvider provider) : base(provider)
        {
        }

        public TestGraphRelationshipQueryable(TestGraphQueryProvider provider, Expression expression) : base(provider, expression)
        {
        }
    }
}
