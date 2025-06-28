"""Graph traversal functionality and utilities."""

from dataclasses import dataclass
from enum import Enum
from typing import Any, List, Optional, Protocol, Type, TypeVar

from ..core.node import INode
from ..core.relationship import IRelationship

N = TypeVar("N", bound=INode)
R = TypeVar("R", bound=IRelationship)


class TraversalDirection(Enum):
    """Defines the direction for graph traversal operations."""
    
    OUTGOING = "outgoing"
    """Traverse in the outgoing direction (follow relationship arrows)"""
    
    INCOMING = "incoming"
    """Traverse in the incoming direction (reverse relationship arrows)"""
    
    BOTH = "both"
    """Traverse in both directions (ignore relationship direction)"""


@dataclass(frozen=True)
class TraversalStep:
    """
    Represents a single step in a graph traversal operation.
    
    Defines what relationship type to follow and what target node type to find.
    """
    
    relationship_type: Type[IRelationship]
    """The type of relationship to traverse."""
    
    target_node_type: Type[INode]
    """The expected type of target nodes."""
    
    direction: TraversalDirection = TraversalDirection.OUTGOING
    """The direction to traverse the relationship."""
    
    min_depth: int = 1
    """Minimum depth for this traversal step."""
    
    max_depth: int = 1
    """Maximum depth for this traversal step."""
    
    def __post_init__(self) -> None:
        """Validate traversal step parameters."""
        if self.min_depth < 0:
            raise ValueError("Minimum depth must be non-negative")
        if self.max_depth < self.min_depth:
            raise ValueError("Maximum depth must be greater than or equal to minimum depth")


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


class IGraphTraversal(Protocol):
    """
    Protocol for graph traversal operations.
    
    Provides methods for configuring and executing complex graph traversals
    with multiple steps and depth controls.
    """
    
    def add_step(
        self,
        relationship_type: Type[R],
        target_node_type: Type[N],
        direction: TraversalDirection = TraversalDirection.OUTGOING,
        min_depth: int = 1,
        max_depth: int = 1
    ) -> "IGraphTraversal":
        """
        Add a traversal step to the path.
        
        Args:
            relationship_type: The type of relationship to traverse.
            target_node_type: The expected type of target nodes.
            direction: The direction to traverse.
            min_depth: Minimum depth for this step.
            max_depth: Maximum depth for this step.
        
        Returns:
            The traversal instance for method chaining.
        """
        ...
    
    def with_depth_limit(self, max_total_depth: int) -> "IGraphTraversal":
        """
        Set the maximum total depth for the entire traversal.
        
        Args:
            max_total_depth: The maximum total depth allowed.
        
        Returns:
            The traversal instance for method chaining.
        """
        ...
    
    def include_paths(self) -> "IGraphTraversal":
        """
        Configure the traversal to return full path information.
        
        Returns:
            The traversal instance for method chaining.
        """
        ...
    
    async def execute(self) -> List[INode]:
        """
        Execute the traversal and return the target nodes.
        
        Returns:
            A list of nodes found at the end of the traversal paths.
        """
        ...
    
    async def execute_with_paths(self) -> List[TraversalPath]:
        """
        Execute the traversal and return full path information.
        
        Returns:
            A list of complete paths through the graph.
        """
        ...


class GraphTraversal:
    """
    Base implementation of graph traversal functionality.
    
    Provides a fluent interface for building complex graph traversal queries.
    """
    
    def __init__(self, start_nodes: List[INode]) -> None:
        """
        Initialize a traversal from the given start nodes.
        
        Args:
            start_nodes: The nodes to start traversal from.
        """
        self._start_nodes = start_nodes
        self._steps: List[TraversalStep] = []
        self._max_total_depth: Optional[int] = None
        self._include_paths = False
    
    def add_step(
        self,
        relationship_type: Type[R],
        target_node_type: Type[N],
        direction: TraversalDirection = TraversalDirection.OUTGOING,
        min_depth: int = 1,
        max_depth: int = 1
    ) -> "GraphTraversal":
        """Add a traversal step."""
        step = TraversalStep(
            relationship_type=relationship_type,
            target_node_type=target_node_type,
            direction=direction,
            min_depth=min_depth,
            max_depth=max_depth
        )
        
        new_traversal = GraphTraversal(self._start_nodes)
        new_traversal._steps = self._steps + [step]
        new_traversal._max_total_depth = self._max_total_depth
        new_traversal._include_paths = self._include_paths
        return new_traversal
    
    def with_depth_limit(self, max_total_depth: int) -> "GraphTraversal":
        """Set maximum total depth."""
        if max_total_depth < 0:
            raise ValueError("Maximum depth must be non-negative")
        
        new_traversal = GraphTraversal(self._start_nodes)
        new_traversal._steps = self._steps.copy()
        new_traversal._max_total_depth = max_total_depth
        new_traversal._include_paths = self._include_paths
        return new_traversal
    
    def include_paths(self) -> "GraphTraversal":
        """Enable path tracking."""
        new_traversal = GraphTraversal(self._start_nodes)
        new_traversal._steps = self._steps.copy()
        new_traversal._max_total_depth = self._max_total_depth
        new_traversal._include_paths = True
        return new_traversal
    
    async def execute(self) -> List[INode]:
        """Execute traversal and return target nodes."""
        # This would be implemented by provider-specific subclasses
        raise NotImplementedError("Traversal execution must be implemented by provider")
    
    async def execute_with_paths(self) -> List[TraversalPath]:
        """Execute traversal and return full paths."""
        # This would be implemented by provider-specific subclasses
        raise NotImplementedError("Path traversal execution must be implemented by provider")


def create_traversal_from_nodes(nodes: List[INode]) -> GraphTraversal:
    """
    Create a new traversal starting from the given nodes.
    
    Args:
        nodes: The nodes to start traversal from.
    
    Returns:
        A new GraphTraversal instance.
    """
    return GraphTraversal(nodes)


# Export for compatibility with main package imports
GraphTraversalDirection = TraversalDirection 