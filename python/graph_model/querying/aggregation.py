"""
Aggregation and GroupBy functionality for graph queries.

This module provides LINQ-style aggregation operations that match the .NET GraphModel implementation.
"""

from abc import ABC, abstractmethod
from dataclasses import dataclass
from typing import Any, Callable, Dict, Generic, List, Optional, TypeVar, Union

T = TypeVar('T')
K = TypeVar('K')
V = TypeVar('V')


@dataclass(frozen=True)
class GroupByResult(Generic[K, V]):
    """
    Represents a grouped result similar to .NET's IGrouping<TKey, TElement>.
    
    This matches the .NET IGrouping interface for compatibility.
    """
    
    key: K
    """The key that the group was formed by."""
    
    values: List[V]
    """The collection of values in this group."""
    
    def count(self) -> int:
        """Get the count of items in this group."""
        return len(self.values)
    
    def sum(self, selector: Optional[Callable[[V], Union[int, float]]] = None) -> Union[int, float]:
        """
        Calculate the sum of values in this group.
        
        Args:
            selector: Optional function to extract numeric values from items.
            
        Returns:
            The sum of the values.
        """
        if selector:
            return sum(selector(item) for item in self.values)
        return sum(self.values)
    
    def average(self, selector: Optional[Callable[[V], Union[int, float]]] = None) -> float:
        """
        Calculate the average of values in this group.
        
        Args:
            selector: Optional function to extract numeric values from items.
            
        Returns:
            The average of the values.
        """
        if not self.values:
            raise ValueError("Cannot calculate average of empty group")
            
        if selector:
            values = [selector(item) for item in self.values]
        else:
            values = self.values
            
        return sum(values) / len(values)
    
    def min(self, selector: Optional[Callable[[V], Any]] = None) -> Any:
        """
        Find the minimum value in this group.
        
        Args:
            selector: Optional function to extract comparable values from items.
            
        Returns:
            The minimum value.
        """
        if not self.values:
            raise ValueError("Cannot find minimum of empty group")
            
        if selector:
            return min(selector(item) for item in self.values)
        return min(self.values)
    
    def max(self, selector: Optional[Callable[[V], Any]] = None) -> Any:
        """
        Find the maximum value in this group.
        
        Args:
            selector: Optional function to extract comparable values from items.
            
        Returns:
            The maximum value.
        """
        if not self.values:
            raise ValueError("Cannot find maximum of empty group")
            
        if selector:
            return max(selector(item) for item in self.values)
        return max(self.values)


class IAggregationExpression(ABC):
    """
    Base interface for aggregation expressions.
    
    Matches the .NET aggregation expression pattern.
    """
    
    @abstractmethod
    def to_cypher(self, alias: str) -> str:
        """
        Convert this aggregation expression to Cypher.
        
        Args:
            alias: The alias to use for the aggregated entity.
            
        Returns:
            Cypher expression string.
        """
        pass


@dataclass(frozen=True)
class CountExpression(IAggregationExpression):
    """Count aggregation expression."""
    
    predicate: Optional[str] = None
    """Optional WHERE predicate for conditional counting."""
    
    def to_cypher(self, alias: str) -> str:
        if self.predicate:
            return f"count(CASE WHEN {self.predicate} THEN 1 END)"
        return f"count({alias})"


@dataclass(frozen=True)
class SumExpression(IAggregationExpression):
    """Sum aggregation expression."""
    
    property_path: str
    """The property path to sum (e.g., 'n.age')."""
    
    def to_cypher(self, alias: str) -> str:
        return f"sum({self.property_path})"


@dataclass(frozen=True)
class AverageExpression(IAggregationExpression):
    """Average aggregation expression."""
    
    property_path: str
    """The property path to average (e.g., 'n.age')."""
    
    def to_cypher(self, alias: str) -> str:
        return f"avg({self.property_path})"


@dataclass(frozen=True)
class MinExpression(IAggregationExpression):
    """Minimum aggregation expression."""
    
    property_path: str
    """The property path to find minimum of (e.g., 'n.age')."""
    
    def to_cypher(self, alias: str) -> str:
        return f"min({self.property_path})"


@dataclass(frozen=True)
class MaxExpression(IAggregationExpression):
    """Maximum aggregation expression."""
    
    property_path: str
    """The property path to find maximum of (e.g., 'n.age')."""
    
    def to_cypher(self, alias: str) -> str:
        return f"max({self.property_path})"


@dataclass(frozen=True)
class GroupByClause:
    """
    Represents a GROUP BY clause in a graph query.
    
    Matches .NET LINQ GroupBy functionality.
    """
    
    key_expression: str
    """The Cypher expression to group by (e.g., 'n.department')."""
    
    having_clause: Optional[str] = None
    """Optional HAVING clause for filtering groups."""
    
    def to_cypher(self) -> str:
        """Convert to Cypher GROUP BY clause."""
        cypher = f"GROUP BY {self.key_expression}"
        if self.having_clause:
            cypher += f" HAVING {self.having_clause}"
        return cypher


class AggregationBuilder:
    """
    Builder for constructing aggregation queries.
    
    Provides a fluent interface for building complex aggregation queries
    that match .NET LINQ patterns.
    """
    
    def __init__(self):
        self._group_by: Optional[GroupByClause] = None
        self._aggregations: List[IAggregationExpression] = []
        self._having_clauses: List[str] = []
    
    def group_by(self, key_expression: str) -> "AggregationBuilder":
        """
        Add a GROUP BY clause.
        
        Args:
            key_expression: The expression to group by.
            
        Returns:
            Self for method chaining.
        """
        self._group_by = GroupByClause(key_expression=key_expression)
        return self
    
    def having(self, condition: str) -> "AggregationBuilder":
        """
        Add a HAVING clause.
        
        Args:
            condition: The condition for filtering groups.
            
        Returns:
            Self for method chaining.
        """
        self._having_clauses.append(condition)
        return self
    
    def count(self, predicate: Optional[str] = None) -> "AggregationBuilder":
        """
        Add a COUNT aggregation.
        
        Args:
            predicate: Optional predicate for conditional counting.
            
        Returns:
            Self for method chaining.
        """
        self._aggregations.append(CountExpression(predicate=predicate))
        return self
    
    def sum(self, property_path: str) -> "AggregationBuilder":
        """
        Add a SUM aggregation.
        
        Args:
            property_path: The property path to sum.
            
        Returns:
            Self for method chaining.
        """
        self._aggregations.append(SumExpression(property_path=property_path))
        return self
    
    def average(self, property_path: str) -> "AggregationBuilder":
        """
        Add an AVERAGE aggregation.
        
        Args:
            property_path: The property path to average.
            
        Returns:
            Self for method chaining.
        """
        self._aggregations.append(AverageExpression(property_path=property_path))
        return self
    
    def min(self, property_path: str) -> "AggregationBuilder":
        """
        Add a MIN aggregation.
        
        Args:
            property_path: The property path to find minimum of.
            
        Returns:
            Self for method chaining.
        """
        self._aggregations.append(MinExpression(property_path=property_path))
        return self
    
    def max(self, property_path: str) -> "AggregationBuilder":
        """
        Add a MAX aggregation.
        
        Args:
            property_path: The property path to find maximum of.
            
        Returns:
            Self for method chaining.
        """
        self._aggregations.append(MaxExpression(property_path=property_path))
        return self
    
    def build_cypher(self, base_query: str, alias: str) -> str:
        """
        Build the complete Cypher query with aggregations.
        
        Args:
            base_query: The base query to add aggregations to.
            alias: The alias for the aggregated entity.
            
        Returns:
            Complete Cypher query with aggregations.
        """
        query_parts = [base_query]
        
        # Add GROUP BY if specified
        if self._group_by:
            having_clause = None
            if self._having_clauses:
                having_clause = " AND ".join(self._having_clauses)
            
            group_by = GroupByClause(
                key_expression=self._group_by.key_expression,
                having_clause=having_clause
            )
            query_parts.append(group_by.to_cypher())
        
        # Add aggregation expressions to RETURN clause
        if self._aggregations:
            agg_expressions = [agg.to_cypher(alias) for agg in self._aggregations]
            query_parts.append(f"RETURN {', '.join(agg_expressions)}")
        
        return "\n".join(query_parts)


def group_by_key_selector(items: List[T], key_func: Callable[[T], K]) -> List[GroupByResult[K, T]]:
    """
    Group items by a key selector function.
    
    This provides in-memory grouping for cases where Cypher grouping isn't sufficient.
    
    Args:
        items: The items to group.
        key_func: Function to extract the grouping key from each item.
        
    Returns:
        List of GroupByResult objects.
    """
    groups: Dict[K, List[T]] = {}
    
    for item in items:
        key = key_func(item)
        if key not in groups:
            groups[key] = []
        groups[key].append(item)
    
    return [GroupByResult(key=key, values=values) for key, values in groups.items()]


def aggregate_groups(
    groups: List[GroupByResult[K, V]], 
    aggregation_func: Callable[[GroupByResult[K, V]], Any]
) -> List[Any]:
    """
    Apply an aggregation function to each group.
    
    Args:
        groups: The grouped results.
        aggregation_func: Function to apply to each group.
        
    Returns:
        List of aggregated results.
    """
    return [aggregation_func(group) for group in groups] 