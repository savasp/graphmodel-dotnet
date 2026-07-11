// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.InMemory;

/// <summary>
/// Populates provider-managed runtime metadata (<c>INode.Labels</c>, <c>IRelationship.Type</c>)
/// on caller instances after a successful create, matching the public contract that these
/// properties are empty before persistence and reflect stored values afterwards. Population is
/// best-effort via the concrete type's setter; dynamic entities manage their own metadata.
/// </summary>
internal static class RuntimeMetadata
{
    /// <summary>Sets the stored labels on the created node instance when possible.</summary>
    public static void PopulateNodeLabels(INode node, IReadOnlyList<string> labels)
    {
        if (node is DynamicNode)
        {
            return;
        }

        var property = node.GetType().GetProperty(nameof(INode.Labels));
        if (property?.CanWrite == true && property.PropertyType.IsAssignableFrom(typeof(List<string>)))
        {
            property.SetValue(node, labels.ToList());
        }
    }

    /// <summary>Sets the stored relationship type on the created instance when it was unset.</summary>
    public static void PopulateRelationshipType(IRelationship relationship, string type)
    {
        if (relationship is DynamicRelationship || !string.IsNullOrEmpty(relationship.Type))
        {
            return;
        }

        var property = relationship.GetType().GetProperty(nameof(IRelationship.Type));
        if (property?.CanWrite == true && property.PropertyType == typeof(string))
        {
            property.SetValue(relationship, type);
        }
    }
}
