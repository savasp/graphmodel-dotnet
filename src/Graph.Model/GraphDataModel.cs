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

namespace Cvoya.Graph.Model;

/// <summary>
/// Provides predicates for determining simple and complex types in the graph data model.
/// </summary>
public static class GraphDataModel
{
    private static readonly HashSet<Type> SupportedSimpleTypes =
    [
        typeof(bool), typeof(bool?),
        typeof(byte), typeof(byte?),
        typeof(sbyte), typeof(sbyte?),
        typeof(short), typeof(short?),
        typeof(ushort), typeof(ushort?),
        typeof(int), typeof(int?),
        typeof(uint), typeof(uint?),
        typeof(long), typeof(long?),
        typeof(ulong), typeof(ulong?),
        typeof(decimal), typeof(decimal?),
        typeof(float), typeof(float?),
        typeof(double), typeof(double?),
        typeof(char), typeof(char?),
        typeof(string),
        typeof(DateTime), typeof(DateTime?),
        typeof(DateTimeOffset), typeof(DateTimeOffset?),
        typeof(TimeSpan), typeof(TimeSpan?),
        typeof(DateOnly), typeof(DateOnly?),
        typeof(TimeOnly), typeof(TimeOnly?),
        typeof(Guid), typeof(Guid?),
        typeof(Uri),
        typeof(Point), typeof(Point?)
    ];

    /// <summary>
    /// Determines if a type is a simple type supported by the graph data model.
    /// Simple types are primitive types, strings, date/time types, and other basic types
    /// that can be directly stored in graph databases.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>True if the type is a simple type, false otherwise.</returns>
    public static bool IsSimple(Type type)
    {
        // Check if it's a supported simple type
        if (SupportedSimpleTypes.Contains(type))
            return true;

        // Check for enums
        if (type.IsEnum || (Nullable.GetUnderlyingType(type)?.IsEnum == true))
            return true;

        // Check for single-dimensional arrays of simple types
        if (type.IsArray && type.GetArrayRank() == 1)
            return IsSimple(type.GetElementType()!);

        // Check for generic collections of simple types
        if (type.IsGenericType && IsCollectionType(type))
        {
            var elementType = type.GetGenericArguments().FirstOrDefault();
            return elementType != null && IsSimple(elementType);
        }

        return false;
    }

    /// <summary>
    /// Determines if a type is a complex type supported by the graph data model.
    /// Complex types are user-defined classes that can be serialized as nested objects
    /// in graph databases, but have specific constraints.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>True if the type is a valid complex type, false otherwise.</returns>
    public static bool IsComplex(Type type)
    {
        // Simple types are not complex
        if (IsSimple(type))
            return false;

        // Check for collections of complex types
        if (type.IsArray && type.GetArrayRank() == 1)
        {
            var elementType = type.GetElementType()!;
            return IsComplex(elementType);
        }

        if (type.IsGenericType && IsCollectionType(type))
        {
            var elementType = type.GetGenericArguments().FirstOrDefault();
            return elementType != null && IsComplex(elementType);
        }

        // Must be a class (not struct, interface, delegate, etc.)
        if (!type.IsClass || type.IsAbstract || type.IsInterface)
            return false;

        // Cannot be INode or IRelationship
        if (IsNodeOrRelationshipType(type))
            return false;

        // Must have a parameterless constructor
        if (!HasParameterlessConstructor(type))
            return false;

        // All properties must be simple types
        var properties = type.GetProperties()
            .Where(p => p.CanRead && p.CanWrite && p.GetGetMethod()?.IsPublic == true && p.GetSetMethod()?.IsPublic == true);

        return properties.All(p => IsSimple(p.PropertyType));
    }

    private static bool IsCollectionType(Type type)
    {
        if (!type.IsGenericType)
            return false;

        var genericTypeDefinition = type.GetGenericTypeDefinition();
        
        return genericTypeDefinition == typeof(List<>) ||
               genericTypeDefinition == typeof(HashSet<>) ||
               genericTypeDefinition == typeof(SortedSet<>) ||
               genericTypeDefinition == typeof(LinkedList<>) ||
               genericTypeDefinition == typeof(Queue<>) ||
               genericTypeDefinition == typeof(Stack<>) ||
               genericTypeDefinition == typeof(IList<>) ||
               genericTypeDefinition == typeof(ICollection<>) ||
               genericTypeDefinition == typeof(IEnumerable<>) ||
               genericTypeDefinition == typeof(IReadOnlyCollection<>) ||
               genericTypeDefinition == typeof(IReadOnlyList<>) ||
               genericTypeDefinition == typeof(ISet<>) ||
               genericTypeDefinition == typeof(IReadOnlySet<>);
    }

    private static bool IsNodeOrRelationshipType(Type type)
    {
        return type.GetInterfaces().Any(i => 
            i.Name == "INode" || 
            i.Name == "IRelationship" ||
            (i.IsGenericType && i.GetGenericTypeDefinition().Name == "IRelationship"));
    }

    private static bool HasParameterlessConstructor(Type type)
    {
        var constructors = type.GetConstructors();
        
        // If no explicit constructors, C# provides a default parameterless constructor
        if (!constructors.Any())
            return true;

        // Check for parameterless constructor (public or internal)
        return constructors.Any(c => c.GetParameters().Length == 0 && (c.IsPublic || c.IsAssembly));
    }
}