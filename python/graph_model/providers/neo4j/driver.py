"""
Async Neo4j driver and session management for the Python Graph Model library.
"""

from typing import Optional

from neo4j import AsyncDriver, AsyncGraphDatabase, AsyncSession


class Neo4jDriver:
    """
    Singleton-style async Neo4j driver and connection pool.
    """
    _driver: Optional[AsyncDriver] = None
    _uri: Optional[str] = None
    _auth: Optional[tuple] = None

    @classmethod
    async def initialize(cls, uri: str, user: str, password: str, **kwargs) -> None:
        if cls._driver is not None:
            await cls.close()
        cls._uri = uri
        cls._auth = (user, password)
        cls._driver = AsyncGraphDatabase.driver(uri, auth=(user, password), **kwargs)

    @classmethod
    def get_driver(cls) -> AsyncDriver:
        if cls._driver is None:
            raise RuntimeError("Neo4j driver not initialized. Call Neo4jDriver.initialize() first.")
        return cls._driver

    @classmethod
    async def close(cls) -> None:
        if cls._driver is not None:
            await cls._driver.close()
            cls._driver = None

    @classmethod
    def session(cls, **kwargs) -> AsyncSession:
        """Get a new async session."""
        return cls.get_driver().session(**kwargs) 