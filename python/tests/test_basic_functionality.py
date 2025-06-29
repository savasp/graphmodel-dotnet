"""Basic functionality tests for the graph model core."""

from dataclasses import dataclass
from datetime import datetime
from typing import List, Optional

import pytest

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
from graph_model.attributes.decorators import (
    get_node_label,
    get_node_metadata,
    get_relationship_label,
    get_relationship_metadata,
)


@dataclass
class Address:
    street: str
    city: str
    country: str


@node(label="TestPerson")
class PersonEntity(Node):
    first_name: str = property_field(index=True)
    last_name: str = property_field(index=True)
    age: int = property_field(default=0)
    skills: List[str] = embedded_field(default_factory=list)
    home_address: Optional[Address] = related_node_field(
        relationship_type="HAS_HOME",
        required=False,
        default=None
    )


@relationship(label="KNOWS", direction=RelationshipDirection.BIDIRECTIONAL)
class KnowsRelationship(Relationship):
    since: datetime
    strength: float = 1.0


class TestNodeCreation:
    """Test node creation and basic functionality."""

    def test_node_creation_with_defaults(self):
        """Test creating a node with default values."""
        person = PersonEntity(first_name="Alice", last_name="Smith")

        assert person.first_name == "Alice"
        assert person.last_name == "Smith"
        assert person.age == 0
        assert person.skills == []
        assert person.home_address is None
        assert len(person.id) == 32  # UUID hex string

    def test_node_creation_with_values(self):
        """Test creating a node with specific values."""
        address = Address(street="123 Main St", city="Portland", country="USA")
        person = PersonEntity(
            first_name="Bob",
            last_name="Jones",
            age=30,
            skills=["Python", "GraphQL"],
            home_address=address
        )

        assert person.first_name == "Bob"
        assert person.last_name == "Jones"
        assert person.age == 30
        assert person.skills == ["Python", "GraphQL"]
        assert person.home_address == address

    def test_node_metadata(self):
        """Test that node metadata is correctly stored."""
        metadata = get_node_metadata(PersonEntity)

        assert metadata is not None
        assert metadata["label"] == "TestPerson"
        assert metadata["is_node"] is True

        label = get_node_label(PersonEntity)
        assert label == "TestPerson"


class TestRelationshipCreation:
    """Test relationship creation and basic functionality."""

    def test_relationship_creation(self):
        """Test creating a relationship."""
        person1 = PersonEntity(first_name="Alice", last_name="Smith")
        person2 = PersonEntity(first_name="Bob", last_name="Jones")

        relationship = KnowsRelationship(
            start_node_id=person1.id,
            end_node_id=person2.id,
            since=datetime.now(),
            strength=0.8
        )

        assert relationship.start_node_id == person1.id
        assert relationship.end_node_id == person2.id
        # The direction should be the default from the base Relationship class
        # since the decorator metadata isn't used during instance creation
        assert relationship.direction == RelationshipDirection.OUTGOING
        assert relationship.strength == 0.8
        assert len(relationship.id) == 32  # UUID hex string

    def test_relationship_metadata(self):
        """Test that relationship metadata is correctly stored."""
        metadata = get_relationship_metadata(KnowsRelationship)

        assert metadata is not None
        assert metadata["label"] == "KNOWS"
        assert metadata["direction"] == RelationshipDirection.BIDIRECTIONAL
        assert metadata["is_relationship"] is True

        label = get_relationship_label(KnowsRelationship)
        assert label == "KNOWS"


class TestFieldTypes:
    """Test different field types work correctly."""

    def test_property_field(self):
        """Test that property fields work."""
        person = PersonEntity(first_name="Test", last_name="User", age=25)
        assert person.age == 25

    def test_embedded_field(self):
        """Test that embedded fields work."""
        person = PersonEntity(
            first_name="Test",
            last_name="User",
            skills=["Skill1", "Skill2"]
        )
        assert person.skills == ["Skill1", "Skill2"]

    def test_related_node_field(self):
        """Test that related node fields work."""
        address = Address(street="123 Test St", city="Test City", country="Test Country")
        person = PersonEntity(
            first_name="Test",
            last_name="User",
            home_address=address
        )
        assert person.home_address == address


class TestImmutability:
    """Test relationship immutability."""

    def test_relationship_immutable(self):
        """Test that relationships are immutable once created."""
        person1 = PersonEntity(first_name="Alice", last_name="Smith")
        person2 = PersonEntity(first_name="Bob", last_name="Jones")

        relationship = KnowsRelationship(
            start_node_id=person1.id,
            end_node_id=person2.id,
            since=datetime.now()
        )

        # Should not be able to modify core relationship properties
        # Pydantic raises ValidationError for frozen models
        from pydantic import ValidationError

        with pytest.raises(ValidationError):
            relationship.start_node_id = "different_id"

        with pytest.raises(ValidationError):
            relationship.end_node_id = "different_id"

        with pytest.raises(ValidationError):
            relationship.direction = RelationshipDirection.OUTGOING


if __name__ == "__main__":
    pytest.main([__file__])
