# Example 3: Transaction management

This example demonstrates explicit Neo4j transactions with a banking scenario: successful
transfers, validation failures, rollback, and multi-operation commits.

Pass the transaction to every query root and write that belongs to the unit of work:

```csharp
await using var transaction = await graph.GetTransactionAsync();

try
{
    var source = graph.Nodes<Account>(transaction)
        .Where(account => account.AccountNumber == "ACC-001");
    var target = graph.Nodes<Account>(transaction)
        .Where(account => account.AccountNumber == "ACC-002");

    await source.UpdateAsync(setters => setters
        .SetProperty(account => account.Balance, account => account.Balance - 100m));
    await target.UpdateAsync(setters => setters
        .SetProperty(account => account.Balance, account => account.Balance + 100m));

    await graph.CreateRelationshipAsync(
        source,
        new Transfer { Amount = 100m, Timestamp = DateTime.UtcNow },
        target);

    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}
```

Acquisition, graph writes, and query terminals accept cancellation tokens. `CommitAsync()` and
`RollbackAsync()` do not.

## Run

Start Neo4j at `bolt://localhost:7687` with username `neo4j` and password `password`, then run:

```bash
dotnet run --project examples/Example3.TransactionManagement
```
