"""
Core graph interface and implementation.

This module defines the core interfaces and base classes for the graph model.
"""

from abc import ABC, abstractmethod
from typing import Any, Dict, Generic, List, Optional, Type, TypeVar
from uuid import uuid4

from pydantic import BaseModel

from .entity import IEntity
from .exceptions import GraphException
from .node import INode, Node
from .relationship import IRelationship, Relationship
from .transaction import IGraphTransaction

T = TypeVar('T', bound=IEntity)
TNode = TypeVar('TNode', bound=INode)
TRelationship = TypeVar('TRelationship', bound=IRelationship)


class IGraph(ABC, Generic[TNode, TRelationship]):
    """
    Core interface for graph database operations.
    
    Provides CRUD operations for nodes and relationships, query capabilities,
    and transaction management.
    """
    
    @abstractmethod
    async def create_node(self, node: TNode, transaction: Optional[IGraphTransaction] = None) -> TNode:
        """Create a new node in the graph."""
        pass
    
    @abstractmethod
    async def get_node(self, node_id: str, transaction: Optional[IGraphTransaction] = None) -> Optional[TNode]:
        """Retrieve a node by its ID."""
        pass
    
    @abstractmethod
    async def update_node(self, node: TNode, transaction: Optional[IGraphTransaction] = None) -> TNode:
        """Update an existing node."""
        pass
    
    @abstractmethod
    async def delete_node(self, node_id: str, transaction: Optional[IGraphTransaction] = None) -> bool:
        """Delete a node by its ID."""
        pass
    
    @abstractmethod
    async def create_relationship(self, relationship: TRelationship, transaction: Optional[IGraphTransaction] = None) -> TRelationship:
        """Create a new relationship in the graph."""
        pass
    
    @abstractmethod
    async def get_relationship(self, relationship_id: str, transaction: Optional[IGraphTransaction] = None) -> Optional[TRelationship]:
        """Retrieve a relationship by its ID."""
        pass
    
    @abstractmethod
    async def update_relationship(self, relationship: TRelationship, transaction: Optional[IGraphTransaction] = None) -> TRelationship:
        """Update an existing relationship."""
        pass
    
    @abstractmethod
    async def delete_relationship(self, relationship_id: str, transaction: Optional[IGraphTransaction] = None) -> bool:
        """Delete a relationship by its ID."""
        pass
    
    @abstractmethod
    def transaction(self) -> IGraphTransaction:
        """Start a new transaction."""
        pass
    
    @abstractmethod
    def nodes(self, node_type: Type[TNode]) -> 'IGraphNodeQueryable[TNode]':
        """Get a queryable for nodes of the specified type."""
        pass
    
    @abstractmethod
    def relationships(self, relationship_type: Type[TRelationship]) -> 'IGraphRelationshipQueryable[TRelationship]':
        """Get a queryable for relationships of the specified type."""
        pass


class IGraphNodeQueryable(Generic[TNode]):
    """Interface for queryable node collections."""
    
    @abstractmethod
    def where(self, predicate) -> 'IGraphNodeQueryable[TNode]':
        """Filter nodes based on a predicate."""
        pass
    
    @abstractmethod
    def order_by(self, key_selector) -> 'IOrderedGraphNodeQueryable[TNode]':
        """Order nodes by a key selector."""
        pass
    
    @abstractmethod
    def take(self, count: int) -> 'IGraphNodeQueryable[TNode]':
        """Take the first N nodes."""
        pass
    
    @abstractmethod
    def skip(self, count: int) -> 'IGraphNodeQueryable[TNode]':
        """Skip the first N nodes."""
        pass
    
    @abstractmethod
    async def to_list(self) -> List[TNode]:
        """Execute the query and return results as a list."""
        pass
    
    @abstractmethod
    async def first_or_default(self) -> Optional[TNode]:
        """Get the first node or None if no nodes match."""
        pass
    
    @abstractmethod
    async def single_or_default(self) -> Optional[TNode]:
        """Get the single node or None if no nodes match."""
        pass


class IOrderedGraphNodeQueryable(IGraphNodeQueryable[TNode]):
    """Interface for ordered queryable node collections."""
    
    @abstractmethod
    def then_by(self, key_selector) -> 'IOrderedGraphNodeQueryable[TNode]':
        """Add a secondary ordering."""
        pass


class IGraphRelationshipQueryable(Generic[TRelationship]):
    """Interface for queryable relationship collections."""
    
    @abstractmethod
    def where(self, predicate) -> 'IGraphRelationshipQueryable[TRelationship]':
        """Filter relationships based on a predicate."""
        pass
    
    @abstractmethod
    def order_by(self, key_selector) -> 'IOrderedGraphRelationshipQueryable[TRelationship]':
        """Order relationships by a key selector."""
        pass
    
    @abstractmethod
    def take(self, count: int) -> 'IGraphRelationshipQueryable[TRelationship]':
        """Take the first N relationships."""
        pass
    
    @abstractmethod
    def skip(self, count: int) -> 'IGraphRelationshipQueryable[TRelationship]':
        """Skip the first N relationships."""
        pass
    
    @abstractmethod
    async def to_list(self) -> List[TRelationship]:
        """Execute the query and return results as a list."""
        pass


class IOrderedGraphRelationshipQueryable(IGraphRelationshipQueryable[TRelationship]):
    """Interface for ordered queryable relationship collections."""
    
    @abstractmethod
    def then_by(self, key_selector) -> 'IOrderedGraphRelationshipQueryable[TRelationship]':
        """Add a secondary ordering."""
        pass


class GraphDataModel:
    """
    Utility class for graph data model operations.
    
    Matches the .NET GraphDataModel functionality for compatibility.
    """
    
    # Default maximum depth for complex property traversal
    DEFAULT_DEPTH_ALLOWED = 5
    
    # Relationship type naming convention for complex properties
    PROPERTY_RELATIONSHIP_TYPE_NAME_PREFIX = "__PROPERTY__"
    PROPERTY_RELATIONSHIP_TYPE_NAME_SUFFIX = "__"
    
    @staticmethod
    def property_name_to_relationship_type_name(property_name: str) -> str:
        """
        Convert a property name to a relationship type name.
        
        This follows the .NET convention: "__PROPERTY__{propertyName}__"
        """
        return f"{GraphDataModel.PROPERTY_RELATIONSHIP_TYPE_NAME_PREFIX}{property_name}{GraphDataModel.PROPERTY_RELATIONSHIP_TYPE_NAME_SUFFIX}"
    
    @staticmethod
    def relationship_type_name_to_property_name(relationship_type_name: str) -> str:
        """
        Convert a relationship type name back to a property name.
        
        Extracts the property name from "__PROPERTY__{propertyName}__"
        """
        prefix = GraphDataModel.PROPERTY_RELATIONSHIP_TYPE_NAME_PREFIX
        suffix = GraphDataModel.PROPERTY_RELATIONSHIP_TYPE_NAME_SUFFIX
        
        if (relationship_type_name.startswith(prefix) and 
            relationship_type_name.endswith(suffix)):
            return relationship_type_name[len(prefix):-len(suffix)]
        
        return relationship_type_name
    
    @staticmethod
    def is_simple_type(type_obj: Type) -> bool:
        """
        Check if a type is considered "simple" for graph storage.
        
        Simple types can be stored directly on nodes/relationships.
        """
        # Basic types
        if type_obj in (str, int, float, bool, bytes):
            return True
        
        # Datetime types
        from datetime import date, datetime, time
        if type_obj in (datetime, date, time):
            return True
        
        # Collections of simple types
        from typing import get_args, get_origin
        origin = get_origin(type_obj)
        if origin in (list, tuple, set):
            args = get_args(type_obj)
            if len(args) == 1:
                return GraphDataModel.is_simple_type(args[0])
        
        return False
    
    @staticmethod
    def is_complex_type(type_obj: Type) -> bool:
        """
        Check if a type is considered "complex" for graph storage.
        
        Complex types need to be stored as separate nodes with relationships.
        """
        # If it's not simple, it's complex
        return not GraphDataModel.is_simple_type(type_obj)
    
    @staticmethod
    def is_collection_of_simple(type_obj: Type) -> bool:
        """Check if a type is a collection of simple types."""
        from typing import get_args, get_origin
        origin = get_origin(type_obj)
        if origin in (list, tuple, set):
            args = get_args(type_obj)
            if len(args) == 1:
                return GraphDataModel.is_simple_type(args[0])
        return False
    
    @staticmethod
    def is_collection_of_complex(type_obj: Type) -> bool:
        """Check if a type is a collection of complex types."""
        from typing import get_args, get_origin
        origin = get_origin(type_obj)
        if origin in (list, tuple, set):
            args = get_args(type_obj)
            if len(args) == 1:
                return GraphDataModel.is_complex_type(args[0])
        return False
