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

namespace Cvoya.Graph.Model.Neo4j.Linq.Helpers;

using System.Collections;
using System.Runtime.CompilerServices;


internal static class TypeHelpers
{
    /// <summary>
    /// Gets the element type from a sequence type (<see cref="IEnumerable{T}"/>, <see cref="IQueryable{T}"/>, etc.).
    /// </summary>
    public static Type GetElementType(Type sequenceType)
    {
        // Check if it's already the element type (not a sequence)
        if (!IsSequenceType(sequenceType))
            return sequenceType;

        // Check if it's an array
        if (sequenceType.IsArray)
            return sequenceType.GetElementType()!;

        // Check for IEnumerable<T> or IQueryable<T>
        var ienum = FindIEnumerable(sequenceType);
        if (ienum != null)
            return ienum.GetGenericArguments()[0];

        // Fallback - return the type itself
        return sequenceType;
    }

    private static bool IsSequenceType(Type type)
    {
        return type != typeof(string) &&
               type != typeof(byte[]) &&
               typeof(IEnumerable).IsAssignableFrom(type);
    }

    private static Type? FindIEnumerable(Type sequenceType)
    {
        if (sequenceType.IsGenericType &&
            sequenceType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            return sequenceType;
        }

        return sequenceType
            .GetInterfaces()
            .FirstOrDefault(t => t.IsGenericType &&
                               t.GetGenericTypeDefinition() == typeof(IEnumerable<>));
    }

    public static bool IsAnonymousType(Type type)
    {
        return type.IsClass
            && type.IsSealed
            && type.IsNotPublic
            && (string.IsNullOrEmpty(type.Namespace) || type.Namespace.StartsWith("<>"))
            && type.GetCustomAttributes(typeof(CompilerGeneratedAttribute), false).Length > 0
            && type.Name.Contains("AnonymousType");
    }
}