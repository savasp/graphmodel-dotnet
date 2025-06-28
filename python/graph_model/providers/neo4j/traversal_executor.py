"""
Neo4j implementation of PathSegments traversal execution.

This module provides the concrete implementation of PathSegments functionality
for Neo4j, which is the foundational method for all graph traversal operations.
"""

from typing import Any, Dict, List, Optional, Type, TypeVar

from neo4j import AsyncSession, AsyncTransaction

from ...core.node import INode
from ...core.relationship import IRelationship
from ...querying.traversal import (
    GraphPathSegment,
    GraphTraversal,
    GraphTraversalDirection,
    TraversalPath,
)
from .cypher_builder import CypherQuery
from .serialization import Neo4jSerializer

TStartNode = TypeVar('TStartNode', bound=INode)
TRelationship = TypeVar('TRelationship', bound=IRelationship)
TEndNode = TypeVar('TEndNode', bound=INode)


class Neo4jTraversalExecutor:
    """
    Neo4j implementation of graph traversal execution.
    
    Provides the concrete implementation of PathSegments and other traversal
    operations using Cypher queries.
    """
    
    def __init__(self, session: AsyncSession, serializer: Neo4jSerializer):
        """
        Initialize the traversal executor.
        
        Args:
            session: Neo4j async session for query execution.
            serializer: Serializer for converting between Python objects and Neo4j data.
        """
        self._session = session
        self._serializer = serializer
    
    async def execute_path_segments(
        self,
        traversal: GraphTraversal
    ) -> List[GraphPathSegment[INode, IRelationship, INode]]:
        """
        Execute PathSegments traversal and return path segments.
        
        This is the foundational method that matches .NET's PathSegments functionality.
        All other traversal methods are built on top of this.
        
        Args:
            traversal: The configured GraphTraversal to execute.
            
        Returns:
            List of GraphPathSegment objects representing each traversal step.
        """
        # Build Cypher query for PathSegments
        cypher_query = self._build_path_segments_query(traversal)
        
        # Execute query
        result = await self._session.run(cypher_query.query, cypher_query.parameters)
        records = await result.data()
        
        # Convert results to PathSegments
        path_segments = []
        for record in records:
            segment = self._create_path_segment_from_record(record, traversal)
            path_segments.append(segment)
        
        return path_segments
    
    async def execute_nodes(self, traversal: GraphTraversal) -> List[INode]:
        """
        Execute traversal and return target nodes.
        
        This is equivalent to .NET's Traverse<TStartNode, TRelationship, TEndNode>()
        which internally calls PathSegments().Select(ps => ps.EndNode).
        
        Args:
            traversal: The configured GraphTraversal to execute.
            
        Returns:
            List of target nodes reached through traversal.
        """
        # Build Cypher query for node traversal
        cypher_query = self._build_node_traversal_query(traversal)
        
        # Execute query
        result = await self._session.run(cypher_query.query, cypher_query.parameters)
        records = await result.data()
        
        # Convert results to nodes
        nodes = []
        for record in records:
            node = self._create_node_from_record(record, traversal._target_node_type)
            nodes.append(node)
        
        return nodes
    
    async def execute_relationships(self, traversal: GraphTraversal) -> List[IRelationship]:
        """
        Execute traversal and return relationships.
        
        This is equivalent to .NET's TraverseRelationships<TStartNode, TRelationship, TEndNode>()
        which internally calls PathSegments().Select(ps => ps.Relationship).
        
        Args:
            traversal: The configured GraphTraversal to execute.
            
        Returns:
            List of relationships traversed.
        """
        # Build Cypher query for relationship traversal
        cypher_query = self._build_relationship_traversal_query(traversal)
        
        # Execute query
        result = await self._session.run(cypher_query.query, cypher_query.parameters)
        records = await result.data()
        
        # Convert results to relationships
        relationships = []
        for record in records:
            relationship = self._create_relationship_from_record(record, traversal._relationship_type)
            relationships.append(relationship)
        
        return relationships
    
    async def execute_paths(self, traversal: GraphTraversal) -> List[TraversalPath]:
        """
        Execute traversal and return complete paths.
        
        Args:
            traversal: The configured GraphTraversal to execute.
            
        Returns:
            List of TraversalPath objects representing complete paths from start to end.
        """
        # Build Cypher query for path traversal
        cypher_query = self._build_path_traversal_query(traversal)
        
        # Execute query
        result = await self._session.run(cypher_query.query, cypher_query.parameters)
        records = await result.data()
        
        # Convert results to paths
        paths = []
        for record in records:
            path = self._create_path_from_record(record, traversal)
            paths.append(path)
        
        return paths
    
    def _build_path_segments_query(self, traversal: GraphTraversal) -> CypherQuery:
        """
        Build Cypher query for PathSegments traversal.
        
        Args:
            traversal: The configured GraphTraversal.
            
        Returns:
            CypherQuery for PathSegments execution.
        """
        # Get start node IDs
        start_ids = [node.id for node in traversal._start_nodes]
        
        # Build traversal pattern
        pattern = traversal.build_cypher_pattern()
        
        # Get start node label
        if traversal._start_nodes and hasattr(traversal._start_nodes[0], '__class__'):
            start_node_cls = traversal._start_nodes[0].__class__
            start_labels = getattr(start_node_cls, '__graph_node_metadata__', {}).get('label', start_node_cls.__name__)
            start_labels = f":{start_labels}" if start_labels else ''
        else:
            start_labels = ''

        # Get target node label
        target_labels = ''
        if traversal._target_node_type:
            labels = getattr(traversal._target_node_type, '__graph_node_metadata__', {}).get('label', traversal._target_node_type.__name__)
            target_labels = f":{labels}" if labels else ''
        
        # Build WHERE clause for start nodes
        where_clauses = [f"start.id IN $start_ids"]
        
        # Add custom WHERE clauses
        if traversal._where_clauses:
            where_clauses.extend(traversal._where_clauses)
        
        where_clause = " AND ".join(where_clauses)
        
        # Build complete query
        query = f"""
        MATCH (start{start_labels}){pattern}(target{target_labels})
        WHERE {where_clause}
        RETURN start, r, target
        """
        
        parameters = {
            "start_ids": start_ids
        }
        
        return CypherQuery(query, parameters)
    
    def _build_node_traversal_query(self, traversal: GraphTraversal) -> CypherQuery:
        """Build Cypher query for node traversal (optimized for target nodes only)."""
        start_ids = [node.id for node in traversal._start_nodes]
        pattern = traversal.build_cypher_pattern()
        
        target_labels = ""
        if traversal._target_node_type:
            labels = getattr(traversal._target_node_type, '__graph_labels__', [traversal._target_node_type.__name__])
            target_labels = f":{':'.join(labels)}"
        
        where_clauses = [f"start.id IN $start_ids"]
        if traversal._where_clauses:
            where_clauses.extend(traversal._where_clauses)
        
        where_clause = " AND ".join(where_clauses)
        
        query = f"""
        MATCH (start){pattern}(target{target_labels})
        WHERE {where_clause}
        RETURN DISTINCT target
        """
        
        return CypherQuery(query, {"start_ids": start_ids})
    
    def _build_relationship_traversal_query(self, traversal: GraphTraversal) -> CypherQuery:
        """Build Cypher query for relationship traversal (optimized for relationships only)."""
        start_ids = [node.id for node in traversal._start_nodes]
        pattern = traversal.build_cypher_pattern()
        
        target_labels = ""
        if traversal._target_node_type:
            labels = getattr(traversal._target_node_type, '__graph_labels__', [traversal._target_node_type.__name__])
            target_labels = f":{':'.join(labels)}"
        
        where_clauses = [f"start.id IN $start_ids"]
        if traversal._where_clauses:
            where_clauses.extend(traversal._where_clauses)
        
        where_clause = " AND ".join(where_clauses)
        
        query = f"""
        MATCH (start){pattern}(target{target_labels})
        WHERE {where_clause}
        RETURN DISTINCT r
        """
        
        return CypherQuery(query, {"start_ids": start_ids})
    
    def _build_path_traversal_query(self, traversal: GraphTraversal) -> CypherQuery:
        """Build Cypher query for complete path traversal."""
        start_ids = [node.id for node in traversal._start_nodes]
        pattern = traversal.build_cypher_pattern()
        
        target_labels = ""
        if traversal._target_node_type:
            labels = getattr(traversal._target_node_type, '__graph_labels__', [traversal._target_node_type.__name__])
            target_labels = f":{':'.join(labels)}"
        
        where_clauses = [f"start.id IN $start_ids"]
        if traversal._where_clauses:
            where_clauses.extend(traversal._where_clauses)
        
        where_clause = " AND ".join(where_clauses)
        
        query = f"""
        MATCH path = (start){pattern}(target{target_labels})
        WHERE {where_clause}
        RETURN path
        """
        
        return CypherQuery(query, {"start_ids": start_ids})
    
    def _create_path_segment_from_record(
        self, 
        record: Dict[str, Any], 
        traversal: GraphTraversal
    ) -> GraphPathSegment[INode, IRelationship, INode]:
        """Create a GraphPathSegment from a Neo4j record."""
        # Deserialize start node
        start_data = record["start"]
        start_node = self._serializer.deserialize_node(
            start_data, 
            type(traversal._start_nodes[0])  # Use type of first start node
        )
        
        # Deserialize relationship
        rel_data = record["r"]
        relationship = self._serializer.deserialize_relationship(
            rel_data, 
            traversal._relationship_type
        )
        
        # Deserialize target node
        target_data = record["target"]
        target_node = self._serializer.deserialize_node(
            target_data, 
            traversal._target_node_type
        )
        
        return GraphPathSegment(
            start_node=start_node,
            relationship=relationship,
            end_node=target_node
        )
    
    def _create_node_from_record(self, record: Dict[str, Any], node_type: Type[INode]) -> INode:
        """Create a node from a Neo4j record."""
        node_data = record["target"]
        return self._serializer.deserialize_node(node_data, node_type)
    
    def _create_relationship_from_record(
        self, 
        record: Dict[str, Any], 
        relationship_type: Type[IRelationship]
    ) -> IRelationship:
        """Create a relationship from a Neo4j record."""
        rel_data = record["r"]
        return self._serializer.deserialize_relationship(rel_data, relationship_type)
    
    def _create_path_from_record(self, record: Dict[str, Any], traversal: GraphTraversal) -> TraversalPath:
        """Create a TraversalPath from a Neo4j path record."""
        path_data = record["path"]
        
        # Extract nodes and relationships from path
        nodes = []
        relationships = []
        
        # Neo4j path structure: nodes and relationships are separate arrays
        for i, node_data in enumerate(path_data.nodes):
            if i == 0:
                # First node is start node type
                node_type = type(traversal._start_nodes[0])
            else:
                # Other nodes are target node type
                node_type = traversal._target_node_type
            
            node = self._serializer.deserialize_node(node_data, node_type)
            nodes.append(node)
        
        for rel_data in path_data.relationships:
            relationship = self._serializer.deserialize_relationship(rel_data, traversal._relationship_type)
            relationships.append(relationship)
        
        return TraversalPath(nodes=nodes, relationships=relationships) 