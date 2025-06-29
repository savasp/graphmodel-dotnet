"""
Field type definitions for graph model attributes.

This module provides field types that can be used with Pydantic models
to define how properties should be handled in the graph database.
"""

from dataclasses import dataclass
from typing import Any, Callable, Optional, Type, get_args, get_origin

from pydantic import Field

from ..core.graph import GraphDataModel


class PropertyFieldType:
    """Enumeration of property field types."""
    
    SIMPLE = "simple"
    """Simple property stored directly on the node/relationship."""
    
    EMBEDDED = "embedded"
    """Complex property stored as embedded JSON."""
    
    RELATED_NODE = "related_node"
    """Complex property stored as a separate node with a relationship."""


@dataclass(frozen=True)
class PropertyFieldInfo:
    """
    Metadata about a property field in a graph entity.
    
    Contains information about how the property should be handled,
    stored, and queried in the graph database.
    """
    
    field_type: PropertyFieldType
    """The type of field (simple, embedded, or related node)."""
    
    label: Optional[str] = None
    """Custom property name in graph storage. If None, uses the field name."""
    
    index: bool = False
    """Whether this property should be indexed for performance."""
    
    ignore: bool = False
    """Whether to exclude this property from graph persistence."""
    
    relationship_type: Optional[str] = None
    """For related node fields, the relationship type to use."""
    
    private_relationship: bool = True
    """For related node fields, whether the relationship should be private."""
    
    required: bool = True
    """Whether this property is required."""
    
    default: Any = ...
    """Default value for the property."""
    
    default_factory: Optional[Callable[[], Any]] = None
    """Factory function for default values."""
    
    storage_type: Optional[str] = None
    """For embedded fields, the storage strategy to use."""


def property_field(
    label: Optional[str] = None,
    *,
    index: bool = False,
    required: bool = True,
    default: Any = ...,
    default_factory: Optional[Callable[[], Any]] = None,
    **kwargs: Any
) -> Any:
    """
    Create a simple property field that stores primitive values directly on the node.
    
    Simple properties are stored as native graph database properties and can be
    indexed and queried efficiently.
    
    Args:
        label: Custom property name in graph storage.
        index: Whether to index this property for performance.
        required: Whether this property is required.
        default: Default value for the property.
        default_factory: Factory function for default values.
        **kwargs: Additional arguments passed to Pydantic Field.
    
    Returns:
        A Pydantic Field configured for simple property storage.
    
    Example:
        ```python
        @node(label="Person")
        class Person(Node):
            name: str = property_field(index=True)
            age: int = property_field(default=0)
            email: str = property_field(label="email_address")
        ```
    """
    field_info = PropertyFieldInfo(
        field_type=PropertyFieldType.SIMPLE,
        label=label,
        index=index,
        required=required,
        default=default,
        default_factory=default_factory
    )
    
    return Field(
        default=default,
        default_factory=default_factory,
        **kwargs,
        json_schema_extra={"graph_field_info": field_info}
    )


def embedded_field(
    label: Optional[str] = None,
    *,
    index: bool = False,
    required: bool = True,
    default: Any = ...,
    default_factory: Optional[Callable[[], Any]] = None,
    storage: str = "json",  # "json", "flattened", "map", "array"
    **kwargs: Any
) -> Any:
    """
    Create an embedded field that serializes complex objects as JSON on the node.
    
    Embedded fields store complex objects (like dataclasses, lists, dicts) as
    serialized JSON directly on the node. This is efficient for small objects
    that don't need to be queried independently.
    
    Args:
        label: Custom property name in graph storage.
        index: Whether to index this property (limited indexing capabilities).
        required: Whether this property is required.
        default: Default value for the property.
        default_factory: Factory function for default values.
        storage: Storage strategy ("json", "flattened", "map", "array").
        **kwargs: Additional arguments passed to Pydantic Field.
    
    Returns:
        A Pydantic Field configured for embedded storage.
    
    Example:
        ```python
        @dataclass
        class Address:
            street: str
            city: str
            country: str
        
        @node(label="Person")
        class Person(Node):
            contact_info: Address = embedded_field(storage="json")
            tags: List[str] = embedded_field(default_factory=list)
        ```
    """
    field_info = PropertyFieldInfo(
        field_type=PropertyFieldType.EMBEDDED,
        label=label,
        index=index,
        required=required,
        default=default,
        default_factory=default_factory,
        storage_type=storage
    )
    
    return Field(
        default=default,
        default_factory=default_factory,
        **{k: v for k, v in kwargs.items() if k not in ("storage_type", "private_relationship")},
        json_schema_extra={"graph_field_info": field_info, "storage_type": storage}
    )


def related_node_field(
    relationship_type: Optional[str] = None,
    *,
    private: bool = True,
    required: bool = True,
    default: Any = ...,
    default_factory: Optional[Callable[[], Any]] = None,
    **kwargs: Any
) -> Any:
    """
    Create a related node field that stores complex objects as separate nodes.
    
    Related node fields create separate nodes for complex objects and link them
    via relationships. This provides full querying capabilities and indexing.
    
    Args:
        relationship_type: Custom relationship type. If None, uses .NET convention.
        private: Whether the relationship should be private (not traversable).
        required: Whether this property is required.
        default: Default value for the property.
        default_factory: Factory function for default values.
        **kwargs: Additional arguments passed to Pydantic Field.
    
    Returns:
        A Pydantic Field configured for related node storage.
    
    Example:
        ```python
        @dataclass
        class Address:
            street: str
            city: str
            country: str
        
        @node(label="Person")
        class Person(Node):
            home_address: Address = related_node_field(private=True)
            work_address: Address = related_node_field(
                relationship_type="WORKS_AT",
                private=False
            )
        ```
    """
    field_info = PropertyFieldInfo(
        field_type=PropertyFieldType.RELATED_NODE,
        relationship_type=relationship_type,
        private_relationship=private,
        required=required,
        default=default,
        default_factory=default_factory
    )
    
    return Field(
        default=default,
        default_factory=default_factory,
        **{k: v for k, v in kwargs.items() if k not in ("storage_type", "private_relationship")},
        json_schema_extra={"graph_field_info": field_info, "private_relationship": private}
    )


def auto_field(
    label: Optional[str] = None,
    *,
    index: bool = False,
    required: bool = True,
    default: Any = ...,
    default_factory: Optional[Callable[[], Any]] = None,
    prefer_embedded: bool = False,
    **kwargs: Any
) -> Any:
    """
    Automatically determine the field type based on the annotation.
    
    This is a convenience function that automatically chooses between
    property_field, embedded_field, and related_node_field based on
    the type annotation. It follows these rules:
    
    1. Simple types (str, int, float, bool, datetime, etc.) → property_field
    2. Collections of simple types → property_field  
    3. Complex types → related_node_field (or embedded_field if prefer_embedded=True)
    4. Collections of complex types → related_node_field
    
    Args:
        label: Custom property name in graph storage.
        index: Whether to index this property.
        required: Whether this property is required.
        default: Default value for the property.
        default_factory: Factory function for default values.
        prefer_embedded: Whether to prefer embedded storage for complex types.
        **kwargs: Additional arguments passed to the appropriate field function.
    
    Returns:
        A Pydantic Field with automatically determined storage strategy.
    
    Example:
        ```python
        @node(label="Person")
        class Person(Node):
            # Simple types - automatically use property_field
            name: str = auto_field(index=True)
            age: int = auto_field(default=0)
            
            # Complex types - automatically use related_node_field
            address: Address = auto_field()
            
            # Collections - automatically use related_node_field
            friends: List[Person] = auto_field()
            
            # Force embedded storage for complex types
            metadata: Dict[str, Any] = auto_field(prefer_embedded=True)
        ```
    """
    # Note: This function can't actually inspect the type annotation at runtime
    # because it's called before the class is fully defined. The actual type
    # detection would happen during model validation or schema generation.
    
    # For now, we'll create a special field info that indicates auto-detection
    field_info = PropertyFieldInfo(
        field_type=PropertyFieldType.SIMPLE,  # Will be overridden during processing
        label=label,
        index=index,
        required=required,
        default=default,
        default_factory=default_factory,
        storage_type="auto" if prefer_embedded else None
    )
    
    return Field(
        default=default,
        default_factory=default_factory,
        **{k: v for k, v in kwargs.items() if k not in ("storage_type", "private_relationship")},
        json_schema_extra={"graph_field_info": field_info, "auto_detect": True, "storage_type": ("auto" if prefer_embedded else None)}
    )


def get_field_info(field: Any) -> Optional[PropertyFieldInfo]:
    """
    Extract PropertyFieldInfo from a Pydantic field.
    
    Args:
        field: A Pydantic Field instance.
    
    Returns:
        PropertyFieldInfo if the field has graph metadata, None otherwise.
    """
    if hasattr(field, 'json_schema_extra') and field.json_schema_extra:
        return field.json_schema_extra.get("graph_field_info")
    return None


def determine_field_type_from_annotation(annotation: Type) -> PropertyFieldType:
    """
    Automatically determine the appropriate field type from a type annotation.
    
    Args:
        annotation: The type annotation to analyze.
    
    Returns:
        The appropriate PropertyFieldType for the annotation.
    """
    # Check if it's a simple type
    if GraphDataModel.is_simple_type(annotation):
        return PropertyFieldType.SIMPLE
    
    # Check if it's a collection of simple types
    if GraphDataModel.is_collection_of_simple(annotation):
        return PropertyFieldType.SIMPLE
    
    # Check if it's a collection of complex types
    if GraphDataModel.is_collection_of_complex(annotation):
        return PropertyFieldType.RELATED_NODE
    
    # Default to related node for complex types
    return PropertyFieldType.RELATED_NODE


def get_relationship_type_for_field(field_name: str, custom_type: Optional[str] = None) -> str:
    """
    Get the relationship type for a field, using .NET convention if no custom type provided.
    
    Args:
        field_name: The name of the field.
        custom_type: Custom relationship type if provided.
    
    Returns:
        The relationship type to use.
    """
    if custom_type:
        return custom_type
    
    # Use .NET convention: "__PROPERTY__{fieldName}__"
    return GraphDataModel.property_name_to_relationship_type_name(field_name)