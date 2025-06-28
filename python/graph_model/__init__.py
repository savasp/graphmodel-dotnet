"""
Python Graph Model Library

A modern Python library for working with graph databases, providing type-safe
operations, LINQ-style querying, and seamless integration with Neo4j.

This library is designed to be compatible with the .NET GraphModel library,
allowing Python and .NET applications to read and write the same graph data.
"""

# Core interfaces and base classes
# Decorators for entity configuration
from .attributes.decorators import node, relationship

# Field types for property configuration
from .attributes.fields import (
    PropertyFieldInfo,
    PropertyFieldType,
    auto_field,
    determine_field_type_from_annotation,
    embedded_field,
    get_field_info,
    get_relationship_type_for_field,
    property_field,
    related_node_field,
)
from .core.entity import IEntity
from .core.exceptions import (
    GraphConnectionException,
    GraphException,
    GraphQueryException,
    GraphTransactionException,
    GraphValidationException,
)
from .core.graph import GraphDataModel, IGraph
from .core.node import INode, Node
from .core.relationship import IRelationship, Relationship, RelationshipDirection
from .core.transaction import IGraphTransaction

# Querying interfaces
from .querying.queryable import (
    IGraphNodeQueryable,
    IGraphRelationshipQueryable,
    IOrderedGraphNodeQueryable,
    IOrderedGraphRelationshipQueryable,
)

# Graph traversal
from .querying.traversal import GraphTraversalDirection

# Version information
__version__ = "0.1.0"
__author__ = "Graph Model Team"
__description__ = "A modern Python library for graph databases with .NET compatibility"

# Convenience exports for common use cases
__all__ = [
    # Core interfaces
    "IEntity",
    "INode", 
    "Node",
    "IRelationship",
    "Relationship", 
    "RelationshipDirection",
    "IGraph",
    "IGraphTransaction",
    "GraphDataModel",
    
    # Exceptions
    "GraphException",
    "GraphValidationException", 
    "GraphConnectionException",
    "GraphQueryException",
    "GraphTransactionException",
    
    # Decorators
    "node",
    "relationship",
    
    # Field types
    "property_field",
    "embedded_field", 
    "related_node_field",
    "auto_field",
    "PropertyFieldType",
    "PropertyFieldInfo",
    "get_field_info",
    "determine_field_type_from_annotation",
    "get_relationship_type_for_field",
    
    # Querying
    "IGraphNodeQueryable",
    "IOrderedGraphNodeQueryable", 
    "IGraphRelationshipQueryable",
    "IOrderedGraphRelationshipQueryable",
    "GraphTraversalDirection",
] 