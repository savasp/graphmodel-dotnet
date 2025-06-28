"""
Async streaming and iterator functionality for graph queries.

This module provides IAsyncEnumerable-like functionality that matches the .NET
GraphModel async streaming patterns for efficient processing of large result sets.
"""

import asyncio
from abc import ABC, abstractmethod
from collections.abc import AsyncIterable
from typing import Any, AsyncIterator, Callable, Generic, List, Optional, TypeVar, Union

from ..core.node import INode
from ..core.relationship import IRelationship

T = TypeVar('T')
TResult = TypeVar('TResult')


class IAsyncGraphQueryable(AsyncIterable[T], ABC):
    """
    Base interface for async graph queryables.
    
    Matches the .NET IAsyncEnumerable pattern for graph queries,
    providing efficient streaming of large result sets.
    """
    
    @abstractmethod
    def __aiter__(self) -> AsyncIterator[T]:
        """Return an async iterator for the queryable."""
        ...
    
    @abstractmethod
    async def to_list_async(self) -> List[T]:
        """Materialize all results into a list asynchronously."""
        ...
    
    @abstractmethod
    async def first_async(self, predicate: Optional[Callable[[T], bool]] = None) -> T:
        """Get the first element asynchronously."""
        ...
    
    @abstractmethod
    async def first_or_default_async(self, predicate: Optional[Callable[[T], bool]] = None) -> Optional[T]:
        """Get the first element or None asynchronously."""
        ...
    
    @abstractmethod
    async def single_async(self, predicate: Optional[Callable[[T], bool]] = None) -> T:
        """Get the single element asynchronously."""
        ...
    
    @abstractmethod
    async def single_or_default_async(self, predicate: Optional[Callable[[T], bool]] = None) -> Optional[T]:
        """Get the single element or None asynchronously."""
        ...
    
    @abstractmethod
    async def count_async(self, predicate: Optional[Callable[[T], bool]] = None) -> int:
        """Count elements asynchronously."""
        ...
    
    @abstractmethod
    async def any_async(self, predicate: Optional[Callable[[T], bool]] = None) -> bool:
        """Check if any elements match the predicate asynchronously."""
        ...
    
    @abstractmethod
    async def all_async(self, predicate: Callable[[T], bool]) -> bool:
        """Check if all elements match the predicate asynchronously."""
        ...
    
    @abstractmethod
    def where_async(self, predicate: Callable[[T], bool]) -> "IAsyncGraphQueryable[T]":
        """Filter elements asynchronously."""
        ...
    
    @abstractmethod
    def select_async(self, selector: Callable[[T], TResult]) -> "IAsyncGraphQueryable[TResult]":
        """Project elements asynchronously."""
        ...
    
    @abstractmethod
    def take_async(self, count: int) -> "IAsyncGraphQueryable[T]":
        """Take the first n elements asynchronously."""
        ...
    
    @abstractmethod
    def skip_async(self, count: int) -> "IAsyncGraphQueryable[T]":
        """Skip the first n elements asynchronously."""
        ...


class AsyncGraphQueryable(IAsyncGraphQueryable[T], Generic[T]):
    """
    Concrete implementation of async graph queryable.
    
    Provides streaming query execution with lazy evaluation and efficient
    memory usage for large result sets.
    """
    
    def __init__(
        self,
        query_executor: Callable[[], AsyncIterator[T]],
        batch_size: int = 1000
    ):
        """
        Initialize the async queryable.
        
        Args:
            query_executor: Function that returns an async iterator for query results.
            batch_size: Size of batches for streaming results.
        """
        self._query_executor = query_executor
        self._batch_size = batch_size
        self._filters: List[Callable[[T], bool]] = []
        self._projections: List[Callable[[Any], Any]] = []
        self._take_count: Optional[int] = None
        self._skip_count: int = 0
    
    def __aiter__(self) -> AsyncIterator[T]:
        """Return an async iterator for the queryable."""
        return self._execute_async()
    
    async def _execute_async(self) -> AsyncIterator[T]:
        """Execute the query asynchronously and yield results."""
        skipped = 0
        taken = 0
        
        async for item in self._query_executor():
            # Apply filters
            if self._filters and not all(f(item) for f in self._filters):
                continue
            
            # Handle skip
            if skipped < self._skip_count:
                skipped += 1
                continue
            
            # Handle take
            if self._take_count is not None and taken >= self._take_count:
                break
            
            # Apply projections
            result = item
            for projection in self._projections:
                result = projection(result)
            
            yield result
            taken += 1
    
    async def to_list_async(self) -> List[T]:
        """Materialize all results into a list asynchronously."""
        results = []
        async for item in self:
            results.append(item)
        return results
    
    async def first_async(self, predicate: Optional[Callable[[T], bool]] = None) -> T:
        """Get the first element asynchronously."""
        async for item in self:
            if predicate is None or predicate(item):
                return item
        raise ValueError("Sequence contains no matching elements")
    
    async def first_or_default_async(self, predicate: Optional[Callable[[T], bool]] = None) -> Optional[T]:
        """Get the first element or None asynchronously."""
        try:
            return await self.first_async(predicate)
        except ValueError:
            return None
    
    async def single_async(self, predicate: Optional[Callable[[T], bool]] = None) -> T:
        """Get the single element asynchronously."""
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
    
    async def single_or_default_async(self, predicate: Optional[Callable[[T], bool]] = None) -> Optional[T]:
        """Get the single element or None asynchronously."""
        try:
            return await self.single_async(predicate)
        except ValueError:
            return None
    
    async def count_async(self, predicate: Optional[Callable[[T], bool]] = None) -> int:
        """Count elements asynchronously."""
        count = 0
        async for item in self:
            if predicate is None or predicate(item):
                count += 1
        return count
    
    async def any_async(self, predicate: Optional[Callable[[T], bool]] = None) -> bool:
        """Check if any elements match the predicate asynchronously."""
        async for item in self:
            if predicate is None or predicate(item):
                return True
        return False
    
    async def all_async(self, predicate: Callable[[T], bool]) -> bool:
        """Check if all elements match the predicate asynchronously."""
        async for item in self:
            if not predicate(item):
                return False
        return True
    
    def where_async(self, predicate: Callable[[T], bool]) -> "AsyncGraphQueryable[T]":
        """Filter elements asynchronously."""
        new_queryable = AsyncGraphQueryable(self._query_executor, self._batch_size)
        new_queryable._filters = self._filters + [predicate]
        new_queryable._projections = self._projections.copy()
        new_queryable._take_count = self._take_count
        new_queryable._skip_count = self._skip_count
        return new_queryable
    
    def select_async(self, selector: Callable[[T], TResult]) -> "AsyncGraphQueryable[TResult]":
        """Project elements asynchronously."""
        new_queryable = AsyncGraphQueryable(self._query_executor, self._batch_size)
        new_queryable._filters = self._filters.copy()
        new_queryable._projections = self._projections + [selector]
        new_queryable._take_count = self._take_count
        new_queryable._skip_count = self._skip_count
        return new_queryable
    
    def take_async(self, count: int) -> "AsyncGraphQueryable[T]":
        """Take the first n elements asynchronously."""
        new_queryable = AsyncGraphQueryable(self._query_executor, self._batch_size)
        new_queryable._filters = self._filters.copy()
        new_queryable._projections = self._projections.copy()
        new_queryable._take_count = count
        new_queryable._skip_count = self._skip_count
        return new_queryable
    
    def skip_async(self, count: int) -> "AsyncGraphQueryable[T]":
        """Skip the first n elements asynchronously."""
        new_queryable = AsyncGraphQueryable(self._query_executor, self._batch_size)
        new_queryable._filters = self._filters.copy()
        new_queryable._projections = self._projections.copy()
        new_queryable._take_count = self._take_count
        new_queryable._skip_count = self._skip_count + count
        return new_queryable


class AsyncBatchProcessor(Generic[T]):
    """
    Processor for handling large result sets in batches.
    
    Provides efficient memory usage when processing large graph query results
    by processing items in configurable batch sizes.
    """
    
    def __init__(self, batch_size: int = 1000):
        """
        Initialize the batch processor.
        
        Args:
            batch_size: Number of items to process in each batch.
        """
        self.batch_size = batch_size
    
    async def process_in_batches(
        self,
        items: AsyncIterator[T],
        processor: Callable[[List[T]], Union[Any, asyncio.Future]]
    ) -> List[Any]:
        """
        Process items in batches asynchronously.
        
        Args:
            items: Async iterator of items to process.
            processor: Function to process each batch.
            
        Returns:
            List of results from processing each batch.
        """
        results = []
        batch = []
        
        async for item in items:
            batch.append(item)
            
            if len(batch) >= self.batch_size:
                result = processor(batch)
                if asyncio.iscoroutine(result):
                    result = await result
                results.append(result)
                batch = []
        
        # Process remaining items
        if batch:
            result = processor(batch)
            if asyncio.iscoroutine(result):
                result = await result
            results.append(result)
        
        return results
    
    async def collect_batches(self, items: AsyncIterator[T]) -> AsyncIterator[List[T]]:
        """
        Collect items into batches and yield each batch.
        
        Args:
            items: Async iterator of items to batch.
            
        Yields:
            Batches of items.
        """
        batch = []
        
        async for item in items:
            batch.append(item)
            
            if len(batch) >= self.batch_size:
                yield batch
                batch = []
        
        # Yield remaining items
        if batch:
            yield batch


class AsyncStreamingAggregator(Generic[T]):
    """
    Aggregator for streaming aggregation operations.
    
    Provides memory-efficient aggregation of large result sets without
    materializing all results in memory at once.
    """
    
    @staticmethod
    async def sum_async(
        items: AsyncIterator[T],
        selector: Optional[Callable[[T], Union[int, float]]] = None
    ) -> Union[int, float]:
        """
        Calculate sum asynchronously over a stream.
        
        Args:
            items: Async iterator of items.
            selector: Optional function to extract numeric values.
            
        Returns:
            Sum of the values.
        """
        total = 0
        async for item in items:
            value = selector(item) if selector else item
            total += value
        return total
    
    @staticmethod
    async def average_async(
        items: AsyncIterator[T],
        selector: Optional[Callable[[T], Union[int, float]]] = None
    ) -> float:
        """
        Calculate average asynchronously over a stream.
        
        Args:
            items: Async iterator of items.
            selector: Optional function to extract numeric values.
            
        Returns:
            Average of the values.
        """
        total = 0
        count = 0
        
        async for item in items:
            value = selector(item) if selector else item
            total += value
            count += 1
        
        if count == 0:
            raise ValueError("Cannot calculate average of empty sequence")
        
        return total / count
    
    @staticmethod
    async def min_async(
        items: AsyncIterator[T],
        selector: Optional[Callable[[T], Any]] = None
    ) -> Any:
        """
        Find minimum value asynchronously over a stream.
        
        Args:
            items: Async iterator of items.
            selector: Optional function to extract comparable values.
            
        Returns:
            Minimum value.
        """
        min_value = None
        first = True
        
        async for item in items:
            value = selector(item) if selector else item
            if first or value < min_value:
                min_value = value
                first = False
        
        if first:
            raise ValueError("Cannot find minimum of empty sequence")
        
        return min_value
    
    @staticmethod
    async def max_async(
        items: AsyncIterator[T],
        selector: Optional[Callable[[T], Any]] = None
    ) -> Any:
        """
        Find maximum value asynchronously over a stream.
        
        Args:
            items: Async iterator of items.
            selector: Optional function to extract comparable values.
            
        Returns:
            Maximum value.
        """
        max_value = None
        first = True
        
        async for item in items:
            value = selector(item) if selector else item
            if first or value > max_value:
                max_value = value
                first = False
        
        if first:
            raise ValueError("Cannot find maximum of empty sequence")
        
        return max_value


def create_async_queryable(
    async_generator_func: Callable[[], AsyncIterator[T]], 
    batch_size: int = 1000
) -> IAsyncGraphQueryable[T]:
    """
    Create an async queryable from an async generator function.
    
    Args:
        async_generator_func: Function that returns an async iterator.
        batch_size: Batch size for processing.
        
    Returns:
        IAsyncGraphQueryable wrapping the async generator.
    """
    return AsyncGraphQueryable(async_generator_func, batch_size)


async def materialize_async(async_iterable: AsyncIterable[T]) -> List[T]:
    """
    Materialize an async iterable into a list.
    
    Args:
        async_iterable: The async iterable to materialize.
        
    Returns:
        List containing all items from the async iterable.
    """
    results = []
    async for item in async_iterable:
        results.append(item)
    return results


async def take_async(async_iterable: AsyncIterable[T], count: int) -> List[T]:
    """
    Take the first n items from an async iterable.
    
    Args:
        async_iterable: The async iterable to take from.
        count: Number of items to take.
        
    Returns:
        List containing the first n items.
    """
    results = []
    taken = 0
    
    async for item in async_iterable:
        if taken >= count:
            break
        results.append(item)
        taken += 1
    
    return results


async def skip_async(async_iterable: AsyncIterable[T], count: int) -> AsyncIterator[T]:
    """
    Skip the first n items from an async iterable.
    
    Args:
        async_iterable: The async iterable to skip from.
        count: Number of items to skip.
        
    Yields:
        Items after skipping the first n items.
    """
    skipped = 0
    
    async for item in async_iterable:
        if skipped < count:
            skipped += 1
            continue
        yield item 