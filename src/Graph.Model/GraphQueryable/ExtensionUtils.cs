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

}