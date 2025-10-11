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

public record Email : Memory
{
    [Property(IsRequired = true)]
    public required string Subject { get; init; }

    [Property(IsRequired = true)]
    public required string From { get; init; }

    public required List<string> To { get; init; } = new();
    public required List<string> Cc { get; init; } = new();
    public required List<string> Bcc { get; init; } = new();
    public string? MessageId { get; init; }
    public string? ThreadId { get; init; }

    [Property(IsRequired = true)]
    public string Body { get; init; } = string.Empty;

    public string? BodyHtml { get; init; }
    public bool IsRead { get; init; } = false;
    public bool IsImportant { get; init; } = false;
    public DateTime SentAt { get; init; }
    public DateTime? ReceivedAt { get; init; }
    public string? ExternalId { get; init; } // Original ID from the service
    public string? ServiceType { get; init; } // "Google" or "Microsoft365"
}
