// Copyright 2025 Savas Parastatidis
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      https://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace Cvoya.Graph.Model.Age.Core.Entities;

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cvoya.Graph.Model;

/// <summary>
/// Validates attribute-driven constraints for AGE entities before persisting changes.
/// Delegates to specialized validators for property rules, unique constraints,
/// conflict logging, and Cypher query building.
/// </summary>
internal static class AgeEntityAttributeValidator
{
    public static async Task ValidateNodeAsync<TNode>(
        TNode node,
        AgeGraphContext context,
        AgeGraphTransaction transaction,
        bool isUpdate,
        CancellationToken cancellationToken)
        where TNode : INode
    {
        ArgumentNullException.ThrowIfNull(node);
        await EnsureSchemaInitializedAsync(context.SchemaRegistry, cancellationToken).ConfigureAwait(false);

        // For DynamicNode, use the explicitly set Labels instead of CLR type name
        List<string> schemaLabels;
        if (node is DynamicNode dynamicNode)
        {
            schemaLabels = dynamicNode.Labels.ToList();
        }
        else
        {
            schemaLabels = [Labels.GetLabelFromType(node.GetType())];
        }

        foreach (var schemaLabel in schemaLabels)
        {
            if (context.SchemaRegistry.GetNodeSchema(schemaLabel) is not { } schema)
                continue;

            if (node is DynamicNode dynNode)
                PropertyRuleValidator.ValidateDynamicPropertyRules(dynNode.Properties, schema, schemaLabel);
            else
                PropertyRuleValidator.ValidatePropertyRules(node, schema, schemaLabel);

            if (schema.HasCompositeKey() || schema.Properties.Values.Any(p => p.IsUnique))
            {
                var nodeType = node.GetType();
                await UniqueConstraintValidator.ValidateUniqueConstraintsAsync(
                        entityId: node.Id,
                        entity: node,
                        schema: schema,
                        context: context,
                        transaction: transaction,
                        alias: "n",
                        labelForMatch: Labels.GetBaseTypeLabel(nodeType),
                        entityDisplayName: schema.Label,
                        isRelationship: false,
                        isUpdate: isUpdate,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    public static async Task ValidateRelationshipAsync<TRelationship>(
        TRelationship relationship,
        AgeGraphContext context,
        AgeGraphTransaction transaction,
        bool isUpdate,
        CancellationToken cancellationToken)
        where TRelationship : IRelationship
    {
        ArgumentNullException.ThrowIfNull(relationship);
        await EnsureSchemaInitializedAsync(context.SchemaRegistry, cancellationToken).ConfigureAwait(false);

        // For DynamicRelationship, use the explicitly set Type instead of CLR type name
        List<string> schemaLabels;
        if (relationship is DynamicRelationship dynamicRel)
        {
            schemaLabels = [dynamicRel.Type];
        }
        else
        {
            schemaLabels = [Labels.GetLabelFromType(relationship.GetType())];
        }

        foreach (var schemaLabel in schemaLabels)
        {
            if (context.SchemaRegistry.GetRelationshipSchema(schemaLabel) is not { } schema)
                continue;

            if (relationship is DynamicRelationship dynRel)
                PropertyRuleValidator.ValidateDynamicPropertyRules(dynRel.Properties, schema, schemaLabel);
            else
                PropertyRuleValidator.ValidatePropertyRules(relationship, schema, schemaLabel);

            if (schema.HasCompositeKey() || schema.Properties.Values.Any(p => p.IsUnique))
            {
                var relType = relationship.GetType();
                await UniqueConstraintValidator.ValidateUniqueConstraintsAsync(
                        entityId: relationship.Id,
                        entity: relationship,
                        schema: schema,
                        context: context,
                        transaction: transaction,
                        alias: "r",
                        labelForMatch: Labels.GetBaseTypeLabel(relType),
                        entityDisplayName: schema.Label,
                        isRelationship: true,
                        isUpdate: isUpdate,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private static async Task EnsureSchemaInitializedAsync(SchemaRegistry registry, CancellationToken cancellationToken)
    {
        if (!registry.IsInitialized)
        {
            await registry.InitializeAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
