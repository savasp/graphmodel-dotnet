// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph;

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
    public DateTime OpenedOn { get; set; } = DateTime.UtcNow;
    public string AccountType { get; set; } = string.Empty;
}

[Relationship(Label = "TRANSFER")]
public record Transfer : Relationship
{
    public decimal Amount { get; set; } = 0m;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Description { get; set; } = string.Empty;
}
