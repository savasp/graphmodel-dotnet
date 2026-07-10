// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph;

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
