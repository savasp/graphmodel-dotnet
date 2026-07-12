// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

using System.Text.Json.Serialization;
using Cvoya.Graph;

public record Calendar : Memory
{
    [Property(IsRequired = true)]
    public required string Title { get; init; }

    [Property(IsRequired = true)]
    public required DateTime StartTime { get; init; }

    [Property(IsRequired = true)]
    public required DateTime EndTime { get; init; }

    public string? Description { get; init; }
    public bool IsAllDay { get; init; }
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