# Graph Querying Usage Patterns and Examples

This document provides concrete examples and usage patterns for the recommended graph querying interface evolution, demonstrating how the proposed changes would improve upon the current implementation.

## Current Usage Patterns (Analyzed from codebase)

### Basic Node Queries

```csharp
// Current approach (from QueryTestsBase.cs)
var smiths = graph.Nodes<Person>().Where(p => p.LastName == "Smith").ToList();
var ordered = graph.Nodes<Person>().OrderBy(p => p.FirstName).Take(2).ToList();
var count = graph.Nodes<Person>().Count();
```

### Relationship Queries

```csharp
// Current approach (from AdvancedQueryTestsBase.cs)
var recentConnections = graph.Relationships<Knows>()
    .Where(k => k.Since > DateTime.UtcNow.AddDays(-30))
    .ToList();
```

### Complex Pattern Queries with Navigation Properties

```csharp
// Current approach requiring GraphOperationOptions
var projectedAlice = graph.Nodes<PersonWithNavigationProperty>(
    new GraphOperationOptions { TraversalDepth = 1 })
    .Where(p => p.FirstName == "Alice")
    .Select(p => new {
        Name = p.FirstName,
        FriendCount = p.Knows.Count,
        FriendNames = p.Knows.Select(k => k.Target!.FirstName)
    })
    .FirstOrDefault();
```

## Recommended Usage Patterns

### 1. Enhanced Basic Queries with IGraphQueryable<T>

```csharp
// Recommended approach - maintains backward compatibility
var smiths = graph.Nodes<Person>().Where(p => p.LastName == "Smith").ToList();

// Enhanced with graph-specific options
var smithsWithDepth = graph.Nodes<Person>()
    .WithDepth(2)
    .Where(p => p.LastName == "Smith")
    .ToList();

// Performance hints
var optimizedQuery = graph.Nodes<Person>()
    .UseIndex("Person_LastName")
    .Where(p => p.LastName == "Smith")
    .ToList();
```

### 2. Fluent Traversal Operations

```csharp
// Current verbose approach
var alicesFriends = graph.Nodes<Person>()
    .Where(p => p.FirstName == "Alice")
    .Traverse<Person, Knows>()
    .To<Person>()
    .ToList();

// Recommended fluent approach
var alicesFriends = graph.Query<Person>()
    .Where(p => p.FirstName == "Alice")
    .TraverseTo<Person>()
    .Via<Knows>()
    .Where(friend => friend.Age > 25)
    .Select(friend => new { friend.FirstName, friend.Age })
    .ToList();
```

### 3. Complex Graph Patterns

```csharp
// Current approach - requires multiple operations
var nodes = graph.Nodes<Person>().ToList();
var relationships = graph.Relationships<Knows>().ToList();
// Manual joining in memory...

// Recommended approach - native graph pattern
var socialNetwork = graph.Query<Person>()
    .Where(p => p.FirstName == "Alice")
    .Match("(alice:Person)-[:KNOWS*1..3]-(friend:Person)")
    .Where("friend.age > $minAge", new { minAge = 25 })
    .Return<PersonConnection>();

// Or using fluent traversal
var socialNetwork = graph.Query<Person>()
    .Where(p => p.FirstName == "Alice")
    .TraverseTo<Person>()
    .Via<Knows>()
    .WithDepth(1, 3)
    .Where(friend => friend.Age > 25)
    .Select(alice => new {
        Person = alice,
        Friends = alice.ConnectedNodes<Person, Knows>()
    });
```

### 4. Performance-Optimized Queries

```csharp
// Current approach - limited optimization control
var result = graph.Nodes<Person>(new GraphOperationOptions { TraversalDepth = 2 })
    .Where(p => p.City == "Seattle")
    .ToList();

// Recommended approach - explicit optimization hints
var result = graph.Nodes<Person>()
    .WithDepth(2)
    .UseIndex("Person_City")
    .Hint("USE INDEX")
    .Where(p => p.City == "Seattle")
    .WithBatching(100)
    .ToList();
```

### 5. Transaction-Aware Querying

```csharp
// Current approach - transaction passed as parameter
using var tx = await graph.BeginTransaction();
var people = graph.Nodes<Person>(transaction: tx).ToList();
var relationships = graph.Relationships<Knows>(transaction: tx).ToList();

// Recommended approach - fluent transaction context
using var tx = await graph.BeginTransaction();
var result = graph.Nodes<Person>()
    .InTransaction(tx)
    .Where(p => p.Age > 30)
    .TraverseTo<Company>()
    .Via<WorksFor>()
    .Select(company => new { company.Name, company.Industry })
    .ToList();

// Or with automatic transaction scoping
var result = await graph.WithTransaction(async tx => {
    return graph.Nodes<Person>()
        .Where(p => p.Age > 30)
        .TraverseTo<Company>()
        .Via<WorksFor>()
        .Select(company => new { company.Name, company.Industry })
        .ToListAsync();
});
```

## Advanced Usage Patterns

### 1. Aggregation and Analytics

```csharp
// Current approach - requires manual aggregation
var allPeople = graph.Nodes<Person>().ToList();
var allRelationships = graph.Relationships<Knows>().ToList();
var connectionStats = allPeople.Select(p => new {
    Name = p.FirstName,
    ConnectionCount = allRelationships.Count(r => r.StartNodeId == p.Id || r.EndNodeId == p.Id)
});

// Recommended approach - native graph aggregation
var connectionStats = graph.Query<Person>()
    .AggregateConnections(p => new {
        Name = p.FirstName,
        OutgoingCount = p.OutgoingRelationships<Knows>().Count(),
        IncomingCount = p.IncomingRelationships<Knows>().Count(),
        TotalInfluence = p.ConnectedNodes<Person, Knows>()
            .SelectMany(friend => friend.ConnectedNodes<Person, Knows>())
            .Distinct()
            .Count()
    })
    .OrderByDescending(stats => stats.TotalInfluence)
    .ToList();
```

### 2. Path Finding and Analysis

```csharp
// Current approach - limited path operations
var paths = graph.Nodes<Person>()
    .Where(p => p.FirstName == "Alice")
    .ShortestPath(p => p.FirstName == "Bob")
    .ToList();

// Recommended approach - comprehensive path analysis
var socialPaths = graph.Query<Person>()
    .Where(p => p.FirstName == "Alice")
    .PathsTo(p => p.FirstName == "Bob")
    .Via<Knows>()
    .WithMaxDepth(6)
    .OrderBy(path => path.Length)
    .Select(path => new {
        Length = path.Length,
        Intermediates = path.Nodes.Skip(1).Take(path.Length - 1).Select(n => n.FirstName),
        Strength = path.Relationships.Cast<Knows>().Average(k => k.Strength ?? 1.0)
    })
    .Take(5)
    .ToList();
```

### 3. Complex Multi-Type Queries

```csharp
// Current approach - separate queries and manual joining
var people = graph.Nodes<Person>().ToList();
var companies = graph.Nodes<Company>().ToList();
var worksFor = graph.Relationships<WorksFor>().ToList();

// Recommended approach - unified graph query
var techEcosystem = graph.Query<Person>()
    .Where(p => p.Skills.Contains("Programming"))
    .TraverseTo<Company>()
    .Via<WorksFor>()
    .Where(c => c.Industry == "Technology")
    .TraverseTo<Person>()
    .Via<WorksFor>(direction: TraversalDirection.Incoming)
    .Where(colleague => colleague.Id != p.Id) // Exclude original person
    .Select(p => new {
        Developer = p,
        Company = p.ConnectedNodes<Company, WorksFor>().FirstOrDefault(),
        Colleagues = p.ConnectedNodes<Company, WorksFor>()
            .SelectMany(c => c.ConnectedNodes<Person, WorksFor>())
            .Where(colleague => colleague.Id != p.Id)
            .Take(10)
    })
    .ToList();
```

### 4. Conditional and Dynamic Queries

```csharp
// Current approach - building queries conditionally
IQueryable<Person> query = graph.Nodes<Person>();
if (filterByAge)
    query = query.Where(p => p.Age > minAge);
if (filterByCity)
    query = query.Where(p => p.City == targetCity);

// Recommended approach - fluent conditional building
var dynamicQuery = graph.Query<Person>()
    .WhereIf(filterByAge, p => p.Age > minAge)
    .WhereIf(filterByCity, p => p.City == targetCity)
    .TraverseToIf<Company>(includeCompanies)
    .Via<WorksFor>()
    .SelectConditional(includeCompanyDetails,
        p => new { p.FirstName, p.Age, Company = p.ConnectedNodes<Company, WorksFor>().FirstOrDefault() },
        p => new { p.FirstName, p.Age })
    .ToList();
```

## Performance Comparison Examples

### Memory Usage Optimization

```csharp
// Current approach - may load unnecessary data
var result = graph.Nodes<PersonWithNavigationProperty>(
    new GraphOperationOptions { TraversalDepth = 3 })
    .Where(p => p.FirstName == "Alice")
    .Select(p => p.Knows.Count)
    .FirstOrDefault();

// Recommended approach - projection pushdown
var result = graph.Query<Person>()
    .Where(p => p.FirstName == "Alice")
    .Project(p => p.ConnectedNodes<Person, Knows>().Count())
    .FirstOrDefault();
```

### Query Batching

```csharp
// Current approach - multiple round trips
var results = new List<Person>();
foreach (var name in names) {
    var person = graph.Nodes<Person>().FirstOrDefault(p => p.FirstName == name);
    if (person != null) results.Add(person);
}

// Recommended approach - single batched query
var results = graph.Query<Person>()
    .Where(p => names.Contains(p.FirstName))
    .WithBatching(batchSize: 100)
    .ToList();
```

## Migration Examples

### Step 1: Basic IGraphQueryable Adoption

```csharp
// Before (current code)
var people = graph.Nodes<Person>().Where(p => p.Age > 25).ToList();

// After (with IGraphQueryable) - no changes needed
var people = graph.Nodes<Person>().Where(p => p.Age > 25).ToList();

// Enhanced capabilities available
var peopleWithDepth = graph.Nodes<Person>()
    .WithDepth(2)
    .Where(p => p.Age > 25)
    .ToList();
```

### Step 2: Graph-Specific Extensions

```csharp
// Before - using current graph extensions
var connected = graph.Nodes<Person>()
    .Where(p => p.FirstName == "Alice")
    .ConnectedBy<Person, Knows, Person>()
    .ToList();

// After - enhanced type safety and fluent API
var connected = graph.Query<Person>()
    .Where(p => p.FirstName == "Alice")
    .ConnectedTo<Person>()
    .Via<Knows>()
    .ToList();
```

### Step 3: Advanced Pattern Adoption

```csharp
// Before - manual graph navigation
var options = new GraphOperationOptions { TraversalDepth = 2 };
var alice = graph.Nodes<PersonWithNavigationProperty>(options)
    .Where(p => p.FirstName == "Alice")
    .FirstOrDefault();

var friendsOfFriends = alice?.Knows
    .SelectMany(k => k.Target?.Knows ?? Enumerable.Empty<Knows>())
    .Select(k => k.Target?.FirstName)
    .Distinct()
    .ToList();

// After - native graph patterns
var friendsOfFriends = graph.Query<Person>()
    .Where(p => p.FirstName == "Alice")
    .TraverseTo<Person>()
    .Via<Knows>()
    .WithDepth(2, 2)
    .Select(friend => friend.FirstName)
    .Distinct()
    .ToList();
```

## Best Practices with Recommended Approach

### 1. Use Appropriate Abstraction Level

```csharp
// For simple queries - use standard LINQ
var basicQuery = graph.Nodes<Person>().Where(p => p.Age > 25).ToList();

// For graph traversals - use graph-specific extensions
var graphQuery = graph.Query<Person>()
    .Where(p => p.Age > 25)
    .TraverseTo<Company>()
    .Via<WorksFor>()
    .ToList();

// For complex patterns - use pattern matching
var patternQuery = graph.Query<Person>()
    .Match("(p:Person)-[:WORKS_FOR]->(c:Company)<-[:WORKS_FOR]-(colleague:Person)")
    .Where("p.age > $age AND colleague.age > $age", new { age = 25 })
    .Return<ColleagueConnection>();
```

### 2. Optimize for Performance

```csharp
// Use indexes for large datasets
var optimized = graph.Nodes<Person>()
    .UseIndex("Person_Age_City")
    .Where(p => p.Age > 25 && p.City == "Seattle")
    .ToList();

// Control traversal depth based on needs
var shallow = graph.Query<Person>().WithDepth(1).TraverseTo<Company>();
var deep = graph.Query<Person>().WithDepth(1, 5).TraverseTo<Company>();
```

### 3. Handle Transactions Appropriately

```csharp
// For read operations - use implicit transactions
var data = graph.Query<Person>().Where(p => p.Age > 25).ToList();

// For write operations - use explicit transactions
using var tx = await graph.BeginTransaction();
try {
    var result = graph.Query<Person>()
        .InTransaction(tx)
        .Where(p => p.Age > 65)
        .Update(p => new Person { Status = "Retired" });
    await tx.CommitAsync();
} catch {
    await tx.RollbackAsync();
    throw;
}
```

This comprehensive set of examples demonstrates how the recommended interface evolution improves upon the current implementation while maintaining backward compatibility and providing clear migration paths.
