"""Transaction management interfaces for graph operations."""

from abc import ABC, abstractmethod
from typing import Any, Optional, Protocol


class IGraphTransaction(Protocol):
    """
    Protocol defining the contract for graph transactions.
    
    Provides ACID transaction capabilities for graph operations with
    support for commit, rollback, and context manager usage.
    """

    @property
    def is_active(self) -> bool:
        """
        Check if the transaction is currently active.
        
        Returns:
            True if the transaction is active and can be used for operations.
        """
        ...

    @property 
    def is_committed(self) -> bool:
        """
        Check if the transaction has been committed.
        
        Returns:
            True if the transaction has been successfully committed.
        """
        ...

    @property
    def is_rolled_back(self) -> bool:
        """
        Check if the transaction has been rolled back.
        
        Returns:
            True if the transaction has been rolled back.
        """
        ...

    async def commit(self) -> None:
        """
        Commit the transaction, making all changes permanent.
        
        Raises:
            GraphTransactionException: If the commit operation fails.
        """
        ...

    async def rollback(self) -> None:
        """
        Roll back the transaction, discarding all changes.
        
        Raises:
            GraphTransactionException: If the rollback operation fails.
        """
        ...

    async def close(self) -> None:
        """
        Close the transaction, automatically rolling back if not committed.
        
        This method is idempotent and safe to call multiple times.
        """
        ...

    async def __aenter__(self) -> "IGraphTransaction":
        """Enter the async context manager."""
        ...

    async def __aexit__(
        self, 
        exc_type: Optional[type],
        exc_val: Optional[Exception], 
        exc_tb: Optional[Any]
    ) -> None:
        """
        Exit the async context manager.
        
        Automatically commits the transaction if no exception occurred,
        otherwise rolls back the transaction.
        """
        ...


class BaseGraphTransaction(ABC):
    """
    Abstract base class providing common transaction functionality.
    
    Implements the context manager protocol and provides a foundation
    for database-specific transaction implementations.
    """

    def __init__(self) -> None:
        self._is_active = True
        self._is_committed = False
        self._is_rolled_back = False

    @property
    def is_active(self) -> bool:
        """Check if the transaction is currently active."""
        return self._is_active

    @property
    def is_committed(self) -> bool:
        """Check if the transaction has been committed."""
        return self._is_committed

    @property
    def is_rolled_back(self) -> bool:
        """Check if the transaction has been rolled back."""
        return self._is_rolled_back

    @abstractmethod
    async def _do_commit(self) -> None:
        """Perform the actual commit operation. Must be implemented by subclasses."""
        ...

    @abstractmethod
    async def _do_rollback(self) -> None:
        """Perform the actual rollback operation. Must be implemented by subclasses."""
        ...

    @abstractmethod
    async def _do_close(self) -> None:
        """Perform any cleanup operations. Must be implemented by subclasses."""
        ...

    async def commit(self) -> None:
        """Commit the transaction, making all changes permanent."""
        if not self._is_active:
            raise ValueError("Transaction is not active")
        
        if self._is_committed or self._is_rolled_back:
            raise ValueError("Transaction has already been completed")
        
        try:
            await self._do_commit()
            self._is_committed = True
        finally:
            self._is_active = False

    async def rollback(self) -> None:
        """Roll back the transaction, discarding all changes."""
        if not self._is_active:
            return  # Already inactive, nothing to rollback
        
        if self._is_committed:
            raise ValueError("Cannot rollback a committed transaction")
        
        try:
            await self._do_rollback()
            self._is_rolled_back = True
        finally:
            self._is_active = False

    async def close(self) -> None:
        """Close the transaction, automatically rolling back if not committed."""
        if not self._is_active:
            return  # Already closed
        
        if not self._is_committed:
            await self.rollback()
        
        await self._do_close()

    async def __aenter__(self) -> "BaseGraphTransaction":
        """Enter the async context manager."""
        return self

    async def __aexit__(
        self,
        exc_type: Optional[type],
        exc_val: Optional[Exception],
        exc_tb: Optional[Any]
    ) -> None:
        """Exit the async context manager with automatic commit/rollback."""
        if exc_type is None and self._is_active:
            # No exception, commit the transaction
            await self.commit()
        else:
            # Exception occurred or transaction is not active, rollback
            await self.rollback()
        
        await self.close() 