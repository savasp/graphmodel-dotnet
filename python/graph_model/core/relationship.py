"""Relationship interfaces and base implementations."""

from enum import Enum
from typing import Protocol

from pydantic import BaseModel, Field

from .entity import IEntity, generate_entity_id


class RelationshipDirection(Enum):
    """Defines the direction of a relationship in the graph."""
    
    OUTGOING = "outgoing"
    """Relationship direction from source to target (->)"""
    
    INCOMING = "incoming" 
    """Relationship direction from target to source (<-)"""
    
    BIDIRECTIONAL = "bidirectional"
    """Relationship is bidirectional (<->)"""


class IRelationship(IEntity, Protocol):
    """
    Protocol defining the contract for relationship entities in the graph model.
    
    Relationships connect two nodes and can have their own properties.
    They form the connections between nodes, creating the graph structure.
    """

    @property
    def direction(self) -> RelationshipDirection:
        """
        Gets the direction of this relationship.
        
        The direction determines how the relationship can be traversed.
        
        Returns:
            The relationship direction.
        """
        ...

    @property
    def start_node_id(self) -> str:
        """
        Gets the ID of the start node in this relationship.
        
        This is the ID of the node from which the relationship originates.
        
        Returns:
            The start node ID.
        """
        ...

    @property
    def end_node_id(self) -> str:
        """
        Gets the ID of the end node in this relationship.
        
        This is the ID of the node to which the relationship points.
        
        Returns:
            The end node ID.
        """
        ...


class Relationship(BaseModel):
    """
    Base class for graph relationships that provides a default implementation 
    of the IRelationship interface.
    
    This serves as a foundation for creating domain-specific relationship entities
    with automatic ID generation and basic relationship functionality.
    """

    id: str = Field(default_factory=generate_entity_id, frozen=True)
    """The unique identifier of this relationship."""
    
    start_node_id: str = Field(frozen=True)
    """The ID of the start node in the relationship."""
    
    end_node_id: str = Field(frozen=True)
    """The ID of the end node in the relationship."""
    
    direction: RelationshipDirection = Field(default=RelationshipDirection.OUTGOING, frozen=True)
    """The direction of the relationship."""

    model_config = {
        "frozen": True,  # Make relationships immutable by default
        "use_enum_values": True
    } 