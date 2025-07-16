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

## Working with "complex" types

The Graph Model supports "complex" properties. These are properties whose type isn't one of the primitive types. Neo4j doesn't natively support such properties on graph nodes. The Graph Model requires implementing providers to support them. Consider this example:

```csharp
public record Address
{
    public string Street { get; set; }
    public string City { get; set; }
    public StateEnum State { get; set; }
    public CountryEnum Country { get; set; }
}

public record Person : Node
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public Address HomeAddress { get; set; }
    public Address WorkAddress { get; set; }
}
```

The addresses could have been modelled as separate nodes, which is a great way to model this information. The `Address` would have had to implement `INode`. In this case, however, the developer decided to model the home and work addresses as properties on `Person`. The Graph Model supports this data modeling choice and requires providers to support it. Indeed, the Neo4j implementation of the Graph Model automatically creates a separate graph node for an `Address` using private relationships. Queries that access the members of `Address` properties are supported...

```csharp
// Automatically creates two separate graph nodes for the "HomeAddress" and "WorkAddress" properties
// The relationship between the Person node and its complex properties isn't discoverable. It's considered
// a private implementation detail. The Neo4j provider translates the LINQ query below to the appropriate Cypher
// query so that these private relationships are considered as one would have expected.

// Create many people nodes...
var tx = graph.GetTransactionAsync();
foreach (var p in people)
  await graph.CreateNodeAsync(p);
await tx.CommitAsync();

// Query for those who live in WA state
var waStateResidents = await graph.Nodes<Person>()
    .Where(p => p.HomeAddress.State == StateEnum.WA)
    .ToListAsync();
```

## Advanced Graph Traversal

### Graph Traversal Queries

```csharp
// Multi-hop traversal with relationship filtering
var friendsOfFriends = await graph.Nodes<Person>()
    .Where(p => p.FirstName == "Alice")
    .Traverse<Person, Knows, Person>()
    .WhereRelationship(k => k.Since > DateTime.Now.AddYears(-1)) // Recent friendships only
    .WithDepth(1, 2) // 1-2 hops away
    .Where(p => p.Age > 25) // Filter target nodes
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

// Get single (throws if multiple)
var theAlice = graph.Nodes<Person>()
    .SingleAsync(p => p.FirstName == "Alice");

// Get last
var youngest = graph.Nodes<Person>()
    .OrderBy(p => p.Age)
    .LastAsync();

// Safe versions that return null
var maybeAlice = graph.Nodes<Person>()
    .FirstOrDefault(p => p.FirstName == "Alice");
```

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
    .ToListAsync();

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
    .ToListAsync();
```

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

## Full-Text Search

Graph Model provides comprehensive full-text search capabilities through both direct search methods and LINQ integration.

### Direct Search Methods

```csharp
// Search across all entities (nodes and relationships)
var allResults = await graph.Search("machine learning").ToListAsync();

// Search specific node types
var articleResults = await graph.SearchNodes<Article>("artificial intelligence").ToListAsync();

// Search specific relationship types
var relationshipResults = await graph.SearchRelationships<Knows>("college").ToListAsync();

// Search using generic interfaces
var allNodes = await graph.SearchNodes("graph database").ToListAsync();
var allRelationships = await graph.SearchRelationships("friendship").ToListAsync();
```

### LINQ Integration with Search

The `Search()` method can be used in LINQ chains to perform full-text search on query results:

```csharp
// Basic search in LINQ chain
var results = await graph.Nodes<Person>()
    .Where(p => p.Age > 25)
    .Search("software engineer")
    .ToListAsync();

// Search in path segments traversal
var memories = await graph.Nodes<User>()
    .Where(u => u.Id == "...")
    .PathSegments<User, UserMemory, Memory>()
    .Select(p => p.EndNode)
    .Search("vacation memories")
    .ToListAsync();

// Search with multiple conditions
var filteredResults = await graph.Nodes<Article>()
    .Where(a => a.PublishedDate > DateTime.UtcNow.AddDays(-30))
    .Search("machine learning")
    .Where(a => a.Author.StartsWith("Dr."))
    .ToListAsync();

// Search with projections
var summaries = await graph.Nodes<Article>()
    .Search("artificial intelligence")
    .Select(a => new { a.Title, a.Summary })
    .ToListAsync();
```

### Traditional String Operations

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

### Search Features

- **Case Insensitive**: All search operations are case-insensitive by default
- **Multi-word Support**: Search for phrases like "machine learning" or "artificial intelligence"
- **Property Control**: Use `[Property(IncludeInFullTextSearch = false)]` to exclude properties from search
- **Automatic Indexing**: Full-text indexes are created and managed automatically
- **LINQ Integration**: Seamlessly integrate search into existing LINQ query chains

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
