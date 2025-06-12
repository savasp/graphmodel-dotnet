# Best Practices

This guide covers best practices for using Graph Model effectively in your applications.

## Model Design

### 1. Choose the Right Granularity

**Do**: Model entities at the right level of detail

```csharp
// Good: Separate entities for different concerns
[Node("Person")]
public class Person : INode
{
    public string Id { get; set; }
    public string Name { get; set; }
}

[Node("Address")]
public class Address : INode
{
    public string Id { get; set; }
    public string Street { get; set; }
    public string City { get; set; }
}

[Relationship("LIVES_AT")]
public class LivesAt : IRelationship<Person, Address>
{
    public DateTime Since { get; set; }
}
```

**Don't**: Embed complex objects as properties

```csharp
// Avoid: Complex nested properties
public class Person : INode
{
    public string Id { get; set; }
    public Address HomeAddress { get; set; } // This won't work well
}
```

### 2. Use Meaningful Relationship Types

**Do**: Use descriptive, domain-specific relationship names

```csharp
[Relationship("REPORTS_TO")]
public class ReportsTo : IRelationship<Employee, Manager> { }

[Relationship("PURCHASED")]
public class Purchased : IRelationship<Customer, Product>
{
    public DateTime PurchaseDate { get; set; }
    public decimal Amount { get; set; }
}
```

**Don't**: Use generic relationship names

```csharp
// Avoid: Too generic
[Relationship("RELATED_TO")]
public class RelatedTo : IRelationship<INode, INode> { }
```

### 3. Model Time Appropriately

**Do**: Add temporal properties to relationships when needed

```csharp
[Relationship("WORKED_AT")]
public class WorkedAt : IRelationship<Person, Company>
{
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Position { get; set; }

    public bool IsCurrent => EndDate == null;
}
```

## Query Patterns

### 1. Use Projections to Reduce Data Transfer

**Do**: Project only the data you need

```csharp
// Good: Only fetch required fields
var names = graph.Nodes<Person>()
    .Where(p => p.Department == "Sales")
    .Select(p => new { p.FirstName, p.LastName })
    .ToList();
```

**Don't**: Fetch entire entities when you only need a few properties

```csharp
// Avoid: Fetching entire entities
var people = graph.Nodes<Person>()
    .Where(p => p.Department == "Sales")
    .ToList();
var names = people.Select(p => p.FirstName); // Inefficient
```

### 2. Filter Early and Specifically

**Do**: Apply filters as early as possible in the query

```csharp
// Good: Filter at the database level
var results = graph.Nodes<Order>()
    .Where(o => o.Status == "Pending" && o.Amount > 1000)
    .Take(10)
    .ToList();
```

**Don't**: Filter in memory after fetching data

```csharp
// Avoid: Filtering in memory
var allOrders = graph.Nodes<Order>().ToList();
var results = allOrders
    .Where(o => o.Status == "Pending" && o.Amount > 1000)
    .Take(10);
```

### 3. Control Traversal Depth

**Do**: Explicitly set traversal depth based on your needs

```csharp
// Good: Load only what you need
var options = new GraphOperationOptions { TraversalDepth = 1 };
var peopleWithFriends = graph.Nodes<Person>(options)
    .Where(p => p.City == "Seattle")
    .ToList();
```

**Don't**: Load deep graphs when not needed

```csharp
// Avoid: Loading too much data
var options = new GraphOperationOptions { TraversalDepth = -1 }; // Unlimited
var everyone = graph.Nodes<Person>(options).ToList(); // May be huge!
```

## Transaction Management

### 1. Keep Transactions Short

**Do**: Minimize transaction scope

```csharp
// Good: Focused transaction
public async Task TransferEmployee(string employeeId, string newDeptId)
{
    await using var tx = await graph.BeginTransaction();

    var employee = await graph.GetNode<Employee>(employeeId, transaction: tx);
    var newDept = await graph.GetNode<Department>(newDeptId, transaction: tx);

    // Update the relationship
    await graph.DeleteRelationship(employee.CurrentDeptRelationshipId, tx);
    var newRel = new WorksIn { Source = employee, Target = newDept };
    await graph.CreateRelationship(newRel, transaction: tx);

    await tx.Commit();
}
```

**Don't**: Include unrelated operations in transactions

```csharp
// Avoid: Long-running transaction
await using var tx = await graph.BeginTransaction();
var data = await FetchFromExternalApi(); // Don't do this in a transaction!
await ProcessData(data);
await graph.CreateNode(result, transaction: tx);
await tx.Commit();
```

### 2. Handle Failures Gracefully

**Do**: Implement proper error handling

```csharp
public async Task<bool> SafeCreatePerson(Person person)
{
    try
    {
        await using var tx = await graph.BeginTransaction();

        // Check for duplicates
        var existing = graph.Nodes<Person>(transaction: tx)
            .FirstOrDefault(p => p.Email == person.Email);

        if (existing != null)
        {
            return false; // Already exists
        }

        await graph.CreateNode(person, transaction: tx);
        await tx.Commit();
        return true;
    }
    catch (GraphException ex)
    {
        logger.LogError(ex, "Failed to create person");
        throw;
    }
}
```

## Performance Optimization

### 1. Batch Operations

**Do**: Group related operations

```csharp
// Good: Batch create
public async Task ImportPeople(List<PersonData> peopleData)
{
    const int batchSize = 100;

    for (int i = 0; i < peopleData.Count; i += batchSize)
    {
        var batch = peopleData.Skip(i).Take(batchSize);

        await using var tx = await graph.BeginTransaction();
        foreach (var data in batch)
        {
            var person = new Person { /* map from data */ };
            await graph.CreateNode(person, transaction: tx);
        }
        await tx.Commit();
    }
}
```

### 2. Use Indexes Appropriately

**Do**: Index frequently queried properties

```csharp
[Node("User")]
public class User : INode
{
    public string Id { get; set; }

    [Index]
    public string Email { get; set; } // Frequently searched

    [Index]
    public string Username { get; set; } // Frequently searched

    public string Bio { get; set; } // Rarely searched - no index
}
```

### 3. Optimize Relationship Queries

**Do**: Query relationships efficiently

```csharp
// Good: Direct relationship query
var knows = graph.Relationships<Knows>()
    .Where(k => k.StartNodeId == personId || k.EndNodeId == personId)
    .ToList();
```

**Don't**: Load all nodes to find relationships

```csharp
// Avoid: Inefficient relationship discovery
var allPeople = graph.Nodes<Person>(new GraphOperationOptions { TraversalDepth = 1 })
    .ToList();
var personRelationships = allPeople
    .First(p => p.Id == personId)
    .Knows;
```

## Error Handling

### 1. Distinguish Error Types

```csharp
try
{
    await graph.CreateNode(node);
}
catch (GraphTransactionException ex)
{
    // Handle transaction-specific errors
    logger.LogError("Transaction failed: {Message}", ex.Message);
}
catch (GraphException ex)
{
    // Handle general graph errors
    logger.LogError("Graph operation failed: {Message}", ex.Message);
}
catch (Exception ex)
{
    // Handle unexpected errors
    logger.LogError(ex, "Unexpected error");
    throw;
}
```

### 2. Implement Retry Logic

```csharp
public async Task<T> ExecuteWithRetry<T>(
    Func<Task<T>> operation,
    int maxRetries = 3)
{
    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            return await operation();
        }
        catch (GraphTransactionException) when (i < maxRetries - 1)
        {
            // Retry on transaction conflicts
            await Task.Delay(TimeSpan.FromMilliseconds(100 * (i + 1)));
        }
    }
    throw new Exception("Operation failed after retries");
}
```

## Testing

### 1. Use Interfaces for Testability

```csharp
public class PersonService
{
    private readonly IGraph graph;

    public PersonService(IGraph graph)
    {
        this.graph = graph;
    }

    public async Task<Person> GetPersonWithFriends(string id)
    {
        var options = new GraphOperationOptions { TraversalDepth = 1 };
        return await graph.GetNode<Person>(id, options);
    }
}

// In tests
var mockGraph = new Mock<IGraph>();
var service = new PersonService(mockGraph.Object);
```

### 2. Test Transactions

```csharp
[Fact]
public async Task Transaction_RollsBackOnError()
{
    await using var tx = await graph.BeginTransaction();

    var person = new Person { FirstName = "Test" };
    await graph.CreateNode(person, transaction: tx);

    // Don't commit - simulate error
    await tx.Rollback();

    // Verify person wasn't created
    var exists = graph.Nodes<Person>()
        .Any(p => p.FirstName == "Test");
    Assert.False(exists);
}
```

## Security Considerations

### 1. Validate Input

```csharp
public async Task<Person> CreatePerson(PersonInput input)
{
    // Validate input
    if (string.IsNullOrWhiteSpace(input.Email))
        throw new ArgumentException("Email is required");

    if (!IsValidEmail(input.Email))
        throw new ArgumentException("Invalid email format");

    // Sanitize data
    var person = new Person
    {
        FirstName = input.FirstName.Trim(),
        LastName = input.LastName.Trim(),
        Email = input.Email.ToLowerInvariant().Trim()
    };

    await graph.CreateNode(person);
    return person;
}
```

### 2. Implement Access Control

```csharp
public async Task<IEnumerable<Document>> GetUserDocuments(string userId)
{
    // Only return documents the user has access to
    return graph.Nodes<Document>()
        .Where(d => d.OwnerId == userId || d.IsPublic)
        .ToList();
}
```

## Monitoring and Maintenance

### 1. Log Important Operations

```csharp
public async Task<Person> CreatePersonWithLogging(Person person)
{
    using (logger.BeginScope(new { PersonEmail = person.Email }))
    {
        logger.LogInformation("Creating person");

        var stopwatch = Stopwatch.StartNew();
        await graph.CreateNode(person);

        logger.LogInformation("Person created in {ElapsedMs}ms",
            stopwatch.ElapsedMilliseconds);

        return person;
    }
}
```

### 2. Monitor Query Performance

```csharp
public async Task<List<T>> ExecuteQueryWithMetrics<T>(
    IQueryable<T> query,
    string queryName)
{
    using (var activity = Activity.StartActivity(queryName))
    {
        var stopwatch = Stopwatch.StartNew();
        var results = query.ToList();

        activity?.SetTag("query.duration", stopwatch.ElapsedMilliseconds);
        activity?.SetTag("query.count", results.Count);

        if (stopwatch.ElapsedMilliseconds > 1000)
        {
            logger.LogWarning("Slow query {QueryName} took {Duration}ms",
                queryName, stopwatch.ElapsedMilliseconds);
        }

        return results;
    }
}
```

### 3. Regular Maintenance

- Review and optimize slow queries
- Update indexes based on query patterns
- Clean up orphaned relationships
- Archive old data when appropriate

## Summary

Following these best practices will help you build robust, performant, and maintainable applications with Graph Model. Remember to:

- Design your model thoughtfully
- Write efficient queries
- Use transactions appropriately
- Handle errors gracefully
- Test thoroughly
- Monitor performance
- Keep security in mind
