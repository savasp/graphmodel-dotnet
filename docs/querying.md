---
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

Depth applies to a traversal, not to a plain node query — use the depth overloads on `Traverse`/
`TraversePaths` (see [Advanced Graph Traversal](#advanced-graph-traversal) below), not a
free-floating modifier:

```csharp
// Traverse up to 2 levels deep from Alice
var connections = await graph.Nodes<Person>()
    .Where(p => p.FirstName == "Alice")
    .Traverse<Knows, Person>(maxDepth: 2)
    .ToListAsync();

// Specify a depth range
var rangedConnections = await graph.Nodes<Person>()
    .Where(p => p.FirstName == "Alice")
    .Traverse<Knows, Person>(minDepth: 1, maxDepth: 3)
    .ToListAsync();
```

### Performance Optimization

```csharp
// Project only the fields you need
var optimizedQuery = await graph.Nodes<Person>()
    .Where(p => p.Age > 30)
    .Select(p => new { p.Id, p.FirstName, p.LastName })
    .ToListAsync();

// Page large result sets
var firstPage = await graph.Nodes<Person>()
    .Where(p => p.Age > 30)
    .OrderBy(p => p.LastName)
    .Take(100)
    .ToListAsync();
```

### Transaction Context

```csharp
await using var transaction = await graph.GetTransactionAsync();

var results = await graph.Nodes<Person>()
    .Where(p => p.Age > 30)
    .ToListAsync();

var transactionalResults = await graph.Nodes<Person>(transaction: transaction)
    .Where(p => p.Age > 30)
    .ToListAsync();

await transaction.CommitAsync();
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

The developer-facing type remains a value object, but its stored representation is first-class graph
structure. Neo4j creates a separate `:Address` value node for each occurrence and connects it with
`:HomeAddress` or `:WorkAddress`. Use
`[ComplexProperty(RelationshipType = "LIVES_AT")]` when the property name is not the desired graph
relationship type.

```csharp
// Automatically creates two separate, visible Address nodes connected by semantic relationships.

// Create many people nodes...
await using var tx = await graph.GetTransactionAsync();
foreach (var p in people)
  await graph.CreateNodeAsync(p, transaction: tx);
await tx.CommitAsync();

// Query for those who live in WA state
var waStateResidents = await graph.Nodes<Person>()
    .Where(p => p.HomeAddress.State == StateEnum.WA)
    .ToListAsync();
```

Nested navigation is not limited to one level. Complex collections lower to graph patterns as well:

```csharp
var regional = await graph.Nodes<Company>()
    .Where(c => c.Headquarters.Region.Name == "Northwest")
    .Where(c => c.Offices.Any(office => office.City == "Seattle"))
    .Where(c => c.Offices.All(office => office.IsOpen))
    .Where(c => c.Offices.Count > 1)
    .Select(c => c.Offices.Select(office => office.City))
    .ToListAsync();
```

Comparing a complex property with `null` tests whether its relationship exists; it does not read a
scalar property from the owner node. Declared complex properties auto-load recursively with a five-level
depth guard. A slim read type that omits a property does not populate it, while normal traversal/path
projection can still return the owner, semantic relationship, and related value node together.

## Advanced Graph Traversal

### Graph Traversal Queries

```csharp
// Multi-hop traversal, then filter the target nodes
var friendsOfFriends = await graph.Nodes<Person>()
    .Where(p => p.FirstName == "Alice")
    .Traverse<Knows, Person>(minDepth: 1, maxDepth: 2) // 1-2 hops away
    .Where(p => p.Age > 25) // Filter target nodes
    .ToListAsync();

// To filter on the relationship itself (e.g. recent friendships only), use PathSegments instead -
// it exposes the relationship alongside the start/end nodes for a single hop.
var recentFriends = await graph.Nodes<Person>()
    .Where(p => p.FirstName == "Alice")
    .PathSegments<Person, Knows, Person>()
    .Where(ps => ps.Relationship.Since > DateTime.Now.AddYears(-1))
    .Select(ps => ps.EndNode)
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

### Relationship-count projection

Inside a projection you can count a node's relationships (its *degree*) by relationship type and
direction with `CountRelationships<TRel>(...)`. It is a projection-only marker: the provider
recognizes the call and translates it into a native relationship-count subquery — in Cypher a
`COUNT { MATCH (p)-[:REL]->() }` / `size((p)-[:REL]->())` pattern subquery — rather than fetching
relationships to the client.

```csharp
var stats = await graph.Nodes<Person>()
    .Select(p => new
    {
        p.FirstName,
        OutgoingKnows = p.CountRelationships<Knows>(GraphTraversalDirection.Outgoing),
        IncomingKnows = p.CountRelationships<Knows>(GraphTraversalDirection.Incoming),
        TotalKnows    = p.CountRelationships<Knows>(GraphTraversalDirection.Both),
    })
    .ToListAsync();
```

`GraphTraversalDirection` matches the `Traverse` direction semantics: `Outgoing` counts physical
edges leaving the node and `Incoming` counts physical edges arriving at it. A relationship stored
with `RelationshipDirection.Incoming` therefore reverses its logical start/end orientation. `Both`
counts each incident relationship once, including a self-loop. Counting a base relationship type
also includes stored derived relationship types. The direction argument must be a compile-time
constant. This surface requires the `PatternSizeProjection` capability; providers that do not
declare it reject the query at translation time. Calling `CountRelationships` outside a query
projection throws `InvalidOperationException`.

### Projecting collections

You can project a **correlated collection** per row: group a node's outgoing path segments by
their start node and shape the segments into a nested list. On a graph provider this lowers to a
Cypher *pattern comprehension* (`[(src)-[:KNOWS]->(tgt) WHERE ... | ...]`) evaluated per row — no
client-side join.

```csharp
// Per person, the list of their friends' names (a nested collection projection)
var social = await graph.Nodes<Person>()
    .Where(p => p.FirstName == "Alice")
    .PathSegments<Person, Knows, Person>()
    .GroupBy(segment => segment.StartNode)
    .Select(group => new
    {
        Name = group.Key.FirstName,
        FriendCount = group.Count(),
        FriendNames = group.Select(s => s.EndNode.FirstName).ToList()
    })
    .FirstOrDefaultAsync();
```

The inner collection can be filtered — either on the traversal before grouping, or inside the
group — and both the list and any `Count()` observe that filter:

```csharp
// Filter the segments, then project the surviving collection and its count
var youngFriends = await graph.Nodes<Person>()
    .Where(p => p.FirstName == "Alice")
    .PathSegments<Person, Knows, Person>()
    .Where(path => path.EndNode.Age < 30)
    .GroupBy(segment => segment.StartNode)
    .Select(group => new
    {
        PersonName = group.Key.FirstName,
        YoungFriends = group.Select(s => s.EndNode.FirstName).ToList(),
        YoungFriendCount = group.Count()
    })
    .FirstOrDefaultAsync();

// Or filter inside the group (e.g. only recent friendships)
var recent = await graph.Nodes<Person>()
    .Where(p => p.FirstName == "Alice")
    .PathSegments<Person, Knows, Person>()
    .GroupBy(segment => segment.StartNode)
    .Select(group => new
    {
        PersonName = group.Key.FirstName,
        RecentFriends = group
            .Where(s => s.Relationship.Since > DateTime.UtcNow.AddDays(-12))
            .Select(s => s.EndNode.FirstName)
            .ToList()
    })
    .FirstOrDefaultAsync();
```

The inner `Select` may itself build an anonymous type, producing a list of nested records:

```csharp
.Select(group => new
{
    PersonName = group.Key.FirstName,
    Friends = group.Select(s => new { s.EndNode.FirstName, s.EndNode.Age }).ToList()
})
```

The inner collection can also be **aggregated**, **ordered**, or **re-grouped**. On a graph
provider these lower to a scoped `CALL { … }` subquery (a pattern comprehension cannot order or
aggregate a list):

```csharp
.Select(group => new
{
    Name = group.Key.FirstName,
    FriendCount = group.Count(),
    AverageAge = group.Average(s => s.EndNode.Age),
    OldestFriend = group.Max(s => s.EndNode.Age),
    // Ordered collection
    ByAge = group.OrderBy(s => s.EndNode.Age)
        .Select(s => new { s.EndNode.FirstName, s.EndNode.Age }).ToList(),
    // Nested grouping
    ByBand = group.GroupBy(s => s.EndNode.Age >= 30 ? "Senior" : "Junior")
        .Select(g => new { Band = g.Key, Count = g.Count() }).ToList()
})
```

Correlated collection projections require the provider to declare the `CallSubqueries`
capability; projections that use `group.Count()` also require `PatternSizeProjection`. Providers
that do not declare the required capabilities decline the query at translation time. As with LINQ
`GroupBy`, roots with no matching path segment do not produce an empty group row.

### Scalar-key grouping (per-group aggregates)

You can also group a node set by a **scalar key** and project per-group aggregates, either through
a `Select` over the grouping or through the result-selector `GroupBy` overload:

```csharp
var byDepartment = await graph.Nodes<Employee>()
    .Where(e => e.Active)
    .GroupBy(e => e.Department)
    .Select(g => new
    {
        Department = g.Key,
        Count = g.Count(),
        TotalSalary = g.Sum(e => e.Salary),
        AverageAge = g.Average(e => e.Age),
        Youngest = g.Min(e => e.Age),
        Oldest = g.Max(e => e.Age),
    })
    .ToListAsync();

// Result-selector overload, and key-only grouping (distinct keys):
var counts = await graph.Nodes<Employee>()
    .GroupBy(e => e.Department, (dept, group) => new { Department = dept, Count = group.Count() })
    .ToListAsync();
var departments = await graph.Nodes<Employee>()
    .GroupBy(e => e.Department)
    .Select(g => g.Key)
    .ToListAsync();
```

Scalar-key grouping requires the provider to declare the `GroupByAggregation` capability. The
supported surface is deliberately minimal: a node root (optionally with a `Where` before `GroupBy`),
a scalar key, and a projection that reads `g.Key` and/or the `Count`/`Sum`/`Average`/`Min`/`Max`
aggregates. Everything else — entity or composite keys, element selectors, grouping after a
traversal, collection projections such as `g.Select(...).ToList()`, filtered aggregates such as
`g.Count(predicate)`, and `Distinct`/`OrderBy`/`Skip`/`Take` applied to the grouped result — is
declined at translation time with a `NotSupportedException`; materialize the grouped query first and
continue client-side.

For correlated path grouping, the recognized grammar over the group — `Select`, `Where`,
`OrderBy`/`OrderByDescending`,
`Count`, `Average`/`Sum`/`Min`/`Max`, nested `GroupBy` (optionally terminated by
`ToList`/`ToArray`/`AsEnumerable`), or a projection of the group key — is a **shared contract
enforced identically across providers**. Any other operation over the group (for example
`First`, `Take`, `Skip`, `Distinct`, `ElementAt`) cannot be lowered to a pattern comprehension or
subquery and is rejected up-front with the same `GraphQueryTranslationException` on every
provider, so a shape that fails on a graph database fails the same way on the in-memory provider
rather than silently succeeding client-side. Collection projections require exactly one `Select`;
`Where`, ordering, and nested grouping must precede it. A bare group collection, multiple `Select`
operations, or a filter/order applied after `Select` is rejected by the same shared validator.

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
var theAlice = await graph.Nodes<Person>()
    .SingleAsync(p => p.FirstName == "Alice");

// Get last
var youngest = await graph.Nodes<Person>()
    .OrderBy(p => p.Age)
    .LastAsync();

// Safe versions that return null
var maybeAlice = await graph.Nodes<Person>()
    .FirstOrDefaultAsync(p => p.FirstName == "Alice");
```

Value aggregates such as `SumAsync`, `MinAsync`, `MaxAsync`, and `AverageAsync` after unordered `Take` or `Skip` operate over an arbitrary row subset; add `OrderBy` before pagination when deterministic results matter.

### GroupBy and Aggregates

```csharp
// Group by with count
var byLastName = await graph.Nodes<Person>()
    .GroupBy(p => p.LastName)
    .Select(g => new
    {
        LastName = g.Key,
        Count = g.Count()
    })
    .ToListAsync();

// Multiple aggregations
var ageStats = await graph.Nodes<Person>()
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
var recentConnections = await graph.Relationships<Knows>()
    .Where(k => k.Since > DateTime.UtcNow.AddDays(-30))
    .ToListAsync();

// Relationships from specific node
var aliceKnows = await graph.Relationships<Knows>()
    .Where(k => k.StartNodeId == aliceId)
    .ToListAsync();

// Search either endpoint
var connectedToAlice = await graph.Relationships<Knows>()
    .Where(k => k.StartNodeId == aliceId || k.EndNodeId == aliceId)
    .ToListAsync();
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

// Use a typed node search as the source of a traversal
var colleagues = await graph.Nodes<Person>()
    .Search("cloud architect")
    .Traverse<Knows, Person>(options => options
        .Depth(1, 2)
        .Direction(GraphTraversalDirection.Both))
    .Where(person => person.IsActive)
    .OrderBy(person => person.Name)
    .ToListAsync();

// Preserve full path information after a typed node search
var paths = await graph.SearchNodes<Person>("cloud architect")
    .TraversePaths<Knows, Person>(1, 3)
    .ToListAsync();
```

Typed node searches can feed `Traverse<TRel, TEnd>`,
`PathSegments<TStart, TRel, TEnd>`, and `TraversePaths<TRel, TEnd>`; later filters,
projections, ordering, paging, and terminals apply to the traversal results. Mixed node-and-relationship
searches from `graph.Search(...)` cannot be traversal sources because they do not have one typed node
scope. Use `SearchNodes<T>(...)` or `Nodes<T>().Search(...)` when traversal is required.

### Traditional String Operations

```csharp
// Contains search
var results = await graph.Nodes<Article>()
    .Where(a => a.Content.Contains("machine learning"))
    .ToListAsync();

// Case-insensitive search
var caseInsensitive = await graph.Nodes<Article>()
    .Where(a => a.Title.ToLower().Contains("graph"))
    .ToListAsync();

// Multiple term search
var multiTerm = await graph.Nodes<Article>()
    .Where(a => a.Content.Contains("graph") && a.Content.Contains("database"))
    .ToListAsync();

// Starts with / Ends with
var prefixSearch = await graph.Nodes<Person>()
    .Where(p => p.Email.StartsWith("admin@"))
    .ToListAsync();
```

### Search Features

- **Case Insensitive**: All search operations are case-insensitive by default
- **Exact-token matching**: Terms match whole words, not substrings — searching "vaca" does not match "vacation"
- **Multi-word AND**: A multi-term query such as `Search("machine learning")` matches an entity only when **all** terms match, in any order and at any distance (not just when any one term matches)
- **Property Control**: Use `[Property(IncludeInFullTextSearch = false)]` to exclude properties from search; only the entity's own string properties are searched (complex-property value nodes are not)
- **Automatic Indexing**: Full-text indexes are created and managed automatically
- **LINQ Integration**: Seamlessly integrate search into existing LINQ query chains
- **Traversal source**: Typed node search results can directly feed node traversal operators
- **Unordered results**: Search result order is unspecified; add an explicit `OrderBy` when order matters

## Combining Node and Relationship Queries

For complex scenarios, you can execute multiple queries and join in memory:

```csharp
// Get all data
var people = await graph.Nodes<Person>().ToListAsync();
var knows = await graph.Relationships<Knows>().ToListAsync();

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
var categorized = await graph.Nodes<Person>()
    .Select(p => new
    {
        p.FirstName,
        Category = p.Age < 18 ? "Minor" :
                   p.Age < 65 ? "Adult" :
                   "Senior",
        Discount = p.Age < 18 || p.Age >= 65 ? 0.2 : 0.0
    })
    .ToListAsync();
```
