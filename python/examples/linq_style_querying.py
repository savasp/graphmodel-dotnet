"""
LINQ-Style Querying Example

This example demonstrates the full LINQ-style queryable API for the GraphModel library,
showing how to perform complex queries with a familiar LINQ-like syntax.
"""

import asyncio
from datetime import datetime
from typing import Any, Dict, List, Optional

from graph_model import (
    Node,
    Relationship,
    node,
    relationship,
)
from graph_model.providers.neo4j import Neo4jDriver, Neo4jGraph


@node("Person")
class Person(Node):
    name: str
    age: int
    city: Optional[str] = None
    email: Optional[str] = None


@node("Company")
class Company(Node):
    name: str
    industry: str
    founded_year: int
    revenue: Optional[float] = None


@node("Skill")
class Skill(Node):
    name: str
    category: str
    difficulty_level: int


@relationship("WORKS_FOR")
class WorksFor(Relationship):
    position: str
    salary: int
    start_date: Optional[str] = None


@relationship("HAS_SKILL")
class HasSkill(Relationship):
    proficiency_level: int
    years_experience: Optional[int] = None


@relationship("KNOWS")
class Knows(Relationship):
    relationship_type: str = "colleague"


async def demonstrate_linq_style_querying():
    """Demonstrate the full LINQ-style querying capabilities"""
    
    print("=== LINQ-Style Querying Examples ===\n")
    print("Note: This example demonstrates the LINQ-style API structure.")
    print("For actual database operations, a Neo4j instance would be required.\n")
    
    # Initialize the graph (commented out for demo purposes)
    # await Neo4jDriver.initialize("bolt://localhost:7687", "neo4j", "password", database="LINQExamples")
    # await Neo4jDriver.ensure_database_exists()
    # graph = Neo4jGraph()
    
    try:
        # Create sample data (would require actual database)
        # await create_sample_data(graph)
        
        # Demonstrate the API structure without actual execution
        print("1. Basic WHERE clauses:")
        print("-" * 40)
        print("# Find people over 30")
        print("people_over_30 = await graph.nodes(Person).where(lambda p: p.age > 30).to_list()")
        print("# Result would be: [Person objects with age > 30]")
        print()
        
        print("# Find people in specific cities")
        print("boston_people = await graph.nodes(Person).where(lambda p: p.city == 'Boston').to_list()")
        print("# Result would be: [Person objects in Boston]")
        print()
        
        print("# Complex WHERE with multiple conditions")
        print("senior_devs = await graph.nodes(Person).where(")
        print("    lambda p: p.age > 25 and p.city == 'San Francisco'")
        print(").to_list()")
        print("# Result would be: [Person objects matching both conditions]")
        print()
        
        # 2. ORDER BY operations
        print("2. ORDER BY operations:")
        print("-" * 40)
        print("# Order by age ascending")
        print("people_by_age = await graph.nodes(Person).order_by(lambda p: p.age).to_list()")
        print("# Result would be: [Person objects ordered by age]")
        print()
        
        print("# Order by name descending")
        print("people_by_name_desc = await graph.nodes(Person).order_by_descending(lambda p: p.name).to_list()")
        print("# Result would be: [Person objects ordered by name desc]")
        print()
        
        # 3. Pagination with TAKE and SKIP
        print("3. Pagination with TAKE and SKIP:")
        print("-" * 40)
        print("# Get first 3 people")
        print("first_three = await graph.nodes(Person).take(3).to_list()")
        print("# Result would be: [First 3 Person objects]")
        print()
        
        print("# Skip first 2, take next 2")
        print("page_2 = await graph.nodes(Person).skip(2).take(2).to_list()")
        print("# Result would be: [Person objects 3-4]")
        print()
        
        # 4. SELECT projections
        print("4. SELECT projections:")
        print("-" * 40)
        print("# Project to simple dictionary")
        print("name_age_pairs = await graph.nodes(Person).select(")
        print("    lambda p: {'name': p.name, 'age': p.age}")
        print(").to_list()")
        print("# Result would be: [{'name': 'Alice', 'age': 30}, ...]")
        print()
        
        print("# Project with computed values")
        print("salary_info = await graph.nodes(Person).select(")
        print("    lambda p: {")
        print("        'name': p.name,")
        print("        'age_group': 'senior' if p.age > 30 else 'junior',")
        print("        'location': p.city or 'Unknown'")
        print("    }")
        print(").to_list()")
        print("# Result would be: [{'name': 'Alice', 'age_group': 'senior', ...}, ...]")
        print()
        
        # 5. FIRST and FIRST_OR_NONE
        print("5. FIRST and FIRST_OR_NONE:")
        print("-" * 40)
        print("# Get first person")
        print("first_person = await graph.nodes(Person).first()")
        print("# Result would be: Person object or exception if none found")
        print()
        
        print("# Try to find someone who doesn't exist")
        print("non_existent = await graph.nodes(Person).where(lambda p: p.name == 'NonExistent').first_or_none()")
        print("# Result would be: None")
        print()
        
        # 6. Relationship queries
        print("6. Relationship queries:")
        print("-" * 40)
        print("# Find high-paying jobs")
        print("high_paying_jobs = await graph.relationships(WorksFor).where(")
        print("    lambda r: r.salary > 100000")
        print(").to_list()")
        print("# Result would be: [WorksFor relationships with high salaries]")
        print()
        
        print("# Order relationships by salary")
        print("jobs_by_salary = await graph.relationships(WorksFor).order_by_descending(")
        print("    lambda r: r.salary")
        print(").to_list()")
        print("# Result would be: [WorksFor relationships ordered by salary desc]")
        print()
        
        # 7. TRAVERSE relationships
        print("7. TRAVERSE relationships:")
        print("-" * 40)
        print("# Find people who work for tech companies")
        print("tech_workers = await graph.nodes(Person).traverse('WORKS_FOR', Company).where(")
        print("    lambda target: target.industry == 'Technology'")
        print(").to_list()")
        print("# Result would be: [Person objects working in tech]")
        print()
        
        print("# Find people with specific skills")
        print("python_developers = await graph.nodes(Person).traverse('HAS_SKILL', Skill).where(")
        print("    lambda target: target.name == 'Python'")
        print(").to_list()")
        print("# Result would be: [Person objects with Python skill]")
        print()
        
        # 8. Complex chained queries
        print("8. Complex chained queries:")
        print("-" * 40)
        print("# Find senior developers in tech companies")
        print("senior_tech_devs = await (graph.nodes(Person)")
        print("    .where(lambda p: p.age > 30)")
        print("    .traverse('WORKS_FOR', Company)")
        print("    .where(lambda target: target.industry == 'Technology')")
        print("    .order_by_descending(lambda p: p.age)")
        print("    .take(5)")
        print("    .to_list())")
        print("# Result would be: [Top 5 senior tech workers]")
        print()
        
        print("# Complex relationship query with projection")
        print("job_summary = await (graph.relationships(WorksFor)")
        print("    .where(lambda r: r.salary > 50000)")
        print("    .order_by_descending(lambda r: r.salary)")
        print("    .take(3)")
        print("    .select(lambda r: {")
        print("        'position': r.position,")
        print("        'salary_range': 'high' if r.salary > 100000 else 'medium',")
        print("        'start_date': r.start_date or 'Unknown'")
        print("    })")
        print("    .to_list())")
        print("# Result would be: [Job summary dictionaries]")
        print()
        
        print("=== LINQ-Style Features Summary ===")
        print("✓ Type-safe lambda expressions")
        print("✓ Fluent method chaining")
        print("✓ WHERE filtering with complex conditions")
        print("✓ ORDER BY ascending/descending")
        print("✓ TAKE/SKIP pagination")
        print("✓ SELECT projections with computed values")
        print("✓ FIRST/FIRST_OR_NONE safe access")
        print("✓ Relationship traversal")
        print("✓ Depth-limited traversal")
        print("✓ Complex chained operations")
        print("✓ Error handling patterns")
        print()
        
        print("=== .NET Compatibility ===")
        print("This API matches the .NET GraphModel LINQ implementation:")
        print("- Same method names and signatures")
        print("- Compatible lambda expression patterns")
        print("- Identical traversal and filtering concepts")
        print("- Cross-platform data compatibility")
        
    except Exception as e:
        print(f"Demo completed with note: {e}")
        print("For actual database operations, initialize Neo4j driver first.")


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