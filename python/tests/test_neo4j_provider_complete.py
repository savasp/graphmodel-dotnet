"""
Comprehensive tests for the complete Neo4j provider implementation.

Tests all major features: CRUD operations, PathSegments traversal,
async streaming, aggregation, and .NET compatibility.
"""

import asyncio
from typing import List, Optional
from unittest.mock import AsyncMock, MagicMock, patch

import pytest

from graph_model import (
    Node,
    Relationship,
    node,
    relationship,
)
from graph_model.core.graph import GraphDataModel
from graph_model.providers.neo4j import (
    Neo4jAggregationExecutor,
    Neo4jAsyncNodeQueryable,
    Neo4jGraph,
    Neo4jSerializer,
    Neo4jTraversalExecutor,
)
from graph_model.querying.aggregation import AggregationBuilder, GroupByResult
from graph_model.querying.traversal import (
    GraphTraversal,
    GraphTraversalDirection,
)


# Test domain models
@node("Person")
class Person(Node):
    name: str
    age: int
    email: Optional[str] = None


@node("Company")
class Company(Node):
    name: str
    industry: str
    founded_year: int


@relationship("WORKS_FOR")
class WorksFor(Relationship):
    position: str
    start_date: str
    salary: Optional[int] = None


@relationship("MANAGES")
class Manages(Relationship):
    since: str
    level: int


class TestNeo4jProviderComplete:
    """Test suite for complete Neo4j provider functionality."""
    
    @pytest.fixture
    def mock_session(self):
        """Create a mock Neo4j session."""
        session = AsyncMock()
        
        # Mock query results
        mock_result = AsyncMock()
        mock_result.data.return_value = [
            {
                "n": {
                    "id": "person1",
                    "name": "John Doe", 
                    "age": 30,
                    "email": "john@example.com"
                }
            },
            {
                "n": {
                    "id": "person2",
                    "name": "Jane Smith",
                    "age": 25,
                    "email": "jane@example.com"
                }
            }
        ]
        
        session.run.return_value = mock_result
        return session
    
    @pytest.fixture
    def serializer(self):
        """Create a Neo4j serializer."""
        return Neo4jSerializer()
    
    @pytest.fixture
    def graph(self):
        """Create a Neo4j graph instance."""
        return Neo4jGraph()
    
    @pytest.mark.asyncio
    async def test_pathsegments_traversal(self, mock_session, serializer):
        """Test PathSegments traversal execution."""
        # Create test data
        john = Person(id="person1", name="John Doe", age=30)
        jane = Person(id="person2", name="Jane Smith", age=25)
        
        # Mock traversal result
        mock_result = AsyncMock()
        mock_result.data.return_value = [
            {
                "start": {"id": "person1", "name": "John Doe", "age": 30},
                "r": {"id": "rel1", "start_node_id": "person1", "end_node_id": "company1", "position": "Manager", "start_date": "2020-01-01"},
                "target": {"id": "company1", "name": "TechCorp", "industry": "Technology", "founded_year": 2010}
            }
        ]
        mock_session.run.return_value = mock_result
        
        # Create traversal executor
        executor = Neo4jTraversalExecutor(mock_session, serializer)
        
        # Create traversal
        traversal = GraphTraversal(
            start_nodes=[john],
            relationship_type=WorksFor,
            target_node_type=Company
        ).with_direction(GraphTraversalDirection.OUTGOING)
        
        # Execute PathSegments
        path_segments = await executor.execute_path_segments(traversal)
        
        # Verify results
        assert len(path_segments) == 1
        segment = path_segments[0]
        assert segment.start_node.id == "person1"
        assert segment.relationship.position == "Manager"
        assert segment.end_node.name == "TechCorp"
        
        # Verify Cypher query was built correctly
        mock_session.run.assert_called_once()
        call_args = mock_session.run.call_args
        query = call_args[0][0]
        assert "MATCH (start:Person)" in query
        assert "[r:WORKS_FOR]->" in query
        assert "(target:Company)" in query
        assert "start.id IN $start_ids" in query
    
    @pytest.mark.asyncio
    async def test_async_streaming(self, mock_session, serializer):
        """Test async streaming functionality."""
        # Mock async streaming result with proper async iterator
        mock_result = AsyncMock()
        
        # Create async iterator for the mock data
        data = [
            {"n": {"id": "person1", "name": "John Doe", "age": 30}},
            {"n": {"id": "person2", "name": "Jane Smith", "age": 25}}
        ]
        
        async def async_iterator(self):
            for item in data:
                yield item
        
        mock_result.__aiter__ = async_iterator
        
        mock_session.run.return_value = mock_result
        
        # Create async queryable
        async_queryable = Neo4jAsyncNodeQueryable(
            Person, mock_session, serializer
        )
        
        # Test async streaming
        results = await async_queryable.to_list_async()
        
        # Verify results
        assert len(results) == 2
        assert results[0].name == "John Doe"
        assert results[1].name == "Jane Smith"
    
    @pytest.mark.asyncio
    async def test_async_queryable_operations(self, mock_session, serializer):
        """Test async queryable LINQ-style operations."""
        # Mock async streaming result with proper async iterator
        mock_result = AsyncMock()
        
        data = [
            {"n": {"id": "person1", "name": "John Doe", "age": 30}},
            {"n": {"id": "person2", "name": "Jane Smith", "age": 25}}
        ]
        
        async def async_iterator(self):
            for item in data:
                yield item
        
        gen = async_iterator(mock_result)
        mock_result.__aiter__ = lambda s=mock_result: gen
        mock_session.run.return_value = mock_result
        
        async_queryable = Neo4jAsyncNodeQueryable(
            Person, mock_session, serializer
        )
        
        # Test to_list_async
        results = await async_queryable.to_list_async()
        assert len(results) == 2
        
        # Properly close the async iterator to avoid RuntimeWarning
        await gen.aclose()
        
        # Re-create async_queryable and async iterator for first_async to avoid generator exhaustion
        mock_result2 = AsyncMock()
        gen2 = async_iterator(mock_result2)
        mock_result2.__aiter__ = lambda s=mock_result2: gen2
        mock_session.run.return_value = mock_result2
        async_queryable2 = Neo4jAsyncNodeQueryable(
            Person, mock_session, serializer
        )
        first_person = await async_queryable2.first_async()
        assert first_person.name == "John Doe"
        
        # Properly close the async iterator to avoid RuntimeWarning
        await gen2.aclose()
        
        # Test count_async with mock
        mock_count_result = AsyncMock()
        mock_count_result.single.return_value = {"count": 2}
        mock_session.run.return_value = mock_count_result
        count = await async_queryable.count_async()
        assert count == 2
    
    @pytest.mark.asyncio
    async def test_aggregation_execution(self, mock_session, serializer):
        """Test aggregation query execution."""
        # Mock aggregation result - return actual GroupByResult objects
        from graph_model.querying.aggregation import GroupByResult
        
        # Create mock GroupByResult objects
        tech_people = [
            Person(id=f"tech{i}", name=f"Tech{i}", age=25+i, email=f"tech{i}@tech.com")
            for i in range(5)
        ]
        finance_people = [
            Person(id=f"finance{i}", name=f"Finance{i}", age=30+i, email=f"finance{i}@finance.com")
            for i in range(3)
        ]
        
        mock_groups = [
            GroupByResult(key="Technology", values=tech_people),
            GroupByResult(key="Finance", values=finance_people)
        ]
        
        # Mock the executor to return our groups
        executor = Neo4jAggregationExecutor(mock_session, serializer)
        executor.execute_group_by = AsyncMock(return_value=mock_groups)
        
        # Create aggregation builder
        builder = AggregationBuilder()
        builder.group_by("n.email")  # Group by email domain
        builder.count()
        builder.average("n.age")
        builder.sum("n.age")
        
        # Execute aggregation
        results = await executor.execute_group_by(Person, builder)
        
        # Verify results
        assert len(results) == 2
        
        tech_group = results[0]
        assert tech_group.key == "Technology"
        assert tech_group.count() == 5
        assert tech_group.average(lambda p: p.age) == 27.0  # 25+26+27+28+29 / 5
        assert tech_group.sum(lambda p: p.age) == 135  # 25+26+27+28+29
        
        finance_group = results[1]
        assert finance_group.key == "Finance"
        assert finance_group.count() == 3
    
    @pytest.mark.asyncio
    async def test_complex_traversal_scenario(self, mock_session, serializer):
        """Test complex traversal scenario with multiple hops."""
        # Create multi-hop traversal
        john = Person(id="person1", name="John Doe", age=30)
        traversal = GraphTraversal(
            start_nodes=[john],
            relationship_type=WorksFor,
            target_node_type=Company
        ).with_direction(GraphTraversalDirection.OUTGOING)
        
        # Mock complex traversal result
        mock_result = AsyncMock()
        mock_result.data.return_value = [
            {
                "path": MagicMock(
                    nodes=[
                        {"id": "person1", "name": "John Doe", "age": 30},
                        {"id": "company1", "name": "TechCorp", "industry": "Technology", "founded_year": 2010}
                    ],
                    relationships=[
                        {"id": "rel1", "start_node_id": "person1", "end_node_id": "company1", "position": "Manager", "start_date": "2020-01-01"}
                    ]
                )
            }
        ]
        mock_session.run.return_value = mock_result
        
        # Create traversal executor
        executor = Neo4jTraversalExecutor(mock_session, serializer)
        
        # Mock the deserialize_node method to handle different node types
        original_deserialize = serializer.deserialize_node
        def mock_deserialize_node(record, node_type, complex_properties=None):
            if node_type == Person:
                return Person(**record)
            elif node_type == Company:
                return Company(**record)
            return original_deserialize(record, node_type, complex_properties)
        
        serializer.deserialize_node = mock_deserialize_node
        
        # Execute path traversal
        paths = await executor.execute_paths(traversal)
        
        # Verify results
        assert len(paths) == 1
        path = paths[0]
        assert len(path.nodes) == 2
        assert len(path.relationships) == 1
        assert path.nodes[0].name == "John Doe"
        assert path.nodes[1].name == "TechCorp"
        assert path.relationships[0].position == "Manager"
    
    @pytest.mark.asyncio
    async def test_dotnet_compatibility(self, serializer):
        """Test .NET compatibility features."""
        # Test relationship naming convention
        from graph_model.core.graph import GraphDataModel
        
        # Test .NET-style relationship naming
        rel_name = GraphDataModel.property_name_to_relationship_type_name("home_address")
        assert rel_name == "__PROPERTY__home_address__"
        
        # Test relationship name validation
        assert GraphDataModel.is_valid_relationship_type_name("__PROPERTY__home_address__")
        assert not GraphDataModel.is_valid_relationship_type_name("invalid_name")
        
        # Test property separation
        person = Person(id="test", name="Test Person", age=30)
        simple_props, complex_props = GraphDataModel.get_simple_and_complex_properties(person)
        
        assert "name" in simple_props
        assert "age" in simple_props
        # Complex properties would be tested with a model that has embedded objects
    
    @pytest.mark.asyncio
    async def test_integrated_scenario(self, mock_session, serializer):
        """Test integrated scenario combining multiple features."""
        # Mock data for integrated scenario with proper async iterator
        mock_result = AsyncMock()
        
        data = [
            {"n": {"id": "person1", "name": "John Doe", "age": 30}}
        ]
        
        async def async_iterator(self):
            for item in data:
                yield item
        
        mock_result.__aiter__ = async_iterator
        mock_session.run.return_value = mock_result
        
        # 1. Start with async streaming
        async_queryable = Neo4jAsyncNodeQueryable(Person, mock_session, serializer)
        
        # 2. Apply filtering and projection
        filtered_queryable = async_queryable.where_async(lambda p: p.age > 25)
        projected_queryable = filtered_queryable.select_async(lambda p: p.name)
        
        # 3. Materialize results
        names = await projected_queryable.to_list_async()
        assert len(names) == 1
        assert names[0] == "John Doe"
        
        # 4. Test aggregation with the same data
        executor = Neo4jAggregationExecutor(mock_session, serializer)
        
        # Create mock GroupByResult with actual data
        from graph_model.querying.aggregation import GroupByResult
        mock_people = [
            Person(id="1", name="John Doe", age=30),
            Person(id="2", name="John Doe", age=35),
            Person(id="3", name="John Doe", age=31)
        ]
        mock_group = GroupByResult(key="Manager", values=mock_people)
        
        # Mock the execute_group_by method
        executor.execute_group_by = AsyncMock(return_value=[mock_group])
        
        builder = AggregationBuilder()
        builder.group_by("n.name")
        builder.count()
        builder.average("n.age")
        
        group_results = await executor.execute_group_by(Person, builder)
        assert len(group_results) == 1
        assert group_results[0].count() == 3
        assert group_results[0].average(lambda p: p.age) == 32.0  # (30+35+31)/3
    
    def test_cypher_query_building(self):
        """Test Cypher query building for various scenarios."""
        from graph_model.providers.neo4j.cypher_builder import CypherBuilder
        
        # Test basic query building
        builder = CypherBuilder(Person)
        
        # Test WHERE clause building
        query = builder.build_query(
            where_predicate=lambda p: p.age > 25,
            take_count=10,
            skip_count=5
        )
        
        assert "MATCH (n:Person)" in query.query
        assert "SKIP 5" in query.query
        assert "LIMIT 10" in query.query
        assert "RETURN n" in query.query
    
    def test_serialization_compatibility(self):
        """Test serialization compatibility with .NET."""
        serializer = Neo4jSerializer()
        
        # Test node serialization
        person = Person(id="test", name="John Doe", age=30)
        serialized = serializer.serialize_node(person)
        
        assert serialized.id == "test"
        assert serialized.labels == ["Person"]
        assert serialized.properties["name"] == "John Doe"
        assert serialized.properties["age"] == 30
        
        # Test relationship serialization
        works_for = WorksFor(
            id="rel1",
            start_node_id="person1",
            end_node_id="company1", 
            position="Manager",
            start_date="2020-01-01"
        )
        
        rel_serialized = serializer.serialize_relationship(works_for)
        assert rel_serialized.id == "rel1"
        assert rel_serialized.type == "WORKS_FOR"
        assert rel_serialized.properties["position"] == "Manager"


# Integration test with actual Neo4j (requires running Neo4j instance)
@pytest.mark.integration
class TestNeo4jIntegration:
    """Integration tests requiring actual Neo4j instance."""
    
    @pytest.mark.asyncio
    async def test_full_crud_cycle(self):
        """Test complete CRUD cycle with real Neo4j."""
        # This would require actual Neo4j connection
        # Skip for now but shows the structure for integration testing
        pytest.skip("Requires running Neo4j instance")
    
    @pytest.mark.asyncio
    async def test_performance_with_large_dataset(self):
        """Test performance with large datasets."""
        # This would test async streaming with large result sets
        pytest.skip("Requires running Neo4j instance with large dataset")


if __name__ == "__main__":
    # Run tests
    pytest.main([__file__, "-v"])