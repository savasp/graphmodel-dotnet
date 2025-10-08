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

public record MemoryWithoutSourceProperty : Node
{
    [Property(IsRequired = true)]
    public required DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    [Property(IsRequired = true)]
    public required DateTime UpdatedAt { get; init; } = DateTime.UtcNow;

    [Property(IsRequired = true)]
    public required Point Location { get; init; } = new Point { Longitude = 0, Latitude = 0, Height = 0 };

    [Property(IsRequired = true)]
    public required bool Deleted { get; init; } = false;

    [Property(IsRequired = true)]
    public string Text { get; init; } = string.Empty;
}
