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
}
