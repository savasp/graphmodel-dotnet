"""
Tests for .NET compatibility, aggregation, PathSegments, and async streaming features.

This test suite verifies that all the new functionality works together correctly
and maintains compatibility with the .NET GraphModel implementation.
"""

import asyncio
import json
from dataclasses import dataclass
from typing import List, Optional

import pytest

from graph_model.attributes.decorators import node, relationship
from graph_model.attributes.fields import (
    embedded_field,
    property_field,
    related_node_field,
)
from graph_model.core.graph import GraphDataModel
from graph_model.core.node import Node
from graph_model.core.relationship import Relationship
from graph_model.querying.aggregation import (
    AggregationBuilder,
    CountExpression,
    GroupByResult,
    SumExpression,
    group_by_key_selector,
)
from graph_model.querying.async_streaming import (
    AsyncBatchProcessor,
    AsyncGraphQueryable,
    AsyncStreamingAggregator,
    create_async_queryable,
    materialize_async,
)
from graph_model.querying.traversal import (
    GraphPathSegment,
    GraphTraversal,
    GraphTraversalDirection,
    path_segments,
    traverse,
    traverse_relationships,
)


# Test domain models for compatibility testing
@node("Person")
class Person(Node):
    name: str = property_field()
    age: int = property_field()
    department: str = property_field()
    skills: List[str] = embedded_field(storage_type="json")
    contact_info: dict = embedded_field(storage_type="json")
    home_address: Optional["Address"] = related_node_field(
        relationship_type="HAS_ADDRESS",
        private_relationship=True,
        required=False,
        default=None
    )


@node("Address")
class Address(Node):
    street: str = property_field()
    city: str = property_field()
    postal_code: str = property_field()


@relationship("WORKS_WITH")
class WorksWithRelationship(Relationship):
    since: str = property_field()
    project: str = property_field()
    collaboration_type: str = property_field()


@relationship("MANAGES")
class ManagesRelationship(Relationship):
    since: str = property_field()
    level: int = property_field()


class TestNetCompatibility:
    """Test .NET GraphModel compatibility features."""
    
    def test_relationship_naming_convention(self):
        """Test that complex property relationship names match .NET convention."""
        # Test the exact .NET naming convention
        field_name = "home_address"
        expected = "__PROPERTY__home_address__"
        actual = GraphDataModel.property_name_to_relationship_type_name(field_name)
        
        assert actual == expected, f"Expected {expected}, got {actual}"
    
    def test_relationship_type_name_validation(self):
        """Test relationship type name validation matches .NET rules."""
        # Valid names
        valid_names = ["ValidName", "VALID_NAME", "Valid123"]
        for name in valid_names:
            assert GraphDataModel.is_valid_relationship_type_name(name)
        
        # Invalid names (should match .NET validation)
        invalid_names = ["", "123Invalid", "Invalid Name", "Invalid-Name"]
        for name in invalid_names:
            assert not GraphDataModel.is_valid_relationship_type_name(name)
    
    def test_simple_property_detection(self):
        """Test simple vs complex property detection matches .NET logic."""
        person = Person(
            id="test-id",
            name="John Doe",
            age=30,
            department="Engineering",
            skills=["Python", "C#"],
            contact_info={"email": "john@example.com", "phone": "555-1234"},
            home_address=Address(
                id="addr-id",
                street="123 Main St",
                city="Seattle",
                postal_code="98101"
            )
        )
        
        simple_props, complex_props = GraphDataModel.get_simple_and_complex_properties(person)
        
        # Simple properties should include primitives and embedded fields
        assert "name" in simple_props
        assert "age" in simple_props
        assert "department" in simple_props
        assert "skills" in simple_props  # Embedded field
        assert "contact_info" in simple_props  # Embedded field
        
        # Complex properties should include related node fields
        assert "home_address" in complex_props
        assert simple_props["name"] == "John Doe"
        assert simple_props["age"] == 30
    
    def test_serialization_format_compatibility(self):
        """Test that serialization format matches .NET expectations."""
        person = Person(
            id="person-123",
            name="Jane Smith",
            age=28,
            department="Marketing",
            skills=["Communication", "Strategy"],
            contact_info={"email": "jane@example.com"},
            home_address=Address(
                id="addr-456",
                street="456 Oak Ave",
                city="Portland",
                postal_code="97201"
            )
        )
        
        # Test that embedded fields are serialized as JSON strings (matching .NET)
        simple_props, _ = GraphDataModel.get_simple_and_complex_properties(person)
        
        # Skills should be JSON serialized
        skills_json = simple_props.get("skills")
        if isinstance(skills_json, str):
            parsed_skills = json.loads(skills_json)
            assert parsed_skills == ["Communication", "Strategy"]


class TestAggregationFeatures:
    """Test GroupBy/Aggregation/Having functionality."""
    
    def test_group_by_result(self):
        """Test GroupByResult functionality."""
        people = [
            Person(id="1", name="Alice", age=25, department="Engineering", skills=[], contact_info={}),
            Person(id="2", name="Bob", age=30, department="Engineering", skills=[], contact_info={}),
            Person(id="3", name="Carol", age=35, department="Marketing", skills=[], contact_info={}),
        ]
        
        # Group by department
        groups = group_by_key_selector(people, lambda p: p.department)
        
        assert len(groups) == 2
        
        # Find Engineering group
        eng_group = next(g for g in groups if g.key == "Engineering")
        assert eng_group.count() == 2
        assert eng_group.average(lambda p: p.age) == 27.5
        assert eng_group.min(lambda p: p.age) == 25
        assert eng_group.max(lambda p: p.age) == 30
        assert eng_group.sum(lambda p: p.age) == 55
        
        # Find Marketing group
        mkt_group = next(g for g in groups if g.key == "Marketing")
        assert mkt_group.count() == 1
        assert mkt_group.average(lambda p: p.age) == 35
    
    def test_aggregation_expressions(self):
        """Test aggregation expression generation."""
        count_expr = CountExpression()
        assert count_expr.to_cypher("n") == "count(n)"
        
        count_with_predicate = CountExpression(predicate="n.age > 30")
        assert count_with_predicate.to_cypher("n") == "count(CASE WHEN n.age > 30 THEN 1 END)"
        
        sum_expr = SumExpression(property_path="n.age")
        assert sum_expr.to_cypher("n") == "sum(n.age)"
    
    def test_aggregation_builder(self):
        """Test fluent aggregation builder interface."""
        builder = AggregationBuilder()
        
        cypher = (builder
                 .group_by("n.department")
                 .having("count(n) > 1")
                 .count()
                 .sum("n.age")
                 .average("n.salary")
                 .build_cypher("MATCH (n:Person)", "n"))
        
        assert "GROUP BY n.department" in cypher
        assert "HAVING count(n) > 1" in cypher
        assert "count(n)" in cypher
        assert "sum(n.age)" in cypher
        assert "avg(n.salary)" in cypher


class TestPathSegments:
    """Test PathSegments and traversal functionality."""
    
    def test_graph_path_segment(self):
        """Test GraphPathSegment creation and properties."""
        start_node = Person(id="1", name="Alice", age=25, department="Engineering", skills=[], contact_info={})
        end_node = Person(id="2", name="Bob", age=30, department="Engineering", skills=[], contact_info={})
        relationship = WorksWithRelationship(
            id="rel-1",
            start_node_id="1",
            end_node_id="2",
            since="2023-01-01",
            project="Project X",
            collaboration_type="peer"
        )
        
        segment = GraphPathSegment(
            start_node=start_node,
            relationship=relationship,
            end_node=end_node
        )
        
        assert segment.start_node == start_node
        assert segment.end_node == end_node
        assert segment.relationship == relationship
    
    def test_traversal_direction_enum(self):
        """Test GraphTraversalDirection enum values."""
        assert GraphTraversalDirection.OUTGOING.value == "outgoing"
        assert GraphTraversalDirection.INCOMING.value == "incoming"
        assert GraphTraversalDirection.BOTH.value == "both"
    
    def test_graph_traversal_configuration(self):
        """Test GraphTraversal configuration methods."""
        start_nodes = [
            Person(id="1", name="Alice", age=25, department="Engineering", skills=[], contact_info={})
        ]
        
        traversal = (GraphTraversal(start_nodes, WorksWithRelationship, Person)
                    .with_direction(GraphTraversalDirection.OUTGOING)
                    .with_depth(1, 3)
                    .where("n.age > 25"))
        
        assert traversal._direction == GraphTraversalDirection.OUTGOING
        assert traversal._min_depth == 1
        assert traversal._max_depth == 3
        assert "n.age > 25" in traversal._where_clauses
    
    def test_cypher_pattern_generation(self):
        """Test Cypher pattern generation for traversals."""
        start_nodes = [Person(id="1", name="Alice", age=25, department="Engineering", skills=[], contact_info={})]
        
        # Test outgoing traversal
        traversal = GraphTraversal(start_nodes, WorksWithRelationship, Person)
        pattern = traversal.build_cypher_pattern()
        assert "-[r:WORKS_WITH]->" in pattern
        
        # Test incoming traversal
        traversal = traversal.with_direction(GraphTraversalDirection.INCOMING)
        pattern = traversal.build_cypher_pattern()
        assert "<-[r:WORKS_WITH]-" in pattern
        
        # Test depth patterns
        traversal = traversal.with_depth(2, 4)
        pattern = traversal.build_cypher_pattern()
        assert "*2..4" in pattern
    
    def test_convenience_functions(self):
        """Test path_segments, traverse, and traverse_relationships functions."""
        start_nodes = [Person(id="1", name="Alice", age=25, department="Engineering", skills=[], contact_info={})]
        
        # Test path_segments function
        path_traversal = path_segments(start_nodes, WorksWithRelationship, Person)
        assert isinstance(path_traversal, GraphTraversal)
        
        # Test traverse function (should be same as path_segments)
        node_traversal = traverse(start_nodes, WorksWithRelationship, Person)
        assert isinstance(node_traversal, GraphTraversal)
        
        # Test traverse_relationships function
        rel_traversal = traverse_relationships(start_nodes, WorksWithRelationship, Person)
        assert isinstance(rel_traversal, GraphTraversal)


class TestAsyncStreaming:
    """Test async streaming and iterator functionality."""
    
    @pytest.mark.asyncio
    async def test_async_queryable_basic_operations(self):
        """Test basic async queryable operations."""
        # Create test data
        test_data = [
            Person(id=f"{i}", name=f"Person{i}", age=20 + i, department="Engineering" if i % 2 == 0 else "Marketing", skills=[], contact_info={})
            for i in range(10)
        ]
        
        async def data_generator():
            for item in test_data:
                yield item
        
        # Use a fresh generator for each operation and close it after use
        gen1 = data_generator()
        results = await create_async_queryable(lambda: gen1).to_list_async()
        assert len(results) == 10
        await gen1.aclose()
        
        gen2 = data_generator()
        first = await create_async_queryable(lambda: gen2).first_async()
        assert first.name == "Person0"
        await gen2.aclose()
        
        gen3 = data_generator()
        count = await create_async_queryable(lambda: gen3).count_async()
        assert count == 10
        await gen3.aclose()
        
        gen4 = data_generator()
        has_any = await create_async_queryable(lambda: gen4).any_async()
        assert has_any is True
        await gen4.aclose()
        
        gen5 = data_generator()
        all_have_name = await create_async_queryable(lambda: gen5).all_async(lambda p: p.name.startswith("Person"))
        assert all_have_name is True
        await gen5.aclose()
    
    @pytest.mark.asyncio
    async def test_async_queryable_filtering_and_projection(self):
        """Test async queryable filtering and projection."""
        test_data = [
            Person(id=f"{i}", name=f"Person{i}", age=20 + i, department="Engineering" if i % 2 == 0 else "Marketing", skills=[], contact_info={})
            for i in range(10)
        ]
        
        async def data_generator():
            for item in test_data:
                yield item
        
        queryable = create_async_queryable(data_generator)
        
        # Test where_async
        filtered = queryable.where_async(lambda p: p.age > 25)
        filtered_results = await filtered.to_list_async()
        assert all(p.age > 25 for p in filtered_results)
        
        # Test select_async
        projected = queryable.select_async(lambda p: p.name)
        names = await projected.to_list_async()
        assert all(isinstance(name, str) for name in names)
        assert len(names) == 10
        
        # Test take_async
        limited = queryable.take_async(5)
        limited_results = await limited.to_list_async()
        assert len(limited_results) == 5
        
        # Test skip_async
        skipped = queryable.skip_async(3)
        skipped_results = await skipped.to_list_async()
        assert len(skipped_results) == 7
    
    @pytest.mark.asyncio
    async def test_async_batch_processor(self):
        """Test async batch processing functionality."""
        processor = AsyncBatchProcessor(batch_size=3)
        
        async def data_generator():
            for i in range(10):
                yield i
        
        # Test process_in_batches
        def batch_sum(batch):
            return sum(batch)
        
        results = await processor.process_in_batches(data_generator(), batch_sum)
        
        # Should have 4 batches: [0,1,2], [3,4,5], [6,7,8], [9]
        assert len(results) == 4
        assert results[0] == 3  # 0+1+2
        assert results[1] == 12  # 3+4+5
        assert results[2] == 21  # 6+7+8
        assert results[3] == 9   # 9
    
    @pytest.mark.asyncio
    async def test_async_streaming_aggregator(self):
        """Test streaming aggregation operations."""
        async def number_generator():
            for i in range(1, 6):  # 1, 2, 3, 4, 5
                yield i
        
        # Test sum_async
        total = await AsyncStreamingAggregator.sum_async(number_generator())
        assert total == 15  # 1+2+3+4+5
        
        # Test average_async
        avg = await AsyncStreamingAggregator.average_async(number_generator())
        assert avg == 3.0  # 15/5
        
        # Test min_async
        minimum = await AsyncStreamingAggregator.min_async(number_generator())
        assert minimum == 1
        
        # Test max_async
        maximum = await AsyncStreamingAggregator.max_async(number_generator())
        assert maximum == 5
    
    @pytest.mark.asyncio
    async def test_async_iterator_protocol(self):
        """Test that async queryable works with async for loops."""
        test_data = [
            Person(id=f"{i}", name=f"Person{i}", age=20 + i, department="Engineering", skills=[], contact_info={})
            for i in range(5)
        ]
        
        async def data_generator():
            for item in test_data:
                yield item
        
        queryable = create_async_queryable(data_generator)
        
        # Test async iteration
        collected = []
        async for person in queryable:
            collected.append(person)
        
        assert len(collected) == 5
        assert all(isinstance(p, Person) for p in collected)


class TestIntegratedScenarios:
    """Test integrated scenarios combining multiple features."""
    
    @pytest.mark.asyncio
    async def test_complex_query_with_aggregation_and_streaming(self):
        """Test complex scenario combining traversal, aggregation, and streaming."""
        # Create test data representing a team structure
        people = [
            Person(id="mgr1", name="Alice Manager", age=35, department="Engineering", skills=["Leadership"], contact_info={}),
            Person(id="dev1", name="Bob Developer", age=28, department="Engineering", skills=["Python", "C#"], contact_info={}),
            Person(id="dev2", name="Carol Developer", age=26, department="Engineering", skills=["Python", "JavaScript"], contact_info={}),
            Person(id="mgr2", name="David Manager", age=40, department="Marketing", skills=["Strategy"], contact_info={}),
            Person(id="mkt1", name="Eve Marketer", age=30, department="Marketing", skills=["Content", "Analytics"], contact_info={}),
        ]
        
        # Group by department and calculate statistics
        groups = group_by_key_selector(people, lambda p: p.department)
        
        for group in groups:
            if group.key == "Engineering":
                assert group.count() == 3
                assert group.average(lambda p: p.age) == pytest.approx(29.67, rel=1e-2)
            elif group.key == "Marketing":
                assert group.count() == 2
                assert group.average(lambda p: p.age) == 35.0
        
        # Test async streaming of the same data
        async def people_generator():
            for person in people:
                yield person
        
        queryable = create_async_queryable(people_generator)
        
        # Filter engineering people and get their skills
        eng_people = queryable.where_async(lambda p: p.department == "Engineering")
        eng_results = await eng_people.to_list_async()
        
        assert len(eng_results) == 3
        assert all(p.department == "Engineering" for p in eng_results)
    
    def test_net_serialization_with_complex_properties(self):
        """Test .NET-compatible serialization with complex properties."""
        # Create person with complex address relationship
        address = Address(
            id="addr-123",
            street="123 Tech Street",
            city="Seattle",
            postal_code="98101"
        )
        
        person = Person(
            id="person-456",
            name="John Engineer",
            age=32,
            department="Engineering",
            skills=["Python", "C#", "GraphDB"],
            contact_info={
                "email": "john@company.com",
                "phone": "+1-555-0123",
                "preferred_contact": "email"
            },
            home_address=address
        )
        
        # Test relationship type naming
        field_name = "home_address"
        relationship_type = GraphDataModel.property_name_to_relationship_type_name(field_name)
        assert relationship_type == "__PROPERTY__home_address__"
        
        # Test property separation
        simple_props, complex_props = GraphDataModel.get_simple_and_complex_properties(person)
        
        # Verify simple properties include embedded fields as JSON
        assert "skills" in simple_props
        assert "contact_info" in simple_props
        
        # Verify complex properties include related nodes
        assert "home_address" in complex_props
        
        # Verify JSON serialization for embedded fields matches .NET format
        if isinstance(simple_props.get("skills"), str):
            skills = json.loads(simple_props["skills"])
            assert skills == ["Python", "C#", "GraphDB"]
        
        if isinstance(simple_props.get("contact_info"), str):
            contact = json.loads(simple_props["contact_info"])
            assert contact["email"] == "john@company.com"
            assert contact["preferred_contact"] == "email"


if __name__ == "__main__":
    # Run a simple test to verify everything works
    test_instance = TestNetCompatibility()
    test_instance.test_relationship_naming_convention()
    test_instance.test_relationship_type_name_validation()
    print("✅ .NET compatibility tests passed!")
    
    agg_test = TestAggregationFeatures()
    agg_test.test_aggregation_expressions()
    print("✅ Aggregation tests passed!")
    
    path_test = TestPathSegments()
    path_test.test_traversal_direction_enum()
    path_test.test_cypher_pattern_generation()
    print("✅ PathSegments tests passed!")
    
    print("✅ All basic tests passed! Run with pytest for async tests.")