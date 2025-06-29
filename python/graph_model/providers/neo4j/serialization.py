"""
Serialization and deserialization for Neo4j nodes and relationships.

This module handles the mapping between Python objects and Neo4j data structures,
using .NET-compatible conventions for relationship naming and complex properties.
"""

import json
from dataclasses import dataclass
from datetime import date, datetime, time
from enum import Enum
from typing import Any, Dict, List, Optional, Type, Union, get_args, get_origin

from neo4j import Record
from pydantic import BaseModel

from ...attributes.fields import PropertyFieldType, get_field_info
from ...core.graph import GraphDataModel
from ...core.node import INode
from ...core.relationship import IRelationship


def get_relationship_type_for_field(
    field_name: str, 
    custom_type: Optional[str] = None
) -> str:
    """
    Get the relationship type for a complex property field.
    
    Uses .NET-compatible naming convention for full interoperability.
    
    Args:
        field_name: The name of the property field.
        custom_type: Optional custom relationship type.
        
    Returns:
        The relationship type string compatible with .NET implementation.
    """
    if custom_type:
        return custom_type
    
    # Use the exact .NET GraphDataModel naming convention
    return GraphDataModel.property_name_to_relationship_type_name(field_name)


def _convert_enum_values(value: Any) -> Any:
    """Convert enum values to their string representation for Neo4j storage."""
    if hasattr(value, '__class__') and hasattr(value.__class__, '__bases__'):
        # Check if it's an enum
        for base in value.__class__.__bases__:
            if base.__name__ == 'Enum':
                return value.value
    
    if isinstance(value, (list, tuple)):
        return [_convert_enum_values(item) for item in value]
    elif isinstance(value, dict):
        return {k: _convert_enum_values(v) for k, v in value.items()}
    
    return value


@dataclass(frozen=True)
class SerializedNode:
    """
    Represents a serialized node in .NET-compatible format.
    
    This format matches the .NET EntityInfo structure for full interoperability.
    """
    
    id: str
    """The unique identifier of the node."""
    
    labels: List[str]
    """The labels assigned to the node."""
    
    properties: Dict[str, Any]
    """Simple properties that can be stored directly on the node."""
    
    complex_properties: Dict[str, Any]
    """Complex properties that need to be stored as separate nodes with relationships."""


@dataclass(frozen=True)
class SerializedRelationship:
    """
    Represents a serialized relationship in .NET-compatible format.
    
    This format matches the .NET EntityInfo structure for full interoperability.
    """
    
    id: str
    """The unique identifier of the relationship."""
    
    type: str
    """The type/label of the relationship."""
    
    start_node_id: str
    """The ID of the starting node."""
    
    end_node_id: str
    """The ID of the ending node."""
    
    properties: Dict[str, Any]
    """Simple properties that can be stored directly on the relationship."""
    
    complex_properties: Dict[str, Any]
    """Complex properties that need to be stored as separate nodes with relationships."""


class Neo4jSerializer:
    """
    Handles serialization and deserialization of graph entities to/from Neo4j.
    
    Uses .NET-compatible conventions for relationship naming and complex properties.
    Ensures full interoperability with .NET GraphModel implementation.
    """
    
    @staticmethod
    def serialize_node(node: INode) -> SerializedNode:
        """
        Serialize a node to Neo4j format with .NET compatibility.
        
        Args:
            node: The node to serialize.
            
        Returns:
            SerializedNode with properties and complex property metadata.
        """
        # Get node metadata
        node_type = type(node)
        labels = getattr(node_type, '__graph_labels__', [node_type.__name__])
        
        # Use GraphDataModel to separate simple and complex properties (.NET compatible)
        simple_props, complex_props = GraphDataModel.get_simple_and_complex_properties(node)
        
        # Process simple properties - convert enums and handle field metadata
        processed_simple = {}
        for field_name, field_value in simple_props.items():
            if field_value is None:
                continue
                
            field_info = get_field_info(type(node).model_fields[field_name])

            if field_info is None:
                # No field info - treat as simple property with field name as label
                processed_simple[field_name] = _convert_enum_values(field_value)
                continue
                
            if field_info.field_type == PropertyFieldType.SIMPLE:
                # Simple property - use label or field name
                prop_name = field_info.label or field_name
                processed_simple[prop_name] = _convert_enum_values(field_value)
                
            elif field_info.field_type == PropertyFieldType.EMBEDDED:
                # Embedded property - serialize as JSON (matches .NET behavior)
                prop_name = field_info.label or field_name
                if field_info.storage_type == "json":
                    processed_simple[prop_name] = json.dumps(_convert_enum_values(field_value), default=str)
                else:
                    processed_simple[prop_name] = _convert_enum_values(field_value)
        
        # Process complex properties - store metadata for relationship creation
        processed_complex = {}
        for field_name, field_value in complex_props.items():
            if field_value is None:
                continue
            
            field_info = get_field_info(type(node).model_fields[field_name])
            
            if field_info and field_info.field_type == PropertyFieldType.RELATED_NODE:
                # Related node property - store metadata for later processing
                processed_complex[field_name] = {
                    'value': _convert_enum_values(field_value),
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
            properties=processed_simple,
            complex_properties=processed_complex
        )
    
    @staticmethod
    def serialize_relationship(relationship: IRelationship) -> SerializedRelationship:
        """
        Serialize a relationship to Neo4j format with .NET compatibility.
        
        Args:
            relationship: The relationship to serialize.
            
        Returns:
            SerializedRelationship with properties and complex property metadata.
        """
        # Get relationship metadata
        rel_type = type(relationship)
        metadata = getattr(rel_type, '__graph_relationship_metadata__', None)
        if metadata:
            type_name = metadata['label']
        else:
            type_name = rel_type.__name__
        
        # Use GraphDataModel to separate simple and complex properties (.NET compatible)
        simple_props, complex_props = GraphDataModel.get_simple_and_complex_properties(relationship)
        
        # Process simple properties
        processed_simple = {}
        for field_name, field_value in simple_props.items():
            if field_value is None:
                continue
            
            field_info = get_field_info(type(relationship).model_fields[field_name])
            
            if field_info and field_info.field_type == PropertyFieldType.EMBEDDED:
                # Embedded property - serialize as JSON
                prop_name = field_info.label or field_name
                if field_info.storage_type == "json":
                    processed_simple[prop_name] = json.dumps(_convert_enum_values(field_value), default=str)
                else:
                    processed_simple[prop_name] = _convert_enum_values(field_value)
            else:
                # Simple property
                prop_name = field_info.label if field_info else field_name
                processed_simple[prop_name or field_name] = _convert_enum_values(field_value)
        
        # Process complex properties
        processed_complex = {}
        for field_name, field_value in complex_props.items():
            if field_value is None:
                continue
            
            field_info = get_field_info(type(relationship).model_fields[field_name])
            
            if field_info and field_info.field_type == PropertyFieldType.RELATED_NODE:
                processed_complex[field_name] = {
                    'value': _convert_enum_values(field_value),
                    'relationship_type': get_relationship_type_for_field(
                        field_name, 
                        field_info.relationship_type
                    ),
                    'private': field_info.private_relationship,
                    'field_info': field_info
                }
        
        return SerializedRelationship(
            id=relationship.id,
            type=type_name,
            start_node_id=relationship.start_node_id,
            end_node_id=relationship.end_node_id,
            properties=processed_simple,
            complex_properties=processed_complex
        )
    
    @staticmethod
    def deserialize_node(
        record: Record, 
        node_type: Type[INode],
        complex_properties: Optional[Dict[str, Any]] = None
    ) -> INode:
        """
        Deserialize a Neo4j record to a node with .NET compatibility.
        
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
        
        # Deserialize embedded JSON properties
        for field_name, field_info in node_type.model_fields.items():
            if field_name in node_data:
                field_meta = get_field_info(field_info)
                if (field_meta and 
                    field_meta.field_type == PropertyFieldType.EMBEDDED and 
                    field_meta.storage_type == "json" and
                    isinstance(node_data[field_name], str)):
                    try:
                        node_data[field_name] = json.loads(node_data[field_name])
                    except (json.JSONDecodeError, TypeError):
                        # If JSON parsing fails, keep the original value
                        pass
        
        # Create node instance
        return node_type(**node_data)
    
    @staticmethod
    def deserialize_relationship(
        record: Record, 
        relationship_type: Type[IRelationship]
    ) -> IRelationship:
        """
        Deserialize a Neo4j record to a relationship with .NET compatibility.
        
        Args:
            record: The Neo4j record containing relationship data.
            relationship_type: The type of relationship to deserialize to.
            
        Returns:
            The deserialized relationship.
        """
        # Extract relationship properties from record
        rel_data = dict(record)
        
        # Handle embedded properties
        for field_name, field_info in relationship_type.model_fields.items():
            if field_name in rel_data:
                field_meta = get_field_info(field_info)
                if (field_meta and 
                    field_meta.field_type == PropertyFieldType.EMBEDDED and 
                    field_meta.storage_type == "json" and
                    isinstance(rel_data[field_name], str)):
                    try:
                        rel_data[field_name] = json.loads(rel_data[field_name])
                    except (json.JSONDecodeError, TypeError):
                        # If JSON parsing fails, keep the original value
                        pass
        
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
        Generate Cypher for loading complex properties with .NET compatibility.
        
        Uses the same pattern as .NET CypherQueryBuilder for complex property loading.
        
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
            
        # Use the .NET pattern for complex property loading
        return f"""
        OPTIONAL MATCH ({parent_alias})-[{field_name}_rel:{relationship_type}]->({target_alias})
        WHERE type({field_name}_rel) STARTS WITH '{GraphDataModel.PROPERTY_RELATIONSHIP_TYPE_NAME_PREFIX}'
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