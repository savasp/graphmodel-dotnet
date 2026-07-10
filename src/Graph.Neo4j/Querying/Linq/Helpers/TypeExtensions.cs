// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Linq.Helpers;

internal static class TypeExtensions
{
    public static bool IsAssignableFromGeneric(this Type genericType, Type type)
    {
        if (!genericType.IsGenericTypeDefinition)
            return genericType.IsAssignableFrom(type);

        if (!type.IsGenericType)
            return false;

        var typeDef = type.GetGenericTypeDefinition();
        return typeDef == genericType;
    }
}