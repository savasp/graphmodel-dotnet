# Best Practices

> **Note:** To use Graph Model, install the Neo4j provider package:

> ```bash
> dotnet add package Cvoya.Graph.Model.Neo4j
> ```

> The analyzers package is optional but recommended for extra compile-time validation:

> ```bash
> dotnet add package Cvoya.Graph.Model.Analyzers
> ```

This guide covers best practices for using Graph Model effectively in your applications.

## Model Design

### 1. Use Base Classes for Node and Relationship Implementation

**Do**: Inherit from `Node` or `Relationship` base classes

```csharp
// Good: Using base class
[Node("Person")]
public record Person : Node
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
}

// Good: Using Relationship base class
[Relationship("KNOWS")]
public record Knows(string StartNodeId, string EndNodeId) : Relationship(StartNodeId, EndNodeId)
{
    public DateTime Since { get; set; }
}
```

**Don't**: Implement `INode` or `IRelationship` directly

```csharp
// Avoid: Implementing interface directly (triggers GM011 warning)
public record Person : INode
{
    // GM011 warns on direct INode implementations; inherit from Node unless you need full control.
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public IReadOnlyList<string> Labels { get; } = new List<string> { "Person" }; // Don't manage these manually!
    public string Name { get; set; } = string.Empty;
}
```

**Why?** The base classes provide:

- Automatic ID generation
- Runtime metadata properties (`Labels`, `Type`) managed by the graph provider
- Correct initialization patterns
- Protection against manual metadata manipulation

The `Labels` property on `INode` and `Type` property on `IRelationship` are **runtime metadata** populated by the graph provider during serialization/deserialization. They enable polymorphic queries and filtering but should never be set manually.

```csharp
// Querying with runtime metadata
var memorySegments = await graph.Nodes<User>()
    .Where(u => u.Id == userId)
    .PathSegments<User, UserMemory, Memory>()
    .Where(ps => ps.EndNode.Id == memoryId && ps.Relationship.Type == relationshipType) // Using runtime Type property
    .ToListAsync();
```

### 2. Choose the Right Granularity

**Do**: Model entities at the right level of detail

```csharp
// Good: Separate entities for different concerns
[Node("Person")]
public record Person : Node
{
    public string Name { get; set; } = string.Empty;
}

[Node("Address")]
public record Address : Node
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
}

[Relationship("LIVES_AT")]
public record LivesAt(string StartNodeId, string EndNodeId) : Relationship(StartNodeId, EndNodeId)
{
    public DateTime Since { get; set; }
}
```

**Don't**: Embed complex objects as properties

```csharp
// Avoid: Complex nested properties
public record Person : Node
{
    public Address HomeAddress { get; set; } // This won't work well
}
```

### 3. Use Meaningful Relationship Types

**Do**: Use descriptive, domain-specific relationship names

```csharp
[Relationship("REPORTS_TO")]
public record ReportsTo(string StartNodeId, string EndNodeId) : Relationship(StartNodeId, EndNodeId);

[Relationship("PURCHASED")]
public record Purchased(string StartNodeId, string EndNodeId) : Relationship(StartNodeId, EndNodeId)
{
    public DateTime PurchaseDate { get; set; }
    public decimal Amount { get; set; }
}
```

**Don't**: Use generic relationship names

```csharp
// Avoid: Too generic
[Relationship("RELATED_TO")]
public record RelatedTo(string StartNodeId, string EndNodeId) : Relationship(StartNodeId, EndNodeId);
```

### 4. Model Time Appropriately

**Do**: Add temporal properties to relationships when needed

```csharp
[Relationship("WORKED_AT")]
public record WorkedAt(string StartNodeId, string EndNodeId) : Relationship(StartNodeId, EndNodeId)
{
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Position { get; set; } = string.Empty;

    public bool IsCurrent => EndDate == null;
}
```

## Query Patterns

### 1. Use Projections to Reduce Data Transfer

**Do**: Project only the data you need

```csharp
// Good: Only fetch required fields
var names = await graph.Nodes<Person>()
    .Where(p => p.Department == "Sales")
    .Select(p => new { p.FirstName, p.LastName })
    .ToListAsync();
```

**Don't**: Fetch entire entities when you only need a few properties

```csharp
// Avoid: Fetching entire entities
var people = await graph.Nodes<Person>()
    .Where(p => p.Department == "Sales")
    .ToListAsync();
var names = people.Select(p => p.FirstName); // Inefficient
```

### 2. Filter Early and Specifically

**Do**: Apply filters as early as possible in the query

```csharp
// Good: Filter at the database level
var results = await graph.Nodes<Order>()
    .Where(o => o.Status == "Pending" && o.Amount > 1000)
    .Take(10)
    .ToListAsync();
```

**Don't**: Filter in memory after fetching data

```csharp
// Avoid: Filtering in memory
var allOrders = await graph.Nodes<Order>().ToListAsync();
var results = allOrders
    .Where(o => o.Status == "Pending" && o.Amount > 1000)
    .Take(10);
```

### 3. Control Traversal Depth

**Do**: Explicitly set traversal depth based on your needs

```csharp
// Good: Traverse only what you need
var peopleWithFriends = await graph.Nodes<Person>()
    .Where(p => p.City == "Seattle")
    .Traverse<Knows, Person>(1)
    .ToListAsync();
```

**Don't**: Load deep graphs when not needed

```csharp
// Avoid: Traversing too broadly
var everyone = await graph.Nodes<Person>()
    .Traverse<Knows, Person>(1, 10)
    .ToListAsync(); // May be huge!
```

## Transaction Management

### 1. Keep Transactions Short

**Do**: Minimize transaction scope

```csharp
// Good: Focused transaction
public async Task TransferEmployee(string employeeId, string newDeptId)
{
    await using var tx = await graph.GetTransactionAsync();

    var employee = await graph.GetNodeAsync<Employee>(employeeId, transaction: tx);
    var newDept = await graph.GetNodeAsync<Department>(newDeptId, transaction: tx);

    // Update the relationship
    await graph.DeleteRelationshipAsync(employee.CurrentDeptRelationshipId, transaction: tx);
    var newRel = new WorksIn(employee.Id, newDept.Id);
    await graph.CreateRelationshipAsync(newRel, transaction: tx);

    await tx.CommitAsync();
}
```

**Don't**: Include unrelated operations in transactions

```csharp
// Avoid: Long-running transaction
await using var tx = await graph.GetTransactionAsync();
var data = await FetchFromExternalApi(); // Don't do this in a transaction!
await ProcessData(data);
await graph.CreateNodeAsync(person, transaction: tx);
await tx.CommitAsync();
```

### 2. Handle Failures Gracefully

**Do**: Implement proper error handling

```csharp
public async Task<bool> SafeCreatePerson(Person person)
{
    try
    {
        await using var tx = await graph.GetTransactionAsync();

        // Check for duplicates
        var existing = await graph.Nodes<Person>(transaction: tx)
            .FirstOrDefaultAsync(p => p.Email == person.Email);

        if (existing != null)
        {
            return false; // Already exists
        }

        await graph.CreateNodeAsync(person, transaction: tx);
        await tx.CommitAsync();
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

        await using var tx = await graph.GetTransactionAsync();
        foreach (var data in batch)
        {
            var person = new Person { /* map from data */ };
            await graph.CreateNodeAsync(person, transaction: tx);
        }
        await tx.CommitAsync();
    }
}
```

### 2. Optimize Relationship Queries

**Do**: Query relationships efficiently

```csharp
// Good: Direct relationship query
var knows = await graph.Relationships<Knows>()
    .Where(k => k.StartNodeId == personId || k.EndNodeId == personId)
    .ToListAsync();
```

**Don't**: Load all nodes to find relationships

```csharp
// Avoid: Inefficient relationship discovery
var allPeople = await graph.Nodes<Person>()
    .ToListAsync();
var allRelationships = await graph.Relationships<Knows>()
    .ToListAsync();
var personRelationships = allRelationships
    .Where(k => k.StartNodeId == personId || k.EndNodeId == personId)
    .ToList();
```

## Error Handling

### 1. Distinguish Error Types

```csharp
try
{
    await graph.CreateNodeAsync(node);
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
        catch (GraphException) when (i < maxRetries - 1)
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
        return await graph.GetNodeAsync<Person>(id);
    }
}

// In tests, pass your IGraph test double to the service constructor.
```

### 2. Test Transactions

```csharp
[Fact]
public async Task Transaction_RollsBackOnError()
{
    await using var tx = await graph.GetTransactionAsync();

    var person = new Person { FirstName = "Test" };
    await graph.CreateNodeAsync(person, transaction: tx);

    // Don't commit - simulate error
    await tx.RollbackAsync();

    // Verify person wasn't created
    var exists = await graph.Nodes<Person>()
        .AnyAsync(p => p.FirstName == "Test");
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

    await graph.CreateNodeAsync(person);
    return person;
}
```

### 2. Implement Access Control

```csharp
public async Task<IEnumerable<Document>> GetUserDocuments(string userId)
{
    // Only return documents the user has access to
    return await graph.Nodes<Document>()
        .Where(d => d.OwnerId == userId || d.IsPublic)
        .ToListAsync();
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
        await graph.CreateNodeAsync(person);

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
        var results = await query.ToListAsync();

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
