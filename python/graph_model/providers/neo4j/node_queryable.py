"""
Neo4j implementation of node queryable interface.

This module provides the Neo4j-specific implementation of the node queryable
interface, translating LINQ-style operations to Cypher queries.
"""

import asyncio
from typing import Any, Callable, Dict, Generic, List, Optional, Type, TypeVar, Union

from graph_model.attributes.decorators import get_relationship_label

from ...core.entity import IEntity
from ...core.node import INode
from ...querying.queryable import IGraphNodeQueryable, IOrderedGraphNodeQueryable
from .cypher_builder import CypherBuilder

N = TypeVar("N", bound=IEntity)


class Neo4jNodeQueryable(IOrderedGraphNodeQueryable[N], Generic[N]):
    """
    Neo4j implementation of node queryable interface.
    
    Provides LINQ-style querying capabilities for nodes in Neo4j,
    translating lambda expressions to Cypher queries.
    """
    
    def __init__(self, node_type: Type[N], session: Any):
        """
        Initialize the node queryable.
        
        Args:
            node_type: Type of nodes to query.
            session: Neo4j session for executing queries.
        """
        self._node_type = node_type
        self._session = session
        self._cypher_builder = CypherBuilder(node_type)
        
        # Query state
        self._where_predicate: Optional[Callable] = None
        self._order_by_key: Optional[Callable] = None
        self._order_descending: bool = False
        self._take_limit: Optional[int] = None
        self._skip_count: Optional[int] = None
        self._select_projection: Optional[Callable] = None
        self._traversal_relationship: Optional[str] = None
        self._traversal_target_type: Optional[Type] = None
        self._traversal_depth: Optional[int] = None
    
    def where(self, predicate: Callable[[N], bool]) -> "Neo4jNodeQueryable[N]":
        """
        Filter nodes based on a predicate.
        
        Args:
            predicate: Lambda expression defining the filter condition.
        
        Returns:
            Self for method chaining.
        """
        self._where_predicate = predicate
        return self
    
    def order_by(self, key_selector: Callable[[N], Any]) -> "Neo4jNodeQueryable[N]":
        """
        Order nodes by a key selector in ascending order.
        
        Args:
            key_selector: Lambda expression defining the sort key.
        
        Returns:
            Self for method chaining.
        """
        self._order_by_key = key_selector
        self._order_descending = False
        return self
    
    def order_by_descending(self, key_selector: Callable[[N], Any]) -> "Neo4jNodeQueryable[N]":
        """
        Order nodes by a key selector in descending order.
        
        Args:
            key_selector: Lambda expression defining the sort key.
        
        Returns:
            Self for method chaining.
        """
        self._order_by_key = key_selector
        self._order_descending = True
        return self
    
    def take(self, count: int) -> "Neo4jNodeQueryable[N]":
        """
        Limit the number of results.
        
        Args:
            count: Maximum number of results to return.
        
        Returns:
            Self for method chaining.
        """
        self._take_limit = count
        return self
    
    def skip(self, count: int) -> "Neo4jNodeQueryable[N]":
        """
        Skip a number of results.
        
        Args:
            count: Number of results to skip.
        
        Returns:
            Self for method chaining.
        """
        self._skip_count = count
        return self
    
    def select(self, selector: Callable[[N], Any]) -> "Neo4jNodeQueryable[Any]":
        """
        Project results using a selector function.
        
        Args:
            selector: Lambda expression defining the projection.
        
        Returns:
            A new queryable with projected results.
        """
        self._select_projection = selector
        return self
    
    def traverse(self, relationship_type, target_type: Type) -> "Neo4jNodeQueryable[N]":
        """
        Traverse relationships to target nodes.
        
        Args:
            relationship_type: Type of relationship to traverse (class or string).
            target_type: Type of target nodes.
        
        Returns:
            Self for method chaining.
        """
        # Accept either a class or a string for relationship_type
        if isinstance(relationship_type, str):
            rel_label = relationship_type
        else:
            rel_label = get_relationship_label(relationship_type)
        self._traversal_relationship = rel_label
        self._traversal_target_type = target_type
        return self
    
    def with_depth(self, depth: int) -> "Neo4jNodeQueryable[N]":
        """
        Set traversal depth for relationship traversal.
        
        Args:
            depth: Maximum traversal depth.
        
        Returns:
            Self for method chaining.
        """
        self._traversal_depth = depth
        return self
    
    async def to_list(self) -> List[N]:
        """
        Execute the query and return results as a list.
        
        Returns:
            List of nodes matching the query criteria.
        """
        # Build Cypher query using CypherBuilder
        cypher_query = self._cypher_builder.build_query(
            where_predicate=self._where_predicate,
            order_by_key=self._order_by_key,
            order_descending=self._order_descending,
            take_count=self._take_limit,
            skip_count=self._skip_count,
            include_complex_properties=True,
            traversal_relationship=self._traversal_relationship,
            traversal_target_type=self._traversal_target_type,
            select_projection=self._select_projection
        )
        
        # Execute query
        result = await self._session.run(cypher_query.query, cypher_query.parameters)
        data = await result.data()
        
        # Convert results to node objects
        nodes = []
        for record in data:
            if "n" in record:
                node = self._node_type(**record["n"])
            else:
                node = self._node_type(**record)
            nodes.append(node)
        
        # Apply projection if select() was used
        if self._select_projection:
            return [self._select_projection(node) for node in nodes]
        return nodes
    
    async def first(self) -> N:
        """
        Get the first result or raise an exception if none found.
        
        Returns:
            The first node matching the criteria.
        
        Raises:
            Exception: If no results are found.
        """
        results = await self.take(1).to_list()
        if not results:
            raise Exception("No results found")
        return results[0]
    
    async def first_or_none(self) -> Optional[N]:
        """
        Get the first result or None if none found.
        
        Returns:
            The first node matching the criteria, or None.
        """
        try:
            return await self.first()
        except Exception:
            return None 