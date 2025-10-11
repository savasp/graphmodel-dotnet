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

namespace Cvoya.Graph.Model.Neo4j.Entities;

using Cvoya.Graph.Model.Neo4j.Serialization;
using Cvoya.Graph.Model.Serialization;


internal static class SerializationHelpers
{
    public static Dictionary<string, object?> SerializeSimpleProperties(EntityInfo entity)
    {
        var properties = entity.SimpleProperties
            .Where(kv => kv.Value.Value is not null)
            .ToDictionary(
                kv => kv.Key,
                kv => kv.Value.Value switch
                {
                    SimpleValue simple => SerializationBridge.ToNeo4jValue(simple.Object),
                    SimpleCollection collection => collection.Values.Select(v => SerializationBridge.ToNeo4jValue(v.Object)),
                    _ => throw new GraphException("Unexpected value type in simple properties")
                });

        properties[nameof(Model.INode.Labels)] = entity.ActualLabels.Count > 0 ? entity.ActualLabels : Labels.GetCompatibleLabels(entity.ActualType);

        return properties;
    }
}