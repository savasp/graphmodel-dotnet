"""
Cypher query builder for translating queryable expressions to Neo4j Cypher.

This module handles the translation of LINQ-style queryable operations
to Cypher queries, including complex property loading and .NET compatibility.
"""

import ast
import inspect
from dataclasses import dataclass
from typing import Any, Callable, Dict, List, Optional, Type

from ...attributes.fields import (
    PropertyFieldType,
    get_field_info,
    get_relationship_type_for_field,
)
from ...core.graph import GraphDataModel
from ...core.node import INode
from ...core.relationship import IRelationship


@dataclass
class CypherQuery:
    """Represents a complete Cypher query with parameters."""
    
    query: str
    parameters: Dict[str, Any]
    
    def __str__(self) -> str:
        return f"Query: {self.query}\nParameters: {self.parameters}"


class CypherBuilder:
    """
    Builds Cypher queries from queryable expressions.
    
    Handles WHERE clauses, ORDER BY, LIMIT, SKIP, and complex property loading
    using .NET-compatible conventions.
    """
    
    def __init__(self, node_type: Type[INode]):
        """Initialize the Cypher builder for a specific node type."""
        self.node_type = node_type
        self.node_alias = "n"
        self.labels = self._get_node_labels()
        self.complex_properties = self._get_complex_properties()
    
    def build_query(
        self,
        where_predicate: Optional[Callable] = None,
        order_by_key: Optional[Callable] = None,
        order_descending: bool = False,
        take_count: Optional[int] = None,
        skip_count: Optional[int] = None,
        include_complex_properties: bool = True,
        traversal_relationship: Optional[str] = None,
        traversal_target_type: Optional[Type] = None,
        select_projection: Optional[Callable] = None
    ) -> CypherQuery:
        """
        Build a complete Cypher query with all specified operations.
        
        Args:
            where_predicate: Lambda function for WHERE clause.
            order_by_key: Lambda function for ORDER BY clause.
            order_descending: Whether to order descending.
            take_count: Number of results to take (LIMIT).
            skip_count: Number of results to skip (SKIP).
            include_complex_properties: Whether to load complex properties.
            traversal_relationship: Relationship type for traversal.
            traversal_target_type: Target node type for traversal.
            select_projection: Lambda function for projection.
            
        Returns:
            CypherQuery with the complete query and parameters.
        """
        query_parts = []
        parameters = {}
        
        # Start with MATCH clause
        if traversal_relationship and traversal_target_type:
            target_label = traversal_target_type.__name__
            query_parts.append(f"MATCH ({self.node_alias}:{':'.join(self.labels)})-[r:{traversal_relationship}]->(target:{target_label})")
        else:
            labels_str = ':'.join(self.labels)
            query_parts.append(f"MATCH ({self.node_alias}:{labels_str})")
        
        # Add WHERE clause
        if where_predicate:
            where_clause, where_params = self._build_where_clause(where_predicate)
            if where_clause and where_clause != "WHERE 1=1":
                # Ensure WHERE prefix
                if not where_clause.strip().upper().startswith("WHERE"):
                    where_clause = f"WHERE {where_clause}"
                query_parts.append(where_clause)
            parameters.update(where_params)
        
        # Add complex property loading
        if include_complex_properties and self.complex_properties:
            complex_clause = self._build_complex_property_clause()
            query_parts.append(complex_clause)
        
        # Add WITH clause for complex properties
        if include_complex_properties and self.complex_properties:
            with_clause = self._build_with_clause()
            query_parts.append(with_clause)
        
        # Add ORDER BY clause
        if order_by_key:
            order_clause = self._build_order_by_clause(order_by_key, order_descending)
            query_parts.append(order_clause)
        
        # Add SKIP clause
        if skip_count is not None:
            query_parts.append(f"SKIP {skip_count}")
        
        # Add LIMIT clause
        if take_count is not None:
            query_parts.append(f"LIMIT {take_count}")
        
        # Add RETURN clause
        if select_projection:
            return_clause = self._build_projection_return_clause(select_projection)
        else:
            return_clause = self._build_return_clause(include_complex_properties)
        query_parts.append(return_clause)
        
        # Combine all parts
        query = "\n".join(query_parts)
        
        return CypherQuery(query, parameters)
    
    def _get_node_labels(self) -> List[str]:
        """Get the labels for the node type."""
        labels = getattr(self.node_type, '__graph_labels__', [])
        if not labels:
            labels = [self.node_type.__name__]
        return labels
    
    def _get_complex_properties(self) -> Dict[str, Dict[str, Any]]:
        """Get complex properties that need special handling."""
        complex_props = {}
        
        for field_name, field in self.node_type.model_fields.items():
            field_info = get_field_info(field)
            if field_info and field_info.field_type == PropertyFieldType.RELATED_NODE:
                complex_props[field_name] = {
                    'relationship_type': get_relationship_type_for_field(
                        field_name, 
                        field_info.relationship_type
                    ),
                    'private': field_info.private_relationship,
                    'field_info': field_info
                }
        
        return complex_props
    
    @staticmethod
    def extract_lambda_source(func: Callable) -> str:
        import re
        lines, _ = inspect.getsourcelines(func)
        source = ''.join(lines)
        lambda_pattern = r'lambda\s+\w+\s*:\s*[^)]+'
        match = re.search(lambda_pattern, source)
        if match:
            return match.group(0)
        idx = source.find('lambda')
        if idx != -1:
            end_chars = [',', ')', '\n', ';']
            end_idx = len(source)
            for char in end_chars:
                pos = source.find(char, idx)
                if pos != -1 and pos < end_idx:
                    end_idx = pos
            return source[idx:end_idx].strip()
        return source.strip()
    
    def _extract_lambda_source(self, func: Callable) -> str:
        return CypherBuilder.extract_lambda_source(func)
    
    def _build_where_clause(self, predicate: Callable) -> tuple[str, Dict[str, Any]]:
        try:
            source = self._extract_lambda_source(predicate)
            tree = ast.parse(source)
            lambda_node = next((n for n in ast.walk(tree) if isinstance(n, ast.Lambda)), None)
            if not lambda_node:
                return "WHERE 1=1", {}
            return self._parse_expression(lambda_node.body, {}, top_level=True)
        except Exception:
            return "WHERE 1=1", {}
    
    def _parse_expression(self, node: ast.AST, parameters: Dict[str, Any], top_level: bool = True) -> tuple[str, Dict[str, Any]]:
        """Parse an AST expression into Cypher."""
        if isinstance(node, ast.Compare):
            return self._parse_comparison(node, parameters)
        elif isinstance(node, ast.BoolOp):
            return self._parse_bool_op(node, parameters, top_level=top_level)
        elif isinstance(node, ast.NameConstant) and node.value is None:
            return "IS NULL", parameters
        else:
            return "1=1", parameters
    
    def _parse_comparison(self, node: ast.Compare, parameters: Dict[str, Any]) -> tuple[str, Dict[str, Any]]:
        """Parse a comparison expression."""
        if len(node.ops) != 1 or len(node.comparators) != 1:
            return "1=1", parameters
        
        left = node.left
        op = node.ops[0]
        right = node.comparators[0]
        
        # Handle left side (should be n.field)
        if not isinstance(left, ast.Attribute) or not isinstance(left.value, ast.Name):
            return "1=1", parameters
        
        field_name = left.attr
        node_alias = self.node_alias
        
        # Handle right side (should be a constant or variable)
        if isinstance(right, ast.Constant):
            param_name = f"{field_name}_{len(parameters)}"
            parameters[param_name] = right.value
            cypher_op = self._get_cypher_operator(op)
            return f"{node_alias}.{field_name} {cypher_op} ${param_name}", parameters
        elif isinstance(right, ast.NameConstant) and right.value is None:
            cypher_op = self._get_cypher_operator(op)
            return f"{node_alias}.{field_name} {cypher_op} NULL", parameters
        else:
            return "1=1", parameters
    
    def _parse_bool_op(self, node: ast.BoolOp, parameters: Dict[str, Any], top_level: bool = True) -> tuple[str, Dict[str, Any]]:
        """Parse a boolean operation (AND/OR)."""
        if len(node.values) < 2:
            return "1=1", parameters
        
        expressions = []
        for value in node.values:
            if isinstance(value, ast.BoolOp):
                expr, new_params = self._parse_bool_op(value, parameters.copy(), top_level=False)
            else:
                expr, new_params = self._parse_expression(value, parameters.copy())
            expressions.append(expr)
            parameters.update(new_params)
        
        operator = "AND" if isinstance(node.op, ast.And) else "OR"
        joined = f' {operator} '.join(expressions)
        if top_level:
            return joined, parameters
        else:
            return f'({joined})', parameters
    
    def _get_cypher_operator(self, op: ast.cmpop) -> str:
        """Convert Python comparison operator to Cypher operator."""
        if isinstance(op, ast.Eq):
            return "="
        elif isinstance(op, ast.NotEq):
            return "<>"
        elif isinstance(op, ast.Lt):
            return "<"
        elif isinstance(op, ast.LtE):
            return "<="
        elif isinstance(op, ast.Gt):
            return ">"
        elif isinstance(op, ast.GtE):
            return ">="
        else:
            return "="
    
    def _build_complex_property_clause(self) -> str:
        """Build Cypher clause for loading complex properties."""
        clauses = []
        
        for field_name, complex_data in self.complex_properties.items():
            relationship_type = complex_data['relationship_type']
            target_alias = f"{field_name}_node"
            rel_alias = f"{field_name}_rel"
            
            clause = f"""
            OPTIONAL MATCH ({self.node_alias})-[{rel_alias}:{relationship_type}]->({target_alias})
            """
            clauses.append(clause)
        
        return "\n".join(clauses)
    
    def _build_with_clause(self) -> str:
        """Build WITH clause to collect complex properties."""
        if not self.complex_properties:
            return f"WITH {self.node_alias}"
        
        # Build complex property collection
        complex_collections = []
        for field_name in self.complex_properties.keys():
            target_alias = f"{field_name}_node"
            complex_collections.append(f"{target_alias} AS {field_name}")
        
        complex_part = ", ".join(complex_collections)
        return f"WITH {self.node_alias}, {complex_part}"
    
    def _build_order_by_clause(self, key_selector: Callable, descending: bool = False) -> str:
        source = self._extract_lambda_source(key_selector)
        tree = ast.parse(source)
        lambda_node = next((n for n in ast.walk(tree) if isinstance(n, ast.Lambda)), None)
        if not lambda_node or not isinstance(lambda_node.body, ast.Attribute):
            return ""
        field_name = lambda_node.body.attr
        order_direction = "DESC" if descending else "ASC"
        return f"ORDER BY {self.node_alias}.{field_name} {order_direction}"
    
    def _build_return_clause(self, include_complex_properties: bool = True) -> str:
        """Build RETURN clause."""
        if not include_complex_properties or not self.complex_properties:
            return f"RETURN {self.node_alias}"
        
        # Include complex properties in return
        complex_parts = []
        for field_name in self.complex_properties.keys():
            target_alias = f"{field_name}_node"
            complex_parts.append(f"{field_name}: {target_alias}")
        
        complex_part = ", ".join(complex_parts)
        return f"RETURN {self.node_alias}, {complex_part}"
    
    def _build_projection_return_clause(self, projection: Callable) -> str:
        """Build RETURN clause for projections."""
        try:
            source = self._extract_lambda_source(projection)
            tree = ast.parse(source)
            lambda_node = next((n for n in ast.walk(tree) if isinstance(n, ast.Lambda)), None)
            if not lambda_node or not isinstance(lambda_node.body, ast.Dict):
                return f"RETURN {self.node_alias}"
            
            # Extract key-value pairs from the dict
            keys = []
            values = []
            for key, value in zip(lambda_node.body.keys, lambda_node.body.values):
                if isinstance(key, ast.Constant):
                    keys.append(key.value)
                if isinstance(value, ast.Attribute) and isinstance(value.value, ast.Name):
                    values.append(f"{self.node_alias}.{value.attr}")
            
            if keys and values:
                projections = [f"{val} AS {key}" for key, val in zip(keys, values)]
                return f"RETURN {', '.join(projections)}"
            
            return f"RETURN {self.node_alias}"
        except Exception:
            return f"RETURN {self.node_alias}"
    
    def build_count_query(self, where_predicate: Optional[Callable] = None) -> CypherQuery:
        """Build a COUNT query."""
        query_parts = []
        parameters = {}
        
        # Start with MATCH clause
        labels_str = ':'.join(self.labels)
        query_parts.append(f"MATCH ({self.node_alias}:{labels_str})")
        
        # Add WHERE clause
        if where_predicate:
            where_clause, where_params = self._build_where_clause(where_predicate)
            query_parts.append(where_clause)
            parameters.update(where_params)
        
        # Add RETURN clause
        query_parts.append(f"RETURN count({self.node_alias}) as count")
        
        query = "\n".join(query_parts)
        return CypherQuery(query, parameters)
    
    def build_exists_query(self, where_predicate: Optional[Callable] = None) -> CypherQuery:
        """Build an EXISTS query."""
        query_parts = []
        parameters = {}
        
        # Start with MATCH clause
        labels_str = ':'.join(self.labels)
        query_parts.append(f"MATCH ({self.node_alias}:{labels_str})")
        
        # Add WHERE clause
        if where_predicate:
            where_clause, where_params = self._build_where_clause(where_predicate)
            query_parts.append(where_clause)
            parameters.update(where_params)
        
        # Add RETURN clause
        query_parts.append(f"RETURN count({self.node_alias}) > 0 as exists")
        
        query = "\n".join(query_parts)
        return CypherQuery(query, parameters)


class RelationshipCypherBuilder:
    """
    Builds Cypher queries for relationships.
    """
    
    def __init__(self, relationship_type: Type[IRelationship]):
        """Initialize the Cypher builder for a specific relationship type."""
        self.relationship_type = relationship_type
        self.rel_alias = "r"
        self.rel_type = self._get_relationship_type()
    
    def _get_relationship_type(self) -> str:
        """Get the relationship type."""
        # Check for relationship metadata first
        metadata = getattr(self.relationship_type, '__graph_relationship_metadata__', None)
        if metadata and 'label' in metadata:
            return metadata['label'].upper()
        
        # Fallback to __graph_label__ for backward compatibility
        rel_type = getattr(self.relationship_type, '__graph_label__', None)
        if not rel_type:
            rel_type = self.relationship_type.__name__
        # Preserve underscores but make uppercase for Cypher
        return rel_type.upper()
    
    def build_query(
        self,
        where_predicate: Optional[Callable] = None,
        order_by_key: Optional[Callable] = None,
        order_descending: bool = False,
        take_count: Optional[int] = None,
        skip_count: Optional[int] = None,
        select_projection: Optional[Callable] = None
    ) -> CypherQuery:
        """Build a complete Cypher query for relationships."""
        query_parts = []
        parameters = {}
        
        # Start with MATCH clause
        query_parts.append(f"MATCH ()-[{self.rel_alias}:{self.rel_type}]->()")
        
        # Add WHERE clause
        if where_predicate:
            where_clause, where_params = self._build_where_clause(where_predicate)
            if where_clause and where_clause != "WHERE 1=1":
                if not where_clause.strip().upper().startswith("WHERE"):
                    where_clause = f"WHERE {where_clause}"
                query_parts.append(where_clause)
            parameters.update(where_params)
        
        # Add ORDER BY clause
        if order_by_key:
            order_clause = self._build_order_by_clause(order_by_key, order_descending)
            query_parts.append(order_clause)
        
        # Add SKIP clause
        if skip_count is not None:
            query_parts.append(f"SKIP {skip_count}")
        
        # Add LIMIT clause
        if take_count is not None:
            query_parts.append(f"LIMIT {take_count}")
        
        # Add RETURN clause
        if select_projection:
            return_clause = self._build_relationship_projection_return_clause(select_projection)
            query_parts.append(return_clause)
        else:
            query_parts.append(f"RETURN {self.rel_alias}")
        
        query = "\n".join(query_parts)
        return CypherQuery(query, parameters)
    
    def _build_where_clause(self, predicate: Callable) -> tuple[str, Dict[str, Any]]:
        """
        Build WHERE clause from a lambda predicate.
        Supports:
        - Simple field equality: lambda r: r.field == value
        - Comparisons: lambda r: r.field > value, lambda r: r.field < value, etc.
        - Logical AND: lambda r: r.field1 == value1 and r.field2 == value2
        - Logical OR: lambda r: r.field1 == value1 or r.field2 == value2
        """
        try:
            source = inspect.getsource(predicate).strip()
            if source.startswith('@'):
                source = source.split('\n', 1)[1]
            tree = ast.parse(source)
            
            lambda_node = next((n for n in ast.walk(tree) if isinstance(n, ast.Lambda)), None)
            if not lambda_node:
                return "WHERE 1=1", {}
            
            return self._parse_expression(lambda_node.body, {}, top_level=True)
        except Exception:
            return "WHERE 1=1", {}
    
    def _parse_expression(self, node: ast.AST, parameters: Dict[str, Any], top_level: bool = True) -> tuple[str, Dict[str, Any]]:
        """Parse an AST expression into Cypher."""
        if isinstance(node, ast.Compare):
            return self._parse_comparison(node, parameters)
        elif isinstance(node, ast.BoolOp):
            return self._parse_bool_op(node, parameters, top_level=top_level)
        elif isinstance(node, ast.NameConstant) and node.value is None:
            return "IS NULL", parameters
        else:
            return "1=1", parameters
    
    def _parse_comparison(self, node: ast.Compare, parameters: Dict[str, Any]) -> tuple[str, Dict[str, Any]]:
        """Parse a comparison expression."""
        if len(node.ops) != 1 or len(node.comparators) != 1:
            return "1=1", parameters
        
        left = node.left
        op = node.ops[0]
        right = node.comparators[0]
        
        # Handle left side (should be r.field)
        if not isinstance(left, ast.Attribute) or not isinstance(left.value, ast.Name):
            return "1=1", parameters
        
        field_name = left.attr
        rel_alias = self.rel_alias
        
        # Handle right side (should be a constant or variable)
        if isinstance(right, ast.Constant):
            param_name = f"{field_name}_{len(parameters)}"
            parameters[param_name] = right.value
            cypher_op = self._get_cypher_operator(op)
            return f"{rel_alias}.{field_name} {cypher_op} ${param_name}", parameters
        elif isinstance(right, ast.NameConstant) and right.value is None:
            cypher_op = self._get_cypher_operator(op)
            return f"{rel_alias}.{field_name} {cypher_op} NULL", parameters
        else:
            return "1=1", parameters
    
    def _parse_bool_op(self, node: ast.BoolOp, parameters: Dict[str, Any], top_level: bool = True) -> tuple[str, Dict[str, Any]]:
        """Parse a boolean operation (AND/OR)."""
        if len(node.values) < 2:
            return "1=1", parameters
        
        expressions = []
        for value in node.values:
            if isinstance(value, ast.BoolOp):
                expr, new_params = self._parse_bool_op(value, parameters.copy(), top_level=False)
            else:
                expr, new_params = self._parse_expression(value, parameters.copy())
            expressions.append(expr)
            parameters.update(new_params)
        
        operator = "AND" if isinstance(node.op, ast.And) else "OR"
        joined = f' {operator} '.join(expressions)
        if top_level:
            return joined, parameters
        else:
            return f'({joined})', parameters
    
    def _get_cypher_operator(self, op: ast.cmpop) -> str:
        """Convert Python comparison operator to Cypher operator."""
        if isinstance(op, ast.Eq):
            return "="
        elif isinstance(op, ast.NotEq):
            return "<>"
        elif isinstance(op, ast.Lt):
            return "<"
        elif isinstance(op, ast.LtE):
            return "<="
        elif isinstance(op, ast.Gt):
            return ">"
        elif isinstance(op, ast.GtE):
            return ">="
        else:
            return "="
    
    def _build_order_by_clause(self, key_selector: Callable, descending: bool = False) -> str:
        source = inspect.getsource(key_selector).strip()
        if source.startswith('@'):
            source = source.split('\n', 1)[1]
        tree = ast.parse(source)
        lambda_node = next((n for n in ast.walk(tree) if isinstance(n, ast.Lambda)), None)
        if not lambda_node or not isinstance(lambda_node.body, ast.Attribute):
            return ""
        field_name = lambda_node.body.attr
        order_direction = "DESC" if descending else "ASC"
        return f"ORDER BY {self.rel_alias}.{field_name} {order_direction}"
    
    def _build_relationship_projection_return_clause(self, projection: Callable) -> str:
        """Build RETURN clause for relationship projections."""
        try:
            source = CypherBuilder.extract_lambda_source(projection)
            tree = ast.parse(source)
            lambda_node = next((n for n in ast.walk(tree) if isinstance(n, ast.Lambda)), None)
            if not lambda_node or not isinstance(lambda_node.body, ast.Dict):
                return f"RETURN {self.rel_alias}"
            keys = []
            values = []
            for key, value in zip(lambda_node.body.keys, lambda_node.body.values):
                if isinstance(key, ast.Constant):
                    keys.append(key.value)
                if isinstance(value, ast.Attribute) and isinstance(value.value, ast.Name):
                    values.append(f"{self.rel_alias}.{value.attr}")
            print(f"DEBUG: projection keys={keys}, values={values}")
            if keys and values:
                projections = [f"{val} AS {key}" for key, val in zip(keys, values)]
                return f"RETURN {', '.join(projections)}"
            return f"RETURN {self.rel_alias}"
        except Exception as e:
            print(f"DEBUG: projection error: {e}")
            return f"RETURN {self.rel_alias}"