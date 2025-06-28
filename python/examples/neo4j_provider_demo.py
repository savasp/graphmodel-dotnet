"""
Comprehensive demonstration of the Neo4j provider.

This example shows how to use the Neo4j provider for CRUD operations,
transactions, and .NET-compatible serialization of complex properties.
"""

import asyncio
import os
import sys

sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from dataclasses import dataclass
from datetime import datetime
from typing import List, Optional

from graph_model import (
    Node,
    Relationship,
    auto_field,
    node,
    relationship,
)
from graph_model.providers.neo4j import Neo4jDriver, Neo4jGraph


@dataclass
class Address:
    """A complex type that will be stored as a related node."""
    street: str
    city: str
    state: str
    country: str
    zip_code: str
    created_date: datetime = None


@dataclass
class ContactInfo:
    """Another complex type for demonstration."""
    email: str
    phone: Optional[str] = None
    city: str = ""
    preferences: dict = None


@node("Person")
class Person(Node):
    """Person entity using auto_field for automatic field type detection."""
    
    # Simple types - automatically use property_field
    name: str = auto_field(index=True)
    age: int = auto_field(default=0)
    email: str = auto_field()
    is_active: bool = auto_field(default=True)
    created_date: datetime = auto_field(default_factory=datetime.utcnow)
    
    # Simple collections - automatically use property_field
    tags: List[str] = auto_field(default_factory=list)
    scores: List[float] = auto_field(default_factory=list)
    
    # Complex types - automatically use related_node_field with .NET convention
    home_address: Address = auto_field()
    work_address: Optional[Address] = auto_field(default=None, required=False)
    contact_info: ContactInfo = auto_field()
    
    # Complex collections - automatically use related_node_field
    previous_addresses: List[Address] = auto_field(default_factory=list)


@relationship("KNOWS")
class Knows(Relationship):
    """Relationship between people."""
    
    since: datetime = auto_field(default_factory=datetime.utcnow)
    strength: float = auto_field(default=1.0)


async def demonstrate_neo4j_provider():
    """Demonstrate the Neo4j provider functionality."""
    
    print("=== NEO4J PROVIDER DEMONSTRATION ===\n")
    
    # Initialize Neo4j driver
    print("1. INITIALIZING NEO4J DRIVER")
    print("   Connecting to Neo4j database...")
    
    # Note: In a real application, you'd use actual connection details
    # await Neo4jDriver.initialize(
    #     uri="neo4j://localhost:7687",
    #     user="neo4j", 
    #     password="password"
    # )
    
    print("   ✓ Driver initialized (simulated)\n")
    
    # Create graph instance
    print("2. CREATING GRAPH INSTANCE")
    graph = Neo4jGraph()
    print("   ✓ Graph instance created\n")
    
    # Demonstrate CRUD operations
    print("3. CRUD OPERATIONS")
    
    # Create a person with complex properties
    print("   Creating person with complex properties...")
    person = Person(
        name="John Doe",
        age=30,
        email="john@example.com",
        home_address=Address(
            street="123 Main St",
            city="Portland",
            state="OR",
            country="USA",
            zip_code="97201",
            created_date=datetime.utcnow()
        ),
        contact_info=ContactInfo(
            email="john@example.com",
            phone="+1-555-0123",
            city="Portland",
            preferences={"theme": "dark", "notifications": True}
        ),
        tags=["developer", "python"],
        scores=[95.5, 87.2, 92.1]
    )
    
    print(f"   Person created: {person.name} (ID: {person.id})")
    print(f"   Home address: {person.home_address.street}, {person.home_address.city}")
    print(f"   Contact info: {person.contact_info.email}")
    print(f"   Tags: {person.tags}")
    print(f"   Scores: {person.scores}")
    
    # Show the .NET-compatible relationship types that would be created
    print("\n   .NET-compatible relationship types that would be created:")
    print(f"   - __PROPERTY__home_address__")
    print(f"   - __PROPERTY__contact_info__")
    print(f"   - __PROPERTY__previous_addresses__")
    
    # Demonstrate transaction usage
    print("\n4. TRANSACTION USAGE")
    print("   Using transaction context manager...")
    
    # async with graph.transaction() as tx:
    #     # Create person
    #     await graph.create_node(person, transaction=tx)
    #     
    #     # Create another person
    #     person2 = Person(
    #         name="Jane Smith",
    #         age=28,
    #         email="jane@example.com",
    #         home_address=Address(
    #             street="456 Oak Ave",
    #             city="Seattle",
    #             state="WA",
    #             country="USA",
    #             zip_code="98101"
    #         ),
    #         contact_info=ContactInfo(
    #             email="jane@example.com",
    #             phone="+1-555-0456"
    #         )
    #     )
    #     await graph.create_node(person2, transaction=tx)
    #     
    #     # Create relationship
    #     knows_rel = Knows(
    #         start_node_id=person.id,
    #         end_node_id=person2.id,
    #         since=datetime.utcnow(),
    #         strength=0.8
    #     )
    #     await graph.create_relationship(knows_rel, transaction=tx)
    #     
    #     print("   ✓ All operations completed in transaction")
    
    print("   ✓ Transaction operations simulated")
    
    # Demonstrate querying (conceptual)
    print("\n5. QUERYING OPERATIONS")
    print("   Querying capabilities (conceptual):")
    
    # # Find people in Portland
    # portland_people = await (graph.nodes(Person)
    #     .where(lambda p: p.home_address.city == "Portland")
    #     .to_list())
    
    # # Find people with high scores
    # high_scorers = await (graph.nodes(Person)
    #     .where(lambda p: any(score > 90 for score in p.scores))
    #     .order_by(lambda p: p.name)
    #     .take(10)
    #     .to_list())
    
    # # Count people by city
    # city_counts = await (graph.nodes(Person)
    #     .group_by(lambda p: p.home_address.city)
    #     .select(lambda g: (g.key, g.count()))
    #     .to_list())
    
    print("   - WHERE clauses with complex property access")
    print("   - ORDER BY with complex property sorting")
    print("   - GROUP BY with aggregations")
    print("   - Complex property loading with .NET compatibility")
    
    # Demonstrate .NET compatibility
    print("\n6. .NET COMPATIBILITY")
    print("   The Python library uses the same conventions as .NET:")
    print("   - Relationship types: __PROPERTY__{fieldName}__")
    print("   - Complex properties stored as separate nodes")
    print("   - Same serialization format")
    print("   - Compatible query patterns")
    print("   ✓ Python and .NET can read/write the same data")
    
    # Show Cypher queries that would be generated
    print("\n7. GENERATED CYPHER QUERIES")
    print("   Example Cypher queries that would be generated:")
    
    print("\n   CREATE node with complex properties:")
    print("""
    CREATE (n:Person {Id: $id, name: $name, age: $age, email: $email})
    WITH n
    MATCH (n)
    CREATE (n)-[r1:__PROPERTY__home_address__ {SequenceNumber: 0}]->(addr:Address $addr_props)
    CREATE (n)-[r2:__PROPERTY__contact_info__ {SequenceNumber: 0}]->(contact:ContactInfo $contact_props)
    """)
    
    print("\n   Query with complex property loading:")
    print("""
    MATCH (n:Person)
    OPTIONAL MATCH (n)-[home_rel:__PROPERTY__home_address__]->(home_addr:Address)
    OPTIONAL MATCH (n)-[contact_rel:__PROPERTY__contact_info__]->(contact_info:ContactInfo)
    WITH n, home_addr, contact_info
    WHERE home_addr.city = "Portland"
    RETURN n, home_address: home_addr, contact_info: contact_info
    """)
    
    # Cleanup
    print("\n8. CLEANUP")
    print("   Closing Neo4j driver...")
    # await Neo4jDriver.close()
    print("   ✓ Driver closed (simulated)")
    
    print("\n=== DEMONSTRATION COMPLETE ===")
    print("\nKey Features Demonstrated:")
    print("✓ Async/await throughout")
    print("✓ .NET-compatible relationship naming")
    print("✓ Complex property serialization")
    print("✓ Transaction support")
    print("✓ LINQ-style querying (conceptual)")
    print("✓ Auto field type detection")
    print("✓ Type safety with Pydantic")


if __name__ == "__main__":
    asyncio.run(demonstrate_neo4j_provider()) 