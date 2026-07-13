// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Serialization.Results;

internal static class GraphResultTypeHelpers
{
    public static Type GetTargetTypeIfCollection(Type type)
    {
        if (type.IsArray)
        {
            return type.GetElementType()!;
        }

        // If it's already not a collection type, return as-is
        if (!type.IsGenericType)
            return type;

        // Check if it's IEnumerable<T>, List<T>, etc.
        var genericTypeDefinition = type.GetGenericTypeDefinition();

        if (genericTypeDefinition == typeof(IEnumerable<>) ||
            genericTypeDefinition == typeof(List<>) ||
            genericTypeDefinition == typeof(IList<>) ||
            genericTypeDefinition == typeof(ICollection<>))
        {
            return type.GetGenericArguments()[0];
        }

        // Check if it implements IEnumerable<T>
        var enumerableInterface = type.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType &&
                                i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        if (enumerableInterface != null)
        {
            return enumerableInterface.GetGenericArguments()[0];
        }

        // If we can't determine the element type, return the original type
        return type;
    }
}
