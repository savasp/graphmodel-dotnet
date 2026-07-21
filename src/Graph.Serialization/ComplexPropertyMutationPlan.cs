// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Serialization;

using Cvoya.Graph.Querying;

/// <summary>
/// A provider-ready snapshot of the complex-property work in one set-based update. The supplied
/// CLR value graph is serialized once, before provider I/O, and providers create an independent
/// physical copy of <see cref="ReplacementEntity"/> below every frozen target.
/// </summary>
internal sealed record ComplexPropertyMutationPlan(
    EntityInfo ReplacementEntity,
    IReadOnlyList<string> RelationshipTypesToClear,
    IReadOnlyList<string> RootScalarPropertiesToClear)
{
    /// <summary>Gets whether the mutation must alter complex-property storage.</summary>
    public bool HasWork => RelationshipTypesToClear.Count > 0;

    /// <summary>Builds the serialized complex-property plan for a validated mutation.</summary>
    public static ComplexPropertyMutationPlan Create(EntityFactory entityFactory, GraphMutationModel mutation)
    {
        ArgumentNullException.ThrowIfNull(entityFactory);
        ArgumentNullException.ThrowIfNull(mutation);

        var complexProperties = new Dictionary<string, Property>(StringComparer.Ordinal);
        var relationshipTypes = new HashSet<string>(StringComparer.Ordinal);
        var rootScalarProperties = new HashSet<string>(StringComparer.Ordinal);
        var canOwnComplexProperties = mutation.Selection.ElementKind == GraphElementKind.Node;

        foreach (var assignment in mutation.Assignments)
        {
            if (assignment.Dynamic && canOwnComplexProperties)
            {
                // A dynamic key may change between scalar and complex representations. Always
                // remove a prior owned subtree before writing the new scalar representation.
                relationshipTypes.Add(GraphDataModel.PropertyNameToRelationshipTypeName(assignment.StorageName));
            }

            if (assignment is not GraphConstantPropertyAssignment { IsComplex: true } complex)
            {
                continue;
            }

            if (!canOwnComplexProperties)
            {
                throw new GraphQueryTranslationException(
                    "Graph relationships cannot own complex-property value nodes.");
            }

            Property property;
            if (complex.Dynamic)
            {
                if (complex.Value is null)
                {
                    throw new GraphException(
                        "A dynamic null assignment must be represented as a scalar null cleanup.");
                }

                property = entityFactory.SerializeDynamicComplexProperty(
                    complex.StorageName,
                    complex.Value);
                // The old representation may have been a scalar property on the root.
                rootScalarProperties.Add(complex.StorageName);
            }
            else
            {
                property = entityFactory.SerializeComplexProperty(
                    complex.Property ?? throw new GraphException(
                        $"Typed complex property '{complex.StorageName}' has no CLR property metadata."),
                    complex.StorageName,
                    complex.Value);
            }

            complexProperties.Add(complex.StorageName, property);
            relationshipTypes.Add(property.RelationshipType
                ?? GraphDataModel.PropertyNameToRelationshipTypeName(complex.StorageName));
        }

        var entityType = GetEntityType(mutation.Selection.Query.Root);
        return new ComplexPropertyMutationPlan(
            new EntityInfo(
                entityType,
                Labels.GetLabelFromType(entityType),
                [],
                new Dictionary<string, Property>(StringComparer.Ordinal),
                complexProperties),
            [.. relationshipTypes.Order(StringComparer.Ordinal)],
            [.. rootScalarProperties.Order(StringComparer.Ordinal)]);
    }

    private static Type GetEntityType(QueryRoot root) => root switch
    {
        NodeRoot node => node.ElementType,
        RelationshipRoot relationship => relationship.ElementType,
        SearchRoot { ElementType: { } elementType } => elementType,
        SearchRoot { Target: SearchRootTarget.Nodes } => typeof(INode),
        SearchRoot { Target: SearchRootTarget.Relationships } => typeof(IRelationship),
        DynamicRoot { ElementType: { } elementType } => elementType,
        _ => throw new GraphQueryTranslationException(
            $"Query root '{root.GetType().Name}' does not have one graph element type."),
    };
}
