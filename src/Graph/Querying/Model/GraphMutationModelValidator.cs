// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying;

/// <summary>Validates semantic invariants for a set-based graph mutation.</summary>
public static class GraphMutationModelValidator
{
    /// <summary>Validates a graph mutation model.</summary>
    /// <param name="model">The mutation model.</param>
    public static void Validate(GraphMutationModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        GraphElementSelectionModelValidator.Validate(model.Selection);

        if (model.Selection.Mode != GraphElementSelectionMode.Set)
        {
            throw new GraphException("A set-based mutation requires set selection mode.");
        }

        if (model.Kind == GraphMutationKind.Update && model.Assignments.Count == 0)
        {
            throw new GraphException("A graph update requires at least one property assignment.");
        }

        if (model.Kind == GraphMutationKind.Delete && model.Assignments.Count > 0)
        {
            throw new GraphException("A graph delete cannot carry property assignments.");
        }

        if (model.Kind == GraphMutationKind.Update && model.CascadeDelete)
        {
            throw new GraphException("Cascade delete is not valid for a graph update.");
        }

        if (model.CascadeDelete && model.Selection.ElementKind != GraphElementKind.Node)
        {
            throw new GraphException("Cascade delete is only valid for node selections.");
        }

        var duplicate = model.Assignments
            .GroupBy(assignment => assignment.StorageName, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicate is not null)
        {
            throw new GraphQueryTranslationException(
                $"The mapped storage property '{duplicate}' is assigned more than once.");
        }
    }
}
