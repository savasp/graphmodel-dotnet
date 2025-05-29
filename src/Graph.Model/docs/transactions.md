# Transaction Management

Graph Model provides comprehensive transaction support with full async/await patterns to ensure ACID properties and data consistency in your graph operations.

## Key Features

- **Full ACID compliance** - Atomicity, Consistency, Isolation, and Durability
- **Async/await support** - Modern asynchronous transaction patterns
- **Automatic resource management** - Implements `IAsyncDisposable` and `IDisposable`
- **Exception safety** - Automatic rollback on errors
- **Flexible scope** - All operations support optional transaction parameters

## Basic Transaction Usage

### Simple Transaction Pattern

```csharp
await using var transaction = await graph.BeginTransaction();
try
{
    // Perform your operations within the transaction
    await graph.CreateNode(person, transaction: transaction);
    await graph.CreateNode(company, transaction: transaction);

    var worksAt = new WorksAt
    {
        SourceId = person.Id,
        TargetId = company.Id,
        StartDate = DateTime.UtcNow
    };
    await graph.CreateRelationship(worksAt, transaction: transaction);

    // Explicitly commit if all operations succeed
    await transaction.Commit();
}
catch (Exception ex)
{
    // Automatic rollback on any error
    await transaction.Rollback();
    throw;
}
```

### Automatic Disposal Pattern

Transactions implement `IAsyncDisposable`, providing automatic rollback if not explicitly committed:

```csharp
await using (var transaction = await graph.BeginTransaction())
{
    await graph.CreateNode(node, transaction: transaction);
    await graph.UpdateNode(existingNode, transaction: transaction);

    if (someBusinessCondition)
    {
        await transaction.Commit();
    }
    // If not committed, transaction automatically rolls back on disposal
}
```

## Transaction Scope

All graph operations support an optional transaction parameter:

### Node Operations

```csharp
// Create
await graph.CreateNode(node, transaction: transaction);

// Read
var node = await graph.GetNode<Person>(id, transaction: transaction);
var nodes = await graph.GetNodes<Person>(ids, transaction: transaction);

// Update
await graph.UpdateNode(node, transaction: transaction);

// Delete
await graph.DeleteNode(id, transaction: transaction);
```

### Relationship Operations

```csharp
// Create
await graph.CreateRelationship(relationship, transaction: transaction);

// Read
var rel = await graph.GetRelationship<Knows>(id, transaction: transaction);
var rels = await graph.GetRelationships<Knows>(ids, transaction: transaction);

// Update
await graph.UpdateRelationship(relationship, transaction: transaction);

// Delete
await graph.DeleteRelationship(id, transaction: transaction);
```

### Query Operations

```csharp
// Queries also support transactions
var results = graph.Nodes<Person>(transaction: transaction)
    .Where(p => p.Department == "Engineering")
    .ToList();

var relationships = graph.Relationships<Knows>(transaction: transaction)
    .Where(k => k.Since > DateTime.UtcNow.AddDays(-7))
    .ToList();
```

## Transaction Isolation

Transactions provide isolation from other concurrent operations:

```csharp
// Transaction 1
await using var tx1 = await graph.BeginTransaction();
var alice = new Person { FirstName = "Alice" };
await graph.CreateNode(alice, transaction: tx1);

// Transaction 2 (concurrent)
await using var tx2 = await graph.BeginTransaction();
// This won't see Alice until tx1 commits
var count = graph.Nodes<Person>(transaction: tx2).Count();

await tx1.Commit();
// Now tx2 can see Alice
```

## Complex Transaction Scenarios

### Conditional Logic

```csharp
await using var transaction = await graph.BeginTransaction();
try
{
    var person = await graph.GetNode<Person>(personId, transaction: transaction);

    if (person.Status == "Active")
    {
        // Update the person
        person.LastActive = DateTime.UtcNow;
        await graph.UpdateNode(person, transaction: transaction);

        // Create an activity log
        var activity = new Activity
        {
            PersonId = person.Id,
            Timestamp = DateTime.UtcNow
        };
        await graph.CreateNode(activity, transaction: transaction);

        // Create relationship
        var performed = new Performed { Source = person, Target = activity };
        await graph.CreateRelationship(performed, transaction: transaction);

        await transaction.Commit();
    }
    else
    {
        // Don't commit - let it rollback
    }
}
catch
{
    await transaction.Rollback();
    throw;
}
```

### Bulk Operations

```csharp
await using var transaction = await graph.BeginTransaction();
try
{
    // Import a batch of data
    foreach (var personData in importData)
    {
        var person = new Person
        {
            FirstName = personData.FirstName,
            LastName = personData.LastName,
            Email = personData.Email
        };

        await graph.CreateNode(person, transaction: transaction);

        // Create relationships if needed
        if (personData.ManagerEmail != null)
        {
            var manager = graph.Nodes<Person>(transaction: transaction)
                .FirstOrDefault(p => p.Email == personData.ManagerEmail);

            if (manager != null)
            {
                var reportsTo = new ReportsTo
                {
                    Source = person,
                    Target = manager
                };
                await graph.CreateRelationship(reportsTo, transaction: transaction);
            }
        }
    }

    await transaction.Commit();
}
catch (Exception ex)
{
    await transaction.Rollback();
    throw new ImportException("Failed to import data", ex);
}
```

### Validation and Rollback

```csharp
await using var transaction = await graph.BeginTransaction();
try
{
    // Create entities
    await graph.CreateNode(department, transaction: transaction);
    await graph.CreateNode(employee, transaction: transaction);

    // Validate business rules
    var employeeCount = graph.Nodes<Employee>(transaction: transaction)
        .Count(e => e.DepartmentId == department.Id);

    if (employeeCount > department.MaxEmployees)
    {
        // Explicitly rollback due to business rule violation
        await transaction.Rollback();
        throw new BusinessRuleException("Department employee limit exceeded");
    }

    // Create the relationship
    var worksIn = new WorksIn { Source = employee, Target = department };
    await graph.CreateRelationship(worksIn, transaction: transaction);

    await transaction.Commit();
}
catch
{
    // Ensure rollback even if already rolled back
    try { await transaction.Rollback(); } catch { }
    throw;
}
```

## Error Handling

### GraphTransactionException

Thrown when there are transaction-specific errors:

```csharp
try
{
    await using var transaction = await graph.BeginTransaction();
    // ... operations ...
    await transaction.Commit();
}
catch (GraphTransactionException ex)
{
    // Handle transaction-specific errors
    logger.LogError(ex, "Transaction failed");
}
catch (GraphException ex)
{
    // Handle general graph errors
    logger.LogError(ex, "Graph operation failed");
}
```

### Nested Transaction Attempts

Graph Model doesn't support nested transactions. Attempting to start a transaction within another will throw an exception:

```csharp
await using var transaction1 = await graph.BeginTransaction();

// This will throw - nested transactions not supported
await using var transaction2 = await graph.BeginTransaction(); // Throws!
```

## Best Practices

### 1. Keep Transactions Short

```csharp
// Good: Short transaction
await using var tx = await graph.BeginTransaction();
await graph.CreateNode(node, transaction: tx);
await graph.CreateRelationship(rel, transaction: tx);
await tx.Commit();

// Avoid: Long-running transactions
await using var tx = await graph.BeginTransaction();
var data = await FetchDataFromExternalService(); // Don't do this in transaction
await graph.CreateNode(data, transaction: tx);
await tx.Commit();
```

### 2. Use Try-Finally for Critical Cleanup

```csharp
await using var transaction = await graph.BeginTransaction();
try
{
    // Acquire resources
    var lockHandle = await AcquireLock(resourceId);
    try
    {
        // Perform operations
        await graph.UpdateNode(node, transaction: transaction);
        await transaction.Commit();
    }
    finally
    {
        // Always release resources
        await ReleaseLock(lockHandle);
    }
}
catch
{
    await transaction.Rollback();
    throw;
}
```

### 3. Consider Read Consistency

When reading data that will be modified, use the same transaction:

```csharp
await using var transaction = await graph.BeginTransaction();

// Read with the transaction to ensure consistency
var person = await graph.GetNode<Person>(id, transaction: transaction);
var currentFriends = graph.Relationships<Knows>(transaction: transaction)
    .Count(k => k.SourceId == person.Id);

if (currentFriends < 100)
{
    // Safe to add friend - count is consistent with our transaction
    await graph.CreateRelationship(newFriendship, transaction: transaction);
    await transaction.Commit();
}
```

### 4. Handle Partial Failures

```csharp
var results = new List<ImportResult>();

foreach (var batch in dataBatches)
{
    await using var transaction = await graph.BeginTransaction();
    try
    {
        foreach (var item in batch)
        {
            await graph.CreateNode(item, transaction: transaction);
        }
        await transaction.Commit();
        results.Add(new ImportResult { Batch = batch, Success = true });
    }
    catch (Exception ex)
    {
        await transaction.Rollback();
        results.Add(new ImportResult
        {
            Batch = batch,
            Success = false,
            Error = ex.Message
        });
        // Continue with next batch
    }
}
```

## Performance Considerations

1. **Transaction Overhead**: Each transaction has overhead. Batch related operations together.
2. **Lock Duration**: Long transactions can cause lock contention. Keep them short.
3. **Read Operations**: If only reading, consider whether you need a transaction.
4. **Deadlock Prevention**: Access resources in a consistent order across transactions.

## Transaction-Free Operations

For read-only operations or when atomicity isn't required, you can omit the transaction:

```csharp
// Simple reads don't require transactions
var person = await graph.GetNode<Person>(id);
var friends = graph.Nodes<Person>()
    .Where(p => p.Department == "Sales")
    .ToList();

// Single operations have implicit transactions
await graph.CreateNode(person); // Atomic by itself
```

Choose explicit transactions when you need to ensure multiple operations succeed or fail together.
