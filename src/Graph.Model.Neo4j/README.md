# Graph.Model.Neo4j

[![NuGet](https://img.shields.io/nuget/v/Cvoya.Graph.Model.Neo4j.svg)](https://www.nuget.org/packages/Cvoya.Graph.Model.Neo4j/)
[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](https://opensource.org/licenses/Apache-2.0)

High-performance Neo4j implementation of the Graph.Model abstraction layer, providing seamless LINQ-to-Cypher translation, transaction management, and advanced graph operations.

## 🌟 Overview

This package provides a complete implementation of the `IGraph` interface for Neo4j graph databases, enabling seamless integration with the Graph.Model abstraction layer. It translates LINQ queries to optimized Cypher queries, manages Neo4j transactions, and handles complex property serialization automatically.

## 🚀 Features

- **🔄 LINQ-to-Cypher Translation** - Automatic conversion of LINQ queries to optimized Cypher
- **📈 Full `IGraphQueryable<T>` Support** - Advanced graph querying with depth control and traversal
- **⚡ Transaction Management** - Complete ACID transaction support with proper resource disposal
- **🏗️ Complex Property Serialization** - Automatic handling of complex types using internal relationships
- **🔧 Constraint Management** - Automatic creation and management of Neo4j constraints and indexes
- **🌐 Connection Pooling** - Efficient connection management using Neo4j driver pooling
- **📊 Query Optimization** - Performance hints, caching, and query profiling support
- **📍 Spatial Data Support** - Native handling of `Point` types for spatial queries
- **⚙️ Bulk Operations** - Optimized batch processing for large datasets

## 📦 Installation

```bash
dotnet add package Cvoya.Graph.Model.Neo4j
```

## 🔧 Requirements

- **.NET 8.0** or later
- **Neo4j 4.0** or later (5.x recommended)
- **Graph.Model** package

## 🏃‍♂️ Quick Start

### Basic Setup

```csharp
using Cvoya.Graph.Model.Neo4j;

// Option 1: Using connection string (simplest)
var store = new Neo4jGraphStore(
    uri: "bolt://localhost:7687",
    username: "neo4j",
    password: "password",
    databaseName: "neo4j"
);
var graph = store.Graph;

// Option 2: Using existing Neo4j driver
var driver = GraphDatabase.Driver("bolt://localhost:7687",
    AuthTokens.Basic("neo4j", "password"));
var store = new Neo4jGraphStore(driver, "neo4j");
var graph = store.Graph;

// Option 3: Using environment variables
// NEO4J_URI, NEO4J_USER, NEO4J_PASSWORD, NEO4J_DATABASE
var store = new Neo4jGraphStore(null, null, null); // Reads from environment
var graph = store.Graph;
```

### Configuration with Environment Variables

Set these environment variables for automatic configuration:

```bash
export NEO4J_URI="bolt://localhost:7687"
export NEO4J_USER="neo4j"
export NEO4J_PASSWORD="your-password"
export NEO4J_DATABASE="neo4j"
```

Then simply:

```csharp
var store = new Neo4jGraphStore(null, null, null); // Automatically configured
var graph = store.Graph;
```

## 📚 Usage Examples

### Domain Model Definition

```csharp
using Cvoya.Graph.Model;

[Node("Person")]
public class Person : INode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Property("first_name", Index = true)]
    public string FirstName { get; set; } = string.Empty;

    [Property("last_name", Index = true)]
    public string LastName { get; set; } = string.Empty;

    [Property]
    public int Age { get; set; }

    [Property]
    public Address? HomeAddress { get; set; } // Complex type - auto-serialized
}

public class Address
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public Point? Coordinates { get; set; } // Spatial data
}

[Relationship("KNOWS")]
public class Knows : IRelationship<Person, Person>
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string StartNodeId { get; set; } = string.Empty;
    public string EndNodeId { get; set; } = string.Empty;
    public RelationshipDirection Direction { get; init; } = RelationshipDirection.Outgoing;

    public Person? Source { get; set; }
    public Person? Target { get; set; }

    [Property]
    public DateTime Since { get; set; }

    [Property]
    public string RelationshipType { get; set; } = "friend";
}
```

### Basic CRUD Operations

```csharp
await using var store = new Neo4jGraphStore("bolt://localhost:7687", "neo4j", "password");
var graph = store.Graph;

// Create nodes
var alice = new Person
{
    FirstName = "Alice",
    LastName = "Smith",
    Age = 30,
    HomeAddress = new Address
    {
        Street = "123 Main St",
        City = "Portland",
        Country = "USA",
        Coordinates = new Point(45.5152, -122.6784) // Portland coordinates
    }
};

await graph.CreateNodeAsync(alice);

var bob = new Person { FirstName = "Bob", LastName = "Jones", Age = 25 };
await graph.CreateNodeAsync(bob);

// Create relationships
var friendship = new Knows
{
    StartNodeId = alice.Id,
    EndNodeId = bob.Id,
    Since = DateTime.UtcNow.AddYears(-2),
    RelationshipType = "close friend"
};

await graph.CreateRelationshipAsync(friendship);
```

### Advanced LINQ Queries

```csharp
// Complex LINQ queries are automatically translated to optimized Cypher
var youngFriends = await graph.Nodes<Person>()
    .Where(p => p.Age < 30)
    .Where(p => p.HomeAddress != null && p.HomeAddress.City == "Portland")
    .OrderBy(p => p.LastName)
    .ThenBy(p => p.FirstName)
    .ToListAsync();

// Graph-specific operations
var friendsOfAlice = await graph.Nodes<Person>()
    .Where(p => p.FirstName == "Alice")
    .WithDepth(2) // Load relationships up to 2 levels deep
    .FirstOrDefaultAsync();

// Traversal queries
var friendsOfFriends = await graph.Nodes<Person>()
    .Where(p => p.FirstName == "Alice")
    .Traverse<Person, Knows, Person>()
    .WithDepth(1, 2)
    .Where(friend => friend.Age > 20)
    .ToListAsync();

// Spatial queries
var nearbyPeople = await graph.Nodes<Person>()
    .Where(p => p.HomeAddress != null &&
                p.HomeAddress.Coordinates != null &&
                p.HomeAddress.Coordinates.Distance(myLocation) < 1000) // Within 1km
    .ToListAsync();
```

### Transaction Management

```csharp
// Simple transaction with automatic rollback on exception
await using var transaction = await graph.GetTransactionAsync();
try
{
    var person = new Person { FirstName = "John", LastName = "Doe" };
    await graph.CreateNodeAsync(person, transaction: transaction);

    var company = new Company { Name = "TechCorp" };
    await graph.CreateNodeAsync(company, transaction: transaction);

    var employment = new WorksFor
    {
        StartNodeId = person.Id,
        EndNodeId = company.Id,
        StartDate = DateTime.UtcNow
    };
    await graph.CreateRelationshipAsync(employment, transaction: transaction);

    await transaction.Commit();
}
catch
{
    await transaction.Rollback();
    throw;
}

// Automatic transaction management
await store.Graph.WithTransactionAsync(async (graph, tx) =>
{
    await graph.CreateNodeAsync(person, transaction: tx);
    await graph.CreateNodeAsync(company, transaction: tx);
    await graph.CreateRelationshipAsync(employment, transaction: tx);
    // Transaction is automatically committed if no exception occurs
});
```

### Complex Object Serialization

The Neo4j provider automatically handles complex objects by creating special `__PROPERTY__` relationships:

```csharp
public class Company : INode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Property]
    public string Name { get; set; } = string.Empty;

    [Property]
    public Address Headquarters { get; set; } = new(); // Serialized as relationship

    [Property]
    public List<Address> Offices { get; set; } = new(); // Collection serialized

    [Property]
    public ContactInfo Contact { get; set; } = new(); // Nested objects supported
}

public class ContactInfo
{
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public List<string> SocialMedia { get; set; } = new();
}

// Usage - complex objects are automatically serialized/deserialized
var company = new Company
{
    Name = "TechCorp",
    Headquarters = new Address { City = "Seattle", Country = "USA" },
    Offices = new List<Address>
    {
        new() { City = "Portland", Country = "USA" },
        new() { City = "Vancouver", Country = "Canada" }
    },
    Contact = new ContactInfo
    {
        Email = "info@techcorp.com",
        Phone = "+1-555-0123",
        SocialMedia = new List<string> { "@techcorp", "linkedin.com/company/techcorp" }
    }
};

await graph.CreateNodeAsync(company); // All complex properties automatically handled
```

### Spatial Data Operations

```csharp
// Create points (longitude, latitude)
var seattle = new Point(-122.3321, 47.6062);
var portland = new Point(-122.6784, 45.5152);

var person = new Person
{
    FirstName = "Alice",
    HomeAddress = new Address
    {
        City = "Seattle",
        Coordinates = seattle
    }
};

await graph.CreateNodeAsync(person);

// Spatial queries
var nearSeattle = await graph.Nodes<Person>()
    .Where(p => p.HomeAddress != null &&
                p.HomeAddress.Coordinates != null &&
                p.HomeAddress.Coordinates.Distance(seattle) < 100000) // 100km
    .ToListAsync();

// Distance calculations in Cypher
var distances = await graph.Nodes<Person>()
    .Where(p => p.HomeAddress != null && p.HomeAddress.Coordinates != null)
    .Select(p => new
    {
        Name = p.FirstName,
        Distance = p.HomeAddress.Coordinates.Distance(seattle)
    })
    .OrderBy(x => x.Distance)
    .ToListAsync();
```

### Performance Optimization

```csharp
// Index creation is automatic based on [Property(Index = true)]
// But you can also create custom constraints

// Batch operations for better performance
var people = new List<Person>();
for (int i = 0; i < 1000; i++)
{
    people.Add(new Person { FirstName = $"Person{i}", Age = i % 100 });
}

// Bulk insert (if supported by provider)
await graph.BulkCreateNodesAsync(people);

// Query optimization with hints
var optimizedQuery = await graph.Nodes<Person>()
    .Where(p => p.FirstName.StartsWith("A"))
    .OrderBy(p => p.Age)
    .Take(100)
    .ToListAsync();

// Use transactions for multiple operations
await using var tx = await graph.GetTransactionAsync();
foreach (var person in people.Take(10))
{
    await graph.CreateNodeAsync(person, transaction: tx);
}
await tx.Commit();
```

## 🔧 Configuration

### Connection Configuration

```csharp
// Detailed configuration options
var store = new Neo4jGraphStore(
    uri: "neo4j+s://your-instance.databases.neo4j.io:7687", // Secure connection
    username: "neo4j",
    password: "your-password",
    databaseName: "production",
    loggerFactory: loggerFactory // Optional logging
);

// With existing driver for advanced configuration
var driver = GraphDatabase.Driver("bolt://localhost:7687",
    AuthTokens.Basic("neo4j", "password"),
    config => config
        .WithMaxConnectionLifetime(TimeSpan.FromMinutes(30))
        .WithMaxConnectionPoolSize(50)
        .WithConnectionAcquisitionTimeout(TimeSpan.FromMinutes(2)));

var store = new Neo4jGraphStore(driver, "myapp");
```

### Database Management

```csharp
// Create database if it doesn't exist
await Neo4jGraphStore.CreateDatabaseIfNotExistsAsync(driver, "myapp");

// Multiple databases
var store1 = new Neo4jGraphStore(driver, "app1");
var store2 = new Neo4jGraphStore(driver, "app2");
```

## 🎯 LINQ-to-Cypher Translation

The provider automatically translates LINQ expressions to efficient Cypher queries:

```csharp
// This LINQ query...
var result = await graph.Nodes<Person>()
    .Where(p => p.Age > 25 && p.FirstName.StartsWith("A"))
    .OrderBy(p => p.LastName)
    .Take(10)
    .ToListAsync();

// ...is translated to optimized Cypher:
// MATCH (p:Person)
// WHERE p.age > $age AND p.first_name STARTS WITH $prefix
// RETURN p
// ORDER BY p.last_name
// LIMIT 10
```

### Traversal Translation

```csharp
// Complex traversal queries...
var friends = await graph.Nodes<Person>()
    .Where(p => p.FirstName == "Alice")
    .Traverse<Person, Knows, Person>()
    .WithDepth(1, 3)
    .Where(friend => friend.Age > 18)
    .ToListAsync();

// ...become efficient Cypher patterns:
// MATCH (start:Person {first_name: $name})
// MATCH (start)-[:KNOWS*1..3]-(friend:Person)
// WHERE friend.age > $minAge
// RETURN DISTINCT friend
```

## 🔒 Security Considerations

- **Parameterized Queries**: All user input is automatically parameterized to prevent injection attacks
- **Authentication**: Supports all Neo4j authentication methods (basic, Kerberos, custom)
- **Encryption**: Supports encrypted connections (bolt+s, neo4j+s protocols)
- **Access Control**: Respects Neo4j's role-based access control (RBAC)

## 📊 Monitoring and Logging

```csharp
// Configure logging
var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole()
           .AddFile("logs/graph-{Date}.txt")
           .SetMinimumLevel(LogLevel.Information);
});

var store = new Neo4jGraphStore(
    "bolt://localhost:7687",
    "neo4j",
    "password",
    "myapp",
    loggerFactory);

// Query logging automatically includes:
// - Generated Cypher queries
// - Execution times
// - Parameter values (safely logged)
// - Exception details
```

## ⚡ Performance Tips

1. **Use Indexes**: Mark frequently queried properties with `[Property(Index = true)]`
2. **Batch Operations**: Use transactions for multiple operations
3. **Limit Depth**: Be careful with traversal depth in large graphs
4. **Use Specific Types**: Generic queries are less efficient than specific node types
5. **Project Early**: Use `Select()` to limit returned data

```csharp
// Good: Specific and limited
var names = await graph.Nodes<Person>()
    .Where(p => p.Age > 25)
    .Select(p => new { p.FirstName, p.LastName })
    .Take(100)
    .ToListAsync();

// Avoid: Loading everything
var people = await graph.Nodes<Person>()
    .ToListAsync(); // Loads entire table!
```

## 🚨 Troubleshooting

### Common Issues

1. **Connection Problems**

   ```csharp
   // Verify connection
   await using var session = driver.AsyncSession();
   var result = await session.RunAsync("RETURN 1");
   ```

2. **Serialization Issues**

   ```csharp
   // Complex objects must be serializable
   // Avoid circular references
   // Use [Property(Ignore = true)] for computed properties
   ```

3. **Transaction Timeouts**
   ```csharp
   // Configure longer timeouts for large operations
   var config = Config.Builder
       .WithConnectionAcquisitionTimeout(TimeSpan.FromMinutes(5))
       .ToConfig();
   ```

## 📚 Advanced Topics

- **[Custom Serialization](docs/serialization.md)** - Handling complex object graphs
- **[Query Optimization](docs/optimization.md)** - Performance tuning strategies
- **[Testing Strategies](docs/testing.md)** - Unit testing with embedded Neo4j
- **[Migration Patterns](docs/migrations.md)** - Schema evolution strategies

## 🔧 Requirements

- **.NET 8.0** or later
- **Neo4j 4.0+** (5.x recommended)
- **Cvoya.Graph.Model** package

## 📄 License

Licensed under the Apache License, Version 2.0. See [LICENSE](../../LICENSE) for details.
