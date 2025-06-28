import asyncio
from typing import List, Optional
from unittest.mock import AsyncMock, MagicMock

import pytest

from graph_model.attributes.decorators import node, relationship
from graph_model.attributes.fields import property_field, related_node_field
from graph_model.core.entity import IEntity
from graph_model.core.node import INode, Node
from graph_model.core.relationship import IRelationship, Relationship
from graph_model.providers.neo4j.cypher_builder import CypherBuilder
from graph_model.providers.neo4j.node_queryable import Neo4jNodeQueryable
from graph_model.providers.neo4j.relationship_queryable import (
    Neo4jRelationshipQueryable,
)


@node("Person")
class Person(Node):
    name: str = property_field()
    age: int = property_field()
    city: Optional[str] = property_field()


@node("Company")
class Company(Node):
    name: str = property_field()
    industry: str = property_field()


@relationship("WORKS_FOR")
class WorksFor(Relationship):
    position: str = property_field()
    salary: int = property_field()


class TestNeo4jNodeQueryable:
    """Test the Neo4jNodeQueryable LINQ-style API"""
    
    @pytest.fixture
    def mock_session(self):
        """Create a mock Neo4j session"""
        session = AsyncMock()
        session.run = AsyncMock()
        return session
    
    @pytest.fixture
    def queryable(self, mock_session):
        """Create a Neo4jNodeQueryable instance"""
        return Neo4jNodeQueryable(Person, mock_session)
    
    @pytest.mark.asyncio
    async def test_where_single_condition(self, queryable, mock_session):
        """Test where clause with single condition"""
        # Arrange
        mock_result = AsyncMock()
        mock_result.data = AsyncMock(return_value=[{"n": {"name": "Alice", "age": 30, "city": None}}])
        mock_session.run.return_value = mock_result
        
        # Act
        result = await queryable.where(lambda p: p.name == "Alice").to_list()
        
        # Assert
        assert len(result) == 1
        assert result[0].name == "Alice"
        assert result[0].age == 30
        assert result[0].city == None
        
        # Verify Cypher query
        call_args = mock_session.run.call_args[0][0]
        assert "MATCH (n:Person)" in call_args
        assert "WHERE n.name = $name_0" in call_args
        assert "RETURN n" in call_args
    
    @pytest.mark.asyncio
    async def test_where_multiple_conditions(self, queryable, mock_session):
        """Test where clause with multiple conditions"""
        # Arrange
        mock_result = AsyncMock()
        mock_result.data = AsyncMock(return_value=[{"n": {"name": "Bob", "age": 25, "city": None}}])
        mock_session.run.return_value = mock_result
        
        # Act
        result = await queryable.where(
            lambda p: p.age > 20 and p.city == None
        ).to_list()
        
        # Assert
        assert len(result) == 1
        assert result[0].name == "Bob"
        
        # Verify Cypher query
        call_args = mock_session.run.call_args[0][0]
        assert "WHERE n.age > $age_0 AND n.city = $city_1" in call_args
    
    @pytest.mark.asyncio
    async def test_order_by_ascending(self, queryable, mock_session):
        """Test order by ascending"""
        # Arrange
        mock_result = AsyncMock()
        mock_result.data = AsyncMock(return_value=[{"n": {"name": "Alice", "age": 25, "city": None}}, {"n": {"name": "Bob", "age": 30, "city": None}}, {"n": {"name": "Charlie", "age": 35, "city": None}}])
        mock_session.run.return_value = mock_result
        
        # Act
        result = await queryable.order_by(lambda p: p.age).to_list()
        
        # Assert
        assert len(result) == 3
        assert result[0].age == 25
        assert result[1].age == 30
        assert result[2].age == 35
        
        # Verify Cypher query
        call_args = mock_session.run.call_args[0][0]
        assert "ORDER BY n.age" in call_args
    
    @pytest.mark.asyncio
    async def test_order_by_descending(self, queryable, mock_session):
        """Test order by descending"""
        # Arrange
        mock_result = AsyncMock()
        mock_result.data = AsyncMock(return_value=[{"n": {"name": "Charlie", "age": 35, "city": None}}, {"n": {"name": "Bob", "age": 30, "city": None}}, {"n": {"name": "Alice", "age": 25, "city": None}}])
        mock_session.run.return_value = mock_result
        
        # Act
        result = await queryable.order_by_descending(lambda p: p.age).to_list()
        
        # Assert
        assert len(result) == 3
        assert result[0].age == 35
        assert result[1].age == 30
        assert result[2].age == 25
        
        # Verify Cypher query
        call_args = mock_session.run.call_args[0][0]
        assert "ORDER BY n.age DESC" in call_args
    
    @pytest.mark.asyncio
    async def test_take(self, queryable, mock_session):
        """Test take (limit)"""
        # Arrange
        mock_result = AsyncMock()
        mock_result.data = AsyncMock(return_value=[{"n": {"name": "Alice", "age": 25, "city": None}}, {"n": {"name": "Bob", "age": 30, "city": None}}])
        mock_session.run.return_value = mock_result
        
        # Act
        result = await queryable.take(2).to_list()
        
        # Assert
        assert len(result) == 2
        
        # Verify Cypher query
        call_args = mock_session.run.call_args[0][0]
        assert "LIMIT 2" in call_args
    
    @pytest.mark.asyncio
    async def test_skip(self, queryable, mock_session):
        """Test skip"""
        # Arrange
        mock_result = AsyncMock()
        mock_result.data = AsyncMock(return_value=[{"n": {"name": "Charlie", "age": 35, "city": None}}])
        mock_session.run.return_value = mock_result
        
        # Act
        result = await queryable.skip(2).to_list()
        
        # Assert
        assert len(result) == 1
        
        # Verify Cypher query
        call_args = mock_session.run.call_args[0][0]
        assert "SKIP 2" in call_args
    
    @pytest.mark.asyncio
    async def test_take_and_skip(self, queryable, mock_session):
        """Test take and skip together (pagination)"""
        # Arrange
        mock_result = AsyncMock()
        mock_result.data = AsyncMock(return_value=[{"n": {"name": "Bob", "age": 30, "city": None}}, {"n": {"name": "Charlie", "age": 35, "city": None}}])
        mock_session.run.return_value = mock_result
        
        # Act
        result = await queryable.skip(1).take(2).to_list()
        
        # Assert
        assert len(result) == 2
        
        # Verify Cypher query
        call_args = mock_session.run.call_args[0][0]
        assert "SKIP 1" in call_args
        assert "LIMIT 2" in call_args
    
    @pytest.mark.asyncio
    async def test_first(self, queryable, mock_session):
        """Test first method"""
        # Arrange
        mock_result = AsyncMock()
        mock_result.data = AsyncMock(return_value=[{"n": {"name": "Alice", "age": 25, "city": None}}])
        mock_session.run.return_value = mock_result
        
        # Act
        result = await queryable.first()
        
        # Assert
        assert result.name == "Alice"
        assert result.age == 25
        
        # Verify Cypher query
        call_args = mock_session.run.call_args[0][0]
        assert "LIMIT 1" in call_args
    
    @pytest.mark.asyncio
    async def test_first_or_none_found(self, queryable, mock_session):
        """Test first_or_none when item is found"""
        # Arrange
        mock_result = AsyncMock()
        mock_result.data = AsyncMock(return_value=[{"n": {"name": "Alice", "age": 25, "city": None}}])
        mock_session.run.return_value = mock_result
        
        # Act
        result = await queryable.first_or_none()
        
        # Assert
        assert result is not None
        assert result.name == "Alice"
    
    @pytest.mark.asyncio
    async def test_first_or_none_not_found(self, queryable, mock_session):
        """Test first_or_none when no item is found"""
        # Arrange
        mock_result = AsyncMock()
        mock_result.data = AsyncMock(return_value=[])
        mock_session.run.return_value = mock_result
        
        # Act
        result = await queryable.first_or_none()
        
        # Assert
        assert result is None
    
    @pytest.mark.asyncio
    async def test_select_projection(self, queryable, mock_session):
        """Test select for projection"""
        # Arrange
        mock_result = AsyncMock()
        mock_result.data = AsyncMock(return_value=[{"name": "Alice", "age": 25, "city": None}, {"name": "Bob", "age": 30, "city": None}])
        mock_session.run.return_value = mock_result
        
        # Act
        result = await queryable.select(lambda p: {"name": p.name, "age": p.age, "city": p.city}).to_list()
        
        # Assert
        assert len(result) == 2
        assert result[0]["name"] == "Alice"
        assert result[0]["age"] == 25
        assert result[0]["city"] == None
        assert result[1]["name"] == "Bob"
        assert result[1]["age"] == 30
        assert result[1]["city"] == None
        
        # Verify Cypher query
        call_args = mock_session.run.call_args[0][0]
        assert "RETURN n.name AS name, n.age AS age, n.city AS city" in call_args
    
    @pytest.mark.asyncio
    async def test_traverse_relationships(self, queryable, mock_session):
        """Test traverse for relationship traversal"""
        # Arrange
        mock_result = AsyncMock()
        mock_result.data = AsyncMock(return_value=[{"n": {"name": "Alice", "age": 25, "city": None}, "r": {"position": "Developer", "salary": 80000}, "target": {"name": "TechCorp", "industry": "Technology"}}])
        mock_session.run.return_value = mock_result
        
        # Act
        result = await queryable.traverse("WORKS_FOR", Company).to_list()
        
        # Assert
        assert len(result) == 1
        assert result[0].name == "Alice"
        assert result[0].age == 25
        assert result[0].city == None
        
        # Verify Cypher query
        call_args = mock_session.run.call_args[0][0]
        assert "MATCH (n:Person)-[r:WORKS_FOR]->(target:Company)" in call_args
    
    @pytest.mark.asyncio
    async def test_with_depth(self, queryable, mock_session):
        """Test with_depth for controlling traversal depth"""
        # Arrange
        mock_result = AsyncMock()
        mock_result.data = AsyncMock(return_value=[{"n": {"name": "Alice", "age": 25, "city": None}}])
        mock_session.run.return_value = mock_result
        
        # Act
        result = await queryable.with_depth(3).to_list()
        
        # Assert
        assert len(result) == 1
        assert result[0].name == "Alice"
        assert result[0].age == 25
        assert result[0].city == None
        
        # Verify depth is stored in queryable
        assert queryable._traversal_depth == 3
    
    @pytest.mark.asyncio
    async def test_chained_operations(self, queryable, mock_session):
        """Test chaining multiple operations"""
        # Arrange
        mock_result = AsyncMock()
        mock_result.data = AsyncMock(return_value=[{"n": {"name": "Bob", "age": 30, "city": None}}])
        mock_session.run.return_value = mock_result
        
        # Act
        result = await (queryable
            .where(lambda p: p.age > 25)
            .order_by(lambda p: p.name)
            .skip(1)
            .take(1)
            .to_list())
        
        # Assert
        assert len(result) == 1
        assert result[0].name == "Bob"
        
        # Verify Cypher query
        call_args = mock_session.run.call_args[0][0]
        assert "WHERE n.age > $age_0" in call_args
        assert "ORDER BY n.name" in call_args
        assert "SKIP 1" in call_args
        assert "LIMIT 1" in call_args


class TestNeo4jRelationshipQueryable:
    """Test the Neo4jRelationshipQueryable LINQ-style API"""
    
    @pytest.fixture
    def mock_session(self):
        """Create a mock Neo4j session"""
        session = AsyncMock()
        session.run = AsyncMock()
        return session
    
    @pytest.fixture
    def queryable(self, mock_session):
        """Create a Neo4jRelationshipQueryable instance"""
        return Neo4jRelationshipQueryable(WorksFor, mock_session)
    
    @pytest.mark.asyncio
    async def test_where_condition(self, queryable, mock_session):
        """Test where clause on relationships"""
        # Arrange
        mock_result = AsyncMock()
        mock_result.data = AsyncMock(return_value=[{"r": {"position": "Developer", "salary": 80000, "start_node_id": "node1", "end_node_id": "node2"}}])
        mock_session.run.return_value = mock_result
        
        # Act
        result = await queryable.where(lambda r: r.salary > 70000).to_list()
        
        # Assert
        assert len(result) == 1
        assert result[0].position == "Developer"
        assert result[0].salary == 80000
        
        # Verify Cypher query
        call_args = mock_session.run.call_args[0][0]
        assert "MATCH ()-[r:WORKS_FOR]->()" in call_args
        assert "WHERE r.salary > $salary_0" in call_args
    
    @pytest.mark.asyncio
    async def test_order_by_relationship(self, queryable, mock_session):
        """Test order by on relationships"""
        # Arrange
        mock_result = AsyncMock()
        mock_result.data = AsyncMock(return_value=[
            {"r": {"position": "Intern", "salary": 30000, "start_node_id": "node1", "end_node_id": "node2"}}, 
            {"r": {"position": "Developer", "salary": 80000, "start_node_id": "node3", "end_node_id": "node4"}}, 
            {"r": {"position": "Manager", "salary": 120000, "start_node_id": "node5", "end_node_id": "node6"}}
        ])
        mock_session.run.return_value = mock_result
        
        # Act
        result = await queryable.order_by(lambda r: r.salary).to_list()
        
        # Assert
        assert len(result) == 3
        assert result[0].salary == 30000
        assert result[1].salary == 80000
        assert result[2].salary == 120000
        
        # Verify Cypher query
        call_args = mock_session.run.call_args[0][0]
        assert "ORDER BY r.salary" in call_args
    
    @pytest.mark.asyncio
    async def test_select_relationship_projection(self, queryable, mock_session):
        """Test select for relationship projection"""
        # Arrange
        mock_result = AsyncMock()
        mock_result.data = AsyncMock(return_value=[{"position": "Developer", "salary": 80000, "start_node_id": "node1", "end_node_id": "node2"}])
        mock_session.run.return_value = mock_result
        
        # Act
        result = await queryable.select(lambda r: {"position": r.position, "salary": r.salary}).to_list()
        
        # Assert
        assert len(result) == 1
        assert result[0]["position"] == "Developer"
        assert result[0]["salary"] == 80000
        
        # Verify Cypher query
        call_args = mock_session.run.call_args[0][0]
        assert "RETURN r.position AS position, r.salary AS salary" in call_args
    
    @pytest.mark.asyncio
    async def test_first_relationship(self, queryable, mock_session):
        """Test first method on relationships"""
        # Arrange
        mock_result = AsyncMock()
        mock_result.data = AsyncMock(return_value=[{"r": {"position": "Developer", "salary": 80000, "start_node_id": "node1", "end_node_id": "node2"}}])
        mock_session.run.return_value = mock_result
        
        # Act
        result = await queryable.first()
        
        # Assert
        assert result.position == "Developer"
        assert result.salary == 80000
        
        # Verify Cypher query
        call_args = mock_session.run.call_args[0][0]
        assert "LIMIT 1" in call_args


class TestCypherBuilder:
    """Test the CypherBuilder for translating lambda expressions to Cypher"""
    
    def test_simple_equality(self):
        """Test simple equality condition"""
        builder = CypherBuilder(Person)
        condition = lambda p: p.name == "Alice"
        # This would be called internally by the queryable
        # For now, we'll test the basic structure
        assert builder is not None  # Placeholder test
    
    def test_comparison_operators(self):
        """Test comparison operators"""
        builder = CypherBuilder(Person)
        # Test various operators would go here
        assert builder is not None  # Placeholder test
    
    def test_logical_operators(self):
        """Test logical operators (AND, OR)"""
        builder = CypherBuilder(Person)
        # Test AND/OR operators would go here
        assert builder is not None  # Placeholder test


if __name__ == "__main__":
    pytest.main([__file__]) 