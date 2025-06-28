"""
Graph traversal functionality with PathSegments support.

This module provides the foundational PathSegments method that matches the .NET implementation,
along with other traversal operations built on top of it.
"""

from abc import ABC, abstractmethod
from dataclasses import dataclass
from enum import Enum
from typing import Any, Dict, Generic, List, Optional, Protocol, Type, TypeVar, Union

from ..core.node import INode
from ..core.relationship import IRelationship

TStartNode = TypeVar('TStartNode', bound=INode)
TRelationship = TypeVar('TRelationship', bound=IRelationship)
TEndNode = TypeVar('TEndNode', bound=INode)


class GraphTraversalDirection(Enum):
    """
    Represents the direction of graph traversal.
    
    Matches the .NET GraphTraversalDirection enum.
    """
    
    OUTGOING = "outgoing"
    """Follow relationships in the outgoing direction."""
    
    INCOMING = "incoming"
    """Follow relationships in the incoming direction."""
    
    BOTH = "both"
    """Follow relationships in both directions."""


class IGraphPathSegment(Protocol):
    """
    Base interface for graph path segments.
    
    Matches the .NET IGraphPathSegment interface.
    """
    
    @property
    def start_node(self) -> INode:
        """Gets the starting node of the path segment."""
        ...
    
    @property
    def end_node(self) -> INode:
        """Gets the ending node of the path segment."""
        ...
    
    @property
    def relationship(self) -> IRelationship:
        """Gets the relationship connecting the start and end nodes."""
        ...


class IGraphPathSegmentTyped(Protocol, Generic[TStartNode, TRelationship, TEndNode]):
    """
    Strongly-typed interface for graph path segments.
    
    Matches the .NET IGraphPathSegment<TSource, TRel, TTarget> interface.
    """
    
    @property
    def start_node(self) -> TStartNode:
        """Gets the strongly-typed starting node of the path segment."""
        ...
    
    @property
    def end_node(self) -> TEndNode:
        """Gets the strongly-typed ending node of the path segment."""
        ...
    
    @property
    def relationship(self) -> TRelationship:
        """Gets the strongly-typed relationship connecting the nodes."""
        ...


@dataclass(frozen=True)
class GraphPathSegment(Generic[TStartNode, TRelationship, TEndNode]):
    """
    Concrete implementation of a graph path segment.
    
    Matches the .NET GraphPathSegment<TSource, TRel, TTarget> record.
    """
    
    start_node: TStartNode
    """The starting node of the path segment."""
    
    relationship: TRelationship
    """The relationship connecting the start and end nodes."""
    
    end_node: TEndNode
    """The ending node of the path segment."""


@dataclass(frozen=True)
class TraversalPath:
    """
    Represents a complete path through the graph discovered during traversal.
    
    Contains the sequence of nodes and relationships that form the path.
    """
    
    nodes: List[INode]
    """The sequence of nodes in the path."""
    
    relationships: List[IRelationship]
    """The sequence of relationships connecting the nodes."""
    
    def __post_init__(self) -> None:
        """Validate path structure."""
        if len(self.nodes) == 0:
            raise ValueError("Path must contain at least one node")
        if len(self.relationships) != len(self.nodes) - 1:
            raise ValueError("Path must have exactly one fewer relationship than nodes")
    
    @property
    def length(self) -> int:
        """Get the length of the path (number of relationships)."""
        return len(self.relationships)
    
    def get_path_segments(self) -> List[GraphPathSegment[INode, IRelationship, INode]]:
        """
        Convert this path to a list of path segments.
        
        Returns:
            List of GraphPathSegment objects representing each step in the path.
        """
        segments = []
        for i in range(len(self.relationships)):
            segment = GraphPathSegment(
                start_node=self.nodes[i],
                relationship=self.relationships[i],
                end_node=self.nodes[i + 1]
            )
            segments.append(segment)
        return segments


class IGraphTraversal(Protocol):
    """
    Interface for graph traversal operations.
    
    Provides methods for configuring and executing graph traversals.
    """
    
    def with_direction(self, direction: GraphTraversalDirection) -> "IGraphTraversal":
        """Set the traversal direction."""
        ...
    
    def with_depth(self, min_depth: int, max_depth: Optional[int] = None) -> "IGraphTraversal":
        """Set the traversal depth constraints."""
        ...
    
    def where(self, predicate: str) -> "IGraphTraversal":
        """Add a WHERE clause to filter traversal results."""
        ...
    
    async def to_path_segments(self) -> List[GraphPathSegment[INode, IRelationship, INode]]:
        """Execute traversal and return path segments."""
        ...
    
    async def to_nodes(self) -> List[INode]:
        """Execute traversal and return target nodes."""
        ...
    
    async def to_relationships(self) -> List[IRelationship]:
        """Execute traversal and return relationships."""
        ...
    
    async def to_paths(self) -> List[TraversalPath]:
        """Execute traversal and return complete paths."""
        ...


class GraphTraversal:
    """
    Concrete implementation of graph traversal.
    
    This class provides the foundational PathSegments functionality that matches
    the .NET GraphTraversalExtensions.PathSegments method.
    """
    
    def __init__(
        self,
        start_nodes: List[INode],
        relationship_type: Optional[Type[IRelationship]] = None,
        target_node_type: Optional[Type[INode]] = None
    ):
        self._start_nodes = start_nodes
        self._relationship_type = relationship_type
        self._target_node_type = target_node_type
        self._direction = GraphTraversalDirection.OUTGOING
        self._min_depth = 1
        self._max_depth = 1
        self._where_clauses: List[str] = []
        self._include_paths = False
    
    def with_direction(self, direction: GraphTraversalDirection) -> "GraphTraversal":
        """
        Set the traversal direction.
        
        Args:
            direction: The direction to traverse relationships.
            
        Returns:
            New GraphTraversal instance with the specified direction.
        """
        new_traversal = GraphTraversal(
            self._start_nodes, 
            self._relationship_type, 
            self._target_node_type
        )
        new_traversal._direction = direction
        new_traversal._min_depth = self._min_depth
        new_traversal._max_depth = self._max_depth
        new_traversal._where_clauses = self._where_clauses.copy()
        new_traversal._include_paths = self._include_paths
        return new_traversal
    
    def with_depth(self, min_depth: int, max_depth: Optional[int] = None) -> "GraphTraversal":
        """
        Set the traversal depth constraints.
        
        Args:
            min_depth: Minimum depth to traverse.
            max_depth: Maximum depth to traverse (defaults to min_depth).
            
        Returns:
            New GraphTraversal instance with the specified depth constraints.
        """
        new_traversal = GraphTraversal(
            self._start_nodes, 
            self._relationship_type, 
            self._target_node_type
        )
        new_traversal._direction = self._direction
        new_traversal._min_depth = min_depth
        new_traversal._max_depth = max_depth if max_depth is not None else min_depth
        new_traversal._where_clauses = self._where_clauses.copy()
        new_traversal._include_paths = self._include_paths
        return new_traversal
    
    def where(self, predicate: str) -> "GraphTraversal":
        """
        Add a WHERE clause to filter traversal results.
        
        Args:
            predicate: Cypher predicate expression.
            
        Returns:
            New GraphTraversal instance with the added WHERE clause.
        """
        new_traversal = GraphTraversal(
            self._start_nodes, 
            self._relationship_type, 
            self._target_node_type
        )
        new_traversal._direction = self._direction
        new_traversal._min_depth = self._min_depth
        new_traversal._max_depth = self._max_depth
        new_traversal._where_clauses = self._where_clauses + [predicate]
        new_traversal._include_paths = self._include_paths
        return new_traversal
    
    def include_paths(self) -> "GraphTraversal":
        """Enable path tracking."""
        new_traversal = GraphTraversal(
            self._start_nodes, 
            self._relationship_type, 
            self._target_node_type
        )
        new_traversal._direction = self._direction
        new_traversal._min_depth = self._min_depth
        new_traversal._max_depth = self._max_depth
        new_traversal._where_clauses = self._where_clauses.copy()
        new_traversal._include_paths = True
        return new_traversal
    
    async def to_path_segments(self) -> List[GraphPathSegment[INode, IRelationship, INode]]:
        """
        Execute traversal and return path segments.
        
        This is the foundational method that matches .NET's PathSegments functionality.
        All other traversal methods are built on top of this.
        
        Returns:
            List of GraphPathSegment objects representing each traversal step.
        """
        # This would be implemented by provider-specific subclasses
        raise NotImplementedError("PathSegments execution must be implemented by provider")
    
    async def to_nodes(self) -> List[INode]:
        """
        Execute traversal and return target nodes.
        
        This is equivalent to .NET's Traverse<TStartNode, TRelationship, TEndNode>()
        which internally calls PathSegments().Select(ps => ps.EndNode).
        
        Returns:
            List of target nodes reached through traversal.
        """
        path_segments = await self.to_path_segments()
        return [segment.end_node for segment in path_segments]
    
    async def to_relationships(self) -> List[IRelationship]:
        """
        Execute traversal and return relationships.
        
        This is equivalent to .NET's TraverseRelationships<TStartNode, TRelationship, TEndNode>()
        which internally calls PathSegments().Select(ps => ps.Relationship).
        
        Returns:
            List of relationships traversed.
        """
        path_segments = await self.to_path_segments()
        return [segment.relationship for segment in path_segments]
    
    async def to_paths(self) -> List[TraversalPath]:
        """
        Execute traversal and return complete paths.
        
        Returns:
            List of TraversalPath objects representing complete paths from start to end.
        """
        # This would be implemented by provider-specific subclasses
        raise NotImplementedError("Path traversal execution must be implemented by provider")
    
    def build_cypher_pattern(self) -> str:
        """
        Build the Cypher pattern for this traversal.
        
        Returns:
            Cypher pattern string that can be used in MATCH clauses.
        """
        # Direction arrow
        direction_pattern = {
            GraphTraversalDirection.OUTGOING: "->",
            GraphTraversalDirection.INCOMING: "<-",
            GraphTraversalDirection.BOTH: "-"
        }[self._direction]
        
        # Relationship type
        rel_type = ""
        if self._relationship_type:
            # Get the relationship type from metadata
            metadata = getattr(self._relationship_type, '__graph_relationship_metadata__', None)
            if metadata:
                rel_type = f":{metadata['label']}"
            else:
                rel_type = f":{self._relationship_type.__name__}"
        
        # Depth pattern
        depth_pattern = ""
        if self._min_depth == self._max_depth:
            if self._min_depth != 1:
                depth_pattern = f"*{self._min_depth}"
        else:
            depth_pattern = f"*{self._min_depth}..{self._max_depth}"
        
        # Build the pattern
        if self._direction == GraphTraversalDirection.INCOMING:
            pattern = f"<-[r{rel_type}{depth_pattern}]-"
        elif self._direction == GraphTraversalDirection.BOTH:
            pattern = f"-[r{rel_type}{depth_pattern}]-"
        else:  # OUTGOING
            pattern = f"-[r{rel_type}{depth_pattern}]->"
        
        return pattern


def path_segments(
    start_nodes: List[TStartNode],
    relationship_type: Type[TRelationship],
    target_node_type: Type[TEndNode]
) -> GraphTraversal:
    """
    Create a graph traversal that returns path segments.
    
    This is the foundational PathSegments method that matches the .NET implementation.
    All other traversal operations are built on top of this method.
    
    Args:
        start_nodes: The starting nodes for traversal.
        relationship_type: The type of relationships to traverse.
        target_node_type: The type of target nodes to reach.
        
    Returns:
        GraphTraversal configured for path segment traversal.
    """
    return GraphTraversal(start_nodes, relationship_type, target_node_type)


def traverse(
    start_nodes: List[TStartNode],
    relationship_type: Type[TRelationship],
    target_node_type: Type[TEndNode]
) -> GraphTraversal:
    """
    Create a graph traversal that returns target nodes.
    
    This is a convenience method built on top of PathSegments, matching the .NET
    Traverse<TStartNode, TRelationship, TEndNode>() method.
    
    Args:
        start_nodes: The starting nodes for traversal.
        relationship_type: The type of relationships to traverse.
        target_node_type: The type of target nodes to reach.
        
    Returns:
        GraphTraversal configured for node traversal.
    """
    return path_segments(start_nodes, relationship_type, target_node_type)


def traverse_relationships(
    start_nodes: List[TStartNode],
    relationship_type: Type[TRelationship],
    target_node_type: Type[TEndNode]
) -> GraphTraversal:
    """
    Create a graph traversal that returns relationships.
    
    This is a convenience method built on top of PathSegments, matching the .NET
    TraverseRelationships<TStartNode, TRelationship, TEndNode>() method.
    
    Args:
        start_nodes: The starting nodes for traversal.
        relationship_type: The type of relationships to traverse.
        target_node_type: The type of target nodes to reach.
        
    Returns:
        GraphTraversal configured for relationship traversal.
    """
    return path_segments(start_nodes, relationship_type, target_node_type) 