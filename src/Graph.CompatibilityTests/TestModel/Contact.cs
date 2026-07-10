// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

using Cvoya.Graph;

public record Contact : Memory
{
    [Property(IsRequired = true)]
    public required string DisplayName { get; init; }

    public string? GivenName { get; init; }
    public string? FamilyName { get; init; }
    public string? MiddleName { get; init; }
    public string? CompanyName { get; init; }
    public string? JobTitle { get; init; }
    public string? Department { get; init; }
    public List<ContactEmail> EmailAddresses { get; init; } = new();
    public List<ContactPhone> PhoneNumbers { get; init; } = new();
    public List<ContactAddress> Addresses { get; init; } = new();
    public string? Birthday { get; init; } // ISO date string
    public string? Notes { get; init; }
    public string? PhotoUrl { get; init; }
    public string? ExternalId { get; init; } // Original ID from the service
    public string? ServiceType { get; init; } // "Google" or "Microsoft365"
    public List<string> Categories { get; init; } = new();
    public string? WebsiteUrl { get; init; }
    public List<ContactSocialProfile> SocialProfiles { get; init; } = new();
}

public record ContactEmail
{
    public required string Address { get; init; }
    public string Type { get; init; } = "other"; // work, home, other
    public bool IsPrimary { get; init; } = false;
}

public record ContactPhone
{
    public required string Number { get; init; }
    public string Type { get; init; } = "other"; // work, home, mobile, fax, other
    public bool IsPrimary { get; init; } = false;
}

public record ContactAddress
{
    public string? Street { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? PostalCode { get; init; }
    public string? Country { get; init; }
    public string Type { get; init; } = "other"; // work, home, other
    public bool IsPrimary { get; init; } = false;
}

public record ContactSocialProfile
{
    public required string Platform { get; init; } // LinkedIn, Twitter, etc.
    public required string Url { get; init; }
    public string? Username { get; init; }
}