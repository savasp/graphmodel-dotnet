// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying;

/// <summary>Describes a provider-neutral set-based graph mutation.</summary>
public sealed record GraphMutationModel
{
    /// <summary>Initializes a graph mutation model.</summary>
    /// <param name="kind">The mutation kind.</param>
    /// <param name="selection">The frozen-target selection semantics.</param>
    /// <param name="assignments">Mapped assignments for an update.</param>
    /// <param name="cascadeDelete">Whether node deletion removes incident user relationships.</param>
    public GraphMutationModel(
        GraphMutationKind kind,
        GraphElementSelectionModel selection,
        IReadOnlyList<GraphPropertyAssignment> assignments,
        bool cascadeDelete)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        Kind = kind;
        Selection = selection ?? throw new ArgumentNullException(nameof(selection));
        Assignments = QueryModelGuard.CopyRequiredList(assignments, nameof(assignments));
        CascadeDelete = cascadeDelete;
    }

    /// <summary>Gets the mutation kind.</summary>
    public GraphMutationKind Kind { get; }

    /// <summary>Gets the graph-element selection.</summary>
    public GraphElementSelectionModel Selection { get; }

    /// <summary>Gets the mapped update assignments.</summary>
    public IReadOnlyList<GraphPropertyAssignment> Assignments { get; }

    /// <summary>Gets whether node deletion removes incident user relationships.</summary>
    public bool CascadeDelete { get; }
}
