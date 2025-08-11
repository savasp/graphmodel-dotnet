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

// Model for testing various validation rules
[Node(Label = "ValidationTest")]
public record ValidationTest : Memory
{
    [Property(Label = "requiredString", IsRequired = true)]
    public required string RequiredString { get; init; }

    [Property(Label = "optionalString")]
    public string? OptionalString { get; init; }

    [Property(Label = "minLengthString", MinLength = 5)]
    public string MinLengthString { get; init; } = string.Empty;

    [Property(Label = "maxLengthString", MaxLength = 10)]
    public string MaxLengthString { get; init; } = string.Empty;

    [Property(Label = "rangeString", MinLength = 3, MaxLength = 8)]
    public string RangeString { get; init; } = string.Empty;

    [Property(Label = "requiredInt", IsRequired = true)]
    public required int RequiredInt { get; init; }

    [Property(Label = "optionalInt")]
    public int? OptionalInt { get; init; }

    [Property(Label = "requiredDateTime", IsRequired = true)]
    public required DateTime RequiredDateTime { get; init; }

    [Property(Label = "optionalDateTime")]
    public DateTime? OptionalDateTime { get; init; }

    [Property(Label = "requiredBool", IsRequired = true)]
    public required bool RequiredBool { get; init; }

    [Property(Label = "optionalBool")]
    public bool? OptionalBool { get; init; }

    [Property(Label = "requiredEnum", IsRequired = true)]
    public required Priority RequiredEnum { get; init; }

    [Property(Label = "optionalEnum")]
    public Priority? OptionalEnum { get; init; }

    [Property(Label = "requiredList", IsRequired = true)]
    public required List<string> RequiredList { get; init; }

    [Property(Label = "optionalList")]
    public List<string>? OptionalList { get; init; }
}

// Model for testing edge cases
[Node(Label = "EdgeCaseTest")]
public record EdgeCaseTest : Memory
{
    [Property(Label = "emptyString")]
    public string EmptyString { get; init; } = string.Empty;

    [Property(Label = "zeroInt")]
    public int ZeroInt { get; init; } = 0;

    [Property(Label = "falseBool")]
    public bool FalseBool { get; init; } = false;

    [Property(Label = "emptyList")]
    public List<string> EmptyList { get; init; } = new();

    [Property(Label = "nullString")]
    public string? NullString { get; init; } = null;

    [Property(Label = "nullInt")]
    public int? NullInt { get; init; } = null;

    [Property(Label = "nullBool")]
    public bool? NullBool { get; init; } = null;

    [Property(Label = "nullList")]
    public List<string>? NullList { get; init; } = null;
}
