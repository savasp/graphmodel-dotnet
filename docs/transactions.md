---
---

# Transaction management

`IGraphTransaction` groups queries and writes against one provider-owned transaction. It supports
explicit commit, explicit rollback, cancellation, and async disposal.

## Basic pattern

```csharp
await using var transaction = await graph.GetTransactionAsync(cancellationToken);
try
{
    await graph.CreateNodeAsync(
        new Account
        {
            AccountNumber = "ACC-001",
            Owner = "Alice",
            Balance = 1_000m,
        },
        transaction,
        cancellationToken);

    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}
```

Disposal cleans up an uncommitted transaction, but explicit rollback in an error path makes the
intent and any rollback failure visible.

## Transaction-bound queries

Pass the transaction when creating every query root that must participate:

```csharp
await using var transaction = await graph.GetTransactionAsync(cancellationToken);

var alice = graph.Nodes<Account>(transaction)
    .Where(account => account.AccountNumber == "ACC-001");
var bob = graph.Nodes<Account>(transaction)
    .Where(account => account.AccountNumber == "ACC-002");

var aliceBalance = await alice
    .Select(account => account.Balance)
    .SingleAsync(cancellationToken);
var bobBalance = await bob
    .Select(account => account.Balance)
    .SingleAsync(cancellationToken);

await alice.UpdateAsync(
    setters => setters.SetProperty(
        account => account.Balance,
        aliceBalance - 100m),
    cancellationToken);
await bob.UpdateAsync(
    setters => setters.SetProperty(
        account => account.Balance,
        bobBalance + 100m),
    cancellationToken);

await transaction.CommitAsync();
```

A query created without `graph.Nodes<T>(transaction)` owns a separate execution transaction. It
does not silently join another open transaction.

## Relationship creation

Selected endpoints and relationship creation share the active transaction when both selections
come from transaction-bound roots:

```csharp
var source = graph.Nodes<Account>(transaction)
    .Where(account => account.AccountNumber == "ACC-001");
var target = graph.Nodes<Account>(transaction)
    .Where(account => account.AccountNumber == "ACC-002");

await graph.CreateRelationshipAsync(
    source,
    new Transfer
    {
        Amount = 100m,
        Timestamp = DateTime.UtcNow,
    },
    target,
    cancellationToken: cancellationToken);
```

Each selection must resolve to exactly one node inside the transaction. The provider freezes the
selections before creating the edge, so no detached endpoint ID is required.

All-new subgraphs accept the transaction directly:

```csharp
await graph.CreateAsync(
    new Account { AccountNumber = "ACC-003", Owner = "Charlie" },
    new Transfer { Amount = 0m, Timestamp = DateTime.UtcNow },
    new Account { AccountNumber = "ACC-004", Owner = "Dana" },
    transaction: transaction,
    cancellationToken: cancellationToken);
```

The endpoint nodes, relationship, and owned complex-property subtrees are one atomic operation.

## Set-based updates and deletes

Mutation queryables carry their transaction from the root:

```csharp
var inactive = graph.Nodes<Account>(transaction)
    .Where(account => account.ClosedAt != null);

await inactive.UpdateAsync(
    setters => setters.SetProperty(account => account.Balance, 0m),
    cancellationToken);

await inactive.DeleteAsync(
    cascadeDelete: true,
    cancellationToken);
```

The provider freezes and de-duplicates the target set before mutation. Key, unique, and required
constraints are checked in the write transaction. Complex-property replacement and cleanup are
part of the same atomic update.

Relationship selections support `UpdateAsync` and their own `DeleteAsync(cancellationToken)`
overload.

## Rollback example

```csharp
await using var transaction = await graph.GetTransactionAsync(cancellationToken);
try
{
    var selected = graph.Nodes<Account>(transaction)
        .Where(account => account.AccountNumber == "ACC-001");
    var account = await selected.SingleAsync(cancellationToken);

    if (account.Balance < withdrawal)
    {
        throw new InvalidOperationException("Insufficient funds.");
    }

    await selected.UpdateAsync(
        setters => setters.SetProperty(
            item => item.Balance,
            account.Balance - withdrawal),
        cancellationToken);

    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}
```

After rollback, later queries created outside the transaction see none of its writes.

## Ownership and invalid combinations

- A transaction belongs to the graph/provider store that created it. Passing it to another store
  fails.
- Endpoint selections used by one relationship command must come from the same graph/provider and,
  when transaction-bound, the same transaction.
- Committing or rolling back twice fails.
- Nested transactions are not part of the public API. No in-tree provider declares
  `GraphCapability.NestedTransactions`.
- Provider stores own drivers, pools, and backing resources. Dispose the store separately;
  `IGraph` itself does not own them.

## Keep transactions short

Open the transaction after slow external work has completed, select only the rows required, and
commit promptly. Avoid network calls, user interaction, and long CPU work while holding database
resources.

For independent operations, let each graph call own its transaction. Use an explicit transaction
only when the operations require one atomic boundary or one consistent transactional view.

## Provider notes

- Neo4j uses a driver transaction.
- AGE holds a dedicated PostgreSQL connection/transaction and isolates caller-owned
  multi-statement command work behind savepoints where required.
- In-memory buffers writes and commits them atomically under its single-writer lock; it is a test
  double, not a throughput model.

Transaction acquisition and graph/query operations accept cancellation tokens. The transaction's
`CommitAsync()` and `RollbackAsync()` methods have no token parameter.
