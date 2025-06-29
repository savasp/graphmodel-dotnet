"""
Example demonstrating auto_field usage for automatic field type detection.

This shows how developers can avoid explicit related_node_field declarations
by using auto_field, which automatically determines the appropriate storage strategy.
"""

import os
import sys

sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from dataclasses import dataclass
from datetime import datetime
from typing import Any, Dict, List, Optional

from graph_model import (
    Node,
    Relationship,
    auto_field,
    node,
    property_field,  # Still available for explicit control
    related_node_field,  # Still available for explicit control
    relationship,
)


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
    preferences: Dict[str, Any] = None


@dataclass
class Skill:
    """A complex type representing a skill."""
    name: str
    level: int
    years_experience: int
    certifications: List[str] = None


# ============================================================================
# APPROACH 1: Using auto_field (Recommended for most cases)
# ============================================================================

@node("Person")
class Person(Node):
    """
    Person entity using auto_field for automatic field type detection.
    
    auto_field automatically chooses the appropriate storage strategy:
    - Simple types → property_field (stored directly on node)
    - Complex types → related_node_field (stored as separate nodes)
    - Collections → related_node_field (stored as separate nodes)
    """

    # Simple types - automatically use property_field
    name: str = auto_field(index=True)
    age: int = auto_field(default=0)
    email: str = auto_field()
    is_active: bool = auto_field(default=True)
    created_date: datetime = auto_field(default_factory=datetime.utcnow)

    # Simple collections - automatically use property_field
    tags: List[str] = auto_field(default_factory=list)
    scores: List[float] = auto_field(default_factory=list)

    # Complex types - automatically use related_node_field
    home_address: Address = auto_field()
    work_address: Optional[Address] = auto_field(required=False)
    contact_info: ContactInfo = auto_field()

    # Complex collections - automatically use related_node_field
    skills: List[Skill] = auto_field(default_factory=list)
    previous_addresses: List[Address] = auto_field(default_factory=list)

    # Force embedded storage for specific complex types
    metadata: Dict[str, Any] = auto_field(prefer_embedded=True, default_factory=dict)


# ============================================================================
# APPROACH 2: Explicit field types (For fine-grained control)
# ============================================================================

@node("PersonExplicit")
class PersonExplicit(Node):
    """
    Person entity using explicit field types for maximum control.
    
    This approach gives you complete control over storage strategies
    but requires more explicit declarations.
    """

    # Simple properties - explicit property_field
    name: str = property_field(index=True)
    age: int = property_field(default=0)
    email: str = property_field()

    # Simple collections - explicit property_field
    tags: List[str] = property_field(default_factory=list)

    # Complex properties - explicit related_node_field with .NET convention
    home_address: Address = related_node_field()  # Uses "__PROPERTY__home_address__"
    work_address: Address = related_node_field()  # Uses "__PROPERTY__work_address__"

    # Custom relationship types
    contact_info: ContactInfo = related_node_field(
        relationship_type="HAS_CONTACT_INFO",
        private=False
    )

    # Complex collections - explicit related_node_field
    skills: List[Skill] = related_node_field(
        relationship_type="HAS_SKILL",
        private=False,
        default_factory=list
    )

    # Embedded storage for specific cases
    metadata: Dict[str, Any] = property_field(default_factory=dict)  # Simple dict


# ============================================================================
# APPROACH 3: Mixed approach (Best of both worlds)
# ============================================================================

@node("PersonMixed")
class PersonMixed(Node):
    """
    Person entity using a mixed approach.
    
    Use auto_field for most cases, but override with explicit types
    when you need specific control.
    """

    # Use auto_field for most properties
    name: str = auto_field(index=True)
    age: int = auto_field(default=0)
    home_address: Address = auto_field()
    skills: List[Skill] = auto_field(default_factory=list)

    # Override with explicit types when needed
    work_address: Address = related_node_field(
        relationship_type="WORKS_AT",  # Custom relationship type
        private=False  # Public relationship for graph traversal
    )

    # Force embedded storage for performance-critical data
    quick_metadata: Dict[str, Any] = property_field(default_factory=dict)


# ============================================================================
# RELATIONSHIP EXAMPLE
# ============================================================================

@relationship("KNOWS")
class Knows(Relationship):
    """Relationship between people."""

    # Simple properties on relationships
    since: datetime = auto_field(default_factory=datetime.utcnow)
    strength: float = auto_field(default=1.0)

    # Note: Relationships typically don't have complex properties
    # in most graph databases, so auto_field will use property_field


def demonstrate_auto_field_benefits():
    """Show the benefits of using auto_field."""

    print("=== AUTO_FIELD BENEFITS ===\n")

    print("1. AUTOMATIC TYPE DETECTION")
    print("   - Simple types → property_field")
    print("   - Complex types → related_node_field")
    print("   - Collections → related_node_field")
    print("   - No need to remember which field type to use\n")

    print("2. .NET COMPATIBILITY")
    print("   - Uses '__PROPERTY__{fieldName}__' convention")
    print("   - Ensures Python and .NET can read/write same data")
    print("   - Automatic relationship type generation\n")

    print("3. FLEXIBILITY")
    print("   - Override with explicit types when needed")
    print("   - prefer_embedded=True for performance-critical data")
    print("   - Full control when required\n")

    print("4. REDUCED BOILERPLATE")
    print("   - Less explicit field declarations")
    print("   - Cleaner model definitions")
    print("   - Easier to maintain\n")

    print("5. TYPE SAFETY")
    print("   - Still provides full type checking")
    print("   - Pydantic validation works as expected")
    print("   - IDE support and autocomplete\n")

    print("=== COMPARISON ===\n")

    print("WITH auto_field:")
    print("""
    @node("Person")
    class Person(Node):
        name: str = auto_field(index=True)
        age: int = auto_field(default=0)
        address: Address = auto_field()  # Automatically related_node_field
        skills: List[Skill] = auto_field()  # Automatically related_node_field
    """)

    print("WITH explicit fields:")
    print("""
    @node("Person")
    class Person(Node):
        name: str = property_field(index=True)
        age: int = property_field(default=0)
        address: Address = related_node_field()  # Must be explicit
        skills: List[Skill] = related_node_field()  # Must be explicit
    """)

    print("=== RECOMMENDATIONS ===\n")

    print("USE auto_field WHEN:")
    print("- You want minimal boilerplate")
    print("- You're following .NET conventions")
    print("- You don't need custom relationship types")
    print("- You want automatic type detection\n")

    print("USE explicit fields WHEN:")
    print("- You need custom relationship types")
    print("- You want public vs private relationships")
    print("- You need fine-grained control")
    print("- You're optimizing for specific use cases\n")

    print("USE mixed approach WHEN:")
    print("- You want the best of both worlds")
    print("- Most fields can be auto-detected")
    print("- Some fields need special handling")


if __name__ == "__main__":
    demonstrate_auto_field_benefits()
