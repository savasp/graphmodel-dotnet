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

using Cvoya.Graph.Model;

// ==== DOMAIN MODEL ====

[Node("Account")]
public record Account : Node
{
    public string AccountNumber { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public decimal Balance { get; set; }
}

[Node("Bank")]
public record Bank : Node
{
    public string Name { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

[Relationship("HAS_ACCOUNT")]
public record BankAccount(string sourceId, string targetId) : Relationship(sourceId, targetId)
{
    public DateTime OpenedOn { get; set; } = DateTime.UtcNow;
    public string AccountType { get; set; } = string.Empty;
}

[Relationship("TRANSFER")]
public record Transfer(string sourceId, string targetId) : Relationship(sourceId, targetId)
{
    public decimal Amount { get; set; } = 0m;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Description { get; set; } = string.Empty;
}
