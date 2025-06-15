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

namespace Cvoya.Graph.Model.Neo4j.Querying.Cypher.Execution;

internal static class Helpers
{
    public static Type GetTargetTypeIfCollection(Type type)
    {
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