"""
LINQ-Style Querying Example

This example demonstrates the full LINQ-style queryable API for the GraphModel library,
showing how to perform complex queries with a familiar LINQ-like syntax.
"""

import asyncio
from datetime import datetime
from typing import Any, Dict, List, Optional

from graph_model.attributes.decorators import (
    auto_field,
    node,
    property_field,
    related_node_field,
    relationship,
)
from graph_model.core.entity import IEntity
from graph_model.core.node import INode
from graph_model.core.relationship import IRelationship
from graph_model.providers.neo4j.graph import Neo4jGraph


@node("Person")
class Person(IEntity, INode):
    def __init__(self, name: str, age: int, city: str = None, email: str = None):
        self.name = name
        self.age = age
        self.city = city
        self.email = email
    
    name: str = property_field()
    age: int = property_field()
    city: Optional[str] = property_field()
    email: Optional[str] = property_field()


@node("Company")
class Company(IEntity, INode):
    def __init__(self, name: str, industry: str, founded_year: int, revenue: float = None):
        self.name = name
        self.industry = industry
        self.founded_year = founded_year
        self.revenue = revenue
    
    name: str = property_field()
    industry: str = property_field()
    founded_year: int = property_field()
    revenue: Optional[float] = property_field()


@node("Skill")
class Skill(IEntity, INode):
    def __init__(self, name: str, category: str, difficulty_level: int):
        self.name = name
        self.category = category
        self.difficulty_level = difficulty_level
    
    name: str = property_field()
    category: str = property_field()
    difficulty_level: int = property_field()


@relationship("WORKS_FOR")
class WorksFor(IEntity, IRelationship):
    def __init__(self, position: str, salary: int, start_date: str = None):
        self.position = position
        self.salary = salary
        self.start_date = start_date
    
    position: str = property_field()
    salary: int = property_field()
    start_date: Optional[str] = property_field()


@relationship("HAS_SKILL")
class HasSkill(IEntity, IRelationship):
    def __init__(self, proficiency_level: int, years_experience: int = None):
        self.proficiency_level = proficiency_level
        self.years_experience = years_experience
    
    proficiency_level: int = property_field()
    years_experience: Optional[int] = property_field()


@relationship("KNOWS")
class Knows(IEntity, IRelationship):
    def __init__(self, relationship_type: str = "colleague"):
        self.relationship_type = relationship_type
    
    relationship_type: str = property_field()


async def demonstrate_linq_style_querying():
    """Demonstrate the full LINQ-style querying capabilities"""
    
    # Initialize the graph
    graph = Neo4jGraph("bolt://localhost:7687", "neo4j", "password")
    
    try:
        # Create sample data
        await create_sample_data(graph)
        
        print("=== LINQ-Style Querying Examples ===\n")
        
        # 1. Basic WHERE clauses
        print("1. Basic WHERE clauses:")
        print("-" * 40)
        
        # Find people over 30
        people_over_30 = await graph.nodes(Person).where(lambda p: p.age > 30).to_list()
        print(f"People over 30: {[p.name for p in people_over_30]}")
        
        # Find people in specific cities
        boston_people = await graph.nodes(Person).where(lambda p: p.city == "Boston").to_list()
        print(f"People in Boston: {[p.name for p in boston_people]}")
        
        # Complex WHERE with multiple conditions
        senior_devs = await graph.nodes(Person).where(
            lambda p: p.age > 25 and p.city == "San Francisco"
        ).to_list()
        print(f"Senior developers in SF: {[p.name for p in senior_devs]}")
        
        print()
        
        # 2. ORDER BY operations
        print("2. ORDER BY operations:")
        print("-" * 40)
        
        # Order by age ascending
        people_by_age = await graph.nodes(Person).order_by(lambda p: p.age).to_list()
        print(f"People ordered by age: {[f'{p.name}({p.age})' for p in people_by_age]}")
        
        # Order by name descending
        people_by_name_desc = await graph.nodes(Person).order_by_descending(lambda p: p.name).to_list()
        print(f"People ordered by name (desc): {[p.name for p in people_by_name_desc]}")
        
        print()
        
        # 3. Pagination with TAKE and SKIP
        print("3. Pagination with TAKE and SKIP:")
        print("-" * 40)
        
        # Get first 3 people
        first_three = await graph.nodes(Person).take(3).to_list()
        print(f"First 3 people: {[p.name for p in first_three]}")
        
        # Skip first 2, take next 2
        page_2 = await graph.nodes(Person).skip(2).take(2).to_list()
        print(f"Page 2 (skip 2, take 2): {[p.name for p in page_2]}")
        
        print()
        
        # 4. SELECT projections
        print("4. SELECT projections:")
        print("-" * 40)
        
        # Project to simple dictionary
        name_age_pairs = await graph.nodes(Person).select(
            lambda p: {"name": p.name, "age": p.age}
        ).to_list()
        print(f"Name-age pairs: {name_age_pairs}")
        
        # Project with computed values
        salary_info = await graph.nodes(Person).select(
            lambda p: {
                "name": p.name,
                "age_group": "senior" if p.age > 30 else "junior",
                "location": p.city or "Unknown"
            }
        ).to_list()
        print(f"Salary info: {salary_info}")
        
        print()
        
        # 5. FIRST and FIRST_OR_NONE
        print("5. FIRST and FIRST_OR_NONE:")
        print("-" * 40)
        
        # Get first person
        first_person = await graph.nodes(Person).first()
        print(f"First person: {first_person.name}")
        
        # Try to find someone who doesn't exist
        non_existent = await graph.nodes(Person).where(lambda p: p.name == "NonExistent").first_or_none()
        print(f"Non-existent person: {non_existent}")
        
        print()
        
        # 6. Relationship queries
        print("6. Relationship queries:")
        print("-" * 40)
        
        # Find high-paying jobs
        high_paying_jobs = await graph.relationships(WorksFor).where(
            lambda r: r.salary > 100000
        ).to_list()
        print(f"High-paying jobs: {[f'{r.position} (${r.salary})' for r in high_paying_jobs]}")
        
        # Order relationships by salary
        jobs_by_salary = await graph.relationships(WorksFor).order_by_descending(
            lambda r: r.salary
        ).to_list()
        print(f"Jobs by salary (desc): {[f'{r.position} (${r.salary})' for r in jobs_by_salary]}")
        
        print()
        
        # 7. TRAVERSE relationships
        print("7. TRAVERSE relationships:")
        print("-" * 40)
        
        # Find people who work for tech companies
        tech_workers = await graph.nodes(Person).traverse("WORKS_FOR", Company).where(
            lambda target: target.industry == "Technology"
        ).to_list()
        print(f"People working in tech: {[p.name for p in tech_workers]}")
        
        # Find people with specific skills
        python_developers = await graph.nodes(Person).traverse("HAS_SKILL", Skill).where(
            lambda target: target.name == "Python"
        ).to_list()
        print(f"Python developers: {[p.name for p in python_developers]}")
        
        print()
        
        # 8. WITH_DEPTH for traversal control
        print("8. WITH_DEPTH for traversal control:")
        print("-" * 40)
        
        # Traverse with depth limit
        depth_limited = await graph.nodes(Person).with_depth(2).to_list()
        print(f"Traversal with depth 2: {len(depth_limited)} results")
        
        print()
        
        # 9. Complex chained queries
        print("9. Complex chained queries:")
        print("-" * 40)
        
        # Find senior developers in tech companies with high salaries
        senior_tech_devs = await (graph.nodes(Person)
            .where(lambda p: p.age > 30)
            .traverse("WORKS_FOR", Company)
            .where(lambda target: target.industry == "Technology")
            .order_by_descending(lambda p: p.age)
            .take(5)
            .to_list())
        
        print(f"Senior tech developers: {[p.name for p in senior_tech_devs]}")
        
        # Complex relationship query with projection
        job_summary = await (graph.relationships(WorksFor)
            .where(lambda r: r.salary > 50000)
            .order_by_descending(lambda r: r.salary)
            .take(3)
            .select(lambda r: {
                "position": r.position,
                "salary_range": "high" if r.salary > 100000 else "medium",
                "start_date": r.start_date or "Unknown"
            })
            .to_list())
        
        print(f"Job summary: {job_summary}")
        
        print()
        
        # 10. Aggregation-like queries (conceptual)
        print("10. Aggregation-like queries:")
        print("-" * 40)
        
        # Get all people and count them
        all_people = await graph.nodes(Person).to_list()
        print(f"Total people: {len(all_people)}")
        
        # Get average age (would need aggregation support)
        ages = [p.age for p in all_people]
        avg_age = sum(ages) / len(ages) if ages else 0
        print(f"Average age: {avg_age:.1f}")
        
        print()
        
        # 11. Error handling examples
        print("11. Error handling examples:")
        print("-" * 40)
        
        try:
            # This would fail if no results found
            result = await graph.nodes(Person).where(lambda p: p.name == "NonExistent").first()
            print(f"Found: {result.name}")
        except Exception as e:
            print(f"Expected error for non-existent person: {type(e).__name__}")
        
        # Safe alternative
        safe_result = await graph.nodes(Person).where(lambda p: p.name == "NonExistent").first_or_none()
        print(f"Safe result: {safe_result}")
        
        print("\n=== Query Examples Complete ===")
        
    finally:
        await graph.close()


async def create_sample_data(graph: Neo4jGraph):
    """Create sample data for the examples"""
    
    # Create people
    alice = Person("Alice Johnson", 28, "San Francisco", "alice@email.com")
    bob = Person("Bob Smith", 35, "Boston", "bob@email.com")
    charlie = Person("Charlie Brown", 42, "New York", "charlie@email.com")
    diana = Person("Diana Prince", 31, "San Francisco", "diana@email.com")
    eve = Person("Eve Wilson", 26, "Boston", "eve@email.com")
    
    # Create companies
    techcorp = Company("TechCorp", "Technology", 2010, 5000000.0)
    finance_inc = Company("Finance Inc", "Finance", 2005, 10000000.0)
    startup_xyz = Company("StartupXYZ", "Technology", 2020, 1000000.0)
    
    # Create skills
    python_skill = Skill("Python", "Programming", 3)
    java_skill = Skill("Java", "Programming", 4)
    sql_skill = Skill("SQL", "Database", 2)
    ml_skill = Skill("Machine Learning", "AI", 5)
    
    # Add nodes to graph
    await graph.add_node(alice)
    await graph.add_node(bob)
    await graph.add_node(charlie)
    await graph.add_node(diana)
    await graph.add_node(eve)
    
    await graph.add_node(techcorp)
    await graph.add_node(finance_inc)
    await graph.add_node(startup_xyz)
    
    await graph.add_node(python_skill)
    await graph.add_node(java_skill)
    await graph.add_node(sql_skill)
    await graph.add_node(ml_skill)
    
    # Create relationships
    alice_works = WorksFor("Senior Developer", 120000, "2022-01-15")
    bob_works = WorksFor("Manager", 150000, "2018-03-10")
    charlie_works = WorksFor("Director", 200000, "2015-06-20")
    diana_works = WorksFor("Developer", 95000, "2023-02-01")
    eve_works = WorksFor("Intern", 45000, "2024-01-15")
    
    # Add relationships
    await graph.add_relationship(alice, alice_works, techcorp)
    await graph.add_relationship(bob, bob_works, techcorp)
    await graph.add_relationship(charlie, charlie_works, finance_inc)
    await graph.add_relationship(diana, diana_works, startup_xyz)
    await graph.add_relationship(eve, eve_works, techcorp)
    
    # Add skills
    alice_python = HasSkill(4, 3)
    alice_java = HasSkill(3, 2)
    bob_python = HasSkill(5, 5)
    bob_sql = HasSkill(4, 4)
    charlie_ml = HasSkill(5, 8)
    diana_python = HasSkill(3, 1)
    eve_java = HasSkill(2, 1)
    
    await graph.add_relationship(alice, alice_python, python_skill)
    await graph.add_relationship(alice, alice_java, java_skill)
    await graph.add_relationship(bob, bob_python, python_skill)
    await graph.add_relationship(bob, bob_sql, sql_skill)
    await graph.add_relationship(charlie, charlie_ml, ml_skill)
    await graph.add_relationship(diana, diana_python, python_skill)
    await graph.add_relationship(eve, eve_java, java_skill)
    
    # Add some "knows" relationships
    alice_knows_bob = Knows("colleague")
    bob_knows_charlie = Knows("mentor")
    diana_knows_eve = Knows("friend")
    
    await graph.add_relationship(alice, alice_knows_bob, bob)
    await graph.add_relationship(bob, bob_knows_charlie, charlie)
    await graph.add_relationship(diana, diana_knows_eve, eve)
    
    print("Sample data created successfully!")


if __name__ == "__main__":
    asyncio.run(demonstrate_linq_style_querying()) 