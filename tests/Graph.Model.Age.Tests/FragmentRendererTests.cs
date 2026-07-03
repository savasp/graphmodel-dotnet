// Copyright 2025 Savas Parastatidis

namespace Cvoya.Graph.Model.Age.Tests;

using System;
using System.Collections;
using System.Collections.Generic;
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
        // It should only have the final projection: RETURN tgt0 AS c_StartNode, tgt1 AS c_EndNode
        // (tgt0 because that's the StartNode of the chained hop 1; c_ prefix avoids Cypher keyword conflicts)
        Assert.Contains("tgt0 AS c_StartNode", cypher);
        Assert.Contains("tgt1 AS c_EndNode", cypher);
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
        Assert.True(fragments.Count(fragment => fragment is MatchRootFragment or MatchSegmentFragment) >= 2);
        Assert.NotEmpty(fragments.OfType<ProjectionFragment>());

        var builderOutput = context.GetQuery();
        var fragmentOutput = AgeFragmentRenderer.Render(fragments);

        Assert.Equal(builderOutput, fragmentOutput);
    }

    [Fact]
    public void RendererMatchesBuilderForJoinReturningNodeProjection()
    {
        var provider = new TestGraphQueryProvider();
        var relationships = new TestGraphRelationshipQueryable<KnowsRelationship>(provider);
        var people = new TestGraphNodeQueryable<PersonNode>(provider);

        var query = relationships.Join(
            people,
            relationship => relationship.EndNodeId,
            person => person.Id,
            (relationship, person) => person);

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);

        var fragments = context.FragmentSequence.ToList();

        Assert.NotEmpty(fragments);
        var projection = Assert.IsType<ProjectionFragment>(fragments.Last());
        Assert.Single(projection.Returns);
        Assert.Equal(projection.CurrentAlias, projection.Returns.Single());

        var builderOutput = context.GetQuery();
        var fragmentOutput = AgeFragmentRenderer.Render(fragments);

        Assert.Contains($"RETURN {projection.CurrentAlias}", builderOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(builderOutput, fragmentOutput);
    }

    [Fact]
    public void RendererMatchesBuilderForJoinReturningRelationshipProjection()
    {
        var provider = new TestGraphQueryProvider();
        var relationships = new TestGraphRelationshipQueryable<KnowsRelationship>(provider);
        var people = new TestGraphNodeQueryable<PersonNode>(provider);

        var query = relationships.Join(
            people,
            relationship => relationship.EndNodeId,
            person => person.Id,
            (relationship, _) => relationship);

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);

        var fragments = context.FragmentSequence.ToList();

        Assert.NotEmpty(fragments);
        var projection = Assert.IsType<ProjectionFragment>(fragments.Last());
        Assert.Single(projection.Returns);
        Assert.Equal(projection.CurrentAlias, projection.Returns.Single());

        var builderOutput = context.GetQuery();
        var fragmentOutput = AgeFragmentRenderer.Render(fragments);

        Assert.Contains($"RETURN {projection.CurrentAlias}", builderOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(builderOutput, fragmentOutput);
    }

    [Fact]
    public void RendererMatchesBuilderForJoinReturningComplexAnonymousType()
    {
        var provider = new TestGraphQueryProvider();
        var relationships = new TestGraphRelationshipQueryable<KnowsRelationship>(provider);
        var people = new TestGraphNodeQueryable<PersonNode>(provider);

        // Join with complex anonymous type projection: (r, p) => new { Relationship = r, Person = p }
        var query = relationships.Join(
            people,
            relationship => relationship.EndNodeId,
            person => person.Id,
            (relationship, person) => new { Relationship = relationship, Person = person });

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);

        var cypher = context.GetQuery();
        Console.WriteLine($"Join complex anonymous type Cypher:\n{cypher}");

        // The MATCH clause combines patterns with commas: (src0)-[r0]->(tgt0), (tgt1:PersonNode)
        Assert.Contains("MATCH (src0)-[r0:KnowsRelationship]->(tgt0)", cypher);
        Assert.Contains("(tgt1:PersonNode)", cypher);

        // Should have WHERE join condition — note "Id" maps to "user_id" via ExpressionTranslationHelper.MapPropertyName
        Assert.Contains("WHERE r0.EndNodeId = tgt1.user_id", cypher);

        // Should have RETURN with two c_ aliased columns
        Assert.Contains("RETURN r0 AS c_Relationship, tgt1 AS c_Person", cypher);
    }

    [Fact]
    public void RendererMatchesBuilderForJoinReturningComplexAnonymousTypeWithScalars()
    {
        var provider = new TestGraphQueryProvider();
        var relationships = new TestGraphRelationshipQueryable<KnowsRelationship>(provider);
        var people = new TestGraphNodeQueryable<PersonNode>(provider);

        // Join with complex anonymous type projection using scalar property selectors
        var query = relationships.Join(
            people,
            relationship => relationship.EndNodeId,
            person => person.Id,
            (relationship, person) => new { Id = relationship.EndNodeId, Name = person.Name });

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);

        var cypher = context.GetQuery();
        Console.WriteLine($"Join complex scalar projection Cypher:\n{cypher}");

        // MATCH combines patterns with commas
        Assert.Contains("MATCH (src0)-[r0:KnowsRelationship]->(tgt0)", cypher);
        Assert.Contains("(tgt1:PersonNode)", cypher);

        // Should have WHERE join condition — note "Id" maps to "user_id"
        Assert.Contains("WHERE r0.EndNodeId = tgt1.user_id", cypher);

        // Should have RETURN with scalar property projections
        Assert.Contains("RETURN r0.EndNodeId AS c_Id, tgt1.Name AS c_Name", cypher);
    }

    [Fact]
    public void Join_WithMixedParameterAndMemberResultSelector_GeneratesCorrectCypher()
    {
        var provider = new TestGraphQueryProvider();
        var relationships = new TestGraphRelationshipQueryable<KnowsRelationship>(provider);
        var people = new TestGraphNodeQueryable<PersonNode>(provider);

        // Mixed: one parameter reference and one member access
        var query = relationships.Join(
            people,
            relationship => relationship.EndNodeId,
            person => person.Id,
            (relationship, person) => new { relationship, person.Name });

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);

        var cypher = context.GetQuery();
        Console.WriteLine($"Join mixed selector Cypher:\n{cypher}");

        // MATCH combines patterns with commas
        Assert.Contains("MATCH (src0)-[r0:KnowsRelationship]->(tgt0)", cypher);
        Assert.Contains("(tgt1:PersonNode)", cypher);

        // Should have WHERE join condition — note "Id" maps to "user_id"
        Assert.Contains("WHERE r0.EndNodeId = tgt1.user_id", cypher);

        // Should have RETURN with the parameter expanded to alias and the member access
        Assert.Contains("r0 AS c_relationship", cypher);
        Assert.Contains("tgt1.Name AS c_Name", cypher);
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

    /// <summary>
    /// Tests that ToListAsync execution path (ExecuteAsync&lt;List&lt;T&gt;&gt;) generates correct query for path segments.
    /// This mimics the behavior of QueryableAsyncExtensions.ToListAsync which calls ExecuteAsync with List&lt;T&gt; directly.
    /// </summary>
    [Fact]
    public void PathSegments_WithToListAsyncExecution_GeneratesCorrectProjection()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonNode>(provider);

        // Simulate what ToListAsync does: it calls ExecuteAsync<List<IGraphPathSegment<...>>>
        var query = source
            .PathSegments<PersonNode, KnowsRelationship, PersonNode>();

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);

        // ToListAsync creates an expression that requests List<IGraphPathSegment<...>>
        var toListMethod = typeof(Enumerable)
            .GetMethods()
            .Single(method => method.Name == nameof(Enumerable.ToList) && method.GetParameters().Length == 1)
            .MakeGenericMethod(typeof(IGraphPathSegment<PersonNode, KnowsRelationship, PersonNode>));

        var toListExpression = Expression.Call(null, toListMethod, query.Expression);
        visitor.Visit(toListExpression);

        var cypher = context.GetQuery();
        Console.WriteLine($"ToListAsync path query:\n{cypher}");

        // Should return all three components for path segments: src, r, tgt
        Assert.Contains("RETURN src0, r0, tgt0", cypher);
        Assert.Contains("MATCH (src0:PersonNode)-[r0:KnowsRelationship]->(tgt0:PersonNode)", cypher);
    }

    /// <summary>
    /// Tests that await foreach execution path (ExecuteAsync&lt;IEnumerable&lt;T&gt;&gt;) generates correct query for path segments.
    /// This mimics the behavior of GetAsyncEnumerator which calls ExecuteAsync with IEnumerable&lt;T&gt;.
    /// </summary>
    [Fact]
    public void PathSegments_WithAsyncEnumeratorExecution_GeneratesCorrectProjection()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonNode>(provider);

        // Simulate what GetAsyncEnumerator does: it calls ExecuteAsync<IEnumerable<IGraphPathSegment<...>>>
        var query = source
            .PathSegments<PersonNode, KnowsRelationship, PersonNode>();

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);

        // GetAsyncEnumerator uses IEnumerable<T> type hint
        // We visit the expression as-is since PathSegments returns IGraphQueryable<IGraphPathSegment<...>>
        // which is an IEnumerable<IGraphPathSegment<...>>
        visitor.Visit(query.Expression);

        // Finalize the query to add missing default projections
        visitor.FinalizeQuery(typeof(IGraphPathSegment<PersonNode, KnowsRelationship, PersonNode>));

        var cypher = context.GetQuery();
        Console.WriteLine($"await foreach path query:\n{cypher}");

        // Should return all three components for path segments: src, r, tgt
        // This is the KEY test: both execution paths should generate the same projection
        Assert.Contains("RETURN src0, r0, tgt0", cypher);
        Assert.Contains("MATCH (src0:PersonNode)-[r0:KnowsRelationship]->(tgt0:PersonNode)", cypher);
    }

    /// <summary>
    /// Tests that both execution paths (ToListAsync and await foreach) generate identical queries for path segments.
    /// This is a regression test to ensure consistency between the two async enumeration strategies.
    /// </summary>
    [Fact]
    public void PathSegments_BothExecutionPaths_GenerateIdenticalQueries()
    {
        var provider = new TestGraphQueryProvider();

        // Path 1: ToListAsync (ExecuteAsync<List<T>>)
        var source1 = new TestGraphNodeQueryable<PersonNode>(provider);
        var query1 = source1.PathSegments<PersonNode, KnowsRelationship, PersonNode>();
        var context1 = new CypherQueryContext(typeof(PersonNode));
        var visitor1 = new AgeCypherQueryVisitor(context1);
        var toListMethod = typeof(Enumerable)
            .GetMethods()
            .Single(method => method.Name == nameof(Enumerable.ToList) && method.GetParameters().Length == 1)
            .MakeGenericMethod(typeof(IGraphPathSegment<PersonNode, KnowsRelationship, PersonNode>));
        var toListExpression = Expression.Call(null, toListMethod, query1.Expression);
        visitor1.Visit(toListExpression);
        var cypherFromToList = context1.GetQuery();

        // Path 2: await foreach (ExecuteAsync<IEnumerable<T>>)
        var source2 = new TestGraphNodeQueryable<PersonNode>(provider);
        var query2 = source2.PathSegments<PersonNode, KnowsRelationship, PersonNode>();
        var context2 = new CypherQueryContext(typeof(PersonNode));
        var visitor2 = new AgeCypherQueryVisitor(context2);
        visitor2.Visit(query2.Expression);
        // Finalize to add default projection (simulating what BuildCypherQuery does)
        visitor2.FinalizeQuery(typeof(IGraphPathSegment<PersonNode, KnowsRelationship, PersonNode>));
        var cypherFromAwaitForeach = context2.GetQuery();

        // Both should generate identical Cypher queries
        Console.WriteLine($"ToListAsync:     {cypherFromToList}");
        Console.WriteLine($"await foreach:   {cypherFromAwaitForeach}");

        Assert.Equal(cypherFromToList, cypherFromAwaitForeach);
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

        // The predicate push-down optimization inlines Name == "Alice" into the root node pattern
        Assert.Contains(
            "MATCH (src0:PersonNode {Name: $param_0})-[r0:KnowsRelationship]->(tgt0:PersonNode)-[r1:KnowsRelationship]->(tgt1:PersonNode)",
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

        var context = new CypherQueryContext(typeof(PersonWithAddressNode));
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

        var context = new CypherQueryContext(typeof(PersonWithAddressNode));
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

        var context = new CypherQueryContext(typeof(PersonWithAddressNode));
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

        var context = new CypherQueryContext(typeof(PersonWithAddressNode));
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
            .Select(p => new
            {
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
            .Select(ps => new
            {
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
        var context = new CypherQueryContext(typeof(PersonNode));
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

        // Run translation again to ensure determinism
        var secondContext = new CypherQueryContext(typeof(PersonNode));
        var secondVisitor = new AgeCypherQueryVisitor(secondContext);
        secondVisitor.Visit(query.Expression);
        var secondQuery = secondContext.GetQuery();

        Assert.Equal(secondQuery, fragmentQuery);
    }

    // -----------------------------------------------------------------------
    //  Nested GroupBy / Pattern Comprehension Tests (§8.2.2)
    // -----------------------------------------------------------------------

    [Fact]
    public void SimpleGroupByWithSinglePropertyCollect_EmitsWithClause()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonNode>(provider);

        // LINQ: source.GroupBy(p => p.Name).Select(g => new { Name = g.Key, Friends = g.Select(p => p.FirstName).ToList() })
        var query = source
            .GroupBy(p => p.Name)
            .Select(g => new { Name = g.Key, Friends = g.Select(p => p.Name).ToList() });

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);

        var fragments = context.FragmentSequence.ToList();
        var cypher = context.GetQuery();

        Console.WriteLine($"Generated Cypher:\n{cypher}");

        // Assert fragment types
        Assert.Contains(fragments, fragment => fragment is GroupByFragment);
        Assert.Contains(fragments, fragment => fragment is CollectFragment);
        Assert.Contains(fragments, fragment => fragment is ProjectionFragment);

        // Assert GroupByFragment content
        var groupByFragment = fragments.OfType<GroupByFragment>().Last();
        Assert.Contains("Name", groupByFragment.Expression);

        // Assert CollectFragment content
        var collectFragment = fragments.OfType<CollectFragment>().Last();
        Assert.Equal("c_Friends", collectFragment.ProjectionColumn);
        Assert.Equal("src0.Name", collectFragment.CollectExpression);
        Assert.NotNull(collectFragment.GroupByExpression);

        // Assert Cypher structure: MATCH ... WITH ... RETURN ...
        Assert.Contains("MATCH", cypher);
        Assert.Contains("WITH src0.Name AS c_Name", cypher);
        Assert.Contains("collect(src0.Name) AS c_Friends", cypher);
        Assert.Contains("RETURN c_Name, c_Friends", cypher);
    }

    [Fact]
    public void GroupByWithMultiPropertyCollect_EmitsMapSyntax()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonNode>(provider);

        // LINQ: source.GroupBy(p => p.Name).Select(g => new { Name = g.Key, Details = g.Select(p => new { p.Name, p.Age }).ToList() })
        var query = source
            .GroupBy(p => p.Name)
            .Select(g => new { Name = g.Key, Details = g.Select(p => new { p.Name, p.Age }).ToList() });

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);

        var fragments = context.FragmentSequence.ToList();
        var cypher = context.GetQuery();

        Console.WriteLine($"Generated Cypher:\n{cypher}");

        // Assert collect contains map syntax: {Name: src0.Name, Age: src0.Age}
        var collectFragment = fragments.OfType<CollectFragment>().Last();
        Assert.Contains("Name: src0.Name", collectFragment.CollectExpression);
        Assert.Contains("Age: src0.Age", collectFragment.CollectExpression);

        // Assert WITH includes the map collect
        Assert.Contains("WITH src0.Name AS c_Name", cypher);
        Assert.Contains("collect({", cypher);
        Assert.Contains("Name: src0.Name", cypher);
        Assert.Contains("Age: src0.Age", cypher);
    }

    [Fact]
    public void GroupByWithWhereClauseAndCollect_EmitsCorrectClauseOrder()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonNode>(provider);

        // LINQ: source.Where(p => p.Age > 25).GroupBy(p => p.Name).Select(g => new { Name = g.Key, Friends = g.Select(p => p.Name).ToList() })
        var query = source
            .Where(p => p.Age > 25)
            .GroupBy(p => p.Name)
            .Select(g => new { Name = g.Key, Friends = g.Select(p => p.Name).ToList() });

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);

        var cypher = context.GetQuery();
        Console.WriteLine($"Generated Cypher:\n{cypher}");

        // Clause order: MATCH ... WHERE ... WITH ... RETURN ...
        var matchIndex = cypher.IndexOf("MATCH");
        var whereIndex = cypher.IndexOf("WHERE");
        var withIndex = cypher.IndexOf("WITH");
        var returnIndex = cypher.IndexOf("RETURN");

        Assert.True(whereIndex > matchIndex, "WHERE should appear after MATCH");
        Assert.True(withIndex > whereIndex, "WITH should appear after WHERE");
        Assert.True(returnIndex > withIndex, "RETURN should appear after WITH");

        // Verify WHERE condition
        Assert.Contains("src0.Age >", cypher);
    }

    [Fact]
    public void GroupByWithPathSegmentsCollect_ResolvesCorrectHopAliases()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonNode>(provider);

        // LINQ: source.PathSegments<...>().GroupBy(ps => ps.StartNode.Name).Select(g => new { Name = g.Key, Friends = g.Select(ps => ps.EndNode.Name).ToList() })
        var query = source
            .PathSegments<PersonNode, KnowsRelationship, PersonNode>()
            .GroupBy(ps => ps.StartNode.Name)
            .Select(g => new { Name = g.Key, Friends = g.Select(ps => ps.EndNode.Name).ToList() });

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);

        var cypher = context.GetQuery();
        Console.WriteLine($"Generated Cypher:\n{cypher}");

        // In a PathSegments context, ps.EndNode resolves to tgt0
        // The collect expression should reference tgt0.Name
        Assert.Contains("collect(tgt0.Name) AS c_Friends", cypher);

        // Group key should be src0.Name
        Assert.Contains("WITH src0.Name AS c_Name", cypher);

        // MATCH should include the path segment pattern
        Assert.Contains("src0:PersonNode", cypher);
        Assert.Contains("KnowsRelationship", cypher);
    }

    [Fact]
    public void GroupByWithAggregateAndCollect_IncludesBothInWith()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonNode>(provider);

        // LINQ: source.GroupBy(p => p.Name).Select(g => new { Name = g.Key, Count = g.Count(), Friends = g.Select(p => p.Name).ToList() })
        var query = source
            .GroupBy(p => p.Name)
            .Select(g => new { Name = g.Key, Count = g.Count(), Friends = g.Select(p => p.Name).ToList() });

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);

        var cypher = context.GetQuery();
        Console.WriteLine($"Generated Cypher:\n{cypher}");

        // WITH clause should include group key, count aggregate, and collect
        Assert.Contains("WITH", cypher);
        Assert.Contains("c_Name", cypher);
        Assert.Contains("c_Friends", cypher);
        Assert.Contains("c_Count", cypher);

        // RETURN should pass through the WITH aliases
        Assert.Contains("RETURN", cypher);
    }

    [Fact]
    public void NestedGroupByInsideCollect_ReturnsFalseGracefully()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonNode>(provider);

        // LINQ with second-level GroupBy (not supported): should fall back gracefully
        var query = source
            .GroupBy(p => p.Name)
            .Select(g => new { Name = g.Key, Groups = g.GroupBy(p => p.Age).Select(g2 => g2.Key).ToList() });

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);

        var fragments = context.FragmentSequence.ToList();
        var cypher = context.GetQuery();
        Console.WriteLine($"Generated Cypher:\n{cypher}");

        // Should NOT emit a CollectFragment for the nested-GroupBy property
        // The Groups property should fall through to expression visitor which
        // produces either a fallback or raw expression (no crash)
        Assert.Contains("c_Name", cypher);

        // The system should not crash — graceful degradation
        Assert.NotEmpty(cypher);
    }

    [Fact]
    public void SimpleProjectionWithoutGroupBy_NoCollectFragment()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonNode>(provider);

        // Standard projection without GroupBy — no collect involved
        var query = source
            .Select(p => new { p.Name, p.Age });

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);

        var fragments = context.FragmentSequence.ToList();
        var cypher = context.GetQuery();
        Console.WriteLine($"Generated Cypher:\n{cypher}");

        // No collect or group-by fragments
        Assert.DoesNotContain(fragments, fragment => fragment is CollectFragment);
        Assert.DoesNotContain(fragments, fragment => fragment is GroupByFragment);

        // Simple RETURN clause unchanged
        Assert.Contains("RETURN src0.Name AS c_Name, src0.Age AS c_Age", cypher);
        Assert.DoesNotContain("WITH", cypher);
    }

    [Fact]
    public void GroupByWithOnlyKeyProjection_NoCollectFragment()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonNode>(provider);

        // GroupBy with only key projection (no .Select().ToList())
        var query = source
            .GroupBy(p => p.Name)
            .Select(g => g.Key);

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);

        var fragments = context.FragmentSequence.ToList();
        var cypher = context.GetQuery();
        Console.WriteLine($"Generated Cypher:\n{cypher}");

        // Has GroupByFragment but no CollectFragment
        Assert.Contains(fragments, fragment => fragment is GroupByFragment);
        Assert.DoesNotContain(fragments, fragment => fragment is CollectFragment);
    }

    [Fact]
    public void RendererMatchesBuilderForGroupByWithCollect()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonNode>(provider);

        var query = source
            .GroupBy(p => p.Name)
            .Select(g => new { Name = g.Key, Friends = g.Select(p => p.Name).ToList() });

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);

        var fragments = context.FragmentSequence.ToList();
        var fragmentOutput = AgeFragmentRenderer.Render(fragments);

        Assert.NotEmpty(fragments);
        Assert.Contains("WITH", fragmentOutput);
        Assert.Contains("collect(src0.Name) AS c_Friends", fragmentOutput);
    }

    // -----------------------------------------------------------------------
    //  Degree Query Tests (§8.2.3 Closure-Captured Count(lambda))
    // -----------------------------------------------------------------------

    /// <summary>
    /// TC1: Basic degree query with Count() without predicate.
    /// .Where(p => p.Friends.Count() > 5) should produce:
    /// MATCH (src0:PersonNode)-[r0:KnowsRelationship]->(tgt0)
    /// WITH src0, count(r0) AS degree
    /// WHERE degree > $param_0
    /// RETURN src0
    /// </summary>
    [Fact]
    public void DegreeQuery_CountWithoutPredicate_EmitsWithDegreeFilter()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonNode>(provider);

        var query = source.Where(p => p.Friends.Count() > 5);

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);

        var fragments = context.FragmentSequence.ToList();
        var cypher = context.GetQuery();

        Console.WriteLine($"Generated Cypher:\n{cypher}");

        // Assert fragment types
        Assert.Contains(fragments, fragment => fragment is MatchSegmentFragment);
        Assert.Contains(fragments, fragment => fragment is WithFragment);
        Assert.Contains(fragments, fragment => fragment is WhereFragment);
        Assert.Contains(fragments, fragment => fragment is ProjectionFragment);

        // Assert WithFragment content
        var withFragment = fragments.OfType<WithFragment>().Last();
        Assert.Contains("count(r0)", withFragment.WithExpression);
        Assert.Contains("AS degree", withFragment.WithExpression);

        // Assert MATCH contains relationship pattern
        Assert.Contains("MATCH (src0:PersonNode)-[r0:KnowsRelationship]->", cypher);

        // Assert WITH contains count
        Assert.Contains("WITH src0, count(r0) AS degree", cypher);

        // Assert WHERE filters on degree
        Assert.Contains("WHERE degree > ", cypher);

        // Assert RETURN
        Assert.Contains("RETURN src0", cypher);
    }

    /// <summary>
    /// TC2: Degree query with Count predicate.
    /// .Where(p => p.Friends.Count(f => f.Age > 30) > 5) should produce:
    /// MATCH (src0:PersonNode)-[r0:KnowsRelationship]->(tgt0)
    /// WHERE tgt0.Age > 30
    /// WITH src0, count(r0) AS degree
    /// WHERE degree > 5
    /// RETURN src0
    /// </summary>
    [Fact]
    public void DegreeQuery_CountWithPredicate_EmitsWhereThenWithThenDegreeFilter()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonNode>(provider);

        var query = source.Where(p => p.Friends.Count(f => f.Age > 30) > 5);

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);

        var cypher = context.GetQuery();
        Console.WriteLine($"Generated Cypher:\n{cypher}");

        // Assert MATCH
        Assert.Contains("MATCH (src0:PersonNode)-[r0:KnowsRelationship]->", cypher);

        // Assert Count predicate becomes a WHERE on target node
        Assert.Contains("WHERE tgt0.Age >", cypher);

        // Assert WITH
        Assert.Contains("WITH src0, count(r0) AS degree", cypher);

        // Assert WHERE degree filter
        Assert.Contains("WHERE degree >", cypher);

        // Assert RETURN
        Assert.Contains("RETURN src0", cypher);

        // Clause order: MATCH ... WHERE (predicate) ... WITH ... WHERE (degree) ... RETURN
        var matchIdx = cypher.IndexOf("MATCH", StringComparison.Ordinal);
        var firstWhereIdx = cypher.IndexOf("WHERE", StringComparison.Ordinal);
        var withIdx = cypher.IndexOf("WITH", StringComparison.Ordinal);
        var secondWhereIdx = cypher.IndexOf("WHERE", firstWhereIdx + 1, StringComparison.Ordinal);
        var returnIdx = cypher.IndexOf("RETURN", StringComparison.Ordinal);

        Assert.True(matchIdx < firstWhereIdx, "MATCH before first WHERE");
        Assert.True(firstWhereIdx < withIdx, "first WHERE before WITH");
        Assert.True(withIdx < secondWhereIdx, "WITH before second WHERE");
        Assert.True(secondWhereIdx < returnIdx, "second WHERE before RETURN");
    }

    /// <summary>
    /// TC3: Degree query with >= operator.
    /// </summary>
    [Fact]
    public void DegreeQuery_WithGreaterThanOrEqual_EmitsCorrectOperator()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonNode>(provider);

        var query = source.Where(p => p.Friends.Count() >= 3);

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);

        var cypher = context.GetQuery();
        Console.WriteLine($"Generated Cypher:\n{cypher}");

        Assert.Contains("WHERE degree >=", cypher);
    }

    /// <summary>
    /// TC4: Degree query with < operator.
    /// </summary>
    [Fact]
    public void DegreeQuery_WithLessThan_EmitsCorrectOperator()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonNode>(provider);

        var query = source.Where(p => p.Friends.Count() < 10);

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);

        var cypher = context.GetQuery();
        Console.WriteLine($"Generated Cypher:\n{cypher}");

        Assert.Contains("WHERE degree <", cypher);
    }

    /// <summary>
    /// TC5: Non-degree Where predicate (no Count) — unchanged behavior.
    /// Standard Where should not produce WithFragment or degree-related output.
    /// </summary>
    [Fact]
    public void DegreeQuery_NonDegreeWhere_NoWithFragment()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonNode>(provider);

        var query = source.Where(p => p.Age > 30);

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);

        var fragments = context.FragmentSequence.ToList();
        var cypher = context.GetQuery();
        Console.WriteLine($"Generated Cypher:\n{cypher}");

        // No WithFragment or degree-related fragments
        Assert.DoesNotContain(fragments, fragment => fragment is WithFragment);
        Assert.DoesNotContain(fragments, fragment => fragment is MatchSegmentFragment);

        // Cypher should not contain WITH or degree
        Assert.DoesNotContain("WITH", cypher);
        Assert.DoesNotContain("degree", cypher);

        // Standard Where behavior: MATCH + WHERE
        Assert.Contains("MATCH (src0:PersonNode)", cypher);
        Assert.Contains("WHERE", cypher);
        Assert.Contains("src0.Age >", cypher);
    }

    /// <summary>
    /// TC6: Count in Select projection should not be intercepted by DegreeQueryHandler.
    /// Should fall through to normal expression visitor handling (size() or other fallback).
    /// No WITH clause or degree-related pattern should appear.
    /// </summary>
    [Fact]
    public void DegreeQuery_CountInSelect_FallsThroughToNormalHandling()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonNode>(provider);

        var query = source.Select(p => p.Friends.Count());

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);
        var cypher = context.GetQuery();

        Console.WriteLine($"Generated Cypher:\n{cypher}");

        // Should not contain degree query patterns
        Assert.DoesNotContain("WITH", cypher);
        Assert.DoesNotContain("degree", cypher);

        // Should have MATCH and RETURN (normal Select behavior)
        Assert.Contains("MATCH", cypher);
        Assert.Contains("RETURN", cypher);

        // The Count should be handled by the collection expression handler
        // which produces size() or similar
        Assert.NotEmpty(cypher);
    }

    // -----------------------------------------------------------------------
    //  Predicate Push-Down Tests (§8.2.4)
    // -----------------------------------------------------------------------

    /// <summary>
    /// TC1: Simple equality WHERE predicate on root node is pushed into MATCH pattern.
    /// Expected: MATCH (src0:PersonNode {Name: $param_0}) RETURN src0.Name AS c_Name
    /// </summary>
    [Fact]
    public void PredicatePushdown_SimpleEqualityOnRootNode_PushesIntoMatch()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonNode>(provider);

        var query = source
            .Where(p => p.Name == "Alice")
            .Select(p => p.Name);

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);

        var cypher = context.GetQuery();

        // Pattern should contain inline property filter
        Assert.Contains("(src0:PersonNode {Name: $param_0})", cypher);
        // No separate WHERE clause for the pushed predicate
        Assert.DoesNotContain("WHERE src0.Name", cypher);
        // RETURN should still be present
        Assert.Contains("RETURN src0.Name", cypher);
    }

    /// <summary>
    /// TC2: Simple equality on root node with PathSegments — pushed into first MATCH.
    /// Expected: MATCH (src0:PersonNode {Name: $param_0})-[r0:KnowsRelationship]->(tgt0:PersonNode) RETURN tgt0
    /// </summary>
    [Fact]
    public void PredicatePushdown_EqualityWithPathSegment_PushesIntoRootMatch()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonNode>(provider);

        var query = source
            .Where(p => p.Name == "Alice")
            .PathSegments<PersonNode, KnowsRelationship, PersonNode>()
            .Select(ps => ps.EndNode);

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);

        var cypher = context.GetQuery();

        // Pattern should push into first node
        Assert.Contains("(src0:PersonNode {Name: $param_0})", cypher);
        // No separate WHERE clause
        Assert.DoesNotContain("WHERE src0.Name", cypher);
        // RETURN tgt0
        Assert.Contains("RETURN tgt0", cypher);
    }

    /// <summary>
    /// TC3: Non-equality predicate (greater-than) remains as WHERE clause.
    /// Expected: MATCH (src0:PersonNode) WHERE src0.Age > $param_0 RETURN src0.Name AS c_Name
    /// </summary>
    [Fact]
    public void PredicatePushdown_NonEqualityPredicate_StaysAsWhere()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonNode>(provider);

        var query = source
            .Where(p => p.Age > 30)
            .Select(p => p.Name);

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);

        var cypher = context.GetQuery();

        // MATCH should be unmodified
        Assert.Contains("MATCH (src0:PersonNode)", cypher);
        // WHERE clause should contain the > predicate
        Assert.Contains("WHERE src0.Age > $param_0", cypher);
    }

    /// <summary>
    /// TC4: Multiple simple equalities on root node — all pushed into MATCH pattern.
    /// Expected: MATCH (src0:PersonNode {Name: $param_0, Age: $param_1}) RETURN src0.Name AS c_Name
    /// </summary>
    [Fact]
    public void PredicatePushdown_MultipleSimpleEqualities_AllPushed()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonNode>(provider);

        var query = source
            .Where(p => p.Name == "Alice" && p.Age == 30)
            .Select(p => p.Name);

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);

        var cypher = context.GetQuery();

        // Both properties should be in the inline map
        Assert.Contains("(src0:PersonNode {Name: $param_0, Age: $param_1})", cypher);
        // No separate WHERE clause
        Assert.DoesNotContain("WHERE src0.Name", cypher);
        Assert.DoesNotContain("WHERE src0.Age", cypher);
    }

    /// <summary>
    /// TC5: Mixed simple equality + complex predicate — simple pushed, complex stays as WHERE.
    /// Expected: MATCH (src0:PersonNode {Name: $param_0}) WHERE (src0.Age > $param_1) RETURN src0.Name AS c_Name
    /// </summary>
    [Fact]
    public void PredicatePushdown_MixedSimpleAndComplex_SimplePushedComplexStays()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonNode>(provider);

        var query = source
            .Where(p => p.Name == "Alice" && p.Age > 30)
            .Select(p => p.Name);

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);

        var cypher = context.GetQuery();

        // Name equality should be pushed
        Assert.Contains("(src0:PersonNode {Name: $param_0})", cypher);
        // Age comparison should remain as WHERE (may be wrapped in parentheses)
        Assert.Contains("> $param_1", cypher);
        Assert.Contains("WHERE", cypher);
    }

    /// <summary>
    /// TC6: Predicate on non-root alias (e.g., tgt0) stays as WHERE.
    /// Expected: MATCH (src0:PersonNode)-[r0:KnowsRelationship]->(tgt0:PersonNode) WHERE tgt0.Name = $param_0 RETURN tgt0
    /// </summary>
    [Fact]
    public void PredicatePushdown_WhereOnNonRootAlias_NotPushed()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonNode>(provider);

        var query = source
            .PathSegments<PersonNode, KnowsRelationship, PersonNode>()
            .Where(ps => ps.EndNode.Name == "Bob")
            .Select(ps => ps.EndNode);

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);

        var cypher = context.GetQuery();

        // MATCH should be unmodified (no property filter on tgt0)
        Assert.Contains("(tgt0:PersonNode)", cypher);
        Assert.DoesNotContain("tgt0:PersonNode {", cypher);
        // WHERE clause should have the predicate
        Assert.Contains("WHERE tgt0.Name = $param_0", cypher);
    }

    /// <summary>
    /// TC7: After push-down, the WHERE clause is removed because the predicate was consumed.
    /// This verifies that the optimization removes consumed WhereFragments from the sequence.
    /// </summary>
    [Fact]
    public void PredicatePushdown_WhereFragmentRemovedAfterPush()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonNode>(provider);

        var query = source
            .Where(p => p.Name == "Alice")
            .Select(p => p.Name);

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);

        var cypher = context.GetQuery();

        // The pushed predicate should appear in the MATCH pattern
        Assert.Contains("(src0:PersonNode {Name: $param_0})", cypher);
        // No separate WHERE clause — proves the WhereFragment was consumed
        Assert.DoesNotContain("WHERE", cypher);
    }

    /// <summary>
    /// TC8: Regression — query without WHERE clause is unchanged.
    /// Expected: MATCH (src0:PersonNode) RETURN src0.Name AS c_Name
    /// </summary>
    [Fact]
    public void PredicatePushdown_NoWhereClause_Unchanged()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonNode>(provider);

        var query = source
            .Select(p => p.Name);

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);

        var cypher = context.GetQuery();

        Assert.Contains("MATCH (src0:PersonNode)", cypher);
        Assert.DoesNotContain("WHERE", cypher);
        Assert.Contains("RETURN", cypher);
    }

    /// <summary>
    /// TC9: CONTAINS predicate (translated to native CONTAINS operator) stays as WHERE.
    /// Expected: MATCH (src0:PersonNode) WHERE src0.Name CONTAINS 'ohn' RETURN src0.Name AS c_Name
    /// </summary>
    [Fact]
    public void PredicatePushdown_ContainsPredicate_StaysAsWhere()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonNode>(provider);

        var query = source
            .Where(p => p.Name.Contains("ohn"))
            .Select(p => p.Name);

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);

        var cypher = context.GetQuery();

        // MATCH should be unmodified
        Assert.Contains("MATCH (src0:PersonNode)", cypher);
        // WHERE clause should contain CONTAINS (native Cypher operator, not =~ regex)
        // Value is parameterized via $param_0 to prevent Cypher injection
        Assert.Contains("WHERE src0.Name CONTAINS $param_0", cypher);
        // No property filter inline
        Assert.DoesNotContain("src0:PersonNode {", cypher);
    }

    /// <summary>
    /// TC10: Fragment renderer parity test — builder and fragment renderer produce the same output.
    /// This validates that the optimization produces semantically equivalent queries.
    /// </summary>
    [Fact]
    public void RendererMatchesBuilder_WithPredicatePushdown()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonNode>(provider);

        // Query with pushable predicate
        var query = source
            .Where(p => p.Name == "Alice")
            .Select(p => p.Name);

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);

        var fragments = context.FragmentSequence.ToList();
        var builderOutput = context.GetQuery();
        var fragmentOutput = AgeFragmentRenderer.Render(fragments);

        Assert.Equal(builderOutput, fragmentOutput);
    }

    /// <summary>
    /// Tests that projecting a whole <see cref="IGraphPathSegment"/> parameter
    /// (e.g., <c>Select(ps => new {{ Path = ps }})</c>) emits three RETURN columns
    /// with the correct <c>c_</c>-prefixed aliases for source, relationship, and target.
    /// This is the Cypher-generation side of §8.2.5 Path Segment Whole-Object Projection.
    /// </summary>
    [Fact]
    public void PathSegmentWholeObjectProjection_EmitsThreeColumns()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonNode>(provider);

        // LINQ: source.PathSegments<...>().Select(ps => new { Path = ps })
        var query = source
            .PathSegments<PersonNode, KnowsRelationship, PersonNode>()
            .Select(ps => new { Path = ps });

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);

        var cypher = context.GetQuery();
        Console.WriteLine($"Generated Cypher:\n{cypher}");

        // The RETURN clause must expand the single IGraphPathSegment parameter
        // into three separate columns: src0, r0, tgt0.
        Assert.Contains("src0 AS c_Path_src0", cypher);
        Assert.Contains("r0 AS c_Path_r0", cypher);
        Assert.Contains("tgt0 AS c_Path_tgt0", cypher);

        // Verify the MATCH clause still looks correct
        Assert.Contains("MATCH (src0:PersonNode)-[r0:KnowsRelationship]->(tgt0:PersonNode)", cypher);

        // Verify that the fragment renderer produces the same output as the builder
        var fragments = context.FragmentSequence.ToList();
        var fragmentOutput = AgeFragmentRenderer.Render(fragments);
        Assert.Equal(cypher, fragmentOutput);
    }

    // -----------------------------------------------------------------------
    //  Targeted Complex Property Loading Tests (§8.2.6)
    // -----------------------------------------------------------------------

    /// <summary>
    /// <summary>
    /// TC1: Projection accessing a single complex property should emit a targeted
    /// OPTIONAL MATCH for only that property, not the monolithic block.
    /// Expected: OPTIONAL MATCH (src0)-[r_HomeAddress:__PROPERTY__HomeAddress__]->(cp_HomeAddress)
    ///           RETURN cp_HomeAddress.Street AS c_Street
    /// </summary>
    [Fact]
    public void TargetedLoading_SingleComplexPropertyAccess_EmitsTargetedOptionalMatch()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonWithAddressNode>(provider);

        // Select that accesses only HomeAddress.Street
        var query = source
            .Select(p => new { Street = p.HomeAddress.Street });

        var context = new CypherQueryContext(typeof(PersonWithAddressNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);

        var cypher = context.GetQuery();
        Console.WriteLine($"Generated Cypher:\n{cypher}");

        // Should have OPTIONAL MATCH for the specific complex property
        // The relationship type uses __PROPERTY__ prefix/suffix convention from GraphDataModel
        Assert.Contains("OPTIONAL MATCH (src0)-[r_HomeAddress:__PROPERTY__HomeAddress__]->(cp_HomeAddress)", cypher);

        // Should NOT have the monolithic complex property loading block
        Assert.DoesNotContain("prop_rel", cypher);
        Assert.DoesNotContain("complex_properties", cypher);

        // RETURN should reference cp_HomeAddress alias directly
        Assert.Contains("cp_HomeAddress.Street AS c_Street", cypher);

        // Should have exactly 1 OPTIONAL MATCH (no extra ones)
        var optionalMatchCount = System.Text.RegularExpressions.Regex.Matches(cypher, @"OPTIONAL MATCH").Count;
        Assert.Equal(1, optionalMatchCount);
    }

    /// <summary>
    /// TC2: Projection accessing a complex property with null check.
    /// Expected: CASE WHEN cp_HomeAddress IS NOT NULL THEN cp_HomeAddress.Street ELSE null END AS c_Street
    /// </summary>
    [Fact]
    public void TargetedLoading_ComplexPropertyWithNullCheck_UsesCoalescePattern()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonWithAddressNode>(provider);

        // Select with null check on complex property
        var query = source
            .Select(p => new { Street = p.HomeAddress != null ? p.HomeAddress.Street : null });

        var context = new CypherQueryContext(typeof(PersonWithAddressNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);

        var cypher = context.GetQuery();
        Console.WriteLine($"Generated Cypher:\n{cypher}");

        // Should have OPTIONAL MATCH for the specific complex property
        Assert.Contains("OPTIONAL MATCH (src0)-[r_HomeAddress:__PROPERTY__HomeAddress__]->(cp_HomeAddress)", cypher);

        // Should use CASE expression with cp_HomeAddress IS NOT NULL
        Assert.Contains("CASE WHEN cp_HomeAddress IS NOT NULL THEN cp_HomeAddress.Street", cypher);

        // Should NOT have the monolithic block
        Assert.DoesNotContain("prop_rel", cypher);
        Assert.DoesNotContain("complex_properties", cypher);

        // Verify only 1 OPTIONAL MATCH
        var optionalMatchCount = System.Text.RegularExpressions.Regex.Matches(cypher, @"OPTIONAL MATCH").Count;
        Assert.Equal(1, optionalMatchCount);
    }

    /// <summary>
    /// TC3: Simple property projection without complex properties should not emit
    /// any OPTIONAL MATCH clauses.
    /// Expected: MATCH (src0:PersonNode) RETURN src0.Name AS c_Name
    /// </summary>
    [Fact]
    public void TargetedLoading_SimplePropertyOnly_NoOptionalMatch()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonWithAddressNode>(provider);

        // Select that only accesses simple properties, not complex ones
        var query = source
            .Select(p => new { p.Name });

        var context = new CypherQueryContext(typeof(PersonWithAddressNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);

        var cypher = context.GetQuery();
        Console.WriteLine($"Generated Cypher:\n{cypher}");

        // Should NOT have any OPTIONAL MATCH because no complex properties are referenced
        Assert.DoesNotContain("OPTIONAL MATCH", cypher);

        // Should have simple RETURN
        Assert.Contains("RETURN src0.Name AS c_Name", cypher);
    }

    /// <summary>
    /// TC4: Regression test — non-projection queries (e.g., ToList) still use
    /// the monolithic complex property loading block.
    /// Expected: OPTIONAL MATCH (src0)-[prop_rel]->(prop_node)
    ///           RETURN { Node: src0, ComplexProperties: complex_properties }
    /// </summary>
    [Fact]
    public void TargetedLoading_NonProjectionQuery_UsesMonolithicBlock()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonWithAddressNode>(provider);

        // Simulate ToList (no projection) — should use monolithic block
        var toListMethod = typeof(Enumerable)
            .GetMethods()
            .Single(method => method.Name == nameof(Enumerable.ToList) && method.GetParameters().Length == 1)
            .MakeGenericMethod(typeof(PersonWithAddressNode));

        var toListExpression = Expression.Call(null, toListMethod, source.Expression);

        var context = new CypherQueryContext(typeof(PersonWithAddressNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(toListExpression);

        var cypher = context.GetQuery();
        Console.WriteLine($"Generated Cypher:\n{cypher}");

        // Should still use the monolithic block (prop_rel/prop_node pattern)
        Assert.Contains("OPTIONAL MATCH (src0)-[prop_rel]->(prop_node)", cypher);
        Assert.Contains("complex_properties", cypher);
        Assert.Contains("RETURN {", cypher);
        Assert.Contains("Node: src0", cypher);
    }

    /// <summary>
    /// TC5: Regression test — queries on entity types without complex properties
    /// are unchanged (no OPTIONAL MATCH).
    /// </summary>
    [Fact]
    public void TargetedLoading_EntityWithoutComplexProperties_Unchanged()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonNode>(provider);

        var query = source
            .Select(p => new { p.Name, p.Age });

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);

        var cypher = context.GetQuery();
        Console.WriteLine($"Generated Cypher:\n{cypher}");

        // No OPTIONAL MATCH for PersonNode (no complex properties)
        Assert.DoesNotContain("OPTIONAL MATCH", cypher);

        // Simple RETURN
        Assert.Contains("RETURN src0.Name AS c_Name, src0.Age AS c_Age", cypher);
    }

    /// <summary>
    /// TC6: Fragment renderer parity — builder and fragment renderer produce same output
    /// for targeted loading queries.
    /// </summary>
    [Fact]
    public void TargetedLoading_RendererMatchesBuilder()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonWithAddressNode>(provider);

        var query = source
            .Select(p => new { Street = p.HomeAddress.Street });

        var context = new CypherQueryContext(typeof(PersonWithAddressNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);

        var fragments = context.FragmentSequence.ToList();
        var builderOutput = context.GetQuery();
        var fragmentOutput = AgeFragmentRenderer.Render(fragments);

        Assert.Equal(builderOutput, fragmentOutput);
    }

    // -----------------------------------------------------------------------
    //  DateTime Member Translation Tests (§8.2.7)
    // -----------------------------------------------------------------------

    /// <summary>
    /// TC1: DayOfWeek translation uses pg_catalog.date_part for correct weekday computation.
    /// Previously used year extraction (substring 0,4) which was completely incorrect.
    /// Expected: MATCH (src0:PersonNode)
    ///           WHERE pg_catalog.date_part('dow', CAST(substring(toString(src0.CreatedAt), 0, 10) AS date))::integer = 0
    ///           RETURN src0
    /// .NET DayOfWeek enum: Sunday=0, Monday=1, ..., Saturday=6
    /// date_part('dow', ...) returns: 0=Sunday, 1=Monday, ..., 6=Saturday (matching .NET exactly)
    /// </summary>
    [Fact]
    public void DateTimeDayOfWeek_GeneratesCorrectCypherExpression()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonNode>(provider);

        // Query: Where(p => p.CreatedAt.DayOfWeek == DayOfWeek.Sunday)
        // DayOfWeek.Sunday == 0, so the WHERE clause compares against 0
        var query = source.Where(p => p.CreatedAt.DayOfWeek == DayOfWeek.Sunday);

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);

        var cypher = context.GetQuery();
        Console.WriteLine($"Generated Cypher:\n{cypher}");

        // Should NOT contain the broken year-mod-7 computation (the bug)
        Assert.DoesNotContain("% 7", cypher);

        // Should NOT extract only 4 characters (the year) — substring 0,10 is for the date
        Assert.DoesNotContain("substring(src0.CreatedAt, 0, 4)", cypher);

        // Should use pg_catalog.date_part with 'dow' for correct day-of-week
        Assert.Contains("pg_catalog.date_part('dow'", cypher);

        // Should cast the result to integer
        Assert.Contains("::integer", cypher);

        // Should reference the DateTime property
        Assert.Contains("src0.CreatedAt", cypher);

        // The expression should be part of a WHERE clause
        Assert.Contains("WHERE", cypher);

        // Verify fragment renderer parity
        var fragments = context.FragmentSequence.ToList();
        var fragmentOutput = AgeFragmentRenderer.Render(fragments);
        Assert.Equal(cypher, fragmentOutput);
    }

    /// <summary>
    /// TC2: Year, Month, Day, Hour, Minute, Second translations remain UNCHANGED
    /// after the DayOfWeek fix. Verifies no regression on other DateTime members.
    /// </summary>
    [Fact]
    public void DateTimeMembers_YearMonthDayHourMinuteSecond_Unchanged()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonNode>(provider);

        // Test Year translation
        var yearQuery = source.Where(p => p.CreatedAt.Year == 2026);
        var yearContext = new CypherQueryContext(typeof(PersonNode));
        var yearVisitor = new AgeCypherQueryVisitor(yearContext);
        yearVisitor.Visit(yearQuery.Expression);
        var yearCypher = yearContext.GetQuery();
        Assert.Contains("toInteger(substring(src0.CreatedAt, 0, 4))", yearCypher);
        Console.WriteLine($"Year Cypher:\n{yearCypher}");

        // Test Month translation
        var monthQuery = source.Where(p => p.CreatedAt.Month == 7);
        var monthContext = new CypherQueryContext(typeof(PersonNode));
        var monthVisitor = new AgeCypherQueryVisitor(monthContext);
        monthVisitor.Visit(monthQuery.Expression);
        var monthCypher = monthContext.GetQuery();
        Assert.Contains("toInteger(substring(src0.CreatedAt, 5, 2))", monthCypher);
        Console.WriteLine($"Month Cypher:\n{monthCypher}");

        // Test Day translation
        var dayQuery = source.Where(p => p.CreatedAt.Day == 3);
        var dayContext = new CypherQueryContext(typeof(PersonNode));
        var dayVisitor = new AgeCypherQueryVisitor(dayContext);
        dayVisitor.Visit(dayQuery.Expression);
        var dayCypher = dayContext.GetQuery();
        Assert.Contains("toInteger(substring(src0.CreatedAt, 8, 2))", dayCypher);
        Console.WriteLine($"Day Cypher:\n{dayCypher}");

        // Test Hour translation
        var hourQuery = source.Where(p => p.CreatedAt.Hour == 5);
        var hourContext = new CypherQueryContext(typeof(PersonNode));
        var hourVisitor = new AgeCypherQueryVisitor(hourContext);
        hourVisitor.Visit(hourQuery.Expression);
        var hourCypher = hourContext.GetQuery();
        Assert.Contains("toInteger(substring(src0.CreatedAt, 11, 2))", hourCypher);
        Console.WriteLine($"Hour Cypher:\n{hourCypher}");

        // Test Minute translation
        var minuteQuery = source.Where(p => p.CreatedAt.Minute == 43);
        var minuteContext = new CypherQueryContext(typeof(PersonNode));
        var minuteVisitor = new AgeCypherQueryVisitor(minuteContext);
        minuteVisitor.Visit(minuteQuery.Expression);
        var minuteCypher = minuteContext.GetQuery();
        Assert.Contains("toInteger(substring(src0.CreatedAt, 14, 2))", minuteCypher);
        Console.WriteLine($"Minute Cypher:\n{minuteCypher}");

        // Test Second translation
        var secondQuery = source.Where(p => p.CreatedAt.Second == 51);
        var secondContext = new CypherQueryContext(typeof(PersonNode));
        var secondVisitor = new AgeCypherQueryVisitor(secondContext);
        secondVisitor.Visit(secondQuery.Expression);
        var secondCypher = secondContext.GetQuery();
        Assert.Contains("toInteger(substring(src0.CreatedAt, 17, 2))", secondCypher);
        Console.WriteLine($"Second Cypher:\n{secondCypher}");
    }
}
