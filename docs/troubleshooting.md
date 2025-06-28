# 🔧 Troubleshooting Guide

This guide helps you diagnose and resolve common issues when working with GraphModel.

## 🚨 Common Issues and Solutions

### Connection Issues

#### Problem: "Unable to connect to Neo4j database"

```text
Neo4jException: Could not perform discovery for server neo4j://localhost:7687
```

**Solutions:**

1. **Check Neo4j is running**

   ```bash
   # For Neo4j Desktop - ensure database is started
   # For Docker:
   docker ps | grep neo4j

   # Start Neo4j with Docker if not running:
   docker run --name neo4j -p 7474:7474 -p 7687:7687 -e NEO4J_AUTH=neo4j/password neo4j:latest
   ```

2. **Verify connection string format**

   ```csharp
   // ✅ Correct formats:
   "neo4j://localhost:7687"           // Local unencrypted
   "neo4j+s://your-server:7687"       // Remote encrypted
   "neo4j+ssc://your-server:7687"     // Encrypted with self-signed cert

   // ❌ Common mistakes:
   "neo4j://localhost"                // Missing port
   "http://localhost:7474"            // Wrong protocol (that's HTTP interface)
   ```

3. **Check credentials**

   ```csharp
   // Default Neo4j credentials are neo4j/neo4j
   // You must change the password on first login
   var graph = new Neo4jGraph("neo4j://localhost:7687", "neo4j", "your-new-password");
   ```

#### Problem: "Connection timeout"

```text
TimeoutException: The request timed out after 30 seconds
```

**Solutions:**

1. **Increase timeout settings**

   ```csharp
   var config = new ConfigurationOptions
   {
       ConnectionTimeout = TimeSpan.FromSeconds(60),
       ConnectionAcquisitionTimeout = TimeSpan.FromSeconds(60)
   };
   var graph = new Neo4jGraph(connectionString, username, password, config);
   ```

2. **Check network connectivity**

   ```bash
   telnet your-server 7687
   # Should connect if Neo4j is accessible
   ```

### Query Issues

#### Problem: "No nodes found" when you expect results

```csharp
var users = await graph.Nodes<User>().ToListAsync();
// Returns empty list when you expect data
```

**Debugging steps:**

1. **Check node labels in database**

   ```cypher
   // In Neo4j Browser:
   MATCH (n) RETURN DISTINCT labels(n)
   ```

2. **Verify entity class labels**

   ```csharp
   [Node(Label = "User")]  // Must match label in database
   public class User : INode
   ```

3. **Check case sensitivity**

   ```csharp
   [Node(Label = "user")]     // Lowercase
   vs
   [Node(Label = "User")]     // Uppercase - these are different!
   ```

4. **Enable query logging**

   ```csharp
   var loggerFactory = new LoggerFactory { ... }
   var neo4jGraphStore = new Neo4jGraphStore(..., loggerFactory)
   ```

#### Problem: "Invalid cast" when querying

```text
InvalidCastException: Unable to cast object of type 'System.String' to type 'System.Int32'
```

**Solutions:**

1. **Check property types match database**

   ```csharp
   // If database has string IDs but your model expects int:
   public class User : INode
   {
       public string Id { get; set; }  // Use string if Neo4j stores as string
       // Not: public int Id { get; set; }
   }
   ```

2. **Use nullable types for optional properties**

   ```csharp
   public class User : INode
   {
       public string Id { get; set; }
       public int? Age { get; set; }           // Nullable if not always present
       public DateTime? CreatedDate { get; set; }  // Nullable for optional dates
   }
   ```

### Transaction Issues

#### Problem: "Transaction already committed/rolled back"

```text
InvalidOperationException: Cannot execute query on committed/rolled back transaction
```

**Solution:**

```csharp
// ❌ Reusing disposed transaction
using var transaction = await graph.BeginTransactionAsync();
await graph.CreateNodeAsync(user, transaction);
await transaction.CommitAsync();
await graph.CreateNode(anotherUser, transaction);  // Error - transaction already committed

// ✅ Create new transaction or don't commit until all operations complete
using var transaction = await graph.BeginTransactionAsync();
await graph.CreateNodeAsync(user, transaction);
await graph.CreateNodeAsync(anotherUser, transaction);
await transaction.CommitAsync();  // Commit both operations
```

#### Problem: "Deadlock detected"

```text
TransientException: Deadlock detected
```

**Solutions:**

1. **Keep transactions short**

   ```csharp
   // ✅ Short transaction
   using var transaction = await graph.BeginTransactionAsync();
   await graph.CreateNodeAsync(user, transaction);
   await transaction.CommitAsync();

   // ❌ Long-running transaction
   using var transaction = await graph.BeginTransactionAsync();
   await SomeLongRunningOperation();  // Holds locks too long
   await graph.CreateNodeAsync(user, transaction);
   await transaction.CommitAsync();
   ```

2. **Retry logic for deadlocks**

   ```csharp
   public async Task<T> ExecuteWithRetry<T>(Func<Task<T>> operation, int maxRetries = 3)
   {
       for (int attempt = 0; attempt < maxRetries; attempt++)
       {
           try
           {
               return await operation();
           }
           catch (TransientException ex) when (ex.Message.Contains("Deadlock"))
           {
               if (attempt == maxRetries - 1) throw;
               await Task.Delay(TimeSpan.FromMilliseconds(100 * (attempt + 1)));
           }
       }
       throw new InvalidOperationException("Should not reach here");
   }
   ```

### Performance Issues

#### Problem: "Queries are very slow"

**Debugging steps:**

1. **Enable query profiling**

   ```csharp
   var loggerFactory = new LoggerFactory { ... }
   var neo4jGraphStore = new Neo4jGraphStore(..., loggerFactory)
   ```

2. **Look for full table scans**

   ```cypher
   // Profile a slow query:
   PROFILE MATCH (u:User) WHERE u.email = 'test@example.com' RETURN u
   ```

3. **Check for unlimited traversals**

   ```csharp
   // ❌ Large depth - could traverse a large path of the graph
   var connections = await graph.Nodes<User>()
       .Traverse<User, FriendOf, User>()
       .WithDepth(1, 100)
       .ToListAsync();

   // ✅ Limited depth
   var connections = await graph.Nodes<User>()
       .Traverse<User, FriendOf, User>()
       .WithDepth(1, 3)  // Max 3 hops
       .ToListAsync();
   ```

#### Problem: "Out of memory" with large datasets

**Solutions:**

1. **Use pagination**

   ```csharp
   // ❌ Loading everything at once
   var allUsers = await graph.Nodes<User>().ToListAsync();

   // ✅ Paginated loading
   var pageSize = 1000;
   var page = 0;
   List<User> batch;
   do
   {
       batch = await graph.Nodes<User>()
           .OrderBy(u => u.Id)
           .Skip(page * pageSize)
           .Take(pageSize)
           .ToListAsync();

       ProcessBatch(batch);
       page++;
   } while (batch.Count == pageSize);
   ```

2. **Use streaming**

```csharp
   // ✅ Process one at a time
   await foreach (var user in graph.Nodes<User>().AsAsyncEnumerable())
   {
       ProcessUser(user);
   }
```

### Analyzer Issues

#### Problem: Analyzer warnings in IDE

**Understanding warnings:**

- **GM001**: Missing parameterless constructor
- **GM002**: Property must have public accessors
- **GM003**: Property cannot be graph interface type
- **GM004/GM005**: Invalid property type for node/relationship
- **GM006**: Complex type contains graph interface types
- **GM007/GM008/GM009**: Duplicate attribute labels
- **GM010**: Circular reference without nullable

**Solutions:**
See the [Analyzers README](../src/Graph.Model.Analyzers/README.md) for detailed examples of each warning and how to fix them.

## 🔍 Debugging Techniques

### 1. Enable Detailed Logging

```csharp
var loggerFactory = new LoggerFactory { ... }
var neo4jGraphStore = new Neo4jGraphStore(..., loggerFactory)
```

### 2. Inspect Generated Cypher

```csharp
// The logger will show queries like:
// MATCH (n:User) WHERE n.email = $p1 RETURN n
// Parameters: p1 = "test@example.com"
```

### 3. Test Queries Directly in Neo4j Browser

1. Copy the generated Cypher from logs
2. Paste into Neo4j Browser (http://localhost:7474)
3. Run with same parameters
4. Use `PROFILE` or `EXPLAIN` to understand performance

### 4. Verify Database State

```cypher
// Check what's actually in the database:
MATCH (n) RETURN labels(n), count(n)  // Node counts by label
MATCH ()-[r]->() RETURN type(r), count(r)  // Relationship counts by type
MATCH (n:User) RETURN n LIMIT 10  // Sample user nodes
```

### 5. Connection Testing

```csharp
public async Task<bool> TestConnection(string connectionString, string username, string password)
{
    try
    {
        using var graph = new Neo4jGraph(connectionString, username, password);
        await graph.Nodes<object>().Take(1).ToListAsync();
        return true;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Connection failed: {ex.Message}");
        return false;
    }
}
```

## 📞 Getting Help

### 1. Gather Information

When reporting issues, include:

- **GraphModel version**
- **.NET version**
- **Neo4j version**
- **Full error message and stack trace**
- **Minimal code example** that reproduces the issue
- **Generated Cypher query** (if applicable)

### 2. Check Documentation

- [Getting Started Guide](../README.md)
- [Core Concepts](./core-concepts.md)
- [Performance Guide](./performance.md)

### 3. Search Existing Issues

- Check [GitHub Issues](https://github.com/savasp/graphmodel/issues)
- Look for similar problems and solutions

### 4. Create Minimal Reproduction

```csharp
// Example minimal reproduction case:
public class TestNode : INode
{
    public string Id { get; set; }
    public string Name { get; set; }
}

// The issue occurs when:
var graph = new Neo4jGraph("neo4j://localhost:7687", "neo4j", "password");
var node = new TestNode { Id = "1", Name = "Test" };
await graph.CreateNode(node);  // Fails here with [specific error]
```

Remember: The more specific and complete your issue report, the faster we can help! 🚀
