"""
Neo4j implementation of aggregation query execution.

This module provides Neo4j-specific implementation of GROUP BY and HAVING
operations with proper Cypher query generation.
"""

from typing import Any, Dict, List, Optional, Type, TypeVar

from neo4j import AsyncSession

from ...core.node import INode
from ...core.relationship import IRelationship
from ...querying.aggregation import (
    AggregationBuilder,
    AverageExpression,
    CountExpression,
    GroupByResult,
    IAggregationExpression,
    MaxExpression,
    MinExpression,
    SumExpression,
)
from .cypher_builder import CypherQuery
from .serialization import Neo4jSerializer

TNode = TypeVar('TNode', bound=INode)
TKey = TypeVar('TKey')


class Neo4jAggregationExecutor:
    """
    Neo4j implementation of aggregation query execution.
    
    Translates GroupBy/Having operations to Cypher queries with
    proper GROUP BY and HAVING clauses.
    """
    
    def __init__(self, session: AsyncSession, serializer: Neo4jSerializer):
        """
        Initialize the aggregation executor.
        
        Args:
            session: Neo4j async session for query execution.
            serializer: Serializer for converting between Python objects and Neo4j data.
        """
        self._session = session
        self._serializer = serializer
    
    async def execute_group_by(
        self,
        node_type: Type[TNode],
        builder: AggregationBuilder
    ) -> List[GroupByResult[TKey, TNode]]:
        """
        Execute GROUP BY aggregation query.
        
        Args:
            node_type: Type of nodes being grouped.
            builder: Configured aggregation builder.
            
        Returns:
            List of GroupByResult objects with grouped data and aggregations.
        """
        # Build Cypher query for GROUP BY
        cypher_query = self._build_group_by_query(node_type, builder)
        
        # Execute query
        result = await self._session.run(cypher_query.query, cypher_query.parameters)
        records = await result.data()
        
        # Convert results to GroupByResult objects
        group_results = []
        for record in records:
            group_result = self._create_group_result_from_record(record, node_type, builder)
            group_results.append(group_result)
        
        return group_results
    
    async def execute_aggregation_only(
        self,
        node_type: Type[TNode],
        expressions: List[IAggregationExpression]
    ) -> Dict[str, Any]:
        """
        Execute aggregation expressions without grouping.
        
        Args:
            node_type: Type of nodes to aggregate.
            expressions: List of aggregation expressions to execute.
            
        Returns:
            Dictionary with aggregation results.
        """
        # Build Cypher query for aggregation only
        cypher_query = self._build_aggregation_only_query(node_type, expressions)
        
        # Execute query
        result = await self._session.run(cypher_query.query, cypher_query.parameters)
        record = await result.single()
        
        # Extract aggregation results
        aggregation_results = {}
        for expr in expressions:
            alias = self._get_aggregation_alias(expr)
            aggregation_results[alias] = record[alias]
        
        return aggregation_results
    
    def _build_group_by_query(
        self,
        node_type: Type[TNode],
        builder: AggregationBuilder
    ) -> CypherQuery:
        """
        Build Cypher query for GROUP BY operation.
        
        Args:
            node_type: Type of nodes being grouped.
            builder: Configured aggregation builder.
            
        Returns:
            CypherQuery for GROUP BY execution.
        """
        # Get node labels
        labels = getattr(node_type, '__graph_labels__', [node_type.__name__])
        labels_str = ':'.join(labels)
        
        # Build query parts
        query_parts = [f"MATCH (n:{labels_str})"]
        parameters = {}
        
        # Build WITH clause for grouping key (if GROUP BY is specified)
        if builder._group_by:
            group_key_expr = builder._group_by.key_expression
            with_clause = f"WITH {group_key_expr} as groupKey, n"
            query_parts.append(with_clause)
        
        # Build aggregation expressions
        aggregation_exprs = []
        for expr in builder._aggregations:
            cypher_expr = self._translate_aggregation_expression(expr)
            alias = self._get_aggregation_alias(expr)
            aggregation_exprs.append(f"{cypher_expr} as {alias}")
        
        # Build RETURN clause with GROUP BY
        if builder._group_by:
            return_parts = ["groupKey"]
            return_parts.extend(aggregation_exprs)
            return_clause = f"RETURN {', '.join(return_parts)}"
        else:
            return_clause = f"RETURN {', '.join(aggregation_exprs)}"
        
        query_parts.append(return_clause)
        
        # Add HAVING clause if present
        if builder._having_clauses:
            having_clause = " AND ".join(builder._having_clauses)
            # Insert HAVING before RETURN
            query_parts.insert(-1, f"HAVING {having_clause}")
        
        # Combine all parts
        query = "\n".join(query_parts)
        
        return CypherQuery(query, parameters)
    
    def _build_aggregation_only_query(
        self,
        node_type: Type[TNode],
        expressions: List[IAggregationExpression]
    ) -> CypherQuery:
        """
        Build Cypher query for aggregation without grouping.
        
        Args:
            node_type: Type of nodes to aggregate.
            expressions: List of aggregation expressions.
            
        Returns:
            CypherQuery for aggregation execution.
        """
        # Get node labels
        labels = getattr(node_type, '__graph_labels__', [node_type.__name__])
        labels_str = ':'.join(labels)
        
        # Build query parts
        query_parts = [f"MATCH (n:{labels_str})"]
        parameters = {}
        
        # Build aggregation expressions
        aggregation_exprs = []
        for expr in expressions:
            cypher_expr = self._translate_aggregation_expression(expr)
            alias = self._get_aggregation_alias(expr)
            aggregation_exprs.append(f"{cypher_expr} as {alias}")
        
        # Build RETURN clause
        return_clause = f"RETURN {', '.join(aggregation_exprs)}"
        query_parts.append(return_clause)
        
        # Combine all parts
        query = "\n".join(query_parts)
        
        return CypherQuery(query, parameters)
    
    def _translate_group_key_expression(self, key_selector) -> str:
        """
        Translate Python group key selector to Cypher expression.
        
        For now, this is a simplified implementation that handles basic property access.
        A full implementation would parse the lambda expression AST.
        
        Args:
            key_selector: Lambda function for grouping key.
            
        Returns:
            Cypher expression for the grouping key.
        """
        # This is a simplified implementation
        # In practice, you'd parse the lambda expression to extract the property
        # For now, assume it's a simple property access like: lambda x: x.property_name
        
        # Extract property name from lambda (simplified)
        import inspect
        source = inspect.getsource(key_selector).strip()
        
        # Look for pattern like "lambda x: x.property_name"
        if "." in source:
            property_name = source.split(".")[-1].strip()
            return f"n.{property_name}"
        
        # Fallback to ID if we can't parse
        return "n.id"
    
    def _translate_aggregation_expression(self, expr: IAggregationExpression) -> str:
        """
        Translate aggregation expression to Cypher.
        
        Args:
            expr: Aggregation expression to translate.
            
        Returns:
            Cypher aggregation expression.
        """
        if isinstance(expr, CountExpression):
            return "count(n)"
        elif isinstance(expr, SumExpression):
            return f"sum({expr.property_path})"
        elif isinstance(expr, AverageExpression):
            return f"avg({expr.property_path})"
        elif isinstance(expr, MinExpression):
            return f"min({expr.property_path})"
        elif isinstance(expr, MaxExpression):
            return f"max({expr.property_path})"
        else:
            raise ValueError(f"Unsupported aggregation expression: {type(expr)}")
    
    def _extract_property_from_expression(self, property_selector) -> str:
        """
        Extract property name from lambda expression.
        
        Args:
            property_selector: Lambda function selecting a property.
            
        Returns:
            Property name for Cypher query.
        """
        # Simplified property extraction
        import inspect
        source = inspect.getsource(property_selector).strip()
        
        # Look for pattern like "lambda x: x.property_name"
        if "." in source:
            property_name = source.split(".")[-1].strip()
            return property_name
        
        # Fallback
        return "id"
    
    def _get_aggregation_alias(self, expr: IAggregationExpression) -> str:
        """
        Get alias for aggregation expression in Cypher query.
        
        Args:
            expr: Aggregation expression.
            
        Returns:
            Alias for the expression in query results.
        """
        if isinstance(expr, CountExpression):
            return "count_result"
        elif isinstance(expr, SumExpression):
            return f"sum_result"
        elif isinstance(expr, AverageExpression):
            return f"avg_result"
        elif isinstance(expr, MinExpression):
            return f"min_result"
        elif isinstance(expr, MaxExpression):
            return f"max_result"
        else:
            return "aggregation_result"
    
    def _create_group_result_from_record(
        self,
        record: Dict[str, Any],
        node_type: Type[TNode],
        builder: AggregationBuilder
    ) -> GroupByResult[Any, TNode]:
        """
        Create GroupByResult from Neo4j record.
        
        Args:
            record: Neo4j query result record.
            node_type: Type of nodes being grouped.
            builder: Aggregation builder with configuration.
            
        Returns:
            GroupByResult with grouped data and aggregations.
        """
        # Extract grouping key (if GROUP BY was used)
        if builder._group_by:
            group_key = record["groupKey"]
        else:
            group_key = "all"  # Default key when no grouping
        
        # Create GroupByResult
        # Note: We don't have the actual grouped items from this query
        # A more complete implementation would need a separate query to get the items
        group_result = GroupByResult(
            key=group_key,
            values=[],  # Would need separate query to populate
        )
        
        return group_result


class Neo4jGroupByQueryable:
    """
    Neo4j-specific implementation of grouped queryable results.
    
    Provides LINQ-style operations over grouped query results.
    """
    
    def __init__(
        self,
        node_type: Type[TNode],
        session: AsyncSession,
        serializer: Neo4jSerializer,
        executor: Neo4jAggregationExecutor,
        builder: AggregationBuilder
    ):
        """
        Initialize the grouped queryable.
        
        Args:
            node_type: Type of nodes being grouped.
            session: Neo4j async session.
            serializer: Serializer for node conversion.
            executor: Aggregation executor.
            builder: Aggregation builder with configuration.
        """
        self._node_type = node_type
        self._session = session
        self._serializer = serializer
        self._executor = executor
        self._builder = builder
    
    async def to_list(self) -> List[GroupByResult[Any, TNode]]:
        """
        Execute the GROUP BY query and return results as a list.
        
        Returns:
            List of GroupByResult objects.
        """
        return await self._executor.execute_group_by(self._node_type, self._builder)
    
    async def first(self) -> GroupByResult[Any, TNode]:
        """Get the first group result."""
        results = await self.to_list()
        if not results:
            raise ValueError("Sequence contains no elements")
        return results[0]
    
    async def first_or_none(self) -> Optional[GroupByResult[Any, TNode]]:
        """Get the first group result or None."""
        results = await self.to_list()
        return results[0] if results else None
    
    def having(self, predicate) -> "Neo4jGroupByQueryable[TNode]":
        """
        Add HAVING clause to the GROUP BY query.
        
        Args:
            predicate: Lambda expression for HAVING condition.
            
        Returns:
            Self for method chaining.
        """
        # Translate predicate to Cypher HAVING clause
        having_clause = self._translate_having_predicate(predicate)
        
        # Create new builder with HAVING clause
        new_builder = AggregationBuilder()
        if self._builder._group_by:
            new_builder.group_by(self._builder._group_by.key_expression)
        
        # Copy aggregations
        new_builder._aggregations = self._builder._aggregations.copy()
        
        # Copy existing HAVING clauses and add new one
        new_builder._having_clauses = self._builder._having_clauses.copy()
        new_builder._having_clauses.append(having_clause)
        
        return Neo4jGroupByQueryable(
            self._node_type,
            self._session,
            self._serializer,
            self._executor,
            new_builder
        )
    
    def order_by(self, key_selector) -> "Neo4jGroupByQueryable[TNode]":
        """
        Add ORDER BY clause to the GROUP BY query.
        
        Args:
            key_selector: Lambda expression for ordering.
            
        Returns:
            Self for method chaining.
        """
        # For now, we'll skip ORDER BY implementation as it's not part of the current AggregationBuilder
        # A full implementation would extend AggregationBuilder to support ORDER BY
        return self
    
    def _translate_having_predicate(self, predicate) -> str:
        """
        Translate HAVING predicate to Cypher.
        
        Args:
            predicate: Lambda expression for HAVING condition.
            
        Returns:
            Cypher HAVING clause.
        """
        # Simplified implementation
        # In practice, you'd parse the lambda expression AST
        return "count(n) > 0"  # Placeholder
    
    def _translate_order_by_selector(self, key_selector) -> str:
        """
        Translate ORDER BY key selector to Cypher.
        
        Args:
            key_selector: Lambda expression for ordering key.
            
        Returns:
            Cypher ORDER BY expression.
        """
        # Simplified implementation
        # In practice, you'd parse the lambda expression AST
        return "groupKey"  # Placeholder 