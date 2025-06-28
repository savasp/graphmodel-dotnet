"""
Async transaction context manager for Neo4j, implementing IGraphTransaction.
"""

from typing import Optional

from neo4j import AsyncSession, AsyncTransaction

from graph_model.core.transaction import IGraphTransaction


class Neo4jTransaction(IGraphTransaction):
    def __init__(self, session: AsyncSession):
        self._session = session
        self._tx: Optional[AsyncTransaction] = None

    async def __aenter__(self):
        self._tx = await self._session.begin_transaction()
        return self

    async def __aexit__(self, exc_type, exc_val, exc_tb):
        if exc_type:
            await self._tx.rollback()
        else:
            await self._tx.commit()
        await self._session.close()

    @property
    def transaction(self) -> AsyncTransaction:
        if self._tx is None:
            raise RuntimeError("Transaction not started.")
        return self._tx 