// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Querying;

using Cvoya.Graph.Age.Entities;
using Cvoya.Graph.Cypher.Ast.Expressions;

/// <summary>
/// Defines AGE's hybrid logical label/type policy for native and legacy CVOYA storage.
/// </summary>
internal static class AgeElementMatcher
{
    internal const string InheritanceLabelsProperty = "inheritance_labels";

    internal static CypherExpression NodePredicate(
        CypherExpression target,
        IReadOnlyList<string> labels) => Alternatives(
            labels,
            label => new BinaryExpression(
                CypherBinaryOperator.Or,
                new BinaryExpression(
                    CypherBinaryOperator.In,
                    new Literal(label),
                    new FunctionCall("labels", [target])),
                LegacyPredicate(target, label)));

    internal static CypherExpression RelationshipPredicate(
        CypherExpression target,
        IReadOnlyList<string> types) => Alternatives(
            types,
            type => new BinaryExpression(
                CypherBinaryOperator.Or,
                new BinaryExpression(
                    CypherBinaryOperator.Equal,
                    new FunctionCall("type", [target]),
                    new Literal(type)),
                LegacyPredicate(target, type)));

    internal static string NodePredicate(string alias, string parameterName) =>
        $"(size([age_label IN labels({alias}) WHERE age_label = {parameterName}]) > 0 OR " +
        $"size([age_label IN coalesce({alias}.{InheritanceLabelsProperty}, []) WHERE age_label = {parameterName}]) > 0)";

    internal static string RelationshipPredicate(string alias, string parameterName) =>
        $"(type({alias}) = {parameterName} OR {parameterName} IN coalesce({alias}.{InheritanceLabelsProperty}, []))";

    internal static string UserRootMatch(string alias, string properties = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);
        var ownerRelationship = $"{alias}_cvoya_owner_relationship";
        var ownerCount = $"{alias}_cvoya_owner_count";
        return $"""
            MATCH ({alias}{properties})
            OPTIONAL MATCH ()-[{ownerRelationship}]->({alias})
            WITH {alias}, count(CASE WHEN coalesce({ownerRelationship}.{ComplexPropertyStorage.RelationshipMarkerProperty}, false) = true THEN {ownerRelationship} ELSE null END) AS {ownerCount}
            WHERE {ownerCount} = 0
            """;
    }

    internal static string UserRelationshipPredicate(string alias) =>
        $"coalesce({alias}.{ComplexPropertyStorage.RelationshipMarkerProperty}, false) = false";

    private static BinaryExpression LegacyPredicate(CypherExpression target, string label) =>
        new BinaryExpression(
            CypherBinaryOperator.In,
            new Literal(label),
            new FunctionCall(
                "coalesce",
                [
                    new PropertyAccess(target, InheritanceLabelsProperty),
                    new ListExpression([]),
                ]));

    private static CypherExpression Alternatives(
        IReadOnlyList<string> names,
        Func<string, CypherExpression> build)
    {
        ArgumentNullException.ThrowIfNull(names);
        if (names.Count == 0)
        {
            throw new ArgumentException("At least one logical label or type is required.", nameof(names));
        }

        return names.Select(build).Aggregate((left, right) =>
            new BinaryExpression(CypherBinaryOperator.Or, left, right));
    }
}
