---
title: Performance Guide
layout: default
---

# 🚀 Performance Guide

This guide provides best practices and optimization techniques for maximizing performance when using GraphModel.

## 📊 General Performance Principles

### 1. Query Optimization

- **Use specific queries** instead of loading large datasets
- **Project only needed properties** to reduce data transfer
- **Use pagination** for large result sets

### 2. Connection Management

- **Use connection pooling** (enabled by default in Neo4j provider)
- **Reuse graph instances** instead of creating new ones
- **Configure timeouts** appropriately for your use case

### 3. Transaction Efficiency

- **Batch operations** within transactions
- **Keep transactions short** to avoid locks
- **Use explicit transactions** for multiple operations

## 🎯 Querying Best Practices

### Efficient Node Queries

```csharp
// ✅ Good - specific query with projection
var userEmails = await graph.Nodes<User>()
    .Where(u => u.IsActive && u.CreatedDate > DateTime.Now.AddDays(-30))
    .Select(u => new { u.Id, u.Email })
    .ToListAsync();

// ❌ Avoid - loading full objects when only emails needed
var users = await graph.Nodes<User>()
    .Where(u => u.IsActive)
    .ToListAsync();
var emails = users.Select(u => u.Email).ToList();
```

### Efficient Relationship Traversal

```csharp
// ✅ Good - depth-limited traversal
var nearbyFriends = await graph.Nodes<User>()
    .Where(u => u.Id == userId)
    .Traverse<Person, FriendOf, Person>()
    .WithDepth(1, 2)  // Limit to 2 degrees of separation
    .Where(friend => friend.City == "Seattle")
    .ToListAsync();

// ❌ Avoid - unlimited depth traversal
var allConnections = await graph.Nodes<User>()
    .Where(u => u.Id == userId)
    .Traverse<Person, FriendOf, Person>()  // By default, Traverse() only traverses 1 hop
    .WithDepth(1, 100) // Large depth limit
    .ToListAsync();
```

### Pagination for Large Results

```csharp
// ✅ Good - paginated results
var pageSize = 100;
var page = 0;

var users = await graph.Nodes<User>()
    .OrderBy(u => u.CreatedDate)
    .Skip(page * pageSize)
    .Take(pageSize)
    .ToListAsync();

// For very large datasets, consider cursor-based pagination
var users = await graph.Nodes<User>()
    .Where(u => u.CreatedDate > lastSeenDate)
    .OrderBy(u => u.CreatedDate)
    .Take(pageSize)
    .ToListAsync();
```

## 🔧 Neo4j-Specific Optimizations

### Connection Configuration

```csharp
// ✅ Optimized connection settings
var graph = new Neo4jGraph(
    connectionString: "neo4j+s://your-server:7687",
    username: "username",
    password: "password",
    config: new ConfigurationOptions
    {
        MaxConnectionLifetime = TimeSpan.FromMinutes(30),
        MaxConnectionPoolSize = 100,
        ConnectionAcquisitionTimeout = TimeSpan.FromSeconds(30),
        ConnectionTimeout = TimeSpan.FromSeconds(30),
        SocketKeepaliveEnabled = true
    }
);
```

### Efficient Batch Operations

```csharp
// ✅ Good - batch create in single transaction
using var transaction = await graph.BeginTransactionAsync();

var users = new List<User>();
for (int i = 0; i < 1000; i++)
{
    users.Add(new User { Id = Guid.NewGuid().ToString(), Email = $"user{i}@example.com" });
}

// Batch create
await graph.CreateNodes(users, transaction);
await transaction.CommitAsync();

// ❌ Avoid - individual transactions
foreach (var user in users)
{
    await graph.CreateNode(user);  // Separate transaction per node!
}
```

### Complex Property Optimization

```csharp
// For large complex objects, consider splitting into separate nodes
public class User : INode
{
    public string Id { get; set; }
    public string Email { get; set; }

    // ✅ Good - simple properties on main node
    public string Name { get; set; }
    public DateTime CreatedDate { get; set; }

    // ❌ Avoid - very large complex properties
    // public List<VeryLargeObject> ComplexData { get; set; }
}

// ✅ Better - separate related entities
public class UserProfile : INode
{
    public string Id { get; set; }
    public string UserId { get; set; }
    public List<SomeComplexData> ProfileData { get; set; }
}
```

## 📈 Monitoring and Profiling

### Query Performance Monitoring

```csharp
// Enable query logging for development
var loggerFactory = new LoggerFactory() { ... }
var graph = new Neo4jGraph(connectionString, username, password, loggerFactory);

// Time critical operations
var stopwatch = Stopwatch.StartNew();
var results = await graph.Nodes<User>().Where(u => u.Email == email).ToListAsync();
stopwatch.Stop();

if (stopwatch.ElapsedMilliseconds > 1000)
{
    _logger.LogWarning($"Slow query detected: {stopwatch.ElapsedMilliseconds}ms");
}
```

### Memory Usage Optimization

```csharp
// ✅ Good - dispose resources properly
using var graph = new Neo4jGraph(connectionString, username, password);

// ✅ Good - use streaming for large datasets
await foreach (var user in graph.Nodes<User>().AsAsyncEnumerable())
{
    // Process one at a time instead of loading all into memory
    ProcessUser(user);
}

// ❌ Avoid - loading huge datasets into memory
var allUsers = await graph.Nodes<User>().ToListAsync();  // Could use lots of RAM
```

## 🎛️ Configuration Tuning

### Neo4j Driver Settings

```csharp
var config = new ConfigurationOptions
{
    // Connection pool settings
    MaxConnectionPoolSize = Environment.ProcessorCount * 2,
    MaxConnectionLifetime = TimeSpan.FromMinutes(30),
    ConnectionAcquisitionTimeout = TimeSpan.FromSeconds(60),

    // Network settings
    ConnectionTimeout = TimeSpan.FromSeconds(30),
    SocketKeepaliveEnabled = true,

    // Security
    EncryptionLevel = EncryptionLevel.Encrypted,
    TrustStrategy = TrustStrategy.TrustSystemCaSignedCertificates,

    // Logging
    Logger = new FileLogger("neo4j-queries.log"),
    LogLevel = LogLevel.Information
};
```

### Application-Level Caching

```csharp
// Consider caching frequently accessed, rarely changing data
private readonly IMemoryCache _cache;

public async Task<User> GetUserAsync(string userId)
{
    if (_cache.TryGetValue($"user:{userId}", out User cachedUser))
    {
        return cachedUser;
    }

    var user = await graph.Nodes<User>()
        .Where(u => u.Id == userId)
        .FirstOrDefaultAsync();

    if (user != null)
    {
        _cache.Set($"user:{userId}", user, TimeSpan.FromMinutes(15));
    }

    return user;
}
```

## 📋 Performance Checklist

### Before Going to Production

- [ ] **Profile query performance** with realistic data volumes
- [ ] **Test connection pool settings** under load
- [ ] **Set appropriate timeouts** for your use case
- [ ] **Implement proper error handling** for timeouts
- [ ] **Monitor memory usage** with large datasets
- [ ] **Test transaction rollback** scenarios
- [ ] **Verify cleanup** of disposed resources

### Common Performance Issues

- [ ] **Loading too much data** at once
- [ ] **Large depth traversals**
- [ ] **Too many small transactions** instead of batching
- [ ] **Connection pool exhaustion**
- [ ] **Memory leaks** from undisposed resources
- [ ] **N+1 query problems** in relationship traversal

## 🔧 Troubleshooting Slow Queries

### 1. Enable Query Logging

```csharp
var loggerFactory = new LoggerFactory { ... }
var graph = new Neo4jGraph(connectionString, username, password, loggerFactory);
```

### 2. Analyze Generated Cypher

Review the generated Cypher queries in logs:

- Are there unnecessary data transfers?
- Can queries be simplified?

### 3. Use Neo4j Query Profiling

```cypher
PROFILE MATCH (u:User) RETURN u
WHERE u.email = $p0
RETURN ...
```

### 4. Common Query Patterns to Avoid

```csharp
// ❌ Avoid - Loading related data in loops (N+1 problem)
foreach (var user in users)
{
    user.Friends = await graph.Relationships<FriendOf>()
        .Where(f => f.SourceId == user.Id)
        .ToListAsync();
}

// ✅ Better - Load related data in batch
var userIds = users.Select(u => u.Id).ToList();
var friendships = await graph.Relationships<FriendOf>()
    .Where(f => userIds.Contains(f.SourceId))
    .ToListAsync();
```

## 📊 Benchmarking

Create benchmarks for critical operations:

```csharp
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class GraphModelBenchmarks
{
    private IGraph _graph;

    [GlobalSetup]
    public void Setup()
    {
        _graph = new Neo4jGraph(connectionString, username, password);
    }

    [Benchmark]
    public async Task<List<User>> QueryUsers()
    {
        return await _graph.Nodes<User>()
            .Where(u => u.IsActive)
            .Take(1000)
            .ToListAsync();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _graph?.Dispose();
    }
}
```

Remember: **Measure first, optimize second**. Use profiling tools to identify actual bottlenecks rather than guessing.

## Performance Testing and Benchmarking

GraphModel includes comprehensive performance testing infrastructure using BenchmarkDotNet to ensure consistent performance across releases and detect regressions.

## 📊 Available Benchmarks

### CRUD Operations Benchmark

- **Node creation**: Bulk creation of nodes with realistic data
- **Node retrieval**: Query performance with various filters
- **Node updates**: Batch update operations
- **Node deletion**: Cleanup performance testing
- **Memory allocation**: Tracks memory usage patterns

### Relationship Benchmark

- **Relationship creation**: Performance of creating relationships between nodes
- **Graph traversal**: Different traversal patterns and depths
- **Complex queries**: Multi-hop traversals and filtering
- **Relationship updates**: Modification performance
- **Memory footprint**: Relationship-specific memory analysis

## 🚀 Running Benchmarks Locally

### Quick Start

Use the provided scripts for easy benchmark execution:

#### PowerShell (Windows)

```powershell
# Run all benchmarks
./scripts/run-benchmarks.ps1

# Run specific benchmark types
./scripts/run-benchmarks.ps1 -Mode crud
./scripts/run-benchmarks.ps1 -Mode relationships

# Interactive selection (choose specific benchmarks)
./scripts/run-benchmarks.ps1 -Mode interactive

# Custom output directory
./scripts/run-benchmarks.ps1 -OutputDir "./my-benchmarks"
```

#### Bash (macOS/Linux)

```bash
# Run all benchmarks
./scripts/run-benchmarks.sh

# Run specific benchmark types
./scripts/run-benchmarks.sh --mode crud
./scripts/run-benchmarks.sh --mode relationships

# Interactive selection
./scripts/run-benchmarks.sh --mode interactive

# Custom output directory
./scripts/run-benchmarks.sh --output "./my-benchmarks"
```

### Manual Execution

You can also run benchmarks directly:

```bash
# Build the performance test project
dotnet build tests/Graph.Model.Performance.Tests --configuration Benchmark

# Run all benchmarks (non-interactive)
dotnet run --project tests/Graph.Model.Performance.Tests --configuration Benchmark -- --all

# Run specific benchmark class
dotnet run --project tests/Graph.Model.Performance.Tests --configuration Benchmark -- --filter "*CrudOperations*"

# Interactive mode (local development only)
dotnet run --project tests/Graph.Model.Performance.Tests --configuration Benchmark
```

## 🔄 CI/CD Integration

### Automated Performance Testing

The GitHub Actions workflow (`.github/workflows/performance.yml`) automatically:

1. **Runs on every push to main and pull requests**
2. **Uses non-interactive mode** with `--all` parameter
3. **Generates multiple report formats**:
   - HTML reports for human readability
   - JSON reports for programmatic analysis
   - Markdown summaries for PR comments
4. **Stores artifacts** for 30 days
5. **Compares performance** against baseline when available

### Non-Interactive Mode

The performance tests are configured to run automatically in CI environments:

- **Default behavior**: When no arguments or `--all` is passed, all benchmarks run without user interaction
- **CI-optimized configuration**: Uses in-process toolchain for faster execution
- **Validation disabled**: Skips optimization validators that require user input

### Benchmark Configuration

GraphModel uses a special **Benchmark** build configuration that combines:

- **Release-level optimizations**: Full compiler optimizations enabled
- **Project references**: Uses local project references instead of NuGet packages
- **Debug symbols**: Maintains debugging capability for analysis
- **TRACE constants**: Enables performance tracking

This configuration is ideal for:

- ✅ **Local development**: Uses project references, no need for published packages
- ✅ **Performance testing**: Full optimizations like Release builds
- ✅ **CI/CD pipelines**: Consistent behavior across environments
- ✅ **Debugging**: Retain symbols for performance analysis

### Workflow Configuration

The performance workflow runs:

- On pushes to `main` branch
- On pull requests (to detect regressions)
- Can be triggered manually via GitHub UI
- Uses .NET 10 runtime for latest performance optimizations
- Uses **Benchmark** configuration for optimal performance with project references
- Benchmarks run on the host process runtime (.NET 10) for accurate performance measurement

## 📈 Interpreting Results

### BenchmarkDotNet Output

Results include:

- **Mean execution time**: Average time per operation
- **Standard deviation**: Consistency of performance
- **Memory allocation**: Managed memory allocated per operation
- **Throughput**: Operations per second (when applicable)

### HTML Reports

Generated HTML reports provide:

- **Interactive charts** comparing different scenarios
- **Detailed statistics** including percentiles
- **Memory analysis** with allocation patterns
- **Historical comparisons** when available

### Performance Baselines

Key performance indicators to monitor:

- **Node creation**: Should handle 1000+ nodes/second
- **Simple queries**: Sub-millisecond response for basic filters
- **Traversals**: Efficient scaling with graph depth
- **Memory usage**: Minimal allocations for read operations

## 🎯 Adding New Benchmarks

### Creating a Benchmark Class

```csharp
[MemoryDiagnoser]
[HtmlExporter]
[MarkdownExporter]
public class MyCustomBenchmark
{
    private IGraph _graph = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Initialize test data
        _graph = CreateTestGraph();
    }

    [Benchmark]
    public async Task<int> MyOperation()
    {
        // Your benchmark code here
        return await _graph.Nodes<MyNode>().CountAsync();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _graph?.Dispose();
    }
}
```

### Benchmark Attributes

- `[MemoryDiagnoser]`: Tracks memory allocations
- `[HtmlExporter]`: Generates HTML reports
- `[MarkdownExporter]`: Creates markdown summaries
- `[Params]`: Parameterize benchmarks with different inputs
- `[Benchmark]`: Marks methods to benchmark

### Best Practices

1. **Realistic data**: Use representative dataset sizes
2. **Proper setup/cleanup**: Initialize in `[GlobalSetup]`, cleanup in `[GlobalCleanup]`
3. **Avoid optimization**: Don't let compiler optimize away your benchmark
4. **Consistent environment**: Use same configuration across runs
5. **Multiple iterations**: Let BenchmarkDotNet determine optimal iteration count

## 🔧 Configuration

### BenchmarkDotNet Configuration

The performance tests use a custom configuration optimized for CI:

```csharp
var config = DefaultConfig.Instance
    .AddJob(Job.Default.WithToolchain(InProcessEmitToolchain.Instance))
    .WithOptions(ConfigOptions.DisableOptimizationsValidator);
```

### Output Formats

Multiple export formats are generated:

- **HTML**: Interactive reports with charts
- **JSON**: Machine-readable data for analysis
- **Markdown**: Summary reports for documentation

### Artifacts

Benchmark results are stored as GitHub artifacts:

- **Retention**: 30 days
- **Location**: `./artifacts/benchmarks`
- **Formats**: All generated reports and raw data

## 🚨 Performance Regression Detection

### Monitoring

- **Automated alerts**: CI fails if performance degrades significantly
- **Trend analysis**: Compare against historical baselines
- **Memory regression**: Track allocation patterns over time

### Thresholds

Performance regression is flagged when:

- **Execution time** increases by >20% for critical operations
- **Memory allocations** increase significantly without justification
- **Throughput** decreases below acceptable levels

### Investigation

When regressions are detected:

1. **Compare reports**: Review before/after performance data
2. **Profile locally**: Use detailed profiling tools for root cause analysis
3. **Isolate changes**: Identify specific commits causing regression
4. **Optimize**: Apply targeted performance improvements
