# Example 1: Basic CRUD Operations

This example demonstrates the fundamental serialization using basic CRUD operations.

## What You'll Learn

- How to define domain models using `Node` records
- Creating nodes without and with complex properties (e.g. `Address`)

## Domain Model

The example uses a simple organizational structure:

- **Person**: Represents a person with properties like Name, Email, Age, and Department
- **PersonWithAddress**: Represents a person with properties like Name, Email, and Address. Address is considered a complex property.

## Key Concepts Demonstrated

### 1. Node Attributes

```csharp
[Node("Person")]
public record Person : Node { ... }
```

### 2. Creating Graph Data

```csharp
await graph.CreateNodeAsync(person);
```

### 3. Creating Nodes with complex properties

```csharp
public record Address { ... }
public record Person : Node { Address = ... }
```

## Prerequisites

- Neo4j instance running on `localhost:7687`
- Username: `neo4j`
- Password: `password`

## Running the Example

```bash
cd examples/Example0.BasicSerialization
dotnet run
```
