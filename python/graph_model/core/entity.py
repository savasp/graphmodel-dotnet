"""Base entity interface for all graph model entities."""

from typing import Protocol
from uuid import uuid4


class IEntity(Protocol):
    """
    Protocol defining the foundation for all entities in the graph model.
    
    This is the base interface for both nodes and relationships, providing
    core identity functionality. All graph entities must have a unique
    identifier that remains immutable after persistence.
    """

    @property
    def id(self) -> str:
        """
        Gets the unique identifier of the entity.
        
        Identifiers should be immutable once the entity has been persisted
        to ensure referential integrity.
        
        Returns:
            The unique identifier as a string.
        """
        ...


def generate_entity_id() -> str:
    """
    Generate a new unique entity ID.
    
    Uses UUID4 without hyphens for consistency with the .NET implementation.
    
    Returns:
        A new unique identifier string.
    """
    return uuid4().hex 