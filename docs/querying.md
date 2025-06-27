---
title: Enhanced Querying with LINQ
layout: default
---

# Enhanced Querying with LINQ

Graph Model provides powerful querying capabilities through enhanced LINQ support via `IGraphQueryable<T>`. This extends standard LINQ with graph-specific operations for traversal, performance optimization, and advanced query patterns.

## Basic Queries

### Filtering Nodes

```csharp
// Simple where clause
var adults = await graph.Nodes<Person>()
    .Where(p => p.Age >= 18)
    .ToListAsync();

// Multiple conditions
var youngEngineers = await graph.Nodes<Person>()
    .Where(p => p.Age < 30 && p.Bio.Contains("engineer"))
    .ToListAsync();

// String operations
var smiths = await graph.Nodes<Person>()
    .Where(p => p.LastName.StartsWith("Sm"))
    .ToListAsync();
```

### Ordering and Pagination

```csharp
// Order by single property
var byAge = await graph.Nodes<Person>()
    .OrderBy(p => p.Age)
    .ToListAsync();

// Order by multiple properties
var sorted = await graph.Nodes<Person>()
    .OrderBy(p => p.LastName)
    .ThenBy(p => p.FirstName)
    .ToListAsync();

// Pagination
var page = await graph.Nodes<Person>()
    .OrderBy(p => p.LastName)
    .Skip(20)
    .Take(10)
    .ToListAsync();
```

## Enhanced Graph Operations

### Depth Control

```csharp
// Load nodes with relationships up to 2 levels deep
var peopleWithConnections = await graph.Nodes<Person>()
    .WithDepth(2)
    .ToListAsync();

// Specify depth range
var connections = await graph.Nodes<Person>()
    .WithDepth(1, 3) // Minimum 1 level, maximum 3 levels
    .ToListAsync();
```

### Performance Optimization

```csharp
// Use caching for expensive queries
var cachedResults = await graph.Nodes<Person>()
    .Where(p => p.Age > 30)
    .Cached(TimeSpan.FromMinutes(5))
    .ToListAsync();

// Provide query hints
var optimizedQuery = await graph.Nodes<Person>()
    .WithHint("USE_INDEX")
    .UseIndex("person_age_idx")
    .Where(p => p.Age > 30)
    .ToListAsync();

// Enable profiling for performance analysis
var profiledQuery = await graph.Nodes<Person>()
    .WithProfiling()
    .Where(p => p.Age > 30)
    .ToListAsync();
```

### Transaction Context

```csharp
await using var transaction = await graph.BeginTransaction();

var results = await graph.Nodes<Person>()
    .InTransaction(transaction)
    .Where(p => p.Age > 30)
    .ToListAsync();

await transaction.Commit();
```

## Advanced Graph Traversal

### Graph Traversal Queries

```csharp
// Multi-hop traversal with relationship filtering
var friendsOfFriends = await graph.Nodes<Person>()
    .Where(p => p.FirstName == "Alice")
    .Traverse<Person, Knows>()
    .InDirection(TraversalDirection.Outgoing)
    .WhereRelationship(k => k.Since > DateTime.Now.AddYears(-1)) // Recent friendships only
    .WithDepth(1, 2) // 1-2 hops away
    .ToTarget<Person>()
    .Where(p => p.Age > 25) // Filter target nodes
    .ToListAsync();

// Complex traversal with multiple relationship types
var socialNetwork = await graph.Nodes<Person>()
    .Where(p => p.Id == "alice-123")
    .Traverse<Person, Knows>()
    .InDirection(TraversalDirection.Bidirectional)
    .WithDepth(2)
    .Union(
        graph.Nodes<Person>()
            .Where(p => p.Id == "alice-123")
            .Traverse<Person, WorksWith>()
            .InDirection(TraversalDirection.Outgoing)
            .WithDepth(1)
    )
    .ToListAsync();
```

### Pattern Matching

```csharp
// Find triangular relationships (mutual friends)
var triangles = await graph.Nodes<Person>()
    .Match<Person>("(a:Person)-[:KNOWS]->(b:Person)-[:KNOWS]->(c:Person)-[:KNOWS]->(a)")
    .ToListAsync();

// Complex patterns with property constraints
var complexPattern = await graph.Nodes<Person>()
    .Match<Person>("(senior:Person {Age > 50})-[:MENTORS]->(junior:Person {Age < 30})")
    .ToListAsync();
```

## Projection and Results

### Anonymous Types

```csharp
var names = await graph.Nodes<Person>()
    .Where(p => p.Age > 25)
    .Select(p => new
    {
        FullName = p.FirstName + " " + p.LastName,
        p.Age,
        IsAdult = p.Age >= 18
    })
    .ToListAsync();
```

### Transformations

```csharp
var projected = await graph.Nodes<Person>()
    .Select(p => new
    {
        UpperName = p.FirstName.ToUpper(),
        NameLength = p.FirstName.Length,
        AgePlusTen = p.Age + 10,
        // DateTime operations
        BirthYear = DateTime.Now.Year - p.Age
    })
    .ToListAsync();
```

## Aggregations

### Count, Any, All

```csharp
// Count with predicate
var adultCount = await graph.Nodes<Person>()
    .CountAsync(p => p.Age >= 18);

// Check existence
var hasMinors = await graph.Nodes<Person>()
    .AnyAsync(p => p.Age < 18);

// Check all match condition
var allAdults = await graph.Nodes<Person>()
    .AllAsync(p => p.Age >= 18);
```

### First, Single, Last

```csharp
// Get first matching
var firstSmith = await graph.Nodes<Person>()
    .Where(p => p.LastName == "Smith")
    .OrderBy(p => p.FirstName)
    .FirstOrDefaultAsync();

// Get exactly one matching (throws if zero or multiple)
var specificPerson = await graph.Nodes<Person>()
    .SingleAsync(p => p.Id == "unique-id");

// Get last matching
var youngestPerson = await graph.Nodes<Person>()
    .OrderBy(p => p.Age)
    .LastOrDefaultAsync();
```

### Mathematical Aggregations

```csharp
// Sum, average, min, max
var stats = await graph.Nodes<Person>()
    .GroupBy(p => 1) // Group all into single group
    .Select(g => new
    {
        TotalAge = g.Sum(p => p.Age),
        AverageAge = g.Average(p => p.Age),
        MinAge = g.Min(p => p.Age),
        MaxAge = g.Max(p => p.Age),
        Count = g.Count()
    })
    .FirstAsync();
```

    .First();

// Get single (throws if multiple)
var theAlice = graph.Nodes<Person>()
.Single(p => p.FirstName == "Alice");

// Get last
var youngest = graph.Nodes<Person>()
.OrderBy(p => p.Age)
.Last();

// Safe versions that return null
var maybeAlice = graph.Nodes<Person>()
.FirstOrDefault(p => p.FirstName == "Alice");

````

### GroupBy and Aggregates

```csharp
// Group by with count
var byLastName = graph.Nodes<Person>()
    .GroupBy(p => p.LastName)
    .Select(g => new
    {
        LastName = g.Key,
        Count = g.Count()
    })
    .ToList();

// Multiple aggregations
var ageStats = graph.Nodes<Person>()
    .GroupBy(p => p.Department)
    .Select(g => new
    {
        Department = g.Key,
        Count = g.Count(),
        AverageAge = g.Average(p => p.Age),
        MinAge = g.Min(p => p.Age),
        MaxAge = g.Max(p => p.Age)
    })
    .ToList();
````

## Working with Relationships

### Querying Relationships

```csharp
// Filter relationships
var recentConnections = graph.Relationships<Knows>()
    .Where(k => k.Since > DateTime.UtcNow.AddDays(-30))
    .ToList();

// Relationships from specific node
var aliceKnows = graph.Relationships<Knows>()
    .Where(k => k.StartNodeId == aliceId)
    .ToList();

// Bidirectional search
var connectedToAlice = graph.Relationships<Knows>()
    .Where(k => k.StartNodeId == aliceId || k.EndNodeId == aliceId)
    .ToList();
```

### Traversal with Navigation Properties

When using `GraphOperationOptions` with `TraversalDepth`, navigation properties are populated:

```csharp
var options = new GraphOperationOptions { TraversalDepth = 1 };

var peopleWithFriends = graph.Nodes<Person>(options)
    .Where(p => p.Department == "Engineering")
    .ToList();

// Navigation properties are loaded
foreach (var person in peopleWithFriends)
{
    var friendNames = person.Knows
        .Select(k => k.Target?.FirstName)
        .Where(name => name != null)
        .ToList();
}
```

### Pattern Comprehension

Complex queries using navigation properties:

```csharp
var options = new GraphOperationOptions { TraversalDepth = 2 };

var socialNetwork = graph.Nodes<Person>(options)
    .Where(p => p.FirstName == "Alice")
    .Select(p => new
    {
        PersonName = p.FirstName,
        DirectFriends = p.Knows.Select(k => k.Target!.FirstName).ToList(),
        FriendsOfFriends = p.Knows
            .SelectMany(k => k.Target!.Knows.Select(k2 => k2.Target!.FirstName))
            .Distinct()
            .ToList(),
        FriendCount = p.Knows.Count,
        OldestFriendship = p.Knows.Min(k => k.Since),
        FriendsWithSameName = p.Knows
            .Where(k => k.Target!.FirstName == p.FirstName)
            .Select(k => k.Target!.LastName)
            .ToList()
    })
    .FirstOrDefault();
```

## Full-Text Search

```csharp
// Contains search
var results = graph.Nodes<Article>()
    .Where(a => a.Content.Contains("machine learning"))
    .ToList();

// Case-insensitive search
var caseInsensitive = graph.Nodes<Article>()
    .Where(a => a.Title.ToLower().Contains("graph"))
    .ToList();

// Multiple term search
var multiTerm = graph.Nodes<Article>()
    .Where(a => a.Content.Contains("graph") && a.Content.Contains("database"))
    .ToList();

// Starts with / Ends with
var prefixSearch = graph.Nodes<Person>()
    .Where(p => p.Email.StartsWith("admin@"))
    .ToList();
```

## Combining Node and Relationship Queries

For complex scenarios, you can execute multiple queries and join in memory:

```csharp
// Get all data
var people = graph.Nodes<Person>().ToList();
var knows = graph.Relationships<Knows>().ToList();

// Join to find connections
var connections = from person in people
                  join k in knows on person.Id equals k.StartNodeId
                  join friend in people on k.EndNodeId equals friend.Id
                  select new
                  {
                      Person = person.FirstName,
                      Friend = friend.FirstName,
                      Since = k.Since
                  };

// Group to find popular people
var popular = knows
    .GroupBy(k => k.EndNodeId)
    .Select(g => new
    {
        PersonId = g.Key,
        IncomingConnections = g.Count()
    })
    .Join(people, g => g.PersonId, p => p.Id, (g, p) => new
    {
        p.FirstName,
        g.IncomingConnections
    })
    .OrderByDescending(x => x.IncomingConnections)
    .ToList();
```

## Performance Considerations

1. **Use projections** to reduce data transfer when you don't need full entities
2. **Filter early** in your query chain to reduce the working set
3. **Be mindful of traversal depth** - deep traversals can be expensive
4. **Consider pagination** for large result sets
5. **Use transactions** for multiple related operations

## Advanced Scenarios

### Conditional Logic in Projections

```csharp
var categorized = graph.Nodes<Person>()
    .Select(p => new
    {
        p.FirstName,
        Category = p.Age < 18 ? "Minor" :
                   p.Age < 65 ? "Adult" :
                   "Senior",
        Discount = p.Age < 18 || p.Age >= 65 ? 0.2 : 0.0
    })
    .ToList();
```

### Complex Aggregations

```csharp
var stats = graph.Nodes<Person>()
    .Where(p => p.Department != null)
    .GroupBy(p => new { p.Department, AgeGroup = p.Age / 10 * 10 })
    .Select(g => new
    {
        g.Key.Department,
        g.Key.AgeGroup,
        Count = g.Count(),
        Names = g.Select(p => p.FirstName).ToList()
    })
    .OrderBy(x => x.Department)
    .ThenBy(x => x.AgeGroup)
    .ToList();
```
