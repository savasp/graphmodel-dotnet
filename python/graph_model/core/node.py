"""Node interfaces and base implementations."""

from typing import Protocol

from pydantic import BaseModel, Field

from .entity import IEntity, generate_entity_id


class INode(IEntity, Protocol):
    """
    Protocol defining the contract for node entities in the graph model.
    
    Nodes represent primary data entities that can be connected via relationships.
    This interface serves as a marker interface that extends IEntity,
    signifying that implementing classes represent nodes rather than relationships.
    """

    pass  # Marker interface - no additional members beyond IEntity


class Node(BaseModel):
    """
    Base class for graph nodes that provides a default implementation of the INode interface.
    
    This serves as a foundation for creating domain-specific node entities with
    automatic ID generation and basic node functionality.
    """

    id: str = Field(default_factory=generate_entity_id, frozen=True)
    """The unique identifier of this node."""

    model_config = {
        "validate_assignment": True,
        "use_enum_values": True
    } 