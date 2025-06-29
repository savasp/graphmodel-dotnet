# Graph Model - Python Implementation

**DISCLAIMER**: This Python package is still very much work in progress!

A powerful, type-safe Python library for working with graph data structures and graph databases. This is the Python equivalent of the .NET GraphModel library, providing a clean abstraction layer over graph databases with advanced querying, transaction management, and relationship traversal capabilities.

## Features

- **🔒 Type-Safe Graph Operations** - Work with strongly-typed nodes and relationships using modern Python features
- **🔍 Advanced LINQ-Style Querying** - Query your graph using familiar method-chaining syntax with graph-specific extensions
- **🔄 Graph Traversal & Path Finding** - Navigate complex relationships with depth control and direction constraints
- **⚡ Transaction Management** - Full ACID transaction support with async/await patterns
- **🎯 Provider Architecture** - Clean abstraction supporting multiple graph database backends
- **📊 Neo4j Integration** - Complete Neo4j implementation with Python-to-Cypher translation
- **🛡️ Runtime Validation** - Pydantic-based validation ensures data integrity
- **🏗️ Complex Object Serialization** - Flexible handling of complex properties (embedded vs. related nodes)
- **🎨 Decorator-Based Configuration** - Configure nodes and relationships using intuitive decorators

## Installation

```bash
# Core library
pip install graph-model

# With Neo4j support
pip install graph-model[neo4j]

# Development dependencies
pip install graph-model[dev]
```

## Quick Start

### 1. Define Your Domain Model

```python
from dataclasses import dataclass
from datetime import datetime
from typing import List, Optional

from graph_model import (
    Node, Relationship, RelationshipDirection,
    node, relationship,
    property_field, embedded_field, related_node_field
)

@dataclass
class Address:
    street: str
    city: str
    country: str

@node(label="Person")
class Person(Node):
    first_name: str = property_field(label="first_name", index=True)
    last_name: str = property_field(label="last_name", index=True)
    age: int = property_field(default=0)

    # Embedded complex property (stored as JSON)
    skills: List[str] = embedded_field(default_factory=list)

    # Related node property (stored as separate node)
    home_address: Optional[Address] = related_node_field(
        relationship_type="HAS_HOME_ADDRESS",
        private=True,  # Private relationship
        required=False,
        default=None
    )

@relationship(label="KNOWS", direction=RelationshipDirection.BIDIRECTIONAL)
class Knows(Relationship):
    since: datetime
    relationship_type: str = "friend"
```

### 2. Create Graph Instance

```python
from graph_model_neo4j import Neo4jGraph

# Neo4j provider (when implemented)
graph = Neo4jGraph(
    uri="bolt://localhost:7687",
    username="neo4j",
    password="password",
    database="myapp"
)
```

### 3. Basic Operations

```python
# Create nodes
alice = Person(
    first_name="Alice",
    last_name="Smith",
    age=30,
    skills=["Python", "Machine Learning"],
    home_address=Address(
        street="123 Main St",
        city="Portland",
        country="USA"
    )
)

bob = Person(first_name="Bob", last_name="Jones", age=25)

async with graph.transaction() as tx:
    await graph.create_node(alice, transaction=tx)
    await graph.create_node(bob, transaction=tx)

    # Create relationship
    friendship = Knows(
        start_node_id=alice.id,
        end_node_id=bob.id,
        since=datetime.now(),
        relationship_type="friend"
    )
    await graph.create_relationship(friendship, transaction=tx)

# Query with LINQ-style syntax
young_people = await (graph.nodes(Person)
    .where(lambda p: p.age < 30)
    .where(lambda p: p.home_address is not None and p.home_address.city == "Portland")
    .order_by(lambda p: p.first_name)
    .to_list())

# Graph traversal
alice_friends = await (graph.nodes(Person)
    .where(lambda p: p.first_name == "Alice")
    .traverse(Knows, Person)
    .where(lambda friend: friend.age > 20)
    .to_list())
```

## Architecture

The Python Graph Model follows the same clean, layered architecture as the .NET version:

```
┌─────────────────────────────────┐
│       Your Application          │
├─────────────────────────────────┤
│     graph_model (Core)          │  ← Abstractions & Querying
├─────────────────────────────────┤
│   graph_model_neo4j (Provider)  │  ← Neo4j Implementation
├─────────────────────────────────┤
│        Neo4j Database           │  ← Storage Layer
└─────────────────────────────────┘
```

**Key Components:**

- **IGraph** - Main entry point for all graph operations
- **INode / IRelationship** - Type-safe entity protocols
- **IGraphQueryable** - LINQ-style provider with graph-specific extensions
- **IGraphTransaction** - ACID transaction management
- **Decorators** - Declarative configuration (@node, @relationship)

## Property Types

### Simple Properties

```python
@node(label="Person")
class Person(Node):
    # Simple properties stored directly on the node
    name: str = property_field(index=True)
    age: int = property_field(default=0)
    active: bool = property_field(default=True)
```

### Embedded Properties

```python
@node(label="Person")
class Person(Node):
    # Complex objects serialized as JSON on the node
    skills: List[str] = embedded_field(default_factory=list)
    metadata: dict = embedded_field(default_factory=dict)
    contact_info: ContactInfo = embedded_field(default_factory=ContactInfo)
```

### Related Node Properties

```python
@node(label="Person")
class Person(Node):
    # Complex objects stored as separate nodes with relationships
    home_address: Address = related_node_field(
        relationship_type="HAS_HOME_ADDRESS",
        private=True  # Not discoverable in graph traversals
    )

    work_addresses: List[Address] = related_node_field(
        relationship_type="WORKS_AT_ADDRESS",
        private=False,  # Discoverable in graph traversals
        default_factory=list
    )
```

## Key Differences from .NET Version

1. **Python-Native Types**: Uses `dataclasses`, `typing`, and Pydantic instead of C# constructs
2. **Explicit Property Types**: Developers choose between `embedded_field()` and `related_node_field()` instead of automatic detection
3. **Decorator-Based Configuration**: Uses `@node` and `@relationship` decorators instead of attributes
4. **Async-First**: All I/O operations are async by default
5. **Method Chaining**: LINQ-style operations use Python method chaining patterns

## Development Status

**✅ Implemented:**

- Core interfaces and protocols
- Base node and relationship classes
- Decorator-based configuration
- Property field types (simple, embedded, related)
- LINQ-style queryable interfaces
- Transaction management interfaces
- Graph traversal definitions

**🚧 In Progress:**

- Neo4j provider implementation
- Cypher query translation
- Complex property serialization
- Unit tests

**📋 Planned:**

- Additional database providers
- Performance optimizations
- Documentation generation
- Migration tools from .NET version

## Requirements

- **Python 3.11+**
- **Pydantic 2.5+** for validation and serialization
- **Neo4j 5.0+** for the Neo4j provider (when implemented)

## Contributing

This implementation aims to maintain API compatibility with the .NET version while feeling native to Python developers. Contributions are welcome!

## License

Licensed under the Apache License, Version 2.0.
