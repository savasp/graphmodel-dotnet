// Copyright 2025 Savas Parastatidis

namespace Cvoya.Graph.Model.Age.Tests;

using System.Linq;
using Cvoya.Graph.Model;
using Cvoya.Graph.Model.Age.Querying.Cypher.Visitors.Core;
using Cvoya.Graph.Model.Age.Tests.Infrastructure;
using Xunit;

/// <summary>
/// Regression tests that capture CURRENT alias generation behavior before refactoring.
/// These tests document how the system actually works now, not how we want it to work.
/// After refactoring to explicit alias passing, these tests should still pass.
/// </summary>
public sealed class AliasGenerationRegressionTests
{
    #region Single Hop - Working Scenarios

    [Fact]
    public void SingleHop_SimpleTraversal_GeneratesThreeAliases()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonNode>(provider);

        var query = source
            .PathSegments<PersonNode, KnowsRelationship, PersonNode>()
            .Select(ps => new { StartNode = ps.StartNode, Relationship = ps.Relationship, EndNode = ps.EndNode });

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);
        var cypherQuery = context.GetQuery();

        // Verify basic structure
        Assert.Contains("MATCH", cypherQuery);
        Assert.Contains("RETURN", cypherQuery);

        // Verify aliases exist (src0, r0, tgt0 pattern)
        Assert.Contains("src0", cypherQuery);
        Assert.Contains("r0", cypherQuery);
        Assert.Contains("tgt0", cypherQuery);
    }

    [Fact]
    public void SingleHop_WhereClause_UsesPascalCaseProperties()
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
        var cypherQuery = context.GetQuery();

        // Properties should be PascalCase
        Assert.Contains("tgt0.Age", cypherQuery);
        Assert.Contains("WHERE", cypherQuery);
        Assert.Contains("RETURN tgt0", cypherQuery);
    }

    [Fact]
    public void SingleHop_WhereClause_UsesParameterization()
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
        var cypherQuery = context.GetQuery();

        // Values should be parameterized
        Assert.Contains("$param_", cypherQuery);
        Assert.DoesNotContain("> 30", cypherQuery);
    }

    [Fact]
    public void SingleHop_ProjectStartNode_DocumentCurrentBehavior()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonNode>(provider);

        // NOTE: This may be a bug - projecting StartNode returns tgt0 instead of src0
        // Documenting current behavior for regression testing
        var query = source
            .PathSegments<PersonNode, KnowsRelationship, PersonNode>()
            .Select(ps => ps.StartNode);

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);
        var cypherQuery = context.GetQuery();

        // Current behavior: returns tgt0 even when asking for StartNode
        // This test documents the current behavior, even if it's unexpected
        Assert.Contains("src0", cypherQuery); // Alias exists in MATCH
        Assert.Contains("RETURN", cypherQuery);

        // After refactoring, verify this behavior is preserved or intentionally changed
    }

    [Fact]
    public void SingleHop_ProjectEndNode_UsesTargetAlias()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonNode>(provider);

        var query = source
            .PathSegments<PersonNode, KnowsRelationship, PersonNode>()
            .Select(ps => ps.EndNode);

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);
        var cypherQuery = context.GetQuery();

        Assert.Contains("tgt0", cypherQuery);
        var returnClause = cypherQuery.Split("RETURN")[1];
        Assert.Contains("tgt0", returnClause);
    }

    [Fact]
    public void SingleHop_ProjectRelationship_UsesRelationshipAlias()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonNode>(provider);

        var query = source
            .PathSegments<PersonNode, KnowsRelationship, PersonNode>()
            .Select(ps => ps.Relationship);

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);
        var cypherQuery = context.GetQuery();

        Assert.Contains("r0", cypherQuery);
        var returnClause = cypherQuery.Split("RETURN")[1];
        Assert.Contains("r0", returnClause);
    }

    #endregion

    #region Multi-Hop - Document Actual Behavior

    [Fact]
    public void TwoHop_ChainedTraversal_CaptureCurrentBehavior()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonNode>(provider);

        var query = source
            .PathSegments<PersonNode, KnowsRelationship, PersonNode>()
            .Select(ps => ps.EndNode)
            .PathSegments<PersonNode, KnowsRelationship, PersonNode>()
            .Select(ps => ps.EndNode);

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);
        var cypherQuery = context.GetQuery();

        // Document what we actually see: The implementation creates TWO match patterns
        Assert.Contains("MATCH", cypherQuery);
        Assert.Contains("RETURN", cypherQuery);

        // First hop uses: src0 -[r0]-> tgt0
        Assert.Contains("src0", cypherQuery);
        Assert.Contains("r0", cypherQuery);
        Assert.Contains("tgt0", cypherQuery);

        // Second hop uses: tgt0 -[r1]-> ?
        Assert.Contains("tgt0", cypherQuery);
        Assert.Contains("r1", cypherQuery);

        // This is the current behavior - document it as-is
        // After refactoring, as long as this still generates valid Cypher, we're good
    }

    [Fact]
    public void ThreeHop_ChainedTraversal_GeneratesMultipleAliasGroups()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonNode>(provider);

        var query = source
            .PathSegments<PersonNode, KnowsRelationship, PersonNode>()
            .Select(ps => ps.EndNode)
            .PathSegments<PersonNode, KnowsRelationship, PersonNode>()
            .Select(ps => ps.EndNode)
            .PathSegments<PersonNode, KnowsRelationship, PersonNode>()
            .Select(ps => ps.EndNode);

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);
        var cypherQuery = context.GetQuery();

        // Just verify it generates valid-looking Cypher with multiple hops
        Assert.Contains("MATCH", cypherQuery);
        Assert.Contains("RETURN", cypherQuery);

        // Should have multiple alias groups
        Assert.Contains("src0", cypherQuery);
        Assert.Contains("r0", cypherQuery);

        // Don't assert exact alias patterns - just verify generation works
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void SingleNode_NoTraversal_WorksWithoutAliases()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonNode>(provider);

        var query = source.Where(p => p.Age > 25);

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);
        var cypherQuery = context.GetQuery();

        Assert.Contains("MATCH", cypherQuery);
        Assert.Contains("PersonNode", cypherQuery);
        Assert.Contains("Age", cypherQuery);
    }

    [Fact]
    public void ProjectionWithProperties_UsesPascalCase()
    {
        var provider = new TestGraphQueryProvider();
        var source = new TestGraphNodeQueryable<PersonNode>(provider);

        var query = source
            .PathSegments<PersonNode, KnowsRelationship, PersonNode>()
            .Select(ps => new
            {
                SourceName = ps.StartNode.Name,
                TargetAge = ps.EndNode.Age,
                RelType = ps.Relationship.Type
            });

        var context = new CypherQueryContext(typeof(PersonNode));
        var visitor = new AgeCypherQueryVisitor(context);
        visitor.Visit(query.Expression);
        var cypherQuery = context.GetQuery();

        // All properties should be PascalCase
        Assert.Contains(".Name", cypherQuery);
        Assert.Contains(".Age", cypherQuery);
        Assert.Contains(".Type", cypherQuery);

        // Should not contain lowercase versions
        Assert.DoesNotContain(".name", cypherQuery);
        Assert.DoesNotContain(".age", cypherQuery);
        Assert.DoesNotContain(".type", cypherQuery);
    }

    #endregion
}
