---
title: Getting Started with Graph Model
layout: default
---

# Getting Started with Graph Model

This guide will walk you through the basics of using Graph Model in your .NET applications, covering installation, domain modeling, basic operations, and key concepts.

## 📦 Installation

Add the Graph Model package to your project:

```bash
dotnet add package Cvoya.Graph.Model
```

You'll also need a graph provider implementation, such as:

```bash
dotnet add package Cvoya.Graph.Model.Neo4j
```

For compile-time validation (recommended):

```bash
dotnet add package Cvoya.Graph.Model.Analyzers
```

## 🏗️ Core Concepts

### Entities: The Foundation

All graph entities implement the `IEntity` interface, providing a unique identifier:

```csharp
public interface IEntity
{
    string Id { get; init; }
}
```

### Nodes: Your Data Entities

Nodes represent the primary entities in your graph. They implement `INode`:

```csharp
public interface INode : IEntity
{
    // Marker interface extending IEntity
}
```

### Relationships: Connecting Your Data

Relationships connect nodes and can have properties. There are two main interfaces:

```csharp
// Basic relationship
public interface IRelationship : IEntity
{
    RelationshipDirection Direction { get; init; }
    string StartNodeId { get; init; }
    string EndNodeId { get; init; }
}

// Strongly-typed relationship with navigation properties
public interface IRelationship<TSource, TTarget> : IRelationship
    where TSource : class, INode, new()
    where TTarget : class, INode, new()
{
    TSource Source { get; set; }
    TTarget Target { get; set; }
}
```

## 🎯 Define Your Domain Model

Let's build a social media domain model to demonstrate the concepts:

### 1. Basic Node Definition

```csharp
using Cvoya.Graph.Model;

[Node("Person")]
public class Person : INode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Property(Label = "first_name")]
    public string FirstName { get; set; } = string.Empty;

    [Property(Label = "last_name")]
    public string LastName { get; set; } = string.Empty;

    [Property] // If a label isn't defined, the name of the property will be used
    public int Age { get; set; }

    [Property(Label = "email")]
    public string Email { get; set; } = string.Empty;

    // The Property attribute is optional - the name of the property will be used
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Property("bio")]
    public string Bio { get; set; } = string.Empty;

    // Computed property - excluded from persistence
    [Property(Ignore = true)]
    public string FullName => $"{FirstName} {LastName}";
}
```

### 2. Complex Properties

Graph Model supports complex object properties that are automatically serialized:

```csharp
public class Address
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
}

[Node("Company")]
public class Company : INode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Industry { get; set; } = string.Empty;
    public Address Headquarters { get; set; } = new();
    public List<Address> Offices { get; set; } = new();
    public DateTime Founded { get; set; }
    public int EmployeeCount { get; set; }
}
```

### 3. Relationship Definitions

```csharp
[Relationship("KNOWS")]
public class Knows : IRelationship
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string StartNodeId { get; set; } = string.Empty;
    public string EndNodeId { get; set; } = string.Empty;
    public RelationshipDirection Direction { get; init; } = RelationshipDirection.Outgoing;

    public DateTime Since { get; set; }
    public int StrengthScore { get; set; } = 1; // 1-10 scale
}

[Relationship("WORKS_FOR")]
public class WorksFor : IRelationship
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string StartNodeId { get; set; } = string.Empty;
    public string EndNodeId { get; set; } = string.Empty;
    public RelationshipDirection Direction { get; init; } = RelationshipDirection.Outgoing;

    public string Position { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal Salary { get; set; }
    public string Department { get; set; } = string.Empty;
}

[Relationship("MANAGES")]
public class Manages : IRelationship
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string StartNodeId { get; set; } = string.Empty;
    public string EndNodeId { get; set; } = string.Empty;
    public RelationshipDirection Direction { get; init; } = RelationshipDirection.Outgoing;

    public DateTime StartDate { get; set; }
    public string ManagementLevel { get; set; } = "Direct"; // Direct, Skip-level, etc.
}
```

## 🚀 Create a Graph Instance

The specific implementation depends on your chosen provider. Here's how to set up the Neo4j provider:

```csharp
using Cvoya.Graph.Model.Neo4j;

// Simple setup
var store = new Neo4jGraphStore(
    uri: "bolt://localhost:7687",
    username: "neo4j",
    password: "password",
    databaseName: "social_network"
);
var graph = store.Graph;

// Or with environment variables
// Set NEO4J_URI, NEO4J_USER, NEO4J_PASSWORD, NEO4J_DATABASE
var store = new Neo4jGraphStore(null, null, null); // Reads from environment
var graph = store.Graph;
```

## 🎯 Basic Operations

### Creating Nodes

```csharp
// Create a person
var alice = new Person
{
    FirstName = "Alice",
    LastName = "Johnson",
    Age = 30,
    Email = "alice.johnson@email.com",
    Bio = "Software engineer passionate about graph databases"
};

await graph.CreateNodeAsync(alice);
Console.WriteLine($"Created person with ID: {alice.Id}");

// Create a company
var techCorp = new Company
{
    Name = "TechCorp",
    Industry = "Software",
    Headquarters = new Address
    {
        Street = "123 Tech Street",
        City = "San Francisco",
        State = "CA",
        Country = "USA",
        ZipCode = "94105"
    },
    Founded = new DateTime(2010, 1, 1),
    EmployeeCount = 500
};

await graph.CreateNodeAsync(techCorp);
```

### Creating Relationships

```csharp
// Create employment relationship
var employment = new WorksFor
{
    StartNodeId = alice.Id,
    EndNodeId = techCorp.Id,
    Position = "Senior Software Engineer",
    StartDate = DateTime.UtcNow.AddYears(-2),
    Salary = 120000,
    Department = "Engineering"
};

await graph.CreateRelationshipAsync(employment);

// Create friendship
var bob = new Person
{
    FirstName = "Bob",
    LastName = "Smith",
    Age = 28,
    Email = "bob.smith@email.com"
};
await graph.CreateNodeAsync(bob);

var friendship = new Knows
{
    StartNodeId = alice.Id,
    EndNodeId = bob.Id,
    Since = DateTime.UtcNow.AddYears(-5),
    RelationshipType = "college friend",
    StrengthScore = 8
};

await graph.CreateRelationshipAsync(friendship);
```

### Querying Nodes

```csharp
// Get a single node by ID
var person = await graph.GetNodeAsync<Person>(alice.Id);
Console.WriteLine($"Found: {person.FullName}");

// Basic LINQ queries
var engineers = await graph.Nodes<Person>()
    .Where(p => p.Bio.Contains("engineer"))
    .OrderBy(p => p.FirstName)
    .ToListAsync();

Console.WriteLine($"Found {engineers.Count} engineers");

// Complex queries with multiple conditions
var youngProfessionals = await graph.Nodes<Person>()
    .Where(p => p.Age >= 25 && p.Age <= 35)
    .Where(p => !string.IsNullOrEmpty(p.Email))
    .Select(p => new { p.FirstName, p.LastName, p.Age, p.Email })
    .ToListAsync();

// Company queries
var techCompanies = await graph.Nodes<Company>()
    .Where(c => c.Industry == "Software" || c.Industry == "Technology")
    .Where(c => c.EmployeeCount > 100)
    .OrderByDescending(c => c.EmployeeCount)
    .ToListAsync();
```

### Querying Relationships

```csharp
// Find recent employment relationships
var recentHires = await graph.Relationships<WorksFor>()
    .Where(w => w.StartDate > DateTime.UtcNow.AddMonths(-6))
    .ToListAsync();

// Find strong friendships
var closeFriends = await graph.Relationships<Knows>()
    .Where(k => k.StrengthScore >= 7)
    .Where(k => k.Since < DateTime.UtcNow.AddYears(-3))
    .ToListAsync();
```

## 🔄 Graph Traversal

Graph traversal is where Graph Model really shines:

### Basic Traversal

```csharp
// Find all of Alice's friends
var alicesFriends = await graph.Nodes<Person>()
    .Where(p => p.Id == alice.Id)
    .Traverse<Person, Knows, Person>()
    .ToListAsync();

Console.WriteLine($"Alice knows {alicesFriends.Count} people");

// Find Alice's colleagues
var alicesColleagues = await graph.Nodes<Person>()
    .Where(p => p.Id == alice.Id)
    .Traverse<Person, WorksFor, Company>()  // Alice -> Company
    .Traverse<Company, WorksFor, Person>()  // Company -> Other employees
    .Where(colleague => colleague.Id != alice.Id) // Exclude Alice herself
    .ToListAsync();
```

### Depth-Controlled Traversal

```csharp
// Friends and friends-of-friends (social network up to 2 hops)
var extendedNetwork = await graph.Nodes<Person>()
    .Where(p => p.Id == alice.Id)
    .Traverse<Person, Knows, Person>()
    .WithDepth(1, 2) // 1 to 2 hops away
    .Where(friend => friend.Age >= 25)
    .Distinct() // Remove duplicates
    .ToListAsync();

Console.WriteLine($"Alice's extended network: {extendedNetwork.Count} people");
```

### Path Analysis

```csharp
// Analyze the paths between Alice and her connections
var connectionPaths = await graph.Nodes<Person>()
    .Where(p => p.Id == alice.Id)
    .PathSegments<Person, Knows, Person>()
    .Where(path => path.EndNode.Age > 25)
    .Select(path => new {
        Friend = path.EndNode.FullName,
        FriendsSince = path.Relationship.Since,
        RelationshipType = path.Relationship.RelationshipType,
        Strength = path.Relationship.StrengthScore
    })
    .OrderByDescending(x => x.Strength)
    .ToListAsync();

foreach (var connection in connectionPaths)
{
    Console.WriteLine($"{connection.Friend}: {connection.RelationshipType} since {connection.FriendsSince:yyyy-MM-dd} (strength: {connection.Strength})");
}
```

## ⚡ Transaction Management

Transactions ensure data consistency for complex operations:

### Basic Transactions

```csharp
await using var transaction = await graph.GetTransactionAsync();
try
{
    // Create multiple related entities atomically
    var newPerson = new Person
    {
        FirstName = "Charlie",
        LastName = "Brown",
        Age = 32,
        Email = "charlie.brown@email.com"
    };

    await graph.CreateNodeAsync(newPerson, transaction: transaction);

    // Create relationships to existing entities
    var friendship1 = new Knows
    {
        StartNodeId = alice.Id,
        EndNodeId = newPerson.Id,
        Since = DateTime.UtcNow,
        RelationshipType = "new friend",
        StrengthScore = 5
    };

    var friendship2 = new Knows
    {
        StartNodeId = bob.Id,
        EndNodeId = newPerson.Id,
        Since = DateTime.UtcNow,
        RelationshipType = "mutual friend",
        StrengthScore = 6
    };

    await graph.CreateRelationshipAsync(friendship1, transaction: transaction);
    await graph.CreateRelationshipAsync(friendship2, transaction: transaction);

    // Commit all changes
    await transaction.Commit();
    Console.WriteLine("Successfully created person and relationships");
}
catch (Exception ex)
{
    await transaction.Rollback();
    Console.WriteLine($"Transaction failed: {ex.Message}");
    throw;
}
```

### Complex Business Operations

```csharp
// Business operation: Hire a new employee
public async Task HireEmployeeAsync(IGraph graph, Person person, Company company,
    string position, decimal salary, string department)
{
    await using var transaction = await graph.GetTransactionAsync();
    try
    {
        // Create the person if they don't exist
        try
        {
            await graph.GetNodeAsync<Person>(person.Id, transaction: transaction);
        }
        catch (KeyNotFoundException)
        {
            await graph.CreateNodeAsync(person, transaction: transaction);
        }

        // Create employment relationship
        var employment = new WorksFor
        {
            StartNodeId = person.Id,
            EndNodeId = company.Id,
            Position = position,
            StartDate = DateTime.UtcNow,
            Salary = salary,
            Department = department
        };

        await graph.CreateRelationshipAsync(employment, transaction: transaction);

        // Update company employee count
        company.EmployeeCount++;
        await graph.UpdateNodeAsync(company, transaction: transaction);

        await transaction.Commit();
    }
    catch
    {
        await transaction.Rollback();
        throw;
    }
}

// Usage
await HireEmployeeAsync(graph, newEmployee, techCorp, "Software Engineer", 95000, "Engineering");
```

## 🔧 Updating and Deleting

### Updating Entities

```csharp
// Update a person's information
alice.Age = 31;
alice.Bio = "Senior software engineer and graph database enthusiast";
await graph.UpdateNodeAsync(alice);

// Update a relationship
friendship.StrengthScore = 9; // Friendship got stronger
friendship.RelationshipType = "best friend";
await graph.UpdateRelationshipAsync(friendship);
```

### Deleting Entities

```csharp
// Delete a relationship
await graph.DeleteRelationshipAsync(friendship.Id);

// Delete a node (and optionally cascade delete relationships)
await graph.DeleteNodeAsync(alice.Id, cascadeDelete: false); // Fails if relationships exist
await graph.DeleteNodeAsync(alice.Id, cascadeDelete: true);  // Deletes all relationships too
```

## 📊 Advanced Queries

### Aggregations

```csharp
// Count nodes
var personCount = await graph.Nodes<Person>().CountAsync();
var companyCount = await graph.Nodes<Company>().CountAsync();

// Count relationships
var friendshipCount = await graph.Relationships<Knows>().CountAsync();
var employmentCount = await graph.Relationships<WorksFor>().CountAsync();

// Group by attributes
var ageGroups = await graph.Nodes<Person>()
    .GroupBy(p => p.Age / 10 * 10) // Group by decade
    .Select(g => new { AgeRange = g.Key, Count = g.Count() })
    .ToListAsync();

// Company statistics
var industryStats = await graph.Nodes<Company>()
    .GroupBy(c => c.Industry)
    .Select(g => new
    {
        Industry = g.Key,
        CompanyCount = g.Count(),
        TotalEmployees = g.Sum(c => c.EmployeeCount),
        AverageSize = g.Average(c => c.EmployeeCount)
    })
    .OrderByDescending(x => x.TotalEmployees)
    .ToListAsync();
```

### Complex Business Queries

```csharp
// Find potential mutual connections
var mutualConnectionOpportunities = await graph.Nodes<Person>()
    .Where(p => p.Id == alice.Id)
    .PathSegments<Person, Knows, Person>()
    .SelectMany(path1 => graph.Nodes<Person>()
        .Where(p => p.Id == path1.EndNode.Id)
        .PathSegments<Person, Knows, Person>()
        .Where(path2 => path2.EndNode.Id != alice.Id)
        .Select(path2 => new
        {
            MutualFriend = path1.EndNode.FullName,
            PotentialConnection = path2.EndNode.FullName,
            MutualConnectionStrength = path1.Relationship.StrengthScore
        }))
    .ToListAsync();

// Find hiring opportunities (people not employed)
var availableCandidates = await graph.Nodes<Person>()
    .Where(p => !graph.Relationships<WorksFor>()
        .Where(w => w.EndDate == null) // Currently employed
        .Any(w => w.StartNodeId == p.Id))
    .Where(p => p.Bio.Contains("engineer"))
    .ToListAsync();
```

## 🎯 Best Practices

### 1. Model Design

```csharp
// Good: Use meaningful labels and properties
[Node("SoftwareEngineer", "Person", "Employee")]
public class SoftwareEngineer : Person
{
    [Property("years_experience", Index = true)]
    public int YearsExperience { get; set; }

    [Property("programming_languages")]
    public List<string> ProgrammingLanguages { get; set; } = new();

    [Property("github_username", Index = true)]
    public string? GitHubUsername { get; set; }
}

// Good: Use specific relationship types
[Relationship("MENTORS")]
public class Mentors : IRelationship<SoftwareEngineer, SoftwareEngineer>
{
    // Implementation...

    [Property]
    public DateTime StartDate { get; set; }

    [Property]
    public string MentorshipType { get; set; } = string.Empty; // Technical, Career, etc.
}
```

### 2. Querying Efficiently

```csharp
// Good: Use indexes and limit results
var seniorEngineers = await graph.Nodes<SoftwareEngineer>()
    .Where(e => e.YearsExperience >= 5) // Uses index
    .Take(100) // Limit results
    .Select(e => new { e.FirstName, e.LastName, e.YearsExperience }) // Project only needed fields
    .ToListAsync();

// Avoid: Loading everything
var allEngineers = await graph.Nodes<SoftwareEngineer>().ToListAsync(); // Expensive!
```

### 3. Resource Management

```csharp
// Good: Use proper disposal
await using var store = new Neo4jGraphStore(connectionString, username, password);
var graph = store.Graph;

// Use transactions for multiple operations
await using var transaction = await graph.GetTransactionAsync();
// ... operations
await transaction.Commit();
```

## 🔍 Next Steps

Now that you understand the basics, explore these advanced topics:

- **[Core Interfaces](core-interfaces.md)** - Deep dive into the type system
- **[Advanced Querying](querying.md)** - Complex LINQ patterns and optimizations
- **[Transaction Management](transactions.md)** - Advanced transaction patterns
- **[Attributes and Configuration](attributes.md)** - Customizing behavior
- **[Best Practices](best-practices.md)** - Performance and design patterns

## 📚 Examples

Check out the comprehensive examples in the repository:

- **[Basic CRUD](../../../examples/Example1.BasicCRUD/)** - Fundamental operations
- **[LINQ & Traversal](../../../examples/Example2.LINQAndTraversal/)** - Advanced querying
- **[Transaction Management](../../../examples/Example3.TransactionManagement/)** - ACID transactions
- **[Advanced Scenarios](../../../examples/Example4.AdvancedScenarios/)** - Complex patterns
- **[Social Network](../../../examples/Example5.SocialNetwork/)** - Real-world implementation

Happy graphing! 🚀
