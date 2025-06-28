"""Core interfaces and base classes for the graph model."""

from .entity import IEntity
from .exceptions import GraphException, GraphValidationException
from .graph import IGraph
from .node import INode, Node
from .relationship import IRelationship, Relationship, RelationshipDirection
from .transaction import IGraphTransaction

__all__ = [
    "IEntity",
    "INode",
    "Node", 
    "IRelationship",
    "Relationship",
    "RelationshipDirection",
    "IGraph",
    "IGraphTransaction",
    "GraphException",
    "GraphValidationException",
] 