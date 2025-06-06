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

using System.Runtime.CompilerServices;

namespace Cvoya.Graph.Model.Neo4j.Linq;

internal static class TypeExtensions
{
    public static bool IsAnonymousType(this Type type)
    {
        return type.IsClass
            && type.IsSealed
            && type.IsNotPublic
            && (string.IsNullOrEmpty(type.Namespace) || type.Namespace.StartsWith("<>"))
            && type.GetCustomAttributes(typeof(CompilerGeneratedAttribute), false).Length > 0
            && type.Name.Contains("AnonymousType");
    }
}