"""Queryable interfaces and base implementations for LINQ-like graph operations."""

from abc import ABC, abstractmethod
from typing import (
    TYPE_CHECKING,
    Any,
    AsyncIterator,
    Awaitable,
    Callable,
    Generic,
    List,
    Optional,
    Protocol,
    TypeVar,
    Union,
)

from ..core.node import INode
from ..core.relationship import IRelationship
from ..core.transaction import IGraphTransaction

# Forward declarations to avoid circular imports
if TYPE_CHECKING:
    from .aggregation import AggregationBuilder, GroupByResult
    from .async_streaming import IAsyncGraphQueryable

# Type variables for generic types
T = TypeVar("T")
N = TypeVar("N", bound=INode)
R = TypeVar("R", bound=IRelationship)
U = TypeVar("U")

# Filter and ordering function types
FilterFunc = Callable[[T], bool]
OrderFunc = Callable[[T], Any]
ProjectFunc = Callable[[T], U]


class IGraphQueryable(Protocol[T]):
    """
    Base protocol for queryable graph collections with LINQ-like operations.
    
    Provides common querying functionality that applies to both nodes and relationships.
    """

    def where(self, predicate: FilterFunc[T]) -> "IGraphQueryable[T]":
        """
        Filters elements based on a predicate.
        
        Args:
            predicate: A function that takes an element and returns a boolean.
        
        Returns:
            A new queryable with the filter applied.
        """
        ...

    def order_by(self, key_func: OrderFunc[T]) -> "IGraphQueryable[T]":
        """
        Orders elements by the specified key function in ascending order.
        
        Args:
            key_func: A function that extracts the ordering key from an element.
        
        Returns:
            A new ordered queryable.
        """
        ...

    def order_by_desc(self, key_func: OrderFunc[T]) -> "IGraphQueryable[T]":
        """
        Orders elements by the specified key function in descending order.
        
        Args:
            key_func: A function that extracts the ordering key from an element.
        
        Returns:
            A new ordered queryable.
        """
        ...

    def take(self, count: int) -> "IGraphQueryable[T]":
        """
        Takes the first 'count' elements.
        
        Args:
            count: The number of elements to take.
        
        Returns:
            A new queryable limited to the specified count.
        """
        ...

    def skip(self, count: int) -> "IGraphQueryable[T]":
        """
        Skips the first 'count' elements.
        
        Args:
            count: The number of elements to skip.
        
        Returns:
            A new queryable that skips the specified number of elements.
        """
        ...

    def select(self, selector: ProjectFunc[T, U]) -> "IGraphQueryable[U]":
        """
        Projects each element to a new form.
        
        Args:
            selector: A function to transform each element.
        
        Returns:
            A new queryable with the projection applied.
        """
        ...

    async def to_list(self) -> List[T]:
        """
        Executes the query and returns all results as a list.
        
        Returns:
            A list containing all matching elements.
        """
        ...

    async def first(self) -> T:
        """
        Returns the first element of the query.
        
        Returns:
            The first element.
            
        Raises:
            IndexError: If no elements are found.
        """
        ...

    async def first_or_none(self) -> Optional[T]:
        """
        Returns the first element of the query, or None if no elements are found.
        
        Returns:
            The first element or None.
        """
        ...

    async def count(self) -> int:
        """
        Returns the number of elements in the query.
        
        Returns:
            The count of elements.
        """
        ...

    async def any(self) -> bool:
        """
        Determines whether any elements exist.
        
        Returns:
            True if any elements exist, False otherwise.
        """
        ...

    async def all(self, predicate: FilterFunc[T]) -> bool:
        """
        Determines whether all elements satisfy a condition.
        
        Args:
            predicate: A function to test each element for a condition.
        
        Returns:
            True if all elements satisfy the condition, False otherwise.
        """
        ...
    
    def group_by(self, key_selector: Callable[[T], Any]) -> "IGraphQueryable[GroupByResult[Any, T]]":
        """
        Groups elements by a key selector function.
        
        Args:
            key_selector: A function that extracts the grouping key from each element.
        
        Returns:
            A new queryable of grouped results.
        """
        ...
    
    def aggregate(self) -> "AggregationBuilder":
        """
        Creates an aggregation builder for this queryable.
        
        Returns:
            An AggregationBuilder for constructing aggregation queries.
        """
        ...
    
    def as_async_queryable(self) -> "IAsyncGraphQueryable[T]":
        """
        Converts this queryable to an async queryable for streaming operations.
        
        Returns:
            An async queryable for streaming query execution.
        """
        ...
    
    def __aiter__(self) -> AsyncIterator[T]:
        """
        Returns an async iterator for streaming query results.
        
        Returns:
            Async iterator over query results.
        """
        ...


class IOrderedGraphQueryable(IGraphQueryable[T], Protocol[T]):
    """
    Protocol for ordered queryable collections with additional ordering operations.
    """

    def then_by(self, key_func: OrderFunc[T]) -> "IOrderedGraphQueryable[T]":
        """
        Performs a subsequent ordering of the elements in ascending order.
        
        Args:
            key_func: A function that extracts the ordering key from an element.
        
        Returns:
            A new ordered queryable with the additional ordering applied.
        """
        ...

    def then_by_desc(self, key_func: OrderFunc[T]) -> "IOrderedGraphQueryable[T]":
        """
        Performs a subsequent ordering of the elements in descending order.
        
        Args:
            key_func: A function that extracts the ordering key from an element.
        
        Returns:
            A new ordered queryable with the additional ordering applied.
        """
        ...


class IGraphNodeQueryable(IGraphQueryable[N], Protocol[N]):
    """
    Protocol for queryable node collections with graph-specific operations.
    
    Extends the base queryable with node-specific functionality like graph traversal.
    """

    def traverse(
        self,
        relationship_type: type[R],
        target_node_type: type[U],
        direction: Optional["TraversalDirection"] = None
    ) -> "IGraphNodeQueryable[U]":
        """
        Traverses relationships to find connected nodes.
        
        Args:
            relationship_type: The type of relationship to traverse.
            target_node_type: The type of target nodes to find.
            direction: The direction of traversal (optional).
        
        Returns:
            A new queryable for the connected nodes.
        """
        ...

    def with_depth(self, min_depth: int, max_depth: Optional[int] = None) -> "IGraphNodeQueryable[N]":
        """
        Specifies the depth range for graph traversal operations.
        
        Args:
            min_depth: The minimum depth to traverse.
            max_depth: The maximum depth to traverse (optional).
        
        Returns:
            A new queryable with the depth constraint applied.
        """
        ...


class IOrderedGraphNodeQueryable(IOrderedGraphQueryable[N], IGraphNodeQueryable[N], Protocol[N]):
    """
    Protocol for ordered queryable node collections.
    
    Combines ordering operations with node-specific functionality.
    """
    pass


class IGraphRelationshipQueryable(IGraphQueryable[R], Protocol[R]):
    """
    Protocol for queryable relationship collections with relationship-specific operations.
    """

    def where_start_node(self, node_type: type[N], predicate: FilterFunc[N]) -> "IGraphRelationshipQueryable[R]":
        """
        Filters relationships based on a predicate applied to the start node.
        
        Args:
            node_type: The type of the start node.
            predicate: A function to test the start node.
        
        Returns:
            A new queryable with the filter applied.
        """
        ...

    def where_end_node(self, node_type: type[N], predicate: FilterFunc[N]) -> "IGraphRelationshipQueryable[R]":
        """
        Filters relationships based on a predicate applied to the end node.
        
        Args:
            node_type: The type of the end node.
            predicate: A function to test the end node.
        
        Returns:
            A new queryable with the filter applied.
        """
        ...


class IOrderedGraphRelationshipQueryable(IOrderedGraphQueryable[R], IGraphRelationshipQueryable[R], Protocol[R]):
    """
    Protocol for ordered queryable relationship collections.
    
    Combines ordering operations with relationship-specific functionality.
    """
    pass


class QueryableBase(ABC, Generic[T]):
    """
    Abstract base class providing common queryable functionality.
    
    Implements the core queryable operations and provides a foundation
    for database-specific queryable implementations.
    """

    def __init__(self, transaction: Optional[IGraphTransaction] = None) -> None:
        self._transaction = transaction
        self._filters: List[FilterFunc[T]] = []
        self._order_funcs: List[tuple[OrderFunc[T], bool]] = []  # (func, is_descending)
        self._skip_count: Optional[int] = None
        self._take_count: Optional[int] = None
        self._projections: List[Callable[[Any], Any]] = []

    def where(self, predicate: FilterFunc[T]) -> "QueryableBase[T]":
        """Add a filter to the query."""
        new_queryable = self._clone()
        new_queryable._filters.append(predicate)
        return new_queryable

    def order_by(self, key_func: OrderFunc[T]) -> "QueryableBase[T]":
        """Add ascending ordering to the query."""
        new_queryable = self._clone()
        new_queryable._order_funcs.append((key_func, False))
        return new_queryable

    def order_by_desc(self, key_func: OrderFunc[T]) -> "QueryableBase[T]":
        """Add descending ordering to the query."""
        new_queryable = self._clone()
        new_queryable._order_funcs.append((key_func, True))
        return new_queryable

    def take(self, count: int) -> "QueryableBase[T]":
        """Limit the number of results."""
        if count < 0:
            raise ValueError("Count must be non-negative")
        new_queryable = self._clone()
        new_queryable._take_count = count
        return new_queryable

    def skip(self, count: int) -> "QueryableBase[T]":
        """Skip a number of results."""
        if count < 0:
            raise ValueError("Count must be non-negative")
        new_queryable = self._clone()
        new_queryable._skip_count = count
        return new_queryable

    def select(self, selector: ProjectFunc[T, U]) -> "QueryableBase[U]":
        """Project results to a new form."""
        new_queryable = self._clone()
        new_queryable._projections.append(selector)
        return new_queryable

    @abstractmethod
    def _clone(self) -> "QueryableBase[T]":
        """Create a copy of this queryable for chaining operations."""
        ...

    @abstractmethod
    async def _execute_query(self) -> List[T]:
        """Execute the query and return raw results."""
        ...

    async def to_list(self) -> List[T]:
        """Execute the query and return all results as a list."""
        return await self._execute_query()

    async def first(self) -> T:
        """Get the first result or raise an error."""
        results = await self.take(1).to_list()
        if not results:
            raise IndexError("No elements found")
        return results[0]

    async def first_or_none(self) -> Optional[T]:
        """Get the first result or None."""
        results = await self.take(1).to_list()
        return results[0] if results else None

    async def count(self) -> int:
        """Get the count of results."""
        results = await self._execute_query()
        return len(results)

    async def any(self) -> bool:
        """Check if any results exist."""
        return await self.take(1).count() > 0

    async def all(self, predicate: FilterFunc[T]) -> bool:
        """Check if all results satisfy the predicate."""
        results = await self.to_list()
        return all(predicate(item) for item in results)


class GraphNodeQueryable(QueryableBase[N]):
    """Base implementation for node queryables."""

    def __init__(
        self, 
        node_type: type[N], 
        transaction: Optional[IGraphTransaction] = None
    ) -> None:
        super().__init__(transaction)
        self._node_type = node_type
        self._traversals: List["TraversalStep"] = []
        self._min_depth: Optional[int] = None
        self._max_depth: Optional[int] = None

    def traverse(
        self,
        relationship_type: type[R],
        target_node_type: type[U],
        direction: Optional["TraversalDirection"] = None
    ) -> "GraphNodeQueryable[U]":
        """Traverse to connected nodes."""
        from .traversal import TraversalDirection as TD
        from .traversal import TraversalStep
        
        new_queryable = GraphNodeQueryable(target_node_type, self._transaction)
        new_queryable._copy_state_from(self)
        
        traversal = TraversalStep(
            relationship_type=relationship_type,
            target_node_type=target_node_type,
            direction=direction or TD.OUTGOING
        )
        new_queryable._traversals.append(traversal)
        return new_queryable

    def with_depth(self, min_depth: int, max_depth: Optional[int] = None) -> "GraphNodeQueryable[N]":
        """Set depth constraints for traversals."""
        if min_depth < 0:
            raise ValueError("Minimum depth must be non-negative")
        if max_depth is not None and max_depth < min_depth:
            raise ValueError("Maximum depth must be greater than or equal to minimum depth")
        
        new_queryable = self._clone()
        new_queryable._min_depth = min_depth
        new_queryable._max_depth = max_depth if max_depth is not None else min_depth
        return new_queryable

    def _clone(self) -> "GraphNodeQueryable[N]":
        """Create a copy for chaining operations."""
        new_queryable = GraphNodeQueryable(self._node_type, self._transaction)
        new_queryable._copy_state_from(self)
        return new_queryable

    def _copy_state_from(self, other: "QueryableBase") -> None:
        """Copy state from another queryable."""
        self._filters = other._filters.copy()
        self._order_funcs = other._order_funcs.copy()
        self._skip_count = other._skip_count
        self._take_count = other._take_count
        self._projections = other._projections.copy()
        
        if isinstance(other, GraphNodeQueryable):
            self._traversals = other._traversals.copy()
            self._min_depth = other._min_depth
            self._max_depth = other._max_depth

    async def _execute_query(self) -> List[N]:
        """Execute the node query. Must be implemented by provider-specific subclasses."""
        raise NotImplementedError("Node query execution must be implemented by provider")


class GraphRelationshipQueryable(QueryableBase[R]):
    """Base implementation for relationship queryables."""

    def __init__(
        self, 
        relationship_type: type[R], 
        transaction: Optional[IGraphTransaction] = None
    ) -> None:
        super().__init__(transaction)
        self._relationship_type = relationship_type
        self._start_node_filters: List[tuple[type, FilterFunc]] = []
        self._end_node_filters: List[tuple[type, FilterFunc]] = []

    def where_start_node(self, node_type: type[N], predicate: FilterFunc[N]) -> "GraphRelationshipQueryable[R]":
        """Filter by start node properties."""
        new_queryable = self._clone()
        new_queryable._start_node_filters.append((node_type, predicate))
        return new_queryable

    def where_end_node(self, node_type: type[N], predicate: FilterFunc[N]) -> "GraphRelationshipQueryable[R]":
        """Filter by end node properties."""
        new_queryable = self._clone()
        new_queryable._end_node_filters.append((node_type, predicate))
        return new_queryable

    def _clone(self) -> "GraphRelationshipQueryable[R]":
        """Create a copy for chaining operations."""
        new_queryable = GraphRelationshipQueryable(self._relationship_type, self._transaction)
        new_queryable._copy_state_from(self)
        return new_queryable

    def _copy_state_from(self, other: "QueryableBase") -> None:
        """Copy state from another queryable."""
        self._filters = other._filters.copy()
        self._order_funcs = other._order_funcs.copy()
        self._skip_count = other._skip_count
        self._take_count = other._take_count
        self._projections = other._projections.copy()
        
        if isinstance(other, GraphRelationshipQueryable):
            self._start_node_filters = other._start_node_filters.copy()
            self._end_node_filters = other._end_node_filters.copy()

    async def _execute_query(self) -> List[R]:
        """Execute the relationship query. Must be implemented by provider-specific subclasses."""
        raise NotImplementedError("Relationship query execution must be implemented by provider")


# Forward declaration for circular imports
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from .traversal import TraversalDirection, TraversalStep
