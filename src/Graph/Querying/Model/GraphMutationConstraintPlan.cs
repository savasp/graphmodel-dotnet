// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying;

using System.Collections;
using System.Reflection;

/// <summary>
/// Freezes the schema constraints affected by one typed mutation before provider execution.
/// </summary>
internal sealed class GraphMutationConstraintPlan
{
    private GraphMutationConstraintPlan(
        GraphElementKind elementKind,
        string labelOrType,
        IReadOnlyList<GraphMutationConstraint> constraints)
    {
        ElementKind = elementKind;
        LabelOrType = labelOrType;
        Constraints = constraints;
        Properties = constraints
            .SelectMany(constraint => constraint.Properties)
            .DistinctBy(property => property.StorageName, StringComparer.Ordinal)
            .ToArray();
    }

    public GraphElementKind ElementKind { get; }

    public string LabelOrType { get; }

    public IReadOnlyList<GraphMutationConstraint> Constraints { get; }

    public IReadOnlyList<GraphMutationConstraintProperty> Properties { get; }

    public bool IsEmpty => Constraints.Count == 0;

    public static GraphMutationConstraintPlan Create(
        GraphMutationModel mutation,
        SchemaRegistry schemaRegistry)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        ArgumentNullException.ThrowIfNull(schemaRegistry);

        var hasAffectedConstraint = mutation.Assignments.Any(assignment =>
        {
            var attribute = assignment.Property?.GetCustomAttribute<PropertyAttribute>(inherit: true);
            return attribute?.IsKey == true || attribute?.IsUnique == true;
        });
        if (!hasAffectedConstraint)
        {
            return new GraphMutationConstraintPlan(
                mutation.Selection.ElementKind,
                string.Empty,
                []);
        }

        var entityType = GetEntityType(mutation.Selection.Query.Root);
        var labelOrType = Labels.GetLabelFromType(entityType);
        var schema = mutation.Selection.ElementKind == GraphElementKind.Node
            ? schemaRegistry.GetNodeSchema(labelOrType)
            : schemaRegistry.GetRelationshipSchema(labelOrType);
        if (schema is null)
        {
            throw new GraphException(
                $"No schema is registered for graph mutation type '{entityType.FullName}'.");
        }

        var assignments = mutation.Assignments.ToDictionary(
            assignment => assignment.StorageName,
            StringComparer.Ordinal);
        var affectedSchemaProperties = mutation.Assignments
            .Select(assignment => assignment.Property is not null &&
                schema.Properties.TryGetValue(assignment.Property.Name, out var property)
                    ? property
                    : null)
            .OfType<PropertySchemaInfo>()
            .ToArray();
        var constraints = new List<GraphMutationConstraint>();
        if (affectedSchemaProperties.Any(property => property.IsKey))
        {
            constraints.Add(new GraphMutationConstraint(
                "composite key",
                schema.GetKeyProperties()
                    .Select(property => CreateProperty(property, assignments))
                    .ToArray(),
                RequiresValues: true));
        }

        foreach (var property in affectedSchemaProperties.Where(property =>
                     property.IsUnique && (!property.IsKey || schema.HasCompositeKey())))
        {
            constraints.Add(new GraphMutationConstraint(
                $"unique property '{property.Name}'",
                [CreateProperty(property, assignments)],
                RequiresValues: false));
        }

        return new GraphMutationConstraintPlan(
            mutation.Selection.ElementKind,
            schema.Label,
            constraints);
    }

    public IReadOnlyList<GraphMutationConstraintValue> ValidateFinalValues(
        IReadOnlyList<GraphMutationConstraintRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var result = new List<GraphMutationConstraintValue>();
        foreach (var constraint in Constraints)
        {
            var seen = new List<IReadOnlyList<object?>>();
            foreach (var row in rows)
            {
                var values = constraint.Properties
                    .Select(property => row.Values.TryGetValue(property.StorageName, out var value)
                        ? value
                        : throw new GraphException(
                            $"The mutation preflight did not return constrained property '{property.StorageName}'."))
                    .ToArray();
                if (constraint.RequiresValues && values.Any(value =>
                        value is null || value is string text && string.IsNullOrWhiteSpace(text)))
                {
                    throw new GraphException(
                        $"The {ElementName} '{LabelOrType}' key must contain non-null, non-empty values.");
                }

                if (constraint.Ignores(values))
                {
                    continue;
                }

                if (seen.Any(previous => GraphMutationConstraint.Matches(previous, values)))
                {
                    throw CreateViolation(constraint);
                }

                seen.Add(values);
                result.Add(new GraphMutationConstraintValue(constraint, values));
            }
        }

        return result;
    }

    public static void ValidateTargetRows(
        IReadOnlyList<object> expectedIdentities,
        IReadOnlyList<GraphMutationConstraintRow> rows)
    {
        ArgumentNullException.ThrowIfNull(expectedIdentities);
        ArgumentNullException.ThrowIfNull(rows);

        var expected = expectedIdentities.ToHashSet();
        var actual = rows.Select(row => row.NativeIdentity).ToHashSet();
        if (expected.Count != expectedIdentities.Count ||
            actual.Count != rows.Count ||
            !expected.SetEquals(actual))
        {
            throw new GraphException(
                "The constrained mutation target set changed before final-state validation could lock it.");
        }
    }

    public GraphException CreateViolation(GraphMutationConstraint constraint) =>
        new($"{char.ToUpperInvariant(ElementName[0])}{ElementName[1..]} '{LabelOrType}' violates {constraint.Description} uniqueness.");

    private string ElementName => ElementKind == GraphElementKind.Node ? "node" : "relationship";

    private static GraphMutationConstraintProperty CreateProperty(
        PropertySchemaInfo property,
        IReadOnlyDictionary<string, GraphPropertyAssignment> assignments) =>
        new(
            property.Name,
            assignments.GetValueOrDefault(property.Name));

    private static Type GetEntityType(QueryRoot root) => root switch
    {
        NodeRoot node => node.ElementType,
        RelationshipRoot relationship => relationship.ElementType,
        SearchRoot { ElementType: { } elementType } => elementType,
        DynamicRoot { ElementType: { } elementType } => elementType,
        _ => throw new GraphQueryTranslationException(
            $"Query root '{root.GetType().Name}' does not have one typed graph element schema."),
    };
}

internal sealed record GraphMutationConstraint(
    string Description,
    IReadOnlyList<GraphMutationConstraintProperty> Properties,
    bool RequiresValues)
{
    public bool Ignores(IReadOnlyList<object?> values) =>
        !RequiresValues && values.Count == 1 && values[0] is null;

    public static bool Matches(IReadOnlyList<object?> left, IReadOnlyList<object?> right) =>
        left.Count == right.Count && left
            .Zip(right)
            .All(pair => GraphMutationConstraintValueComparer.Equals(pair.First, pair.Second));
}

internal sealed record GraphMutationConstraintProperty(
    string StorageName,
    GraphPropertyAssignment? Assignment);

internal sealed record GraphMutationConstraintRow(
    object NativeIdentity,
    IReadOnlyDictionary<string, object?> Values);

internal sealed record GraphMutationConstraintValue(
    GraphMutationConstraint Constraint,
    IReadOnlyList<object?> Values);

internal static class GraphMutationConstraintValueComparer
{
    public static new bool Equals(object? left, object? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        if (left is IEnumerable leftSequence && right is IEnumerable rightSequence &&
            left is not string && right is not string)
        {
            var leftEnumerator = leftSequence.GetEnumerator();
            var rightEnumerator = rightSequence.GetEnumerator();
            while (true)
            {
                var hasLeft = leftEnumerator.MoveNext();
                var hasRight = rightEnumerator.MoveNext();
                if (!hasLeft || !hasRight)
                {
                    return hasLeft == hasRight;
                }

                if (!Equals(leftEnumerator.Current, rightEnumerator.Current))
                {
                    return false;
                }
            }
        }

        return object.Equals(left, right);
    }
}
