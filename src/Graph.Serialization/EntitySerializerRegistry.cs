// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Serialization;

using System.Collections.Concurrent;


/// <summary>
/// Registry for entity serializers
/// </summary>
public class EntitySerializerRegistry
{
    private readonly ConcurrentDictionary<Type, IEntitySerializer> _serializers = new();

    // Proper singleton instance
    private static readonly Lazy<EntitySerializerRegistry> _instance = new(() => new EntitySerializerRegistry());

    private EntitySerializerRegistry()
    {
        // Private constructor to enforce singleton pattern
    }

    /// <summary>
    /// Gets the collection of registered serializers
    /// </summary>
    public static EntitySerializerRegistry Instance => _instance.Value;

    /// <summary>
    /// Registers a serializer for a specific type
    /// </summary>
    /// <typeparam name="T">The type of the entity</typeparam>
    /// <param name="serializer">The serializer instance</param>
    public void Register<T>(IEntitySerializer serializer) where T : IEntity
    {
        _serializers[typeof(T)] = serializer;
    }

    /// <summary>
    /// Registers a serializer for any type (including complex property types)
    /// </summary>
    public void Register(Type type, IEntitySerializer serializer)
    {
        _serializers[type] = serializer;
    }

    /// <summary>
    /// Gets a serializer for the specified type.
    /// </summary>
    /// <param name="type">The type for which we are getting the serializer</param>
    /// <returns>The serializer for the specified type, or null if not found</returns>
    public IEntitySerializer? GetSerializer(Type type)
    {
        return _serializers.TryGetValue(type, out var serializer) ? serializer : null;
    }

    /// <summary>
    /// Gets a serializer for the specified type.
    /// </summary>
    /// <typeparam name="T">The type for which we are getting the serializer</typeparam>
    /// <returns>The serializer for the specified type, or null if not found</returns>
    public IEntitySerializer? GetSerializer<T>() where T : IEntity
    {
        return GetSerializer(typeof(T));
    }

    /// <summary>
    /// Checks if a serializer for the specified type exists in the registry.
    /// </summary>
    /// <param name="type">The type to check for a serializer</param>
    /// <returns>True if a serializer exists for the specified type, otherwise false</returns>
    public bool ContainsType(Type type)
    {
        return _serializers.ContainsKey(type);
    }
}