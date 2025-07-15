# Example 6: Full Text Search

This example demonstrates the comprehensive full text search capabilities of the GraphModel library with Neo4j.

## What You'll Learn

- How to search across all entity types (nodes and relationships)
- Type-specific searching for nodes and relationships
- Using generic interfaces for flexible searching
- Property-level control over search inclusion
- Case-insensitive search behavior
- Working with multi-word search terms
- Automatic full text index management

## Key Features Demonstrated

### 1. Universal Search

```csharp
// Search across all entities (nodes and relationships)
var results = await graph.Search("British").ToListAsync();
```

### 2. Type-Specific Node Search

```csharp
// Search specific node types
var authors = await graph.SearchNodes<Author>("science").ToListAsync();
var books = await graph.SearchNodes<Book>("adventure").ToListAsync();

// Search using generic node interface
var allNodes = await graph.SearchNodes("fantasy").ToListAsync();
```

### 3. Type-Specific Relationship Search

```csharp
// Search specific relationship types
var writings = await graph.SearchRelationships<Wrote>("mathematical").ToListAsync();

// Search using generic relationship interface
var allRelationships = await graph.SearchRelationships("collaboration").ToListAsync();
```

### 4. Property-Level Search Control

```csharp
public record Author : Node
{
    public string Name { get; set; } = string.Empty;
    public string Bio { get; set; } = string.Empty;  // Included in search by default
    
    [Property(IncludeInFullTextSearch = false)]
    public string PersonalNotes { get; set; } = string.Empty;  // Excluded from search
}
```

### 5. Advanced Search Features

- **Case Insensitive**: Search terms work regardless of case
- **Multi-word Terms**: Support for phrases like "golden ticket"
- **Automatic Indexing**: Full text indexes are created and managed automatically
- **Cross-Entity Search**: Find content across different node and relationship types

## Domain Model

The example uses a book publishing domain:

- **Author**: Writers with names, biographies, and nationalities
- **Book**: Publications with titles, genres, summaries, and publication years
- **Publisher**: Publishing companies with descriptions and locations
- **Wrote**: Relationships connecting authors to their books
- **Published**: Relationships connecting publishers to books
- **Collaborated**: Relationships between authors for joint projects

## Search Scenarios Demonstrated

### Content Discovery
- Find authors by expertise: "science fiction"
- Locate books by theme: "adventure", "fantasy"
- Search relationships by style: "mathematical", "world-building"

### Cross-Entity Search
- Search for "British" across authors, books, and publishers
- Find entities related to specific themes or concepts

### Privacy and Control
- Exclude sensitive data from search using property attributes
- Demonstrate that excluded properties don't appear in search results

### Edge Cases
- Empty results for non-existent terms
- Case-insensitive matching
- Multi-word phrase searching

## Prerequisites

- Neo4j instance running on `localhost:7687`
- Username: `neo4j`
- Password: `password`

## Running the Example

```bash
cd examples/Example6.FullTextSearch
dotnet run
```

## Expected Output

The example will:

1. Create a fresh database with sample data
2. Demonstrate various search patterns across different entity types
3. Show property-level search control in action
4. Display case-insensitive and multi-word search capabilities
5. Provide a comprehensive summary of all demonstrated features

## Key Technical Concepts

### Automatic Index Management
The GraphModel library automatically creates and manages full text indexes for:
- All string properties on nodes and relationships
- Proper handling of property inclusion/exclusion settings
- Dynamic index updates when new entity types are discovered

### Search Query Processing
- Full text searches are converted to efficient Neo4j Cypher queries
- Uses Neo4j's `db.index.fulltext.queryNodes` and `db.index.fulltext.queryRelationships` procedures
- Seamless integration with existing LINQ-to-Cypher pipeline

### Property Control
- By default, all string properties are included in full text search
- Use `[Property(IncludeInFullTextSearch = false)]` to exclude sensitive or irrelevant properties
- Non-string properties (numbers, dates, etc.) are automatically excluded

This example provides a foundation for implementing powerful search functionality in graph-based applications.