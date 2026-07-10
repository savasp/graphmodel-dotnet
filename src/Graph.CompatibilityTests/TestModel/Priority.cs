// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

using System.Text.Json.Serialization;

[JsonConverter(typeof(JsonStringEnumConverter<Priority>))]
public enum Priority
{
    Low,
    Normal,
    High
}
