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
    _database: Optional[str] = None

    @classmethod
    async def initialize(cls, uri: str, user: str, password: str, database: Optional[str] = None, **kwargs) -> None:
        if cls._driver is not None:
            await cls.close()
        cls._uri = uri
        cls._auth = (user, password)
        cls._database = database
        cls._driver = AsyncGraphDatabase.driver(uri, auth=(user, password), **kwargs)

    @classmethod
    def get_database(cls) -> Optional[str]:
        return cls._database

    @classmethod
    async def ensure_database_exists(cls) -> None:
        if cls._database is None:
            raise ValueError("Database is not set. Call Neo4jDriver.initialize() with a database name first.")
        driver = cls.get_driver()
        async with driver.session() as session:
            await session.run(f"CREATE DATABASE {cls._database} IF NOT EXISTS WAIT 10 SECONDS")

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
        session_kwargs = kwargs.copy()
        if cls._database is not None and 'database' not in session_kwargs:
            session_kwargs['database'] = cls._database
        return cls.get_driver().session(**session_kwargs)