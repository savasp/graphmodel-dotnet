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
/// Defines validation rules for properties.
/// </summary>
public readonly record struct PropertyValidation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PropertyValidation"/> struct with optional validation parameters.
    /// </summary>
    /// <param name="minLength">Minimum length for string properties.</param>
    /// <param name="maxLength">Maximum length for string properties.</param>
    /// <param name="minValue">Minimum value for numeric properties.</param>
    /// <param name="maxValue">Maximum value for numeric properties.</param>
    /// <param name="pattern">Regular expression pattern for string validation.</param>
    public PropertyValidation(
        int? minLength = null,
        int? maxLength = null,
        object? minValue = null,
        object? maxValue = null,
        string? pattern = null)
    {
        MinLength = minLength;
        MaxLength = maxLength;
        MinValue = minValue;
        MaxValue = maxValue;
        Pattern = pattern;
    }

    /// <summary>
    /// Gets or sets the minimum length for string properties.
    /// </summary>
    /// <value>The minimum length for string properties, or null if not specified.</value>
    public int? MinLength { get; init; } = null;

    /// <summary>
    /// Gets or sets the maximum length for string properties.
    /// </summary>
    /// <value>The maximum length for string properties, or null if not specified.</value>
    public int? MaxLength { get; init; } = null;

    /// <summary>
    /// Gets or sets the minimum value for numeric properties.
    /// </summary>
    /// <value>The minimum value for numeric properties, or null if not specified.</value>
    public object? MinValue { get; init; } = null;

    /// <summary>
    /// Gets or sets the maximum value for numeric properties.
    /// </summary>
    /// <value>The maximum value for numeric properties, or null if not specified.</value>
    public object? MaxValue { get; init; } = null;

    /// <summary>
    /// Gets or sets a regular expression pattern for string validation.
    /// </summary>
    /// <value>The regular expression pattern for string validation, or null if not specified.</value>
    public string? Pattern { get; init; } = null;
}
