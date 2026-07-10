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

namespace Cvoya.Graph.Querying;

internal static class QueryModelGuard
{
    public static IReadOnlyList<T> CopyRequiredList<T>(IReadOnlyList<T> values, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);

        var copy = new T[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            copy[i] = values[i] ?? throw new ArgumentException($"The {parameterName} collection cannot contain null elements.", parameterName);
        }

        return Array.AsReadOnly(copy);
    }

    public static void RequireNullOrNotWhiteSpace(string? value, string parameterName)
    {
        if (value is not null && string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("The value cannot be empty or whitespace.", parameterName);
        }
    }

    public static void RequireDefinedEnum<T>(T value, string parameterName)
        where T : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, $"Unknown {typeof(T).Name} value.");
        }
    }

    public static void RequireAssignableTo(Type type, Type expectedBaseType, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(type, parameterName);

        if (!expectedBaseType.IsAssignableFrom(type))
        {
            throw new ArgumentException($"Type '{type.FullName}' must be assignable to '{expectedBaseType.FullName}'.", parameterName);
        }
    }
}
