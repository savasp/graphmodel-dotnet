# Querying with LINQ

Graph Model provides full LINQ support for querying nodes and relationships in your graph. This allows you to use familiar C# syntax for complex graph queries.

## Basic Queries

### Filtering Nodes

```csharp
// Simple where clause
var adults = graph.Nodes<Person>()
    .Where(p => p.Age >= 18)
    .ToList();

// Multiple conditions
var youngEngineers = graph.Nodes<Person>()
    .Where(p => p.Age < 30 && p.Bio.Contains("engineer"))
    .ToList();

// String operations
var smiths = graph.Nodes<Person>()
    .Where(p => p.LastName.StartsWith("Sm"))
    .ToList();
```

### Ordering and Pagination

```csharp
// Order by single property
var byAge = graph.Nodes<Person>()
    .OrderBy(p => p.Age)
    .ToList();

// Order by multiple properties
var sorted = graph.Nodes<Person>()
    .OrderBy(p => p.LastName)
    .ThenBy(p => p.FirstName)
    .ToList();

// Pagination
var page = graph.Nodes<Person>()
    .OrderBy(p => p.LastName)
    .Skip(20)
    .Take(10)
    .ToList();
```

## Projection

### Anonymous Types

```csharp
var names = graph.Nodes<Person>()
    .Where(p => p.Age > 25)
    .Select(p => new
    {
        FullName = p.FirstName + " " + p.LastName,
        p.Age
    })
    .ToList();
```

### String and Math Functions

```csharp
var projected = graph.Nodes<Person>()
    .Select(p => new
    {
        UpperName = p.FirstName.ToUpper(),
        NameLength = p.FirstName.Length,
        Trimmed = p.Bio.Trim(),
        Substring = p.FirstName.Substring(0, 1),
        Replaced = p.Bio.Replace("engineer", "developer"),
        // Math operations
        AgePlusTen = p.Age + 10,
        AgeSquared = p.Age * p.Age,
        SqrtAge = Math.Sqrt(p.Age)
    })
    .ToList();
```

### DateTime Functions

```csharp
var timeQueries = graph.Nodes<Event>()
    .Select(e => new
    {
        e.Name,
        Year = e.Date.Year,
        Month = e.Date.Month,
        Day = e.Date.Day,
        Now = DateTime.Now,
        Today = DateTime.Today,
        UtcNow = DateTime.UtcNow
    })
    .ToList();
```

## Aggregations

### Count, Any, All

```csharp
// Count with predicate
var adultCount = graph.Nodes<Person>()
    .Count(p => p.Age >= 18);

// Check existence
var hasMinors = graph.Nodes<Person>()
    .Any(p => p.Age < 18);

// Check all match condition
var allAdults = graph.Nodes<Person>()
    .All(p => p.Age >= 18);
```

### First, Single, Last

```csharp
// Get first matching
var firstSmith = graph.Nodes<Person>()
    .Where(p => p.LastName == "Smith")
    .OrderBy(p => p.FirstName)
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
    .Where(k => k.SourceId == aliceId)
    .ToList();

// Bidirectional search
var connectedToAlice = graph.Relationships<Knows>()
    .Where(k => k.SourceId == aliceId || k.TargetId == aliceId)
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
                  join k in knows on person.Id equals k.SourceId
                  join friend in people on k.TargetId equals friend.Id
                  select new
                  {
                      Person = person.FirstName,
                      Friend = friend.FirstName,
                      Since = k.Since
                  };

// Group to find popular people
var popular = knows
    .GroupBy(k => k.TargetId)
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
