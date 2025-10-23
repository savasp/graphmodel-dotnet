# Cypher Provider Implementation Guide

This guide explains how to implement a new Cypher provider using the `Graph.Model.Cypher` package, which provides a provider-agnostic foundation for building Cypher queries.

## Overview

The `Graph.Model.Cypher` package contains the abstraction layer that allows different graph database providers to implement their own Cypher-specific functionality while sharing the core query building logic. This approach enables support for multiple Cypher-compatible databases like Neo4j, PostgreSQL AGE, and others.

## Architecture

The architecture follows a clean separation of concerns:

- **Core Abstractions**: Interfaces that define the contracts for provider-specific behavior
- **Standard Implementation**: OpenCypher-compliant implementations that work with any Cypher database
- **Provider-Specific**: Custom implementations that leverage database-specific extensions

## Required Interfaces

To implement a new Cypher provider, you need to implement four core interfaces:

### 1. ICypherExpressionProcessor

This interface handles the conversion of .NET LINQ expressions to Cypher syntax.

```csharp
public interface ICypherExpressionProcessor
{
    string ProcessExpression(Expression expression, string alias);
}
```

**Purpose**: Convert .NET expressions (lambda functions, property access, method calls) into valid Cypher query syntax.

**Implementation Considerations**:
- Handle basic expression types: property access, method calls, constants, binary operations
- Support provider-specific functions (e.g., APOC for Neo4j)
- Handle DateTime formatting for your target database
- Process collection operations
- Support relationship property access and path segments

### 2. ICypherCollectionProvider

This interface provides collection operations that may vary between Cypher implementations.

```csharp
public interface ICypherCollectionProvider
{
    string ToSet(string collectionExpression);
    string Size(string collectionExpression);
    string Contains(string collectionExpression, string valueExpression);
}
```

**Purpose**: Abstract collection operations that might use provider-specific functions.

**Implementation Options**:
- **Standard**: Use only OpenCypher functions (provided by `StandardCypherCollectionProvider`)
- **Provider-Specific**: Use optimized functions like APOC for Neo4j

### 3. ICypherQueryScope

This interface manages variable scope and context during query building.

```csharp
public interface ICypherQueryScope
{
    bool IsInPathSegmentContext();
    string? CurrentAlias { get; }
    void PushAlias(string alias);
    void PopAlias();
}
```

**Purpose**: Track variable aliases and context during complex query construction.

### 4. ICypherQueryBuilderContext

This interface ties together all the provider-specific components.

```csharp
public interface ICypherQueryBuilderContext
{
    ICypherQueryScope Scope { get; }
    ILoggerFactory? LoggerFactory { get; }
    ICypherCollectionProvider CollectionProvider { get; }
    ICypherExpressionProcessor ExpressionProcessor { get; }
}
```

**Purpose**: Provide a unified context that the `CypherQueryBuilder` can use.

## Implementation Steps

### Step 1: Create Your Provider Project

Create a new project following the naming convention:
```
Graph.Model.[ProviderName]
```

For example:
- `Graph.Model.Neo4j` (existing)
- `Graph.Model.Age` (for PostgreSQL AGE)
- `Graph.Model.YourDatabase`

### Step 2: Add Package Reference

Add a reference to the Cypher abstraction package:

```xml
<PackageReference Include="Cvoya.Graph.Model.Cypher" Version="1.0.0" />
```

### Step 3: Implement the Core Interfaces

#### Expression Processor Example

```csharp
public class YourDatabaseCypherExpressionProcessor : ICypherExpressionProcessor
{
    public string ProcessExpression(Expression expression, string alias)
    {
        return expression switch
        {
            MemberExpression member => ProcessMemberExpression(member, alias),
            MethodCallExpression method => ProcessMethodCall(method, alias),
            ConstantExpression constant => ProcessConstant(constant),
            BinaryExpression binary => ProcessBinaryExpression(binary, alias),
            _ => throw new NotSupportedException($"Expression type {expression.GetType()} not supported")
        };
    }

    private string ProcessMemberExpression(MemberExpression member, string alias)
    {
        // Handle property access: entity.PropertyName -> alias.PropertyName
        return $"{alias}.{member.Member.Name}";
    }

    private string ProcessMethodCall(MethodCallExpression method, string alias)
    {
        // Handle method calls - customize for your database
        return method.Method.Name switch
        {
            "Contains" when method.Method.DeclaringType == typeof(string) => 
                ProcessStringContains(method, alias),
            "StartsWith" => ProcessStringStartsWith(method, alias),
            "EndsWith" => ProcessStringEndsWith(method, alias),
            // Add your database-specific methods here
            _ => throw new NotSupportedException($"Method {method.Method.Name} not supported")
        };
    }

    private string ProcessStringContains(MethodCallExpression method, string alias)
    {
        // Example: entity.Name.Contains("value") -> alias.Name CONTAINS $param
        var property = ProcessExpression(method.Object!, alias);
        var value = ProcessExpression(method.Arguments[0], alias);
        return $"{property} CONTAINS {value}";
    }
}
```

#### Collection Provider Example

```csharp
public class YourDatabaseCypherCollectionProvider : ICypherCollectionProvider
{
    public string ToSet(string collectionExpression)
    {
        // Option 1: Use standard Cypher
        return $"[item IN {collectionExpression} WHERE item IS NOT NULL | DISTINCT item]";
        
        // Option 2: Use database-specific optimized function
        // return $"your_database_unique_function({collectionExpression})";
    }

    public string Size(string collectionExpression)
    {
        // Standard Cypher - works with all databases
        return $"size({collectionExpression})";
    }

    public string Contains(string collectionExpression, string valueExpression)
    {
        // Standard Cypher - works with all databases
        return $"{valueExpression} IN {collectionExpression}";
    }
}
```

#### Query Scope Implementation

```csharp
public class YourDatabaseCypherQueryScope : ICypherQueryScope
{
    private readonly Stack<string> _aliasStack = new();
    private bool _isInPathSegmentContext;

    public string? CurrentAlias => _aliasStack.Count > 0 ? _aliasStack.Peek() : null;

    public bool IsInPathSegmentContext() => _isInPathSegmentContext;

    public void PushAlias(string alias)
    {
        _aliasStack.Push(alias);
    }

    public void PopAlias()
    {
        if (_aliasStack.Count > 0)
            _aliasStack.Pop();
    }

    public void EnterPathSegmentContext() => _isInPathSegmentContext = true;
    public void ExitPathSegmentContext() => _isInPathSegmentContext = false;
}
```

#### Context Implementation

```csharp
public class YourDatabaseCypherQueryBuilderContext : ICypherQueryBuilderContext
{
    public ICypherQueryScope Scope { get; }
    public ILoggerFactory? LoggerFactory { get; }
    public ICypherCollectionProvider CollectionProvider { get; }
    public ICypherExpressionProcessor ExpressionProcessor { get; }

    public YourDatabaseCypherQueryBuilderContext(
        ILoggerFactory? loggerFactory = null)
    {
        LoggerFactory = loggerFactory;
        Scope = new YourDatabaseCypherQueryScope();
        CollectionProvider = new YourDatabaseCypherCollectionProvider();
        ExpressionProcessor = new YourDatabaseCypherExpressionProcessor();
    }
}
```

### Step 4: Create Your Provider-Specific Query Builder

You can either use the base `CypherQueryBuilder` directly or extend it:

```csharp
public class YourDatabaseCypherQueryBuilder : CypherQueryBuilder
{
    public YourDatabaseCypherQueryBuilder(ILoggerFactory? loggerFactory = null)
        : base(
            new YourDatabaseCypherQueryBuilderContext(loggerFactory),
            new YourDatabaseCypherExpressionProcessor(),
            new YourDatabaseCypherCollectionProvider())
    {
    }

    // Add any database-specific methods here
    public void AddYourDatabaseSpecificClause(string clause)
    {
        // Custom functionality for your database
    }
}
```

## Advanced Implementation Patterns

### Provider-Specific Extensions

You can add database-specific functionality while maintaining compatibility:

```csharp
public class Neo4jCypherCollectionProvider : ICypherCollectionProvider
{
    public string ToSet(string collectionExpression)
    {
        // Use APOC for Neo4j optimization
        return $"apoc.coll.toSet({collectionExpression})";
    }

    // ... other methods
}

public class AgeCypherCollectionProvider : ICypherCollectionProvider
{
    public string ToSet(string collectionExpression)
    {
        // Use PostgreSQL AGE functions
        return $"array_remove(array_agg(DISTINCT unnest({collectionExpression})), NULL)";
    }

    // ... other methods
}
```

### Expression Processing Best Practices

1. **Handle Null Values**: Always check for null expressions and provide meaningful defaults
2. **Parameter Binding**: Use parameterized queries to prevent injection attacks
3. **Type Conversion**: Handle .NET to Cypher type conversion properly
4. **Error Messages**: Provide clear error messages for unsupported operations
5. **Performance**: Optimize for your target database's specific capabilities

### Testing Your Implementation

Create comprehensive tests to ensure your provider works correctly:

```csharp
[Test]
public void Should_Convert_String_Contains_Expression()
{
    var processor = new YourDatabaseCypherExpressionProcessor();
    Expression<Func<Person, bool>> expr = p => p.Name.Contains("John");
    
    var result = processor.ProcessExpression(expr.Body, "p");
    
    Assert.That(result, Is.EqualTo("p.Name CONTAINS $param0"));
}
```

## Integration with Graph.Model

Your provider should integrate with the main `Graph.Model` package by:

1. **Implementing Graph Context**: Create a graph context that uses your Cypher query builder
2. **Provider Registration**: Allow users to configure your provider in dependency injection
3. **Connection Management**: Handle database connections and transactions
4. **Result Mapping**: Map Cypher query results back to .NET objects

## Examples and References

### Existing Implementations

- **Neo4j Provider**: `Graph.Model.Neo4j` - Shows how to implement APOC extensions
- **Standard Implementation**: `StandardCypherCollectionProvider` - OpenCypher baseline

### Key Files to Study

1. `CypherQueryBuilder.cs` - Main query building logic
2. `ICypherExpressionProcessor.cs` - Expression processing contract
3. `ICypherCollectionProvider.cs` - Collection operations contract
4. `WhereQueryPart.cs` - Example of query part implementation

## Support and Contributing

- Review existing provider implementations for patterns and best practices
- Follow the project's coding conventions and documentation standards
- Submit issues and pull requests to improve the abstraction layer
- Share your provider implementation with the community

This architecture enables a rich ecosystem of Cypher providers while maintaining a consistent developer experience across different graph databases.