// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph.Cypher.Ast;

namespace Cvoya.Graph.Cypher;

/// <summary>
/// Defines the syntax and user-visible capabilities of a Cypher implementation.
/// </summary>
public interface ICypherDialect
{
    /// <summary>Gets the dialect name used in translation diagnostics.</summary>
    string Name { get; }

    /// <summary>Gets the graph features supported by this dialect.</summary>
    CapabilitySet Capabilities { get; }

    /// <summary>Renders a parameter reference.</summary>
    /// <param name="name">The parameter name without a prefix.</param>
    string RenderParameter(string name);

    /// <summary>Renders a property or stored entity-ID access.</summary>
    /// <param name="target">The already-rendered target expression.</param>
    /// <param name="property">The stored property name.</param>
    /// <param name="escape">Whether the property name must be escaped.</param>
    string RenderPropertyAccess(string target, string property, bool escape);

    /// <summary>Maps a provider-neutral function name to dialect syntax.</summary>
    /// <param name="function">The provider-neutral function name.</param>
    string RenderFunctionName(string function);

    /// <summary>Gets the translation strategy for a provider-neutral function.</summary>
    /// <param name="function">The provider-neutral function name.</param>
    CypherFunctionBehavior GetFunctionBehavior(string function);

    /// <summary>Renders labels in a node pattern, without the leading colon.</summary>
    /// <param name="labels">The labels to render.</param>
    string RenderNodeLabels(IReadOnlyList<string> labels);

    /// <summary>Renders types in a relationship pattern, without the leading colon.</summary>
    /// <param name="types">The relationship types to render.</param>
    string RenderRelationshipTypes(IReadOnlyList<string> types);

    /// <summary>Renders a relationship-depth suffix, including the leading asterisk.</summary>
    /// <param name="depth">The validated depth range.</param>
    string RenderDepth(DepthRange depth);

    /// <summary>Renders a label-membership predicate.</summary>
    /// <param name="target">The already-rendered target expression.</param>
    /// <param name="labels">The labels to test.</param>
    /// <param name="renderLiteral">A renderer for dialect string literals.</param>
    string RenderLabelTest(string target, IReadOnlyList<string> labels, Func<string, string> renderLiteral);

    /// <summary>Gets the node full-text procedure name.</summary>
    string FullTextNodeProcedure { get; }

    /// <summary>Gets the relationship full-text procedure name.</summary>
    string FullTextRelationshipProcedure { get; }

    /// <summary>Gets the node full-text index name.</summary>
    string FullTextNodeIndex { get; }

    /// <summary>Gets the relationship full-text index name.</summary>
    string FullTextRelationshipIndex { get; }

    /// <summary>Gets the stored marker property that identifies owned complex-property relationships.</summary>
    string ComplexPropertyRelationshipMarker { get; }
}
