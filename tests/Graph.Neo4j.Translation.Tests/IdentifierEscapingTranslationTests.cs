// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Translation.Tests;

using Cvoya.Graph.Neo4j.Translation.Tests.Harness;
using Cvoya.Graph.Neo4j.Translation.Tests.Model;

/// <summary>
/// Read-path escaping coverage for #214: labels and relationship types that are not plain Cypher
/// symbolic names must render backtick-escaped in MATCH patterns (the write path already escaped
/// them, so a type could be created but never queried). Ordinary identifiers stay unquoted -
/// every pre-existing snapshot pins that.
/// </summary>
public class IdentifierEscapingTranslationTests : TranslationTestBase
{
    [Fact]
    public Task Where_OnNodeWithSpacedLabel_EscapesLabelInMatch()
    {
        var query = Root.Nodes<SpacedLabelNode>().Where(n => n.Name == "Alice");
        return VerifyTranslation(query);
    }

    /// <summary>
    /// The AST carries relationship type names individually, so a literal '|' in a type name is
    /// escaped as part of the name instead of being read as an alternation separator.
    /// </summary>
    [Fact]
    public Task RelationshipRoot_WithPipeInTypeName_EscapesTypeAsSingleName()
    {
        var query = Root.Relationships<PipeTypedRelationship>().Where(r => r.StartNodeId != "");
        return VerifyTranslation(query);
    }

    [Fact]
    public Task RelationshipRoot_WithBacktickInTypeName_DoublesTheBacktick()
    {
        var query = Root.Relationships<BacktickTypedRelationship>().Where(r => r.StartNodeId != "");
        return VerifyTranslation(query);
    }

    /// <summary>
    /// LabelTest renders labels as single-quoted string literals (already quote-escaped), which is
    /// safe for any label text; pinned here for the escapable-label case.
    /// </summary>
    [Fact]
    public Task Search_OnSpacedLabelNode_RendersLabelTestAsStringLiteral()
    {
        var query = Root.Nodes<SpacedLabelNode>().Search("alice");
        return VerifyTranslation(query);
    }

    /// <summary>
    /// A typed property whose custom storage name contains a space must render as a single
    /// backtick-escaped identifier in the WHERE predicate (#367), not a bare name that would break
    /// the property-access position.
    /// </summary>
    [Fact]
    public Task Where_OnTypedPropertyWithSpacedLabel_EscapesPropertyName()
    {
        var query = Root.Nodes<HostilePropertyLabelNode>().Where(n => n.LastName == "Smith");
        return VerifyTranslation(query);
    }

    /// <summary>
    /// A custom property label that itself looks like a backtick break-out plus an injected clause
    /// must be rendered as one escaped identifier with its embedded backtick doubled.
    /// </summary>
    [Fact]
    public Task Where_OnTypedPropertyWithBacktickBreakoutLabel_DoublesBacktick()
    {
        var query = Root.Nodes<HostilePropertyLabelNode>().Where(n => n.Score > 0);
        return VerifyTranslation(query);
    }

    /// <summary>
    /// Escaping applies across the read path: projecting and ordering by an escapable typed
    /// property name must escape it everywhere it is rendered, not only in predicates.
    /// </summary>
    [Fact]
    public Task ProjectAndOrderByTypedPropertyWithSpacedLabel_EscapesPropertyName()
    {
        var query = Root.Nodes<HostilePropertyLabelNode>()
            .OrderBy(n => n.LastName)
            .Select(n => n.LastName);
        return VerifyTranslation(query);
    }
}
