"""
Serialization and deserialization for Neo4j nodes and relationships.

This module handles the mapping between Python objects and Neo4j data structures,
using .NET-compatible conventions for relationship naming and complex properties.
"""

import json
from dataclasses import dataclass
from datetime import date, datetime, time
from typing import Any, Dict, List, Optional, Type, get_args, get_origin

from neo4j import Record
from pydantic import BaseModel

from graph_model.attributes.fields import (
    PropertyFieldInfo,
    PropertyFieldType,
    determine_field_type_from_annotation,
    get_field_info,
    get_relationship_type_for_field,
)
from graph_model.core.entity import IEntity
from graph_model.core.graph import GraphDataModel
from graph_model.core.node import INode, Node
from graph_model.core.relationship import IRelationship, Relationship


@dataclass
class SerializedNode:
    """Represents a serialized node with its properties and metadata."""
    
    id: str
    labels: List[str]
    properties: Dict[str, Any]
    complex_properties: Dict[str, Any]  # For related node properties


@dataclass
class SerializedRelationship:
    """Represents a serialized relationship with its properties."""
    
    id: str
    type: str
    start_node_id: str
    end_node_id: str
    properties: Dict[str, Any]


class Neo4jSerializer:
    """
    Handles serialization and deserialization of graph entities to/from Neo4j.
    
    Uses .NET-compatible conventions for relationship naming and complex properties.
    """
    
    @staticmethod
    def serialize_node(node: INode) -> SerializedNode:
        """
        Serialize a node to Neo4j format.
        
        Args:
            node: The node to serialize.
            
        Returns:
            SerializedNode with properties and complex property metadata.
        """
        # Get node metadata
        node_type = type(node)
        labels = getattr(node_type, '__graph_labels__', [node_type.__name__])
        
        # Separate simple and complex properties
        simple_props = {}
        complex_props = {}
        
        for field_name, field_value in node.model_dump().items():
            if field_value is None:
                continue
                
            field_info = get_field_info(node.model_fields[field_name])
            
            if field_info is None:
                # No field info - treat as simple property
                simple_props[field_name] = field_value
                continue
                
            if field_info.field_type == PropertyFieldType.SIMPLE:
                # Simple property - store directly
                prop_name = field_info.label or field_name
                simple_props[prop_name] = field_value
                
            elif field_info.field_type == PropertyFieldType.EMBEDDED:
                # Embedded property - serialize as JSON
                prop_name = field_info.label or field_name
                if field_info.storage_type == "json":
                    simple_props[prop_name] = json.dumps(field_value, default=str)
                else:
                    simple_props[prop_name] = field_value
                    
            elif field_info.field_type == PropertyFieldType.RELATED_NODE:
                # Related node property - store metadata for later processing
                complex_props[field_name] = {
                    'value': field_value,
                    'relationship_type': get_relationship_type_for_field(
                        field_name, 
                        field_info.relationship_type
                    ),
                    'private': field_info.private_relationship,
                    'field_info': field_info
                }
        
        return SerializedNode(
            id=node.id,
            labels=labels,
            properties=simple_props,
            complex_properties=complex_props
        )
    
    @staticmethod
    def serialize_relationship(relationship: IRelationship) -> SerializedRelationship:
        """
        Serialize a relationship to Neo4j format.
        
        Args:
            relationship: The relationship to serialize.
            
        Returns:
            SerializedRelationship with properties.
        """
        # Get relationship metadata
        rel_type = getattr(type(relationship), '__graph_label__', type(relationship).__name__)
        
        # Serialize properties
        properties = {}
        for field_name, field_value in relationship.model_dump().items():
            if field_value is None:
                continue
                
            field_info = get_field_info(relationship.model_fields[field_name])
            
            if field_info is None or field_info.field_type == PropertyFieldType.SIMPLE:
                # Simple property or no field info
                prop_name = field_info.label if field_info else field_name
                properties[prop_name] = field_value
                
            elif field_info.field_type == PropertyFieldType.EMBEDDED:
                # Embedded property - serialize as JSON
                prop_name = field_info.label or field_name
                if field_info.storage_type == "json":
                    properties[prop_name] = json.dumps(field_value, default=str)
                else:
                    properties[prop_name] = field_value
        
        return SerializedRelationship(
            id=relationship.id,
            type=rel_type,
            start_node_id=relationship.start_node_id,
            end_node_id=relationship.end_node_id,
            properties=properties
        )
    
    @staticmethod
    def deserialize_node(
        record: Record, 
        node_type: Type[INode],
        complex_properties: Optional[Dict[str, Any]] = None
    ) -> INode:
        """
        Deserialize a Neo4j record to a node.
        
        Args:
            record: The Neo4j record containing node data.
            node_type: The type of node to deserialize to.
            complex_properties: Pre-loaded complex properties.
            
        Returns:
            The deserialized node.
        """
        # Extract node properties from record
        node_data = dict(record)
        
        # Handle complex properties if provided
        if complex_properties:
            for field_name, complex_data in complex_properties.items():
                if field_name in node_data:
                    # Deserialize complex property
                    field_info = get_field_info(node_type.model_fields[field_name])
                    if field_info and field_info.field_type == PropertyFieldType.EMBEDDED:
                        if field_info.storage_type == "json":
                            node_data[field_name] = json.loads(node_data[field_name])
                    else:
                        # Related node property - should be handled separately
                        node_data[field_name] = complex_data
        
        # Create node instance
        return node_type(**node_data)
    
    @staticmethod
    def deserialize_relationship(
        record: Record, 
        relationship_type: Type[IRelationship]
    ) -> IRelationship:
        """
        Deserialize a Neo4j record to a relationship.
        
        Args:
            record: The Neo4j record containing relationship data.
            relationship_type: The type of relationship to deserialize to.
            
        Returns:
            The deserialized relationship.
        """
        # Extract relationship properties from record
        rel_data = dict(record)
        
        # Handle embedded properties
        for field_name, field_value in rel_data.items():
            field_info = get_field_info(relationship_type.model_fields[field_name])
            if field_info and field_info.field_type == PropertyFieldType.EMBEDDED:
                if field_info.storage_type == "json":
                    rel_data[field_name] = json.loads(field_value)
        
        # Create relationship instance
        return relationship_type(**rel_data)
    
    @staticmethod
    def get_complex_property_cypher(
        parent_alias: str,
        field_name: str,
        relationship_type: str,
        target_alias: str = None
    ) -> str:
        """
        Generate Cypher for loading complex properties.
        
        Args:
            parent_alias: The alias of the parent node.
            field_name: The name of the complex property field.
            relationship_type: The relationship type to use.
            target_alias: The alias for the target node (optional).
            
        Returns:
            Cypher query fragment for loading complex properties.
        """
        if target_alias is None:
            target_alias = f"{field_name}_node"
            
        return f"""
        OPTIONAL MATCH ({parent_alias})-[{field_name}_rel:{relationship_type}]->({target_alias})
        """
    
    @staticmethod
    def get_complex_property_return(
        field_name: str,
        target_alias: str = None
    ) -> str:
        """
        Generate Cypher return clause for complex properties.
        
        Args:
            field_name: The name of the complex property field.
            target_alias: The alias for the target node (optional).
            
        Returns:
            Cypher return clause fragment.
        """
        if target_alias is None:
            target_alias = f"{field_name}_node"
            
        return f"{field_name}: {target_alias}" 