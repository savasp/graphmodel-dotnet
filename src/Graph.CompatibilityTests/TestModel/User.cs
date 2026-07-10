// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

using Cvoya.Graph;

[Node("User")]
public record User : Node
{
    public required string? Name { get; init; }
    public required string? Email { get; init; }
    public required string? GoogleId { get; init; }
    public DateTime? DateOfBirth { get; set; }
    public string? Job { get; set; }
    public List<string>? Hobbies { get; set; }
    public List<string>? Preferences { get; set; }
    public List<string>? PersonalityTraits { get; set; }
    public List<string>? Interests { get; set; }
    public List<string>? Goals { get; set; }
}
