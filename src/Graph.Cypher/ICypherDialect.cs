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
    /// <param name="escape">
    /// When <see langword="true"/>, the property name is a dynamic (untrusted) identifier and is
    /// always escaped. When <see langword="false"/>, it is a compile-time data-model property
    /// identifier and is rendered byte-stably: bare when it is a plain symbolic name, escaped only
    /// when it would otherwise break out of the identifier (spaces, punctuation, backticks).
    /// </param>
    string RenderPropertyAccess(string target, string property, bool escape);

    /// <summary>Renders transaction-local native identity for a bound graph element.</summary>
    /// <param name="target">The already-rendered node or relationship expression.</param>
    string RenderNativeElementIdentity(string target);

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

    /// <summary>
    /// Renders a full-text search clause in this dialect's syntax, or throws
    /// <see cref="GraphQueryTranslationException"/> if the dialect does not support full-text search.
    /// </summary>
    /// <param name="clause">The full-text search clause to render.</param>
    /// <param name="context">Renderer services (expressions, literals) for the dialect to use.</param>
    /// <remarks>
    /// The default implementation declines full-text search; dialects that declare
    /// <see cref="GraphCapability.FullTextSearch"/> override it. This keeps the whole full-text
    /// scaffolding — procedure/index names and any mixed-entity subquery shape — private to the
    /// dialect rather than hard-coded in the shared renderer.
    /// </remarks>
    string RenderFullTextSearch(FullTextSearchClause clause, ICypherRenderContext context) =>
        throw new GraphQueryTranslationException(
            $"The {Name} dialect does not support capability {GraphCapability.FullTextSearch}.");

    /// <summary>Gets the stored marker property that identifies owned complex-property relationships.</summary>
    string ComplexPropertyRelationshipMarker { get; }
}
