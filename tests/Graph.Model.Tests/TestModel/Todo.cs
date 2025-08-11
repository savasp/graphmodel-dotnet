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

public record Todo : Memory
{
    [Property(Label = "note", IsRequired = true)]
    public required string Note { get; init; }

    [Property(Label = "done", IsRequired = true)]
    public required bool Done { get; init; } = false;

    [Property(Label = "due", IsRequired = true)]
    public required DateTime Due { get; init; } = DateTime.UtcNow;

    [Property(Label = "priority", IsRequired = true)]
    public required Priority Priority { get; init; } = Priority.Normal;

    // Additional fields for productivity service integration
    [Property(Label = "externalId")]
    public string? ExternalId { get; init; } // Original ID from the service (Google Tasks, M365 To Do)

    [Property(Label = "serviceType")]
    public string? ServiceType { get; init; } // "Google" or "Microsoft365" - null for local todos

    [Property(Label = "listId")]
    public string? ListId { get; init; } // ID of the list/project this todo belongs to

    [Property(Label = "listName")]
    public string? ListName { get; init; } // Name of the list/project

    [Property(Label = "completedAt")]
    public DateTime? CompletedAt { get; init; }

    [Property(Label = "categories")]
    public List<string> Categories { get; init; } = new();
}
