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

[Node(Label = "Account")]
public record Account : Node
{
    public string AccountNumber { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public decimal Balance { get; set; }
}

[Node(Label = "Bank")]
public record Bank : Node
{
    public string Name { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

[Relationship(Label = "HAS_ACCOUNT")]
public record BankAccount : Relationship
{
    public BankAccount() : base(string.Empty, string.Empty) { }

    public BankAccount(string startNodeId, string endNodeId, DateTime? openedOn = null, string accountType = "")
        : base(startNodeId, endNodeId)
    {
        OpenedOn = openedOn ?? DateTime.UtcNow;
        AccountType = accountType;
    }

    public DateTime OpenedOn { get; set; } = DateTime.UtcNow;
    public string AccountType { get; set; } = string.Empty;
}

[Relationship(Label = "TRANSFER")]
public record Transfer : Relationship
{
    public Transfer() : base(string.Empty, string.Empty)
    {
    }

    public Transfer(
        string startNodeId,
        string endNodeId,
        decimal amount = 0m,
        DateTime? timestamp = null,
        string description = ""
    ) : base(startNodeId, endNodeId)
    {
        Amount = amount;
        Timestamp = timestamp ?? DateTime.UtcNow;
        Description = description;
    }

    public decimal Amount { get; set; } = 0m;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Description { get; set; } = string.Empty;
}
