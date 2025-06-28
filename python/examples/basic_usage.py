"""
Basic usage example of the Python Graph Model library.

This example demonstrates the core concepts and API patterns.
"""

from dataclasses import dataclass
from datetime import datetime
from typing import List, Optional

from graph_model import (
    Node,
    Relationship,
    RelationshipDirection,
    embedded_field,
    node,
    property_field,
    related_node_field,
    relationship,
)


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
@node(label="Person", indexed_properties=["email"])
class Person(Node):
    """A person in our graph model."""
    
    # Simple properties
    first_name: str = property_field(label="first_name", index=True)
    last_name: str = property_field(label="last_name", index=True) 
    age: int = property_field(default=0)
    
    # Embedded complex property (stored as JSON on the node)
    contact_info: ContactInfo = embedded_field(default_factory=ContactInfo)
    skills: List[str] = embedded_field(default_factory=list)
    
    # Related node properties (stored as separate nodes)
    home_address: Optional[Address] = related_node_field(
        relationship_type="HAS_HOME_ADDRESS",
        private=True,  # Private relationship
        required=False,
        default=None
    )
    work_address: Optional[Address] = related_node_field(
        relationship_type="HAS_WORK_ADDRESS", 
        private=True,
        required=False,
        default=None
    )
    
    # Computed property (ignored in persistence)
    full_name: str = property_field(ignore=True, default="")
    
    def model_post_init(self, __context):
        """Update computed properties after initialization (Pydantic method)."""
        if not self.full_name:
            self.full_name = f"{self.first_name} {self.last_name}"


@node(label="Company")
class Company(Node):
    """A company in our graph model."""
    
    name: str = property_field(index=True)
    industry: str
    founded: datetime
    
    # Embedded address
    headquarters: Address = embedded_field()
    
    # List of embedded addresses
    offices: List[Address] = embedded_field(default_factory=list)


# Relationship definitions
@relationship(label="WORKS_FOR", direction=RelationshipDirection.OUTGOING)
class WorksFor(Relationship):
    """A person works for a company."""
    
    position: str
    start_date: datetime
    salary: Optional[float] = None
    is_remote: bool = False


@relationship(label="KNOWS", direction=RelationshipDirection.BIDIRECTIONAL)
class Knows(Relationship):
    """Two people know each other."""
    
    since: datetime
    relationship_type: str = "friend"  # friend, colleague, family, etc.
    strength: float = 1.0  # 0.0 to 1.0


@relationship(label="MANAGES", direction=RelationshipDirection.OUTGOING)
class Manages(Relationship):
    """One person manages another."""
    
    since: datetime
    direct_report: bool = True


# Example usage (this would require an actual graph implementation)
async def example_usage():
    """
    Demonstrate how the graph model would be used.
    
    Note: This is conceptual - requires an actual graph implementation.
    """
    # This would be something like:
    # from graph_model_neo4j import Neo4jGraph
    # graph = Neo4jGraph("bolt://localhost:7687", "neo4j", "password")
    
    # For now, just demonstrate object creation
    
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
        first_name="Alice",
        last_name="Smith",
        age=30,
        contact_info=ContactInfo(
            email="alice@example.com",
            phone="555-0101"
        ),
        skills=["Python", "Machine Learning", "Data Science"],
        home_address=home_addr,
        work_address=work_addr
    )
    
    bob = Person(
        first_name="Bob", 
        last_name="Jones",
        age=28,
        contact_info=ContactInfo(email="bob@example.com"),
        skills=["JavaScript", "React", "Node.js"]
    )
    
    # Create a company
    acme_corp = Company(
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
        start_node_id=alice.id,
        end_node_id=acme_corp.id,
        position="Senior Data Scientist",
        start_date=datetime(2020, 3, 15),
        salary=120000.0,
        is_remote=True
    )
    
    bob_works_at_acme = WorksFor(
        start_node_id=bob.id,
        end_node_id=acme_corp.id,
        position="Frontend Developer", 
        start_date=datetime(2021, 6, 1),
        salary=95000.0
    )
    
    alice_knows_bob = Knows(
        start_node_id=alice.id,
        end_node_id=bob.id,
        since=datetime(2021, 6, 1),
        relationship_type="colleague",
        strength=0.8
    )
    
    # Example of what graph operations would look like:
    print("=== Graph Model Example ===")
    print(f"Created person: {alice.full_name} (ID: {alice.id})")
    print(f"Created person: {bob.full_name} (ID: {bob.id})")
    print(f"Created company: {acme_corp.name} (ID: {acme_corp.id})")
    print(f"Created relationship: {alice_works_at_acme.id}")
    print(f"Created relationship: {bob_works_at_acme.id}")
    print(f"Created relationship: {alice_knows_bob.id}")
    
    # This is what the actual graph operations would look like:
    """
    async with graph.transaction() as tx:
        # Create nodes
        await graph.create_node(alice, transaction=tx)
        await graph.create_node(bob, transaction=tx)
        await graph.create_node(acme_corp, transaction=tx)
        
        # Create relationships
        await graph.create_relationship(alice_works_at_acme, transaction=tx)
        await graph.create_relationship(bob_works_at_acme, transaction=tx)
        await graph.create_relationship(alice_knows_bob, transaction=tx)
    
    # Query examples
    senior_employees = await (graph.nodes(Person)
        .traverse(WorksFor, Company)
        .where(lambda c: c.name == "ACME Corp")
        .where(lambda p: "Senior" in p.position)
        .to_list())
    
    alice_colleagues = await (graph.nodes(Person)
        .where(lambda p: p.first_name == "Alice")
        .traverse(Knows, Person)
        .where(lambda rel: rel.relationship_type == "colleague")
        .to_list())
    """


if __name__ == "__main__":
    import asyncio
    asyncio.run(example_usage()) 