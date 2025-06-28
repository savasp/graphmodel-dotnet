"""
Neo4j implementation of async streaming queryables.

This module provides Neo4j-specific implementation of async streaming
for efficient processing of large result sets.
"""

from typing import Any, AsyncIterator, Callable, List, Optional, Type, TypeVar

from neo4j import AsyncSession

from ...core.node import INode
from ...core.relationship import IRelationship
from ...querying.async_streaming import AsyncGraphQueryable, IAsyncGraphQueryable
from .cypher_builder import CypherBuilder, CypherQuery
from .serialization import Neo4jSerializer

T = TypeVar('T')
TNode = TypeVar('TNode', bound=INode)
TRelationship = TypeVar('TRelationship', bound=IRelationship)


class Neo4jAsyncNodeQueryable(IAsyncGraphQueryable[TNode]):
    """
    Neo4j implementation of async node queryable.
    
    Provides streaming query execution for nodes with lazy evaluation
    and efficient memory usage for large result sets.
    """
    
    def __init__(
        self,
        node_type: Type[TNode],
        session: AsyncSession,
        serializer: Neo4jSerializer,
        batch_size: int = 1000
    ):
        """
        Initialize the async node queryable.
        
        Args:
            node_type: Type of nodes to query.
            session: Neo4j async session.
            serializer: Serializer for node conversion.
            batch_size: Size of batches for streaming results.
        """
        self._node_type = node_type
        self._session = session
        self._serializer = serializer
        self._batch_size = batch_size
        self._cypher_builder = CypherBuilder(node_type)
        
        # Query state
        self._where_clauses: List[str] = []
        self._order_by_clauses: List[str] = []
        self._take_count: Optional[int] = None
        self._skip_count: int = 0
    
    def __aiter__(self) -> AsyncIterator[TNode]:
        """Return an async iterator for streaming query results."""
        return self._execute_streaming()
    
    async def _execute_streaming(self) -> AsyncIterator[TNode]:
        """Execute the query and yield results as they stream from Neo4j."""
        # Build Cypher query
        cypher_query = self._build_streaming_query()
        
        # Execute query with streaming cursor
        result = await self._session.run(cypher_query.query, cypher_query.parameters)
        
        count = 0
        async for record in result:
            # Check take limit
            if self._take_count is not None and count >= self._take_count:
                break
            
            # Deserialize node
            node_data = record.get("n") or record
            node = self._serializer.deserialize_node(node_data, self._node_type)
            
            yield node
            count += 1
    
    async def to_list_async(self) -> List[TNode]:
        """Materialize all results into a list asynchronously."""
        results = []
        async for node in self:
            results.append(node)
        return results
    
    async def first_async(self, predicate: Optional[Callable[[TNode], bool]] = None) -> TNode:
        """Get the first element asynchronously."""
        async for node in self:
            if predicate is None or predicate(node):
                return node
        raise ValueError("Sequence contains no matching elements")
    
    async def first_or_default_async(self, predicate: Optional[Callable[[TNode], bool]] = None) -> Optional[TNode]:
        """Get the first element or None asynchronously."""
        try:
            return await self.first_async(predicate)
        except ValueError:
            return None
    
    async def single_async(self, predicate: Optional[Callable[[TNode], bool]] = None) -> TNode:
        """Get the single element asynchronously."""
        found = None
        count = 0
        
        async for node in self:
            if predicate is None or predicate(node):
                if count > 0:
                    raise ValueError("Sequence contains more than one matching element")
                found = node
                count += 1
        
        if count == 0:
            raise ValueError("Sequence contains no matching elements")
        
        return found
    
    async def single_or_default_async(self, predicate: Optional[Callable[[TNode], bool]] = None) -> Optional[TNode]:
        """Get the single element or None asynchronously."""
        try:
            return await self.single_async(predicate)
        except ValueError:
            return None
    
    async def count_async(self, predicate: Optional[Callable[[TNode], bool]] = None) -> int:
        """Count elements asynchronously."""
        if predicate is None:
            # Optimize count query without predicate
            count_query = self._cypher_builder.build_count_query()
            result = await self._session.run(count_query.query, count_query.parameters)
            record = await result.single()
            return record["count"]
        else:
            # Count with predicate requires iteration
            count = 0
            async for node in self:
                if predicate(node):
                    count += 1
            return count
    
    async def any_async(self, predicate: Optional[Callable[[TNode], bool]] = None) -> bool:
        """Check if any elements match the predicate asynchronously."""
        if predicate is None:
            # Optimize exists query without predicate
            exists_query = self._cypher_builder.build_exists_query()
            result = await self._session.run(exists_query.query, exists_query.parameters)
            record = await result.single()
            return record["exists"]
        else:
            # Check with predicate requires iteration
            async for node in self:
                if predicate(node):
                    return True
            return False
    
    async def all_async(self, predicate: Callable[[TNode], bool]) -> bool:
        """Check if all elements match the predicate asynchronously."""
        async for node in self:
            if not predicate(node):
                return False
        return True
    
    def where_async(self, predicate: Callable[[TNode], bool]) -> "Neo4jAsyncNodeQueryable[TNode]":
        """Filter elements asynchronously."""
        # For now, we'll apply filters in memory
        # A more advanced implementation would translate predicates to Cypher
        new_queryable = Neo4jAsyncNodeQueryable(
            self._node_type, 
            self._session, 
            self._serializer, 
            self._batch_size
        )
        new_queryable._where_clauses = self._where_clauses.copy()
        new_queryable._order_by_clauses = self._order_by_clauses.copy()
        new_queryable._take_count = self._take_count
        new_queryable._skip_count = self._skip_count
        
        # Store predicate for in-memory filtering
        new_queryable._predicate = predicate
        return new_queryable
    
    def select_async(self, selector: Callable[[TNode], Any]) -> "Neo4jAsyncProjectionQueryable[Any]":
        """Project elements asynchronously."""
        return Neo4jAsyncProjectionQueryable(
            self._node_type,
            self._session,
            self._serializer,
            selector,
            self._batch_size
        )
    
    def take_async(self, count: int) -> "Neo4jAsyncNodeQueryable[TNode]":
        """Take the first n elements asynchronously."""
        new_queryable = Neo4jAsyncNodeQueryable(
            self._node_type, 
            self._session, 
            self._serializer, 
            self._batch_size
        )
        new_queryable._where_clauses = self._where_clauses.copy()
        new_queryable._order_by_clauses = self._order_by_clauses.copy()
        new_queryable._take_count = count
        new_queryable._skip_count = self._skip_count
        return new_queryable
    
    def skip_async(self, count: int) -> "Neo4jAsyncNodeQueryable[TNode]":
        """Skip the first n elements asynchronously."""
        new_queryable = Neo4jAsyncNodeQueryable(
            self._node_type, 
            self._session, 
            self._serializer, 
            self._batch_size
        )
        new_queryable._where_clauses = self._where_clauses.copy()
        new_queryable._order_by_clauses = self._order_by_clauses.copy()
        new_queryable._take_count = self._take_count
        new_queryable._skip_count = self._skip_count + count
        return new_queryable
    
    def _build_streaming_query(self) -> CypherQuery:
        """Build Cypher query optimized for streaming."""
        # Get node labels
        labels = getattr(self._node_type, '__graph_labels__', [self._node_type.__name__])
        labels_str = ':'.join(labels)
        
        # Build query parts
        query_parts = [f"MATCH (n:{labels_str})"]
        parameters = {}
        
        # Add WHERE clauses
        if self._where_clauses:
            where_clause = " AND ".join(self._where_clauses)
            query_parts.append(f"WHERE {where_clause}")
        
        # Add ORDER BY clauses
        if self._order_by_clauses:
            order_clause = ", ".join(self._order_by_clauses)
            query_parts.append(f"ORDER BY {order_clause}")
        
        # Add SKIP clause
        if self._skip_count > 0:
            query_parts.append(f"SKIP {self._skip_count}")
        
        # Add LIMIT clause
        if self._take_count is not None:
            query_parts.append(f"LIMIT {self._take_count}")
        
        # Add RETURN clause
        query_parts.append("RETURN n")
        
        query = "\n".join(query_parts)
        return CypherQuery(query, parameters)


class Neo4jAsyncProjectionQueryable(IAsyncGraphQueryable[Any]):
    """
    Neo4j async queryable for projected results.
    
    Handles projection of query results using selector functions.
    """
    
    def __init__(
        self,
        source_type: Type,
        session: AsyncSession,
        serializer: Neo4jSerializer,
        selector: Callable[[Any], Any],
        batch_size: int = 1000
    ):
        """Initialize the projection queryable."""
        self._source_type = source_type
        self._session = session
        self._serializer = serializer
        self._selector = selector
        self._batch_size = batch_size
        self._source_queryable = Neo4jAsyncNodeQueryable(
            source_type, session, serializer, batch_size
        )
    
    def __aiter__(self) -> AsyncIterator[Any]:
        """Return an async iterator for projected results."""
        return self._execute_projection()
    
    async def _execute_projection(self) -> AsyncIterator[Any]:
        """Execute projection over source queryable."""
        async for item in self._source_queryable:
            yield self._selector(item)
    
    async def to_list_async(self) -> List[Any]:
        """Materialize projected results into a list."""
        results = []
        async for item in self:
            results.append(item)
        return results
    
    async def first_async(self, predicate: Optional[Callable[[Any], bool]] = None) -> Any:
        """Get the first projected element."""
        async for item in self:
            if predicate is None or predicate(item):
                return item
        raise ValueError("Sequence contains no matching elements")
    
    async def first_or_default_async(self, predicate: Optional[Callable[[Any], bool]] = None) -> Optional[Any]:
        """Get the first projected element or None."""
        try:
            return await self.first_async(predicate)
        except ValueError:
            return None
    
    async def single_async(self, predicate: Optional[Callable[[Any], bool]] = None) -> Any:
        """Get the single projected element."""
        found = None
        count = 0
        
        async for item in self:
            if predicate is None or predicate(item):
                if count > 0:
                    raise ValueError("Sequence contains more than one matching element")
                found = item
                count += 1
        
        if count == 0:
            raise ValueError("Sequence contains no matching elements")
        
        return found
    
    async def single_or_default_async(self, predicate: Optional[Callable[[Any], bool]] = None) -> Optional[Any]:
        """Get the single projected element or None."""
        try:
            return await self.single_async(predicate)
        except ValueError:
            return None
    
    async def count_async(self, predicate: Optional[Callable[[Any], bool]] = None) -> int:
        """Count projected elements."""
        count = 0
        async for item in self:
            if predicate is None or predicate(item):
                count += 1
        return count
    
    async def any_async(self, predicate: Optional[Callable[[Any], bool]] = None) -> bool:
        """Check if any projected elements match the predicate."""
        async for item in self:
            if predicate is None or predicate(item):
                return True
        return False
    
    async def all_async(self, predicate: Callable[[Any], bool]) -> bool:
        """Check if all projected elements match the predicate."""
        async for item in self:
            if not predicate(item):
                return False
        return True
    
    def where_async(self, predicate: Callable[[Any], bool]) -> "IAsyncGraphQueryable[Any]":
        """Filter projected elements."""
        # Return a generic async queryable with filtering
        async def filtered_generator():
            async for item in self:
                if predicate(item):
                    yield item
        
        from ...querying.async_streaming import create_async_queryable
        return create_async_queryable(filtered_generator, self._batch_size)
    
    def select_async(self, selector: Callable[[Any], Any]) -> "IAsyncGraphQueryable[Any]":
        """Project projected elements (chained projection)."""
        async def chained_projection():
            async for item in self:
                yield selector(item)
        
        from ...querying.async_streaming import create_async_queryable
        return create_async_queryable(chained_projection, self._batch_size)
    
    def take_async(self, count: int) -> "IAsyncGraphQueryable[Any]":
        """Take the first n projected elements."""
        async def limited_generator():
            taken = 0
            async for item in self:
                if taken >= count:
                    break
                yield item
                taken += 1
        
        from ...querying.async_streaming import create_async_queryable
        return create_async_queryable(limited_generator, self._batch_size)
    
    def skip_async(self, count: int) -> "IAsyncGraphQueryable[Any]":
        """Skip the first n projected elements."""
        async def skipped_generator():
            skipped = 0
            async for item in self:
                if skipped < count:
                    skipped += 1
                    continue
                yield item
        
        from ...querying.async_streaming import create_async_queryable
        return create_async_queryable(skipped_generator, self._batch_size) 