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

namespace Cvoya.Graph.Model.Tests;

using Cvoya.Graph.Model;

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
