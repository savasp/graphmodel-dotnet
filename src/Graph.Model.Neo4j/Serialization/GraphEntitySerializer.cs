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

using Microsoft.Extensions.Logging;

namespace Cvoya.Graph.Model.Neo4j.Serialization;

internal class GraphEntitySerializer(GraphContext context)
{
    private readonly ILogger<GraphEntitySerializer>? _logger = context.LoggerFactory?.CreateLogger<GraphEntitySerializer>();
    private readonly EntityFactory _entityFactory = new EntityFactory(context.LoggerFactory);

    public NodeSerializationResult SerializeNode(IEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var nodeType = entity.GetType();
        var label = Labels.GetLabelFromType(nodeType);

        var serializer = EntitySerializerRegistry.GetSerializer(nodeType) ?? throw new GraphException(
            $"No serializer found for type {nodeType.Name}. Ensure it is registered in the EntitySerializerRegistry.");

        var serializedNode = serializer.Serialize(entity);

        return new NodeSerializationResult
        {
            SerializedEntity = serializedNode,
            Label = label
        };
    }

    public object DeserializeNodeFromNeo4jNode(
            global::Neo4j.Driver.INode neo4jNode,
            Type targetType,
            bool useMostDerivedType = true)
    {
        ArgumentNullException.ThrowIfNull(neo4jNode);
        ArgumentNullException.ThrowIfNull(targetType);

        // Resolve the most derived type if requested
        if (useMostDerivedType)
        {
            // Use the label from the Neo4j node to find the most derived type
            var label = neo4jNode.Labels[0];
            var resolvedType = Labels.GetMostDerivedType(targetType, label)
                ?? throw new GraphException($"No type found for label '{label}' that is assignable to {targetType.Name}. " +
                    "Ensure the label matches a registered type in the GraphDataModel.");

            if (resolvedType != targetType)
            {
                targetType = resolvedType;
                _logger?.LogDebug($"Resolved type {resolvedType.Name} from labels {label} for requested type {targetType.Name}");
            }
        }

        return _entityFactory.CreateInstance(targetType, neo4jNode);
    }

    public RelationshipSerializationResult SerializeRelationship(IRelationship relationship)
    {
        ArgumentNullException.ThrowIfNull(relationship);

        var type = relationship.GetType();
        var relType = Labels.GetLabelFromType(type);

        var serializer = EntitySerializerRegistry.GetSerializer(relationship.GetType()) ?? throw new GraphException(
            $"No serializer found for type {relationship.GetType().Name}. Ensure it is registered in the EntitySerializerRegistry.");

        var serializedRelationship = serializer.Serialize(relationship);

        return new RelationshipSerializationResult
        {
            SerializedEntity = serializedRelationship,
            Type = relType,
            SourceId = relationship.StartNodeId,
            TargetId = relationship.EndNodeId
        };
    }

    public object DeserializeRelationshipFromNeo4jRelationship(
        global::Neo4j.Driver.IRelationship neo4jRelationship,
        Type targetType,
        bool useMostDerivedType = true)
    {
        ArgumentNullException.ThrowIfNull(neo4jRelationship);
        ArgumentNullException.ThrowIfNull(targetType);

        // Resolve the most derived type if requested
        if (useMostDerivedType)
        {
            // Use the label from the Neo4j relationship to find the most derived type
            var label = neo4jRelationship.Type;
            var resolvedType = Labels.GetMostDerivedType(targetType, label)
                ?? throw new GraphException($"No type found for label '{label}' that is assignable to {targetType.Name}. " +
                    "Ensure the label matches a registered type in the GraphDataModel.");

            if (resolvedType != targetType)
            {
                targetType = resolvedType;
                _logger?.LogDebug("Resolved type {ResolvedType} from labels {Labels} for requested type {RequestedType}",
                    resolvedType.Name, label, targetType.Name);
            }
        }

        // Create the relationship instance
        return _entityFactory.CreateInstance(targetType, neo4jRelationship);
    }
}