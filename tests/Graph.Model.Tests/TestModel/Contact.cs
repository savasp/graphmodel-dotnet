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