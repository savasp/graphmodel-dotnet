"""
Core graph interface and implementation.

This module defines the core interfaces and base classes for the graph model.
"""

import datetime
import decimal
import uuid
from abc import ABC, abstractmethod
from typing import (
    Any,
    Dict,
    Generic,
    List,
    Optional,
    Set,
    Tuple,
    Type,
    TypeVar,
    Union,
    get_args,
    get_origin,
)

from .entity import IEntity
from .node import INode
from .relationship import IRelationship
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

    # Relationship type naming convention for complex properties - EXACT .NET compatibility
    PROPERTY_RELATIONSHIP_TYPE_NAME_PREFIX = "__PROPERTY__"
    PROPERTY_RELATIONSHIP_TYPE_NAME_SUFFIX = "__"

    @staticmethod
    def property_name_to_relationship_type_name(property_name: str) -> str:
        """
        Convert a property name to a relationship type name.

        This follows the EXACT .NET convention: "__PROPERTY__{propertyName}__"
        Format matches: GraphDataModel.PropertyNameToRelationshipTypeName() in .NET
        """
        return f"{GraphDataModel.PROPERTY_RELATIONSHIP_TYPE_NAME_PREFIX}{property_name}{GraphDataModel.PROPERTY_RELATIONSHIP_TYPE_NAME_SUFFIX}"

    @staticmethod
    def relationship_type_name_to_property_name(relationship_type_name: str) -> str:
        """
        Convert a relationship type name back to a property name.

        Extracts the property name from "__PROPERTY__{propertyName}__"
        Matches .NET implementation: RelationshipTypeNameToPropertyName()
        """
        prefix = GraphDataModel.PROPERTY_RELATIONSHIP_TYPE_NAME_PREFIX
        suffix = GraphDataModel.PROPERTY_RELATIONSHIP_TYPE_NAME_SUFFIX

        if (relationship_type_name.startswith(prefix) and
            relationship_type_name.endswith(suffix)):
            # .NET uses relationshipTypeName[12..^2] which is substring from index 12 to end-2
            # This is equivalent to removing prefix and suffix
            return relationship_type_name[len(prefix):-len(suffix)]

        return relationship_type_name

    @staticmethod
    def is_valid_relationship_type_name(relationship_type_name: str) -> bool:
        """
        Check if a relationship type name is valid.

        Matches .NET GraphDataModel.IsValidRelationshipTypeName() validation rules.
        Valid names must:
        - Not be empty
        - Start with a letter or underscore
        - Start with uppercase letter or underscore (not lowercase)
        - Contain only letters, digits, and underscores
        - Not contain spaces, hyphens, or other special characters
        - Allow underscores in the middle for regular names like 'VALID_NAME'
        - Allow underscores in the middle for .NET property names (__PROPERTY__...)
        """
        if not relationship_type_name:
            return False

        # Must start with uppercase letter or underscore
        if not (relationship_type_name[0].isupper() or relationship_type_name[0] == '_'):
            return False

        # Must contain only letters, digits, and underscores
        if not all(c.isalnum() or c == '_' for c in relationship_type_name):
            return False

        # Check for invalid characters
        invalid_chars = [' ', '-', '.', ',', ';', ':', '!', '@', '#', '$', '%', '^', '&', '*', '(', ')', '+', '=', '[', ']', '{', '}', '|', '\\', '/', '<', '>', '?', '"', "'"]
        if any(c in invalid_chars for c in relationship_type_name):
            return False

        return True

    @staticmethod
    def is_simple_type(type_hint: Type) -> bool:
        """
        Check if a type is considered "simple" for graph storage.

        Matches .NET GraphDataModel.IsSimple() logic for compatibility.
        Simple types can be stored directly as Neo4j properties.
        """
        # Handle None type
        if type_hint is type(None):
            return True

        # Handle Union types (including Optional)
        origin = get_origin(type_hint)
        if origin is Union:
            args = get_args(type_hint)
            # For Optional[T] (Union[T, None]), check if T is simple
            non_none_args = [arg for arg in args if arg is not type(None)]
            if len(non_none_args) == 1:
                return GraphDataModel.is_simple_type(non_none_args[0])
            # For other unions, consider complex
            return False

        # Basic simple types - matches .NET simple types
        simple_types = {
            str, int, float, bool, bytes,
            datetime.datetime, datetime.date, datetime.time,
            uuid.UUID, decimal.Decimal
        }

        if type_hint in simple_types:
            return True

        # Handle enums
        if hasattr(type_hint, '__bases__') and any(base.__name__ == 'Enum' for base in type_hint.__bases__):
            return True

        return False

    @staticmethod
    def is_collection_of_simple(type_hint: Type) -> bool:
        """
        Check if a type is a collection of simple types.

        Matches .NET GraphDataModel.IsCollectionOfSimple() for compatibility.
        """
        origin = get_origin(type_hint)
        if origin in (list, List, set, Set, tuple, Tuple):
            args = get_args(type_hint)
            if args:
                return GraphDataModel.is_simple_type(args[0])
        return False

    @staticmethod
    def is_complex_type(type_hint: Type, depth: int = DEFAULT_DEPTH_ALLOWED) -> bool:
        """
        Check if a type is considered "complex" for graph storage.

        Matches .NET GraphDataModel.IsComplex() logic with depth checking.
        Complex types need to be stored as separate nodes with relationships.
        """
        if depth <= 0:
            return False

        if GraphDataModel.is_simple_type(type_hint) or GraphDataModel.is_collection_of_simple(type_hint):
            return False

        # Handle Optional types
        origin = get_origin(type_hint)
        if origin is Union:
            args = get_args(type_hint)
            non_none_args = [arg for arg in args if arg is not type(None)]
            if len(non_none_args) == 1:
                return GraphDataModel.is_complex_type(non_none_args[0], depth - 1)

        # Check if it's a Pydantic model or has complex properties
        if hasattr(type_hint, 'model_fields'):
            # Pydantic model - check its fields
            for field_name, field_info in type_hint.model_fields.items():
                field_type = field_info.annotation
                if not GraphDataModel.is_simple_type(field_type) and not GraphDataModel.is_collection_of_simple(field_type):
                    if GraphDataModel.is_complex_type(field_type, depth - 1):
                        return True

        return False

    @staticmethod
    def is_collection_of_complex(type_hint: Type) -> bool:
        """
        Check if a type is a collection of complex types.

        Matches .NET GraphDataModel.IsCollectionOfComplex() for compatibility.
        """
        origin = get_origin(type_hint)
        if origin in (list, List, set, Set, tuple, Tuple):
            args = get_args(type_hint)
            if args:
                return GraphDataModel.is_complex_type(args[0])
        return False

    @staticmethod
    def get_simple_properties(obj: Any) -> Dict[str, Any]:
        """
        Get the simple properties of an object.

        Matches .NET GraphDataModel.GetSimpleProperties() functionality.
        """
        cls = type(obj)
        if not hasattr(cls, 'model_fields'):
            return {}

        simple_props = {}
        for field_name, field_info in cls.model_fields.items():
            value = getattr(obj, field_name, None)
            if value is None:
                continue
            field_type = field_info.annotation

            # Check if it's a simple type, collection of simple types, or embedded field
            if (GraphDataModel.is_simple_type(field_type) or
                GraphDataModel.is_collection_of_simple(field_type) or
                GraphDataModel._is_embedded_field(field_info)):
                # If simple, store the value, not the field_info
                simple_props[field_name] = value

        return simple_props

    @staticmethod
    def _is_embedded_field(field_info) -> bool:
        """Check if a field is an embedded field based on its metadata."""
        if hasattr(field_info, 'json_schema_extra') and field_info.json_schema_extra:
            graph_field_info = field_info.json_schema_extra.get('graph_field_info')
            if graph_field_info:
                return graph_field_info.field_type == "embedded"
        return False

    @staticmethod
    def get_complex_properties(obj: Any) -> Dict[str, Any]:
        """
        Get the complex properties of an object.

        Matches .NET GraphDataModel.GetComplexProperties() functionality.
        """
        cls = type(obj)
        if not hasattr(cls, 'model_fields'):
            return {}

        complex_props = {}
        for field_name in cls.model_fields:
            field_info = obj.__dict__.get(field_name, None)
            if field_info is None:
                continue

            field_type = field_info.annotation

            # Check if it's a complex type or related_node field
            if (GraphDataModel.is_complex_type(field_type) or
                GraphDataModel._is_related_node_field(field_info)):
                complex_props[field_name] = field_info

        return complex_props

    @staticmethod
    def _is_related_node_field(field_info) -> bool:
        """Check if a field is a related node field based on its metadata."""
        if hasattr(field_info, 'json_schema_extra') and field_info.json_schema_extra:
            graph_field_info = field_info.json_schema_extra.get('graph_field_info')
            if graph_field_info:
                return graph_field_info.field_type == "related_node"
        return False

    @staticmethod
    def get_simple_and_complex_properties(obj: Any) -> Tuple[Dict[str, Any], Dict[str, Any]]:
        """
        Get both simple and complex properties of an object.

        Matches .NET GraphDataModel.GetSimpleAndComplexProperties() functionality.
        """
        # Use the class to access model_fields
        cls = type(obj)
        if not hasattr(cls, 'model_fields'):
            return {}, {}

        simple_props = {}
        complex_props = {}
        for field_name, field_info in cls.model_fields.items():
            value = getattr(obj, field_name, None)
            if value is None:
                continue

            field_type = field_info.annotation

            # Check if it's a simple type, collection of simple types, or embedded field
            if (GraphDataModel.is_simple_type(field_type) or
                GraphDataModel.is_collection_of_simple(field_type) or
                GraphDataModel._is_embedded_field(field_info)):
                simple_props[field_name] = value
            # Check if it's a complex type or related_node field
            elif (GraphDataModel.is_complex_type(field_type) or
                  GraphDataModel._is_related_node_field(field_info)):
                complex_props[field_name] = value

        return simple_props, complex_props
