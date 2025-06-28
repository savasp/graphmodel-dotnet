"""
Neo4j implementation of relationship queryable interface.

This module provides the Neo4j-specific implementation of the relationship queryable
interface, translating LINQ-style operations to Cypher queries.
"""

import asyncio
from typing import Any, Callable, Dict, Generic, List, Optional, Type, TypeVar

from ...core.entity import IEntity
from ...core.relationship import IRelationship
from ...querying.queryable import (
    IGraphRelationshipQueryable,
    IOrderedGraphRelationshipQueryable,
)
from .cypher_builder import RelationshipCypherBuilder

R = TypeVar("R", bound=IEntity)


class Neo4jRelationshipQueryable(IOrderedGraphRelationshipQueryable[R], Generic[R]):
    """
    Neo4j implementation of relationship queryable interface.
    
    Provides LINQ-style querying capabilities for relationships in Neo4j,
    translating lambda expressions to Cypher queries.
    """
    
    def __init__(self, relationship_type: Type[R], session: Any):
        """
        Initialize the relationship queryable.
        
        Args:
            relationship_type: Type of relationships to query.
            session: Neo4j session for executing queries.
        """
        self._relationship_type = relationship_type
        self._session = session
        self._cypher_builder = RelationshipCypherBuilder(relationship_type)
        
        # Query state
        self._where_predicate: Optional[Callable] = None
        self._order_by_key: Optional[Callable] = None
        self._order_descending: bool = False
        self._take_limit: Optional[int] = None
        self._skip_count: Optional[int] = None
        self._select_projection: Optional[Callable] = None
    
    def where(self, predicate: Callable[[R], bool]) -> "Neo4jRelationshipQueryable[R]":
        """
        Filter relationships based on a predicate.
        
        Args:
            predicate: Lambda expression defining the filter condition.
        
        Returns:
            Self for method chaining.
        """
        self._where_predicate = predicate
        return self
    
    def order_by(self, key_selector: Callable[[R], Any]) -> "Neo4jRelationshipQueryable[R]":
        """
        Order relationships by a key selector in ascending order.
        
        Args:
            key_selector: Lambda expression defining the sort key.
        
        Returns:
            Self for method chaining.
        """
        self._order_by_key = key_selector
        self._order_descending = False
        return self
    
    def order_by_descending(self, key_selector: Callable[[R], Any]) -> "Neo4jRelationshipQueryable[R]":
        """
        Order relationships by a key selector in descending order.
        
        Args:
            key_selector: Lambda expression defining the sort key.
        
        Returns:
            Self for method chaining.
        """
        self._order_by_key = key_selector
        self._order_descending = True
        return self
    
    def take(self, count: int) -> "Neo4jRelationshipQueryable[R]":
        """
        Limit the number of results.
        
        Args:
            count: Maximum number of results to return.
        
        Returns:
            Self for method chaining.
        """
        self._take_limit = count
        return self
    
    def skip(self, count: int) -> "Neo4jRelationshipQueryable[R]":
        """
        Skip a number of results.
        
        Args:
            count: Number of results to skip.
        
        Returns:
            Self for method chaining.
        """
        self._skip_count = count
        return self
    
    def select(self, selector: Callable[[R], Any]) -> "Neo4jRelationshipQueryable[Any]":
        """
        Project results using a selector function.
        
        Args:
            selector: Lambda expression defining the projection.
        
        Returns:
            A new queryable with projected results.
        """
        self._select_projection = selector
        return self
    
    async def to_list(self) -> List[R]:
        """
        Execute the query and return results as a list.
        
        Returns:
            List of relationships matching the query criteria.
        """
        # Build Cypher query using RelationshipCypherBuilder
        cypher_query = self._cypher_builder.build_query(
            where_predicate=self._where_predicate,
            order_by_key=self._order_by_key,
            order_descending=self._order_descending,
            take_count=self._take_limit,
            skip_count=self._skip_count,
            select_projection=self._select_projection
        )
        
        # Execute query
        result = await self._session.run(cypher_query.query, cypher_query.parameters)
        data = await result.data()
        
        # Convert results to relationship objects
        if self._select_projection:
            # For projections, return the raw dicts
            return data
        relationships = []
        for record in data:
            if "r" in record:
                rel = self._relationship_type(**record["r"])
            else:
                rel = self._relationship_type(**record)
            relationships.append(rel)
        return relationships
    
    async def first(self) -> R:
        """
        Get the first result or raise an exception if none found.
        
        Returns:
            The first relationship matching the criteria.
        
        Raises:
            Exception: If no results are found.
        """
        results = await self.take(1).to_list()
        if not results:
            raise Exception("No results found")
        return results[0]
    
    async def first_or_none(self) -> Optional[R]:
        """
        Get the first result or None if none found.
        
        Returns:
            The first relationship matching the criteria, or None.
        """
        try:
            return await self.first()
        except Exception:
            return None

    async def count(self) -> int:
        # Not implemented in RelationshipCypherBuilder yet
        raise NotImplementedError()

    async def any(self) -> bool:
        # Not implemented in RelationshipCypherBuilder yet
        raise NotImplementedError()

    # TODO: Implement where_start_node, where_end_node, select, all, etc.
    # These can be added as needed for full LINQ compatibility. 