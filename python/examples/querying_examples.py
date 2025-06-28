"""
Examples demonstrating query expressions for embedded vs related properties.

This shows how the same query syntax translates to different underlying operations
depending on whether properties are embedded or stored as related nodes.
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


@dataclass
class ContactInfo:
    email: str
    phone: Optional[str] = None
    city: str = ""


@dataclass
class Address:
    street: str
    city: str
    state: str
    country: str
    zip_code: str


@node(label="Person")
class Person(Node):
    first_name: str = property_field(index=True)
    last_name: str = property_field(index=True)
    age: int = property_field(default=0)
    
    # EMBEDDED PROPERTY - stored as JSON on the Person node
    contact_info: ContactInfo = embedded_field()
    tags: List[str] = embedded_field(default_factory=list)
    
    # RELATED NODE PROPERTIES - stored as separate nodes
    home_address: Optional[Address] = related_node_field(
        relationship_type="HAS_HOME_ADDRESS",
        private=True,  # Private - not discoverable in normal graph traversal
        required=False,
        default=None
    )
    
    work_addresses: List[Address] = related_node_field(
        relationship_type="WORKS_AT_ADDRESS", 
        private=False,  # Public - discoverable in graph traversal
        default_factory=list
    )


@relationship(label="LIVES_IN", direction=RelationshipDirection.OUTGOING)
class LivesIn(Relationship):
    since: datetime


async def demonstrate_query_differences():
    """
    Show how the same query syntax produces different underlying operations
    for embedded vs related properties.
    """
    
    # Note: This is conceptual - requires actual graph implementation
    # from graph_model_neo4j import Neo4jGraph
    # graph = Neo4jGraph("bolt://localhost:7687", "neo4j", "password")
    
    print("=== QUERY EXAMPLES: EMBEDDED vs RELATED PROPERTIES ===\n")
    
    # =========================================================================
    # EMBEDDED PROPERTY QUERIES
    # =========================================================================
    print("1. EMBEDDED PROPERTY QUERIES")
    print("Property stored as JSON on the node\n")
    
    # Query embedded contact_info.city
    print("Query: Find people in Portland (embedded contact_info.city)")
    print("Python syntax:")
    print("""
    people_in_portland = await (graph.nodes(Person)
        .where(lambda p: p.contact_info.city == "Portland")
        .to_list())
    """)
    
    print("Underlying Cypher translation (requires APOC):")
    print("""
    MATCH (p:Person)
    WHERE apoc.json.path(p.contact_info, '$.city') = "Portland"
    RETURN p
    """)
    print("→ Stores contact_info as JSON string, uses APOC functions to query\n")
    
    # Query embedded list property
    print("Query: Find people with 'engineer' tag (embedded list)")
    print("Python syntax:")
    print("""
    engineers = await (graph.nodes(Person)
        .where(lambda p: "engineer" in p.tags)
        .to_list())
    """)
    
    print("Underlying Cypher translation:")
    print("""
    MATCH (p:Person)
    WHERE "engineer" IN p.tags
    RETURN p
    """)
    print("→ Direct array operation on node property\n")
    
    # =========================================================================
    # RELATED NODE PROPERTY QUERIES  
    # =========================================================================
    print("2. RELATED NODE PROPERTY QUERIES")
    print("Property stored as separate node with relationship\n")
    
    # Query related home_address.city (private relationship)
    print("Query: Find people in Portland (related home_address.city)")
    print("Python syntax:")
    print("""
    people_in_portland = await (graph.nodes(Person)
        .where(lambda p: p.home_address.city == "Portland")
        .to_list())
    """)
    
    print("Underlying Cypher translation:")
    print("""
    MATCH (p:Person)-[:HAS_HOME_ADDRESS]->(addr:Address)
    WHERE addr.city = "Portland"
    RETURN p
    """)
    print("→ Automatically traverses private relationship to Address node\n")
    
    # Query related work_addresses (public relationship, list)
    print("Query: Find people who work in Seattle (related work_addresses)")
    print("Python syntax:")
    print("""
    seattle_workers = await (graph.nodes(Person)
        .where(lambda p: any(addr.city == "Seattle" for addr in p.work_addresses))
        .to_list())
    """)
    
    print("Underlying Cypher translation:")
    print("""
    MATCH (p:Person)-[:WORKS_AT_ADDRESS]->(addr:Address)
    WHERE addr.city = "Seattle"
    RETURN DISTINCT p
    """)
    print("→ Traverses public relationship, filters on related node\n")
    
    # =========================================================================
    # COMPLEX NESTED QUERIES
    # =========================================================================
    print("3. COMPLEX NESTED QUERIES")
    print("Combining embedded and related property filters\n")
    
    print("Query: Engineers in Portland (embedded tag + related address)")
    print("Python syntax:")
    print("""
    portland_engineers = await (graph.nodes(Person)
        .where(lambda p: "engineer" in p.tags)
        .where(lambda p: p.home_address.city == "Portland")
        .to_list())
    """)
    
    print("Underlying Cypher translation:")
    print("""
    MATCH (p:Person)-[:HAS_HOME_ADDRESS]->(addr:Address)
    WHERE "engineer" IN p.tags
      AND addr.city = "Portland"
    RETURN p
    """)
    print("→ Combines JSON property access with relationship traversal\n")
    
    # =========================================================================
    # EXPLICIT vs IMPLICIT TRAVERSAL
    # =========================================================================
    print("4. EXPLICIT vs IMPLICIT TRAVERSAL")
    print("Public relationships can be traversed explicitly\n")
    
    print("Implicit traversal (private relationship - home_address):")
    print("""
    # This works but is translated internally
    people = await (graph.nodes(Person)
        .where(lambda p: p.home_address.state == "WA")
        .to_list())
    """)
    
    print("Explicit traversal (public relationship - work_addresses):")
    print("""
    # This allows explicit graph traversal
    wa_addresses = await (graph.nodes(Person)
        .traverse(WorksAtAddress, Address)  # Public relationship
        .where(lambda addr: addr.state == "WA")
        .to_list())
    
    # Or get the people who work at those addresses
    wa_workers = await (graph.nodes(Address)
        .where(lambda addr: addr.state == "WA")
        .traverse(WorksAtAddress, Person, direction=TraversalDirection.INCOMING)
        .to_list())
    """)
    
    # =========================================================================
    # PERFORMANCE CONSIDERATIONS
    # =========================================================================
    print("5. PERFORMANCE CONSIDERATIONS\n")
    
    print("EMBEDDED PROPERTIES:")
    print("✓ Faster for simple queries (no joins)")
    print("✓ Single node read")
    print("✗ Requires APOC functions for nested property queries")
    print("✗ Limited indexing on nested properties")
    print("✗ Cannot query relationships to embedded data")
    print("✗ Data duplication if same embedded object used multiple times\n")
    
    print("RELATED NODE PROPERTIES:")
    print("✓ Full indexing capabilities")
    print("✓ Can establish relationships to related data")
    print("✓ Data normalization (no duplication)")
    print("✓ Can traverse relationships in both directions")
    print("✗ Requires relationship traversal (more complex queries)")
    print("✗ Multiple node reads\n")
    
    # =========================================================================
    # DESIGN RECOMMENDATIONS
    # =========================================================================
    print("6. DESIGN RECOMMENDATIONS\n")
    
    print("USE EMBEDDED FIELDS FOR:")
    print("- Small, simple objects (< 5 properties)")
    print("- Data that's always accessed together")
    print("- Lists of primitive types")
    print("- Data that doesn't need complex querying")
    print("- When you have APOC available for JSON queries")
    print("- Examples: tags, preferences, simple contact info\n")
    
    print("EMBEDDED FIELD IMPLEMENTATION OPTIONS:")
    print("1. JSON String: Store as serialized JSON, query with APOC functions")
    print("2. Flattened Properties: contact_info_city, contact_info_email")
    print("3. Map Properties: Direct Cypher map access (limited querying)\n")
    
    print("USE RELATED NODE FIELDS FOR:")
    print("- Complex objects that need independent querying")
    print("- Data that might be shared between entities")
    print("- Objects that need their own relationships")
    print("- Data that needs full-text search or complex indexing")
    print("- Examples: addresses, documents, rich user profiles\n")


if __name__ == "__main__":
    import asyncio
    asyncio.run(demonstrate_query_differences()) 