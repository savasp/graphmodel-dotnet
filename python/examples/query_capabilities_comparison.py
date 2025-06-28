"""
Comprehensive comparison of query capabilities between APOC-based embedded fields 
and related node approaches.

This demonstrates the significant differences in what types of queries 
and operations are possible with each approach.
"""

import os
import sys
from dataclasses import dataclass
from datetime import date, datetime
from typing import List, Optional

sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from graph_model import Node, embedded_field, node, property_field, related_node_field


@dataclass
class ContactInfo:
    email: str
    phone: Optional[str] = None
    city: str = ""
    created_date: date = None
    score: float = 0.0


@dataclass
class Address:
    street: str
    city: str
    state: str
    country: str
    zip_code: str
    created_date: date = None
    is_primary: bool = False


# APOC-based embedded approach
@node(label="PersonEmbedded")
class PersonEmbedded(Node):
    name: str = property_field(index=True)
    age: int = property_field(default=0)
    contact_info: ContactInfo = embedded_field(storage="json")  # JSON string
    addresses: List[Address] = embedded_field(storage="json", default_factory=list)


# Related node approach  
@node(label="PersonRelated")
class PersonRelated(Node):
    name: str = property_field(index=True)
    age: int = property_field(default=0)
    
    contact_info: Optional[ContactInfo] = related_node_field(
        relationship_type="HAS_CONTACT_INFO",
        private=True,
        required=False
    )
    
    addresses: List[Address] = related_node_field(
        relationship_type="HAS_ADDRESS",
        private=False,
        default_factory=list
    )


def demonstrate_query_capabilities():
    """
    Compare what types of queries are possible with each approach.
    """
    
    print("=== QUERY CAPABILITIES COMPARISON ===\n")
    print("APOC-based Embedded vs Related Nodes\n")
    
    # ========================================================================
    # 1. BASIC FILTERING
    # ========================================================================
    print("1. BASIC FILTERING")
    print("✅ Both approaches support basic filtering\n")
    
    print("Find people in Portland:")
    print("EMBEDDED (APOC):")
    print("""
    MATCH (p:PersonEmbedded)
    WHERE apoc.json.path(p.contact_info, '$.city') = "Portland"
    RETURN p
    """)
    
    print("RELATED NODES:")
    print("""
    MATCH (p:PersonRelated)-[:HAS_CONTACT_INFO]->(c:ContactInfo)
    WHERE c.city = "Portland"
    RETURN p
    """)
    print()
    
    # ========================================================================
    # 2. AGGREGATION FUNCTIONS - MAJOR DIFFERENCES
    # ========================================================================
    print("2. AGGREGATION FUNCTIONS")
    print("❌ EMBEDDED: Limited aggregation capabilities")
    print("✅ RELATED: Full aggregation support\n")
    
    print("Count people by city:")
    print("EMBEDDED (APOC) - POSSIBLE BUT CLUNKY:")
    print("""
    MATCH (p:PersonEmbedded)
    WITH p, apoc.json.path(p.contact_info, '$.city') AS city
    WHERE city IS NOT NULL
    RETURN city, count(p) AS person_count
    ORDER BY person_count DESC
    """)
    
    print("RELATED NODES - NATURAL:")
    print("""
    MATCH (p:PersonRelated)-[:HAS_CONTACT_INFO]->(c:ContactInfo)
    RETURN c.city, count(p) AS person_count
    ORDER BY person_count DESC
    """)
    print()
    
    # ========================================================================
    # 3. COMPLEX AGGREGATIONS
    # ========================================================================
    print("3. COMPLEX AGGREGATIONS")
    print("❌ EMBEDDED: Very difficult or impossible")
    print("✅ RELATED: Full SQL-like aggregation support\n")
    
    print("Average age by city with person count:")
    print("EMBEDDED (APOC) - VERY COMPLEX:")
    print("""
    MATCH (p:PersonEmbedded)
    WITH p, apoc.json.path(p.contact_info, '$.city') AS city
    WHERE city IS NOT NULL
    RETURN 
        city,
        avg(p.age) AS avg_age,
        count(p) AS person_count,
        collect(p.name)[0..3] AS sample_names
    ORDER BY avg_age DESC
    """)
    
    print("RELATED NODES - STRAIGHTFORWARD:")
    print("""
    MATCH (p:PersonRelated)-[:HAS_CONTACT_INFO]->(c:ContactInfo)
    RETURN 
        c.city,
        avg(p.age) AS avg_age,
        count(p) AS person_count,
        collect(p.name)[0..3] AS sample_names
    ORDER BY avg_age DESC
    """)
    print()
    
    # ========================================================================
    # 4. SORTING AND ORDERING
    # ========================================================================
    print("4. SORTING AND ORDERING")
    print("⚠️  EMBEDDED: Limited sorting on extracted JSON values")
    print("✅ RELATED: Full sorting capabilities\n")
    
    print("Sort people by city name, then by contact score:")
    print("EMBEDDED (APOC) - COMPLEX:")
    print("""
    MATCH (p:PersonEmbedded)
    WITH p, 
         apoc.json.path(p.contact_info, '$.city') AS city,
         apoc.json.path(p.contact_info, '$.score') AS score
    WHERE city IS NOT NULL
    RETURN p
    ORDER BY city ASC, score DESC
    """)
    
    print("RELATED NODES - NATURAL:")
    print("""
    MATCH (p:PersonRelated)-[:HAS_CONTACT_INFO]->(c:ContactInfo)
    RETURN p, c
    ORDER BY c.city ASC, c.score DESC
    """)
    print()
    
    # ========================================================================
    # 5. RANGE QUERIES AND COMPARISONS
    # ========================================================================
    print("5. RANGE QUERIES AND COMPARISONS")
    print("⚠️  EMBEDDED: Limited numeric/date comparisons")
    print("✅ RELATED: Full comparison operator support\n")
    
    print("Find people with contact score between 7.0 and 9.0:")
    print("EMBEDDED (APOC) - TYPE CONVERSION ISSUES:")
    print("""
    MATCH (p:PersonEmbedded)
    WITH p, apoc.json.path(p.contact_info, '$.score') AS score_str
    WHERE score_str IS NOT NULL 
      AND toFloat(score_str) >= 7.0 
      AND toFloat(score_str) <= 9.0
    RETURN p
    """)
    
    print("RELATED NODES - DIRECT:")
    print("""
    MATCH (p:PersonRelated)-[:HAS_CONTACT_INFO]->(c:ContactInfo)
    WHERE c.score >= 7.0 AND c.score <= 9.0
    RETURN p, c
    """)
    print()
    
    # ========================================================================
    # 6. INDEXING AND PERFORMANCE
    # ========================================================================
    print("6. INDEXING AND PERFORMANCE")
    print("❌ EMBEDDED: No indexing on JSON properties")
    print("✅ RELATED: Full indexing support\n")
    
    print("Create indexes for performance:")
    print("EMBEDDED (APOC) - NO DIRECT INDEXING:")
    print("""
    // Cannot create index on JSON properties
    // Only full-text search on entire JSON possible:
    CREATE FULLTEXT INDEX contact_fulltext
    FOR (p:PersonEmbedded) ON EACH [p.contact_info]
    """)
    
    print("RELATED NODES - FULL INDEXING:")
    print("""
    CREATE INDEX contact_city_idx FOR (c:ContactInfo) ON (c.city)
    CREATE INDEX contact_score_idx FOR (c:ContactInfo) ON (c.score)
    CREATE INDEX contact_email_idx FOR (c:ContactInfo) ON (c.email)
    CREATE CONSTRAINT contact_email_unique FOR (c:ContactInfo) REQUIRE c.email IS UNIQUE
    """)
    print()
    
    # ========================================================================
    # 7. FULL-TEXT SEARCH
    # ========================================================================
    print("7. FULL-TEXT SEARCH")
    print("⚠️  EMBEDDED: Search entire JSON blob")
    print("✅ RELATED: Targeted field search\n")
    
    print("Search for email domains:")
    print("EMBEDDED (APOC) - BROAD SEARCH:")
    print("""
    CALL db.index.fulltext.queryNodes('contact_fulltext', 'gmail.com')
    YIELD node, score
    RETURN node, score
    // Searches entire JSON, may have false positives
    """)
    
    print("RELATED NODES - PRECISE SEARCH:")
    print("""
    MATCH (p:PersonRelated)-[:HAS_CONTACT_INFO]->(c:ContactInfo)
    WHERE c.email CONTAINS 'gmail.com'
    RETURN p, c
    // Can also use full-text index on specific fields
    """)
    print()
    
    # ========================================================================
    # 8. COMPLEX EXPRESSIONS
    # ========================================================================
    print("8. COMPLEX EXPRESSIONS")
    print("❌ EMBEDDED: Limited expression capabilities")
    print("✅ RELATED: Full Cypher expression support\n")
    
    print("Complex conditional logic:")
    print("EMBEDDED (APOC) - VERY COMPLEX:")
    print("""
    MATCH (p:PersonEmbedded)
    WITH p,
         apoc.json.path(p.contact_info, '$.city') AS city,
         apoc.json.path(p.contact_info, '$.score') AS score_str,
         apoc.json.path(p.contact_info, '$.email') AS email
    WHERE city IS NOT NULL
    WITH p, city, toFloat(score_str) AS score, email
    WHERE (city = "Portland" AND score > 8.0) 
       OR (city = "Seattle" AND email ENDS WITH ".edu")
       OR (p.age > 30 AND score > 9.0)
    RETURN p, city, score, email
    """)
    
    print("RELATED NODES - NATURAL:")
    print("""
    MATCH (p:PersonRelated)-[:HAS_CONTACT_INFO]->(c:ContactInfo)
    WHERE (c.city = "Portland" AND c.score > 8.0)
       OR (c.city = "Seattle" AND c.email ENDS WITH ".edu") 
       OR (p.age > 30 AND c.score > 9.0)
    RETURN p, c
    """)
    print()
    
    # ========================================================================
    # 9. ARRAY OPERATIONS ON EMBEDDED LISTS
    # ========================================================================
    print("9. ARRAY OPERATIONS ON EMBEDDED LISTS")
    print("❌ EMBEDDED: Very limited array query capabilities")
    print("✅ RELATED: Full collection and relationship support\n")
    
    print("Find people with multiple addresses in the same state:")
    print("EMBEDDED (APOC) - EXTREMELY COMPLEX:")
    print("""
    MATCH (p:PersonEmbedded)
    WITH p, apoc.json.path(p.addresses, '$[*].state') AS states
    WHERE size(states) > 1
    WITH p, states, apoc.coll.frequencies(states) AS state_counts
    WHERE any(count IN values(state_counts) WHERE count > 1)
    RETURN p
    // This is very inefficient and complex
    """)
    
    print("RELATED NODES - STRAIGHTFORWARD:")
    print("""
    MATCH (p:PersonRelated)-[:HAS_ADDRESS]->(a:Address)
    WITH p, a.state AS state, count(a) AS addr_count
    WHERE addr_count > 1
    RETURN p, state, addr_count
    """)
    print()
    
    # ========================================================================
    # 10. GRAPH TRAVERSAL
    # ========================================================================
    print("10. GRAPH TRAVERSAL AND PATTERN MATCHING")
    print("❌ EMBEDDED: No graph traversal capabilities")
    print("✅ RELATED: Full graph database power\n")
    
    print("Find people who share addresses:")
    print("EMBEDDED (APOC) - IMPOSSIBLE:")
    print("""
    // Cannot traverse relationships between embedded data
    // Would need complex JSON comparisons and collections
    """)
    
    print("RELATED NODES - NATURAL GRAPH QUERY:")
    print("""
    MATCH (p1:PersonRelated)-[:HAS_ADDRESS]->(addr:Address)<-[:HAS_ADDRESS]-(p2:PersonRelated)
    WHERE p1 <> p2
    RETURN p1, p2, addr
    """)
    print()
    
    # ========================================================================
    # 11. UPDATES AND MODIFICATIONS
    # ========================================================================
    print("11. UPDATES AND MODIFICATIONS")
    print("❌ EMBEDDED: Must rewrite entire JSON")
    print("✅ RELATED: Granular updates\n")
    
    print("Update just the city in contact info:")
    print("EMBEDDED (APOC) - REWRITE ENTIRE OBJECT:")
    print("""
    MATCH (p:PersonEmbedded)
    WHERE apoc.json.path(p.contact_info, '$.email') = 'john@example.com'
    WITH p, apoc.convert.fromJsonMap(p.contact_info) AS contact
    SET p.contact_info = apoc.convert.toJson(
        apoc.map.setKey(contact, 'city', 'New York')
    )
    """)
    
    print("RELATED NODES - DIRECT UPDATE:")
    print("""
    MATCH (p:PersonRelated)-[:HAS_CONTACT_INFO]->(c:ContactInfo)
    WHERE c.email = 'john@example.com'
    SET c.city = 'New York'
    """)
    print()
    
    print("=== SUMMARY ===\n")
    print("EMBEDDED (APOC-based) is better for:")
    print("- Simple object storage with minimal querying")
    print("- Reducing query complexity for basic read operations")
    print("- Maintaining object structure in storage")
    print("- Fewer nodes in the graph\n")
    
    print("RELATED NODES are better for:")
    print("- Complex aggregations and analytics")
    print("- Full indexing and performance optimization")
    print("- Rich querying capabilities")
    print("- Graph traversal and pattern matching")
    print("- Granular updates")
    print("- SQL-like operations")
    print("- Sharing data between entities")
    print("- Full-text search on specific fields")


if __name__ == "__main__":
    demonstrate_query_capabilities() 