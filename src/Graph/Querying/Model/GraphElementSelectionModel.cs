// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying;

/// <summary>Describes an entity-preserving graph-element selection for a command.</summary>
public sealed record GraphElementSelectionModel
{
    /// <summary>Initializes a graph-element selection.</summary>
    /// <param name="query">The provider-neutral read-query model that selects candidates.</param>
    /// <param name="mode">How the selected elements will be consumed.</param>
    public GraphElementSelectionModel(GraphQueryModel query, GraphElementSelectionMode mode)
    {
        Query = query ?? throw new ArgumentNullException(nameof(query));
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        Mode = mode;
        ElementKind = ResolveElementKind(query.Root);
    }

    /// <summary>Gets the underlying provider-neutral query model.</summary>
    public GraphQueryModel Query { get; }

    /// <summary>Gets how the selected elements will be consumed.</summary>
    public GraphElementSelectionMode Mode { get; }

    /// <summary>Gets the selected graph-element kind.</summary>
    public GraphElementKind ElementKind { get; }

    private static GraphElementKind ResolveElementKind(QueryRoot root) => root switch
    {
        NodeRoot => GraphElementKind.Node,
        RelationshipRoot => GraphElementKind.Relationship,
        SearchRoot { Target: SearchRootTarget.Nodes } => GraphElementKind.Node,
        SearchRoot { Target: SearchRootTarget.Relationships } => GraphElementKind.Relationship,
        DynamicRoot { ElementType: { } type } when typeof(INode).IsAssignableFrom(type) => GraphElementKind.Node,
        DynamicRoot { ElementType: { } type } when typeof(IRelationship).IsAssignableFrom(type) => GraphElementKind.Relationship,
        _ => throw new GraphQueryTranslationException(
            $"Query root '{root.GetType().Name}' does not select one node or relationship kind."),
    };
}
