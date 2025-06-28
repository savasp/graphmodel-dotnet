"""
Detailed implementation examples for embedded vs related properties.

This shows how our Python graph model library would actually handle
different storage strategies for complex properties.
"""

import json
from dataclasses import dataclass
from typing import Any, Dict, List, Optional

from graph_model import (
    Node,
    Relationship,
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


# ============================================================================
# APPROACH 1: EMBEDDED AS JSON STRING (with APOC support)
# ============================================================================

@node("PersonJsonEmbedded")
class PersonJsonEmbedded(Node):
    first_name: str = property_field(index=True)
    last_name: str = property_field(index=True)
    
    # Stored as JSON string: '{"email":"john@...", "phone":null, "city":"Portland"}'
    contact_info: ContactInfo = embedded_field(storage="json")
    tags: List[str] = embedded_field(storage="json", default_factory=list)


# ============================================================================
# APPROACH 2: EMBEDDED AS FLATTENED PROPERTIES
# ============================================================================

@node("PersonFlattened")
class PersonFlattened(Node):
    first_name: str = property_field(index=True)
    last_name: str = property_field(index=True)
    
    # Flattened: contact_info_email, contact_info_phone, contact_info_city
    contact_info: ContactInfo = embedded_field(storage="flattened")
    tags: List[str] = embedded_field(storage="array")  # Native Cypher array


# ============================================================================
# APPROACH 3: EMBEDDED AS CYPHER MAP (limited querying)
# ============================================================================

@node("PersonMapEmbedded")
class PersonMapEmbedded(Node):
    first_name: str = property_field(index=True)
    last_name: str = property_field(index=True)
    
    # Stored as Cypher map: {email: "john@...", phone: null, city: "Portland"}
    contact_info: ContactInfo = embedded_field(storage="map")
    tags: List[str] = embedded_field(storage="array")


# ============================================================================
# APPROACH 4: RELATED NODES (full graph capabilities)
# ============================================================================

@node("PersonRelated")
class PersonRelated(Node):
    first_name: str = property_field(index=True)
    last_name: str = property_field(index=True)
    
    # Stored as separate ContactInfo and Address nodes
    contact_info: Optional[ContactInfo] = related_node_field(
        relationship_type="HAS_CONTACT_INFO",
        private=True,  # Not discoverable in normal graph traversal
        required=False,
        default=None
    )
    
    addresses: List[Address] = related_node_field(
        relationship_type="HAS_ADDRESS",
        private=False,  # Discoverable in graph traversal
        default_factory=list
    )


def demonstrate_storage_implementations():
    """
    Show how different storage approaches would work in practice.
    """
    
    print("=== STORAGE IMPLEMENTATION COMPARISON ===\n")
    
    # Sample data
    contact = ContactInfo(email="john@example.com", phone="555-1234", city="Portland")
    tags = ["engineer", "python", "neo4j"]
    
    # ========================================================================
    # JSON STRING STORAGE
    # ========================================================================
    print("1. JSON STRING STORAGE")
    print("Python code:")
    print("""
    person = PersonJsonEmbedded(
        first_name="John",
        contact_info=ContactInfo(email="john@example.com", city="Portland"),
        tags=["engineer", "python"]
    )
    """)
    
    print("Serialized to Neo4j:")
    contact_json = json.dumps(contact.__dict__)
    tags_json = json.dumps(tags)
    print(f"""
    CREATE (p:PersonJsonEmbedded {{
        first_name: "John",
        contact_info: '{contact_json}',
        tags: '{tags_json}'
    }})
    """)
    
    print("Query for people in Portland:")
    print("""
    MATCH (p:PersonJsonEmbedded)
    WHERE apoc.json.path(p.contact_info, '$.city') = "Portland"
    RETURN p
    """)
    
    print("Pros: Preserves exact object structure")
    print("Cons: Requires APOC, limited indexing\n")
    
    # ========================================================================
    # FLATTENED PROPERTIES STORAGE
    # ========================================================================
    print("2. FLATTENED PROPERTIES STORAGE")
    print("Python code:")
    print("""
    person = PersonFlattened(
        first_name="John",
        contact_info=ContactInfo(email="john@example.com", city="Portland"),
        tags=["engineer", "python"]
    )
    """)
    
    print("Serialized to Neo4j:")
    print(f"""
    CREATE (p:PersonFlattened {{
        first_name: "John",
        contact_info_email: "john@example.com",
        contact_info_phone: null,
        contact_info_city: "Portland",
        tags: ["engineer", "python"]
    }})
    """)
    
    print("Query for people in Portland:")
    print("""
    MATCH (p:PersonFlattened)
    WHERE p.contact_info_city = "Portland"
    RETURN p
    """)
    
    print("Pros: Standard Cypher, can index individual fields")
    print("Cons: Property explosion, loses object structure\n")
    
    # ========================================================================
    # CYPHER MAP STORAGE
    # ========================================================================
    print("3. CYPHER MAP STORAGE")
    print("Python code:")
    print("""
    person = PersonMapEmbedded(
        first_name="John",
        contact_info=ContactInfo(email="john@example.com", city="Portland"),
        tags=["engineer", "python"]
    )
    """)
    
    print("Serialized to Neo4j:")
    print(f"""
    CREATE (p:PersonMapEmbedded {{
        first_name: "John",
        contact_info: {{
            email: "john@example.com",
            phone: null,
            city: "Portland"
        }},
        tags: ["engineer", "python"]
    }})
    """)
    
    print("Query for people in Portland (LIMITED):")
    print("""
    MATCH (p:PersonMapEmbedded)
    WHERE p.contact_info.city = "Portland"  // This may NOT work!
    RETURN p
    
    // Safer approach:
    MATCH (p:PersonMapEmbedded)
    WHERE p.contact_info['city'] = "Portland"  // May work in some cases
    RETURN p
    """)
    
    print("Pros: Preserves structure, no APOC needed")
    print("Cons: Very limited querying capabilities\n")
    
    # ========================================================================
    # RELATED NODES STORAGE
    # ========================================================================
    print("4. RELATED NODES STORAGE")
    print("Python code:")
    print("""
    person = PersonRelated(
        first_name="John",
        contact_info=ContactInfo(email="john@example.com", city="Portland")
    )
    """)
    
    print("Serialized to Neo4j:")
    print(f"""
    CREATE (p:PersonRelated {{first_name: "John"}})
    CREATE (c:ContactInfo {{
        email: "john@example.com",
        phone: null,
        city: "Portland"
    }})
    CREATE (p)-[:HAS_CONTACT_INFO]->(c)
    """)
    
    print("Query for people in Portland:")
    print("""
    MATCH (p:PersonRelated)-[:HAS_CONTACT_INFO]->(c:ContactInfo)
    WHERE c.city = "Portland"
    RETURN p
    """)
    
    print("Pros: Full Cypher power, indexing, relationships")
    print("Cons: More complex queries, relationship overhead\n")
    
    # ========================================================================
    # LIBRARY IMPLEMENTATION STRATEGY
    # ========================================================================
    print("5. RECOMMENDED LIBRARY IMPLEMENTATION STRATEGY\n")
    
    print("For embedded_field(), support multiple storage strategies:")
    print("""
    # Default: JSON string (requires APOC but preserves structure)
    contact_info: ContactInfo = embedded_field()
    
    # Explicit JSON
    contact_info: ContactInfo = embedded_field(storage="json")
    
    # Flattened properties (no APOC needed)
    contact_info: ContactInfo = embedded_field(storage="flattened")
    
    # Cypher map (limited querying)
    contact_info: ContactInfo = embedded_field(storage="map")
    """)
    
    print("Query translation should handle storage type automatically:")
    print("""
    # Python query (same syntax regardless of storage)
    people = await (graph.nodes(Person)
        .where(lambda p: p.contact_info.city == "Portland")
        .to_list())
    
    # Translates to appropriate Cypher based on storage type:
    # JSON: WHERE apoc.json.path(p.contact_info, '$.city') = "Portland"
    # Flattened: WHERE p.contact_info_city = "Portland"  
    # Map: WHERE p.contact_info['city'] = "Portland"
    """)
    
    print("This approach gives developers choice based on their needs:")
    print("- Use JSON for rich objects when APOC is available")
    print("- Use flattened for simple objects when APOC isn't available")
    print("- Use related nodes for complex objects that need full querying")


def demonstrate_query_complexity():
    """
    Show how query complexity differs between approaches.
    """
    
    print("\n=== QUERY COMPLEXITY COMPARISON ===\n")
    
    # Simple property access
    print("SIMPLE PROPERTY ACCESS:")
    print("Python: person.contact_info.city")
    print("JSON: apoc.json.path(p.contact_info, '$.city')")
    print("Flattened: p.contact_info_city")
    print("Map: p.contact_info['city']")
    print("Related: (p)-[:HAS_CONTACT_INFO]->(c) WHERE c.city\n")
    
    # Complex nested access
    print("COMPLEX NESTED ACCESS:")
    print("Python: person.contact_info.emergency_contact.phone")
    print("JSON: apoc.json.path(p.contact_info, '$.emergency_contact.phone')")
    print("Flattened: p.contact_info_emergency_contact_phone")
    print("Map: p.contact_info['emergency_contact']['phone'] (may not work)")
    print("Related: (p)-[:HAS_CONTACT_INFO]->(c)-[:HAS_EMERGENCY_CONTACT]->(e) WHERE e.phone\n")
    
    # Array operations
    print("ARRAY OPERATIONS:")
    print("Python: 'engineer' in person.tags")
    print("JSON: apoc.json.path(p.tags, '$[?(@ == \"engineer\")]')")
    print("Flattened: 'engineer' IN p.tags")
    print("Map: 'engineer' IN p.tags")
    print("Related: (p)-[:HAS_TAG]->(t:Tag {name: 'engineer'})\n")


if __name__ == "__main__":
    demonstrate_storage_implementations()
    demonstrate_query_complexity() 