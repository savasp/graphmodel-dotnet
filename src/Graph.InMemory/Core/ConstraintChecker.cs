// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.InMemory;

/// <summary>
/// Enforces the declarative <c>[Property(IsUnique = ...)]</c> and <c>[Property(IsKey = ...)]</c>
/// constraints the reference provider enforces through database constraints: per-property
/// uniqueness, and composite-key uniqueness across all key properties, scoped to entities of the
/// same label. Checks run inside the mutation so they see the transaction's own writes and are
/// re-validated against the latest committed state on commit.
/// </summary>
internal static class ConstraintChecker
{
    /// <summary>The constraint-relevant property names for one entity schema.</summary>
    public sealed record Constraints(IReadOnlyList<string> UniqueProperties, IReadOnlyList<string> KeyProperties)
    {
        /// <summary>Gets whether there is anything to enforce.</summary>
        public bool IsEmpty => UniqueProperties.Count == 0 && KeyProperties.Count == 0;
    }

    /// <summary>Reads the constraint-relevant property names from a schema, if any.</summary>
    public static Constraints? From(EntitySchemaInfo? schema)
    {
        if (schema is null)
        {
            return null;
        }

        var unique = new List<string>();
        var keys = new List<string>();
        foreach (var property in schema.Properties.Values)
        {
            var name = string.IsNullOrEmpty(property.Name) ? property.PropertyInfo.Name : property.Name;
            if (property.IsKey)
            {
                // Key properties are unique only as a composite; the registry also flags them
                // IsUnique, but enforcing that per-property would reject legal rows that differ
                // in another key part.
                keys.Add(name);
            }
            else if (property.IsUnique)
            {
                unique.Add(name);
            }
        }

        var constraints = new Constraints(unique, keys);
        return constraints.IsEmpty ? null : constraints;
    }

    /// <summary>Checks a node about to be written against all stored nodes of its label.</summary>
    public static void CheckNode(StoreState state, NodeRecord record, Constraints constraints)
    {
        var others = state.Nodes.Values.Where(n =>
            !n.IsComplexValue &&
            string.Equals(n.Label, record.Label, StringComparison.Ordinal) &&
            n.Id != record.Id);

        Check(others.Select(n => n.Properties), record.Properties, constraints, record.Label);
    }

    /// <summary>Checks a relationship about to be written against all stored ones of its type.</summary>
    public static void CheckRelationship(StoreState state, RelationshipRecord record, Constraints constraints)
    {
        var others = state.Relationships.Values.Where(r =>
            !r.IsComplexProperty &&
            string.Equals(r.Type, record.Type, StringComparison.Ordinal) &&
            r.Id != record.Id);

        Check(others.Select(r => r.Properties), record.Properties, constraints, record.Type);
    }

    private static void Check(
        IEnumerable<IReadOnlyDictionary<string, StoredProperty>> others,
        IReadOnlyDictionary<string, StoredProperty> candidate,
        Constraints constraints,
        string label)
    {
        foreach (var other in others)
        {
            foreach (var property in constraints.UniqueProperties)
            {
                var value = ValueOf(candidate, property);
                if (value is not null && Equals(value, ValueOf(other, property)))
                {
                    throw new GraphException(
                        $"Unique constraint violated: a {label} with {property} = '{value}' already exists.");
                }
            }

            if (constraints.KeyProperties.Count > 0 &&
                constraints.KeyProperties.All(p =>
                    ValueOf(candidate, p) is { } value && Equals(value, ValueOf(other, p))))
            {
                var key = string.Join(", ", constraints.KeyProperties.Select(p => $"{p} = '{ValueOf(candidate, p)}'"));
                throw new GraphException($"Key constraint violated: a {label} with {key} already exists.");
            }
        }
    }

    private static object? ValueOf(IReadOnlyDictionary<string, StoredProperty> properties, string name) =>
        properties.TryGetValue(name, out var property) ? property.Value : null;
}
