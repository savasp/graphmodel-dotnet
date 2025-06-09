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

namespace Cvoya.Graph.Model.Neo4j.Serialization;

/// <summary>
/// Registry for entity serializers
/// </summary>
public static class EntitySerializerRegistry
{
    private static readonly Dictionary<Type, EntitySerializerBase> _serializers = new();

    /// <summary>
    /// Registers a serializer for a specific type
    /// </summary>
    public static void Register<T>(EntitySerializerBase serializer) where T : IEntity
    {
        _serializers[typeof(T)] = serializer;
    }

    /// <summary>
    /// Gets a serializer for the specified type
    /// </summary>
    public static EntitySerializerBase? GetSerializer(Type type)
    {
        return _serializers.TryGetValue(type, out var serializer) ? serializer : null;
    }

    /// <summary>
    /// Gets a serializer for the specified type
    /// </summary>
    public static EntitySerializerBase? GetSerializer<T>() where T : IEntity
    {
        return GetSerializer(typeof(T));
    }
}