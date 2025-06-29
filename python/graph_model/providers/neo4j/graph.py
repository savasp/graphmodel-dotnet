"""
Neo4j implementation of the IGraph interface.

This module provides the concrete implementation of graph operations
for Neo4j, using the async driver and serialization layer.
"""

from typing import Any, List, Optional, Type, TypeVar

from neo4j import AsyncSession, AsyncTransaction

from graph_model.core.exceptions import GraphException
from graph_model.core.graph import IGraph, IGraphNodeQueryable, IGraphRelationshipQueryable, IGraphTransaction
from graph_model.core.node import INode, Node
from graph_model.core.relationship import IRelationship, Relationship

from .aggregation_executor import Neo4jAggregationExecutor
from .async_queryable import Neo4jAsyncNodeQueryable
from .driver import Neo4jDriver
from .node_queryable import Neo4jNodeQueryable
from .relationship_queryable import Neo4jRelationshipQueryable
from .serialization import Neo4jSerializer, SerializedNode, SerializedRelationship
from .transaction import Neo4jTransaction
from .traversal_executor import Neo4jTraversalExecutor

TNode = TypeVar('TNode', bound=INode)
TRelationship = TypeVar('TRelationship', bound=IRelationship)


class Neo4jGraph(IGraph[TNode, TRelationship]):
    """
    Neo4j implementation of the IGraph interface.
    
    Provides CRUD operations for nodes and relationships using the async
    Neo4j driver and .NET-compatible serialization.
    """
    
    def __init__(self):
        """Initialize the Neo4j graph instance."""
        self._serializer = Neo4jSerializer()
        self._driver = Neo4jDriver()
        self._traversal_executor = None  # Lazy initialization
        self._aggregation_executor = None  # Lazy initialization
    
    def _get_traversal_executor(self, session) -> Neo4jTraversalExecutor:
        """Get or create traversal executor with session."""
        if self._traversal_executor is None:
            self._traversal_executor = Neo4jTraversalExecutor(session, self._serializer)
        return self._traversal_executor
    
    def _get_aggregation_executor(self, session) -> Neo4jAggregationExecutor:
        """Get or create aggregation executor with session."""
        if self._aggregation_executor is None:
            self._aggregation_executor = Neo4jAggregationExecutor(session, self._serializer)
        return self._aggregation_executor
    
    async def create_node(
        self, 
        node: TNode, 
        transaction: Optional[IGraphTransaction] = None
    ) -> TNode:
        """Create a new node in the graph."""
        try:
            # Serialize the node
            serialized = self._serializer.serialize_node(node)
            
            # Build Cypher query
            labels_str = ':'.join(serialized.labels)
            properties_str = ', '.join([f"{k}: ${k}" for k in serialized.properties.keys()])
            
            cypher = f"""
            CREATE (n:{labels_str} {{Id: $id, {properties_str}}})
            RETURN n
            """
            
            # Execute query
            if transaction:
                tx = transaction.transaction
            else:
                session = Neo4jDriver.session()
                tx = await session.begin_transaction()
            
            try:
                result = await tx.run(cypher, {"id": serialized.id, **serialized.properties})
                record = await result.single()
                
                # Handle complex properties
                await self._create_complex_properties(node, serialized, tx)
                
                if not transaction:
                    await tx.commit()
                    await session.close()
                
                return node
                
            except Exception as e:
                if not transaction:
                    await tx.rollback()
                    await session.close()
                raise e
                
        except Exception as e:
            raise GraphException(f"Failed to create node: {str(e)}") from e
    
    async def get_node(
        self, 
        node_id: str, 
        transaction: Optional[IGraphTransaction] = None
    ) -> Optional[TNode]:
        """Retrieve a node by its ID."""
        try:
            cypher = """
            MATCH (n {Id: $id})
            RETURN n
            """
            
            if transaction:
                tx = transaction.transaction
            else:
                session = Neo4jDriver.session()
                tx = await session.begin_transaction()
            
            try:
                result = await tx.run(cypher, {"id": node_id})
                record = await result.single()
                
                if not record:
                    if not transaction:
                        await session.close()
                    return None
                
                # Deserialize the node
                node_data = dict(record["n"])
                # Note: We need the actual node type here - this is a limitation
                # that will be addressed in the queryable implementation
                
                if not transaction:
                    await session.close()
                
                # For now, return None - proper deserialization needs node type
                return None
                
            except Exception as e:
                if not transaction:
                    await session.close()
                raise e
                
        except Exception as e:
            raise GraphException(f"Failed to get node: {str(e)}") from e
    
    async def update_node(
        self, 
        node: TNode, 
        transaction: Optional[IGraphTransaction] = None
    ) -> TNode:
        """Update an existing node."""
        try:
            # Serialize the node
            serialized = self._serializer.serialize_node(node)
            
            # Build Cypher query
            properties_str = ', '.join([f"n.{k} = ${k}" for k in serialized.properties.keys()])
            
            cypher = f"""
            MATCH (n {{Id: $id}})
            SET {properties_str}
            RETURN n
            """
            
            # Execute query
            if transaction:
                tx = transaction.transaction
            else:
                session = Neo4jDriver.session()
                tx = await session.begin_transaction()
            
            try:
                result = await tx.run(cypher, {"id": serialized.id, **serialized.properties})
                record = await result.single()
                
                if not record:
                    raise GraphException(f"Node with ID {node.id} not found")
                
                # Handle complex properties
                await self._update_complex_properties(node, serialized, tx)
                
                if not transaction:
                    await tx.commit()
                    await session.close()
                
                return node
                
            except Exception as e:
                if not transaction:
                    await tx.rollback()
                    await session.close()
                raise e
                
        except Exception as e:
            raise GraphException(f"Failed to update node: {str(e)}") from e
    
    async def delete_node(
        self, 
        node_id: str, 
        transaction: Optional[IGraphTransaction] = None
    ) -> bool:
        """Delete a node by its ID."""
        try:
            cypher = """
            MATCH (n {Id: $id})
            DETACH DELETE n
            RETURN count(n) as deleted
            """
            
            if transaction:
                tx = transaction.transaction
            else:
                session = Neo4jDriver.session()
                tx = await session.begin_transaction()
            
            try:
                result = await tx.run(cypher, {"id": node_id})
                record = await result.single()
                deleted_count = record["deleted"]
                
                if not transaction:
                    await tx.commit()
                    await session.close()
                
                return deleted_count > 0
                
            except Exception as e:
                if not transaction:
                    await tx.rollback()
                    await session.close()
                raise e
                
        except Exception as e:
            raise GraphException(f"Failed to delete node: {str(e)}") from e
    
    async def create_relationship(
        self, 
        relationship: TRelationship, 
        transaction: Optional[IGraphTransaction] = None
    ) -> TRelationship:
        """Create a new relationship in the graph."""
        try:
            # Serialize the relationship
            serialized = self._serializer.serialize_relationship(relationship)
            
            # Build Cypher query
            properties_str = ', '.join([f"{k}: ${k}" for k in serialized.properties.keys()])
            
            cypher = f"""
            MATCH (start {{Id: $start_id}}), (end {{Id: $end_id}})
            CREATE (start)-[r:{serialized.type} {{Id: $id, {properties_str}}}]->(end)
            RETURN r
            """
            
            # Execute query
            if transaction:
                tx = transaction.transaction
            else:
                session = Neo4jDriver.session()
                tx = await session.begin_transaction()
            
            try:
                result = await tx.run(cypher, {
                    "id": serialized.id,
                    "start_id": serialized.start_node_id,
                    "end_id": serialized.end_node_id,
                    **serialized.properties
                })
                record = await result.single()
                
                if not transaction:
                    await tx.commit()
                    await session.close()
                
                return relationship
                
            except Exception as e:
                if not transaction:
                    await tx.rollback()
                    await session.close()
                raise e
                
        except Exception as e:
            raise GraphException(f"Failed to create relationship: {str(e)}") from e
    
    async def get_relationship(
        self, 
        relationship_id: str, 
        transaction: Optional[IGraphTransaction] = None
    ) -> Optional[TRelationship]:
        """Retrieve a relationship by its ID."""
        try:
            cypher = """
            MATCH ()-[r {Id: $id}]->()
            RETURN r
            """
            
            if transaction:
                tx = transaction.transaction
            else:
                session = Neo4jDriver.session()
                tx = await session.begin_transaction()
            
            try:
                result = await tx.run(cypher, {"id": relationship_id})
                record = await result.single()
                
                if not record:
                    if not transaction:
                        await session.close()
                    return None
                
                # Deserialize the relationship
                # Note: Similar limitation as get_node - needs relationship type
                
                if not transaction:
                    await session.close()
                
                return None
                
            except Exception as e:
                if not transaction:
                    await session.close()
                raise e
                
        except Exception as e:
            raise GraphException(f"Failed to get relationship: {str(e)}") from e
    
    async def update_relationship(
        self, 
        relationship: TRelationship, 
        transaction: Optional[IGraphTransaction] = None
    ) -> TRelationship:
        """Update an existing relationship."""
        try:
            # Serialize the relationship
            serialized = self._serializer.serialize_relationship(relationship)
            
            # Build Cypher query
            properties_str = ', '.join([f"r.{k} = ${k}" for k in serialized.properties.keys()])
            
            cypher = f"""
            MATCH ()-[r {{Id: $id}}]->()
            SET {properties_str}
            RETURN r
            """
            
            # Execute query
            if transaction:
                tx = transaction.transaction
            else:
                session = Neo4jDriver.session()
                tx = await session.begin_transaction()
            
            try:
                result = await tx.run(cypher, {"id": serialized.id, **serialized.properties})
                record = await result.single()
                
                if not record:
                    raise GraphException(f"Relationship with ID {relationship.id} not found")
                
                if not transaction:
                    await tx.commit()
                    await session.close()
                
                return relationship
                
            except Exception as e:
                if not transaction:
                    await tx.rollback()
                    await session.close()
                raise e
                
        except Exception as e:
            raise GraphException(f"Failed to update relationship: {str(e)}") from e
    
    async def delete_relationship(
        self, 
        relationship_id: str, 
        transaction: Optional[IGraphTransaction] = None
    ) -> bool:
        """Delete a relationship by its ID."""
        try:
            cypher = """
            MATCH ()-[r {Id: $id}]->()
            DELETE r
            RETURN count(r) as deleted
            """
            
            if transaction:
                tx = transaction.transaction
            else:
                session = Neo4jDriver.session()
                tx = await session.begin_transaction()
            
            try:
                result = await tx.run(cypher, {"id": relationship_id})
                record = await result.single()
                deleted_count = record["deleted"]
                
                if not transaction:
                    await tx.commit()
                    await session.close()
                
                return deleted_count > 0
                
            except Exception as e:
                if not transaction:
                    await tx.rollback()
                    await session.close()
                raise e
                
        except Exception as e:
            raise GraphException(f"Failed to delete relationship: {str(e)}") from e
    
    def transaction(self) -> IGraphTransaction:
        """Start a new transaction."""
        session = Neo4jDriver.session()
        return Neo4jTransaction(session)
    
    def nodes(self, node_type: Type[TNode]) -> IGraphNodeQueryable[TNode]:
        """Get a queryable for nodes of the specified type."""
        session = Neo4jDriver.session()
        return Neo4jNodeQueryable(node_type, session)
    
    def relationships(self, relationship_type: Type[TRelationship]) -> IGraphRelationshipQueryable[TRelationship]:
        """Get a queryable for relationships of the specified type."""
        session = Neo4jDriver.session()
        return Neo4jRelationshipQueryable(relationship_type, session)
    
    async def _create_complex_properties(
        self, 
        node: TNode, 
        serialized: SerializedNode, 
        tx: AsyncTransaction
    ) -> None:
        """Create complex properties as related nodes."""
        for field_name, complex_data in serialized.complex_properties.items():
            value = complex_data['value']
            relationship_type = complex_data['relationship_type']
            
            if value is None:
                continue
            
            # Handle single complex property
            if not isinstance(value, list):
                await self._create_single_complex_property(
                    node.id, field_name, value, relationship_type, tx
                )
            else:
                # Handle collection of complex properties
                for i, item in enumerate(value):
                    await self._create_single_complex_property(
                        node.id, field_name, item, relationship_type, tx, sequence=i
                    )
    
    async def _create_single_complex_property(
        self, 
        parent_id: str, 
        field_name: str, 
        value: Any, 
        relationship_type: str, 
        tx: AsyncTransaction,
        sequence: int = 0
    ) -> None:
        """Create a single complex property as a related node."""
        # Serialize the complex value
        if hasattr(value, 'model_dump'):
            props = value.model_dump()
        else:
            props = value.__dict__ if hasattr(value, '__dict__') else {}
        
        # Get the label for the complex node
        label = type(value).__name__
        
        # Create the complex node and relationship
        cypher = f"""
        MATCH (parent {{Id: $parent_id}})
        CREATE (parent)-[r:{relationship_type} {{SequenceNumber: $sequence}}]->(complex:{label} $props)
        """
        
        await tx.run(cypher, {
            "parent_id": parent_id,
            "sequence": sequence,
            "props": props
        })
    
    async def _update_complex_properties(
        self, 
        node: TNode, 
        serialized: SerializedNode, 
        tx: AsyncTransaction
    ) -> None:
        """Update complex properties."""
        # First delete existing complex properties
        await self._delete_complex_properties(node.id, tx)
        
        # Then create new ones
        await self._create_complex_properties(node, serialized, tx)
    
    async def _delete_complex_properties(
        self, 
        node_id: str, 
        tx: AsyncTransaction
    ) -> None:
        """Delete all complex properties for a node."""
        cypher = """
        MATCH (n {Id: $node_id})-[r]->(complex)
        WHERE type(r) STARTS WITH $property_prefix
        DETACH DELETE complex
        DELETE r
        """
        
        await tx.run(cypher, {
            "node_id": node_id,
            "property_prefix": "__PROPERTY__"
        }) 