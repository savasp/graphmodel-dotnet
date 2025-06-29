"""
Basic usage example of the Python Graph Model library.

This example demonstrates the core concepts and API patterns.
"""

import asyncio
from dataclasses import dataclass
from datetime import datetime
from typing import List, Optional

from graph_model import Node, Relationship, node, relationship
from graph_model.providers.neo4j import Neo4jDriver, Neo4jGraph


# Complex types for properties
@dataclass
class Address:
    """A complex type that can be used as an embedded or related property."""
    street: str
    city: str
    country: str
    postal_code: str


@dataclass
class ContactInfo:
    """Another complex type for demonstration."""
    phone: Optional[str] = None
    email: Optional[str] = None
    linkedin: Optional[str] = None


# Node definitions
@node("Person")
class Person(Node):
    """A person in our graph model."""

    # Simple properties
    first_name: str
    last_name: str
    age: int = 0

    # Optional email for indexing
    email: Optional[str] = None

    # Embedded complex property (stored as JSON on the node)
    contact_info: Optional[ContactInfo] = None
    skills: List[str] = []

    # Related node properties (stored as separate nodes)
    home_address: Optional[Address] = None
    work_address: Optional[Address] = None

    @property
    def full_name(self) -> str:
        """Computed property for full name."""
        return f"{self.first_name} {self.last_name}"


@node("Company")
class Company(Node):
    """A company in our graph model."""

    name: str
    industry: str
    founded: datetime

    # Embedded address
    headquarters: Optional[Address] = None

    # List of embedded addresses
    offices: List[Address] = []


# Relationship definitions
@relationship("WORKS_FOR")
class WorksFor(Relationship):
    """A person works for a company."""

    position: str
    start_date: datetime
    salary: Optional[float] = None
    is_remote: bool = False


@relationship("KNOWS")
class Knows(Relationship):
    """Two people know each other."""

    since: datetime
    relationship_type: str = "friend"  # friend, colleague, family, etc.
    strength: float = 1.0  # 0.0 to 1.0


@relationship("MANAGES")
class Manages(Relationship):
    """One person manages another."""

    since: datetime
    direct_report: bool = True


# Example usage
async def example_usage():
    """
    Demonstrate how the graph model is used.

    This example shows basic CRUD operations and querying.
    """
    print("=== Python Graph Model Basic Usage Example ===\n")

    # Create some addresses
    home_addr = Address(
        street="123 Main St",
        city="Portland",
        country="USA",
        postal_code="97201"
    )

    work_addr = Address(
        street="456 Business Ave",
        city="Portland",
        country="USA",
        postal_code="97205"
    )

    # Create people
    alice = Person(
        id="alice-123",
        first_name="Alice",
        last_name="Smith",
        age=30,
        email="alice@example.com",
        contact_info=ContactInfo(
            email="alice@example.com",
            phone="555-0101"
        ),
        skills=["Python", "Machine Learning", "Data Science"],
        home_address=home_addr,
        work_address=work_addr
    )

    bob = Person(
        id="bob-456",
        first_name="Bob",
        last_name="Jones",
        age=28,
        email="bob@example.com",
        contact_info=ContactInfo(email="bob@example.com"),
        skills=["JavaScript", "React", "Node.js"]
    )

    # Create a company
    acme_corp = Company(
        id="acme-789",
        name="ACME Corp",
        industry="Technology",
        founded=datetime(2010, 1, 1),
        headquarters=Address(
            street="789 Corporate Blvd",
            city="San Francisco",
            country="USA",
            postal_code="94105"
        ),
        offices=[
            Address("100 Tech St", "Seattle", "USA", "98101"),
            Address("200 Innovation Ave", "Austin", "USA", "73301")
        ]
    )

    # Create relationships
    alice_works_at_acme = WorksFor(
        id="alice-works-acme",
        start_node_id=alice.id,
        end_node_id=acme_corp.id,
        position="Senior Data Scientist",
        start_date=datetime(2020, 3, 15),
        salary=120000.0,
        is_remote=True
    )

    bob_works_at_acme = WorksFor(
        id="bob-works-acme",
        start_node_id=bob.id,
        end_node_id=acme_corp.id,
        position="Frontend Developer",
        start_date=datetime(2021, 6, 1),
        salary=95000.0
    )

    alice_knows_bob = Knows(
        id="alice-knows-bob",
        start_node_id=alice.id,
        end_node_id=bob.id,
        since=datetime(2021, 6, 1),
        relationship_type="colleague",
        strength=0.8
    )

    # Display created objects
    print("=== Created Objects ===")
    print(f"Created person: {alice.full_name} (ID: {alice.id})")
    print(f"Created person: {bob.full_name} (ID: {bob.id})")
    print(f"Created company: {acme_corp.name} (ID: {acme_corp.id})")
    print(f"Created relationship: {alice_works_at_acme.id}")
    print(f"Created relationship: {bob_works_at_acme.id}")
    print(f"Created relationship: {alice_knows_bob.id}")

    print("\n=== Model Details ===")
    print(f"Alice's skills: {alice.skills}")
    print(f"Alice's contact info: {alice.contact_info}")
    print(f"Alice's home address: {alice.home_address}")
    print(f"ACME Corp headquarters: {acme_corp.headquarters}")
    print(f"ACME Corp offices: {len(acme_corp.offices)} offices")

    print("\n=== Relationship Details ===")
    print(f"Alice works as: {alice_works_at_acme.position}")
    print(f"Bob works as: {bob_works_at_acme.position}")
    print(f"Alice and Bob relationship: {alice_knows_bob.relationship_type}")

    # Test serialization compatibility
    print("\n=== .NET Compatibility Test ===")
    from graph_model.core.graph import GraphDataModel

    # Test relationship naming
    home_rel_name = GraphDataModel.property_name_to_relationship_type_name("home_address")
    print(f"Home address relationship name: {home_rel_name}")

    # Test property separation
    simple_props, complex_props = GraphDataModel.get_simple_and_complex_properties(alice)
    print(f"Alice simple properties: {list(simple_props.keys())}")
    print(f"Alice complex properties: {list(complex_props.keys())}")

    print("\n=== Basic functionality test completed successfully! ===")

    # Note: Database operations require a running Neo4j instance
    # Uncomment the following section to test with actual Neo4j database:

    try:
        print("\n=== Testing Neo4j Database Operations ===")

        # Initialize Neo4j driver (adjust connection details as needed)
        await Neo4jDriver.initialize(
            uri="bolt://localhost:7687",
            user="neo4j",
            password="password",
            database="PythonExamples"
        )

        # Ensure database exists
        await Neo4jDriver.ensure_database_exists()

        # Create graph instance
        graph = Neo4jGraph()

        print("✅ Connected to Neo4j database")

        # Test basic node creation and retrieval
        async with graph.transaction() as tx:
            # Create nodes
            await graph.create_node(alice, transaction=tx)
            await graph.create_node(bob, transaction=tx)
            await graph.create_node(acme_corp, transaction=tx)

            print("✅ Created nodes in database")

            # Create relationships
            await graph.create_relationship(alice_works_at_acme, transaction=tx)
            await graph.create_relationship(bob_works_at_acme, transaction=tx)
            await graph.create_relationship(alice_knows_bob, transaction=tx)

            print("✅ Created relationships in database")

        # Test node retrieval
        retrieved_alice = await graph.get_node(alice.id)
        if retrieved_alice:
            print(f"✅ Retrieved Alice from database: {retrieved_alice.first_name}")

        # Test querying (basic functionality)
        # Note: Advanced querying requires full integration
        print("✅ Basic database operations completed successfully!")

    except Exception as e:
        print(f"⚠️  Database operations skipped (Neo4j not available): {e}")
        print("   To test database operations:")
        print("   1. Start Neo4j database on localhost:7687")
        print("   2. Set username/password to neo4j/password")
        print("   3. Run this example again")


if __name__ == "__main__":
    asyncio.run(example_usage())
