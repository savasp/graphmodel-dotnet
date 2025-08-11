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

using System.Text.Json.Serialization;
using Cvoya.Graph.Model;

public record Calendar : Memory
{
    [Property(IsRequired = true)]
    public required string Title { get; init; }

    [Property(IsRequired = true)]
    public required DateTime StartTime { get; init; }

    [Property(IsRequired = true)]
    public required DateTime EndTime { get; init; }

    public string? Description { get; init; }
    public bool IsAllDay { get; init; } = false;
    public CalendarStatus Status { get; init; } = CalendarStatus.Confirmed;
    public string? Organizer { get; init; }
    public string? MeetingUrl { get; init; }
    public string? CalendarId { get; init; }

    public string? ExternalId { get; init; } // Original ID from the service
    public string? ServiceType { get; init; } // "Google" or "Microsoft365"
    public string? RecurrenceRule { get; init; }
    public string? TimeZone { get; init; }
    public List<string> Categories { get; init; } = new();
    public CalendarVisibility Visibility { get; init; } = CalendarVisibility.Default;
}

[JsonConverter(typeof(JsonStringEnumConverter<CalendarStatus>))]
public enum CalendarStatus
{
    Tentative,
    Confirmed,
    Cancelled
}

[JsonConverter(typeof(JsonStringEnumConverter<AttendeeResponseStatus>))]
public enum AttendeeResponseStatus
{
    NeedsAction,
    Declined,
    Tentative,
    Accepted
}

[JsonConverter(typeof(JsonStringEnumConverter<CalendarVisibility>))]
public enum CalendarVisibility
{
    Default,
    Public,
    Private,
    Confidential
}