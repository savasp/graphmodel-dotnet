# Example 3: Transaction Management

This example demonstrates transaction support in the GraphModel library, including commit, rollback, and data consistency.

## What You'll Learn

- Beginning and managing transactions
- Committing successful operations
- Rolling back failed operations
- Ensuring data consistency across multiple operations
- Working with transactions in a banking scenario

## Key Concepts Demonstrated

### 1. Basic Transaction Usage

```csharp
using (var transaction = await graph.BeginTransactionAsync())
{
    try
    {
        // Perform operations
        await transaction.CreateNode(node);
        await transaction.UpdateNode(node);

        // Commit if successful
        await transaction.CommitAsync();
    }
    catch
    {
        // Rollback on failure
        await transaction.RollbackAsync();
        throw;
    }
}
```

### 2. Transaction Isolation

All operations within a transaction are isolated from other transactions until committed.

### 3. Automatic Rollback

If an exception occurs and the transaction is not explicitly committed, it will be rolled back automatically when disposed.

### 4. Complex Multi-Operation Transactions

Transactions can include multiple creates, updates, and deletes that all succeed or fail together.

## Banking Example

The example simulates a simple banking system with:

- Account creation
- Money transfers between accounts
- Balance validation
- Transaction history

## Running the Example

```bash
cd examples/Example3.TransactionManagement
dotnet run
```

Make sure Neo4j is running and accessible at `neo4j://localhost:7687`.
