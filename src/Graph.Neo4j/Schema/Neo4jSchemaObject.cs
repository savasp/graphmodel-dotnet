// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Schema;

using global::Neo4j.Driver;

internal enum Neo4jSchemaObjectKind
{
    Unknown,
    NodeUniquenessConstraint,
    RelationshipUniquenessConstraint,
    NodePropertyExistenceConstraint,
    RelationshipPropertyExistenceConstraint,
    RangeIndex,
    FullTextIndex
}

internal enum Neo4jSchemaEntityType
{
    Node,
    Relationship
}

internal sealed record Neo4jSchemaObjectDescriptor(
    string Name,
    Neo4jSchemaObjectKind Kind,
    Neo4jSchemaEntityType EntityType,
    IReadOnlyList<string> LabelsOrTypes,
    IReadOnlyList<string> Properties)
{
    public bool IsEquivalentTo(Neo4jSchemaObjectDescriptor? other)
    {
        return other is not null
            && string.Equals(Name, other.Name, StringComparison.Ordinal)
            && Kind == other.Kind
            && EntityType == other.EntityType
            && HasSameMembers(LabelsOrTypes, other.LabelsOrTypes)
            && HasSameMembers(Properties, other.Properties);
    }

    private static bool HasSameMembers(IReadOnlyList<string> first, IReadOnlyList<string> second)
    {
        return first.Count == second.Count
            && first.ToHashSet(StringComparer.Ordinal).SetEquals(second);
    }
}

internal sealed record Neo4jSchemaObjectCreation(
    Neo4jSchemaObjectDescriptor Descriptor,
    string Cypher);

internal static class Neo4jSchemaErrorClassifier
{
    private const string TransientErrorPrefix = "Neo.TransientError.";

    private static readonly HashSet<string> PotentialEquivalentConflictCodes = new(StringComparer.Ordinal)
    {
        "Neo.ClientError.Schema.ConstraintAlreadyExists",
        "Neo.ClientError.Schema.ConstraintWithNameAlreadyExists",
        "Neo.ClientError.Schema.EquivalentSchemaRuleAlreadyExists",
        "Neo.ClientError.Schema.IndexAlreadyExists",
        "Neo.ClientError.Schema.IndexWithNameAlreadyExists"
    };

    public static bool IsRetryable(Neo4jException exception)
    {
        return exception.Code?.StartsWith(TransientErrorPrefix, StringComparison.Ordinal) == true;
    }

    public static bool IsPotentialEquivalentConflict(Neo4jException exception)
    {
        return !IsRetryable(exception)
            && exception.Code is not null
            && PotentialEquivalentConflictCodes.Contains(exception.Code);
    }
}

internal static class Neo4jSchemaMetadata
{
    public static Neo4jSchemaObjectKind GetConstraintKind(string type)
    {
        return type switch
        {
            "UNIQUENESS" or "NODE_PROPERTY_UNIQUENESS" => Neo4jSchemaObjectKind.NodeUniquenessConstraint,
            "RELATIONSHIP_UNIQUENESS" or "RELATIONSHIP_PROPERTY_UNIQUENESS" => Neo4jSchemaObjectKind.RelationshipUniquenessConstraint,
            "NODE_PROPERTY_EXISTENCE" => Neo4jSchemaObjectKind.NodePropertyExistenceConstraint,
            "RELATIONSHIP_PROPERTY_EXISTENCE" => Neo4jSchemaObjectKind.RelationshipPropertyExistenceConstraint,
            _ => Neo4jSchemaObjectKind.Unknown
        };
    }

    public static Neo4jSchemaObjectKind GetIndexKind(string type)
    {
        return type switch
        {
            "RANGE" => Neo4jSchemaObjectKind.RangeIndex,
            "FULLTEXT" => Neo4jSchemaObjectKind.FullTextIndex,
            _ => Neo4jSchemaObjectKind.Unknown
        };
    }

    public static Neo4jSchemaEntityType GetEntityType(string entityType)
    {
        return entityType switch
        {
            "NODE" => Neo4jSchemaEntityType.Node,
            "RELATIONSHIP" => Neo4jSchemaEntityType.Relationship,
            _ => throw new ArgumentOutOfRangeException(nameof(entityType), entityType, "Unknown Neo4j schema entity type.")
        };
    }
}
