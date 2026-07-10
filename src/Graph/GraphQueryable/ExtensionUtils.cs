// Copyright CVOYA. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.
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

namespace Cvoya.Graph;

using System.Reflection;


internal static class ExtensionUtils
{
    public static MethodInfo GetGenericExtensionMethod(Type type, string name, int genericArgCount, int paramCount)
    {
        var methodInfo = type
            .GetMethods(BindingFlags.Static | BindingFlags.Public)
            .FirstOrDefault(m => m.Name == name
                && m.IsGenericMethodDefinition
                && m.GetGenericArguments().Length == genericArgCount
                && m.GetParameters().Length == paramCount);

        if (methodInfo == null)
            throw new InvalidOperationException($"Failed to find {name} extension method.");

        return methodInfo;
    }

    /// <summary>
    /// Gets the element type <c>T</c> from a closed <see cref="IGraphQueryable{T}"/> (or
    /// <see cref="IQueryable{T}"/>) type. Used by the two-arg traversal operators (issue #94,
    /// "Option C") to recover the actual start node type from a source expression's static type
    /// at the point in the chain where the operator is called - the type is fixed by whatever
    /// built that expression node (e.g. the root <c>Nodes&lt;Person&gt;()</c> call, or an upstream
    /// operator like <c>Where&lt;Person&gt;</c>), independent of the covariant widening to
    /// <see cref="IGraphQueryable{T}">IGraphQueryable&lt;INode&gt;</see> at the two-arg operator's
    /// own call site.
    /// </summary>
    public static Type GetQueryableElementType(Type queryableType)
    {
        if (queryableType.IsGenericType)
        {
            var definition = queryableType.GetGenericTypeDefinition();
            if (definition == typeof(IGraphQueryable<>) || definition == typeof(IQueryable<>) || definition == typeof(IEnumerable<>))
                return queryableType.GetGenericArguments()[0];
        }

        foreach (var iface in queryableType.GetInterfaces().Where(i => i.IsGenericType))
        {
            var definition = iface.GetGenericTypeDefinition();
            if (definition == typeof(IGraphQueryable<>) || definition == typeof(IQueryable<>))
                return iface.GetGenericArguments()[0];
        }

        throw new InvalidOperationException($"Could not determine the element type of '{queryableType}'.");
    }
}