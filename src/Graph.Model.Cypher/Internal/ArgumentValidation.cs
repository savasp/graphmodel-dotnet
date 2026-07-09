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

using System.Collections.ObjectModel;

namespace Cvoya.Graph.Model.Cypher.Internal;

internal static class ArgumentValidation
{
    internal static IReadOnlyList<T> RequiredList<T>(IReadOnlyList<T>? values, string parameterName)
        where T : class
    {
        var copy = List(values, parameterName);

        if (copy.Count == 0)
        {
            throw new ArgumentException("The collection cannot be empty.", parameterName);
        }

        return copy;
    }

    internal static IReadOnlyList<T> List<T>(IReadOnlyList<T>? values, string parameterName)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);

        var copy = values.ToArray();
        if (copy.Any(value => value is null))
        {
            throw new ArgumentException("The collection cannot contain null values.", parameterName);
        }

        return new ReadOnlyCollection<T>(copy);
    }

    internal static IReadOnlyList<string> RequiredStringList(IReadOnlyList<string>? values, string parameterName)
    {
        var copy = StringList(values, parameterName);

        if (copy.Count == 0)
        {
            throw new ArgumentException("The collection cannot be empty.", parameterName);
        }

        return copy;
    }

    internal static IReadOnlyList<string> StringList(IReadOnlyList<string>? values, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);

        var copy = values.ToArray();
        if (copy.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("The collection cannot contain null, empty, or whitespace values.", parameterName);
        }

        return new ReadOnlyCollection<string>(copy);
    }

    internal static string RequiredName(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("The value cannot be null, empty, or whitespace.", parameterName);
        }

        return value;
    }

    internal static string? OptionalName(string? value, string parameterName)
    {
        if (value is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("The value cannot be empty or whitespace.", parameterName);
        }

        return value;
    }

    internal static TEnum DefinedEnum<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "The value is not defined.");
        }

        return value;
    }
}
