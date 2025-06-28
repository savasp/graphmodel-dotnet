"""
Queryable API Demo

This example demonstrates the LINQ-style queryable API structure and usage patterns
without requiring a database connection. It shows the fluent interface and method chaining.
"""

from typing import Any, Dict, List, Optional

from graph_model import (
    Node,
    Relationship,
    node,
    relationship,
)


@node("Person")
class Person(Node):
    name: str
    age: int
    city: Optional[str] = None


@node("Company")
class Company(Node):
    name: str
    industry: str


@relationship("WORKS_FOR")
class WorksFor(Relationship):
    position: str
    salary: int


def demonstrate_queryable_api_structure():
    """Demonstrate the queryable API structure and usage patterns"""
    
    print("=== Queryable API Structure Demo ===\n")
    
    # 1. Node Queryable Methods
    print("1. Node Queryable Methods:")
    print("-" * 40)
    print("graph.nodes(Person)")
    print("  .where(lambda p: p.age > 30)")
    print("  .order_by(lambda p: p.name)")
    print("  .order_by_descending(lambda p: p.age)")
    print("  .take(10)")
    print("  .skip(5)")
    print("  .select(lambda p: {'name': p.name, 'age': p.age})")
    print("  .traverse('WORKS_FOR', Company)")
    print("  .with_depth(3)")
    print("  .first()")
    print("  .first_or_none()")
    print("  .to_list()")
    print()
    
    # 2. Relationship Queryable Methods
    print("2. Relationship Queryable Methods:")
    print("-" * 40)
    print("graph.relationships(WorksFor)")
    print("  .where(lambda r: r.salary > 100000)")
    print("  .order_by(lambda r: r.position)")
    print("  .order_by_descending(lambda r: r.salary)")
    print("  .take(5)")
    print("  .skip(2)")
    print("  .select(lambda r: {'position': r.position, 'salary': r.salary})")
    print("  .first()")
    print("  .first_or_none()")
    print("  .to_list()")
    print()
    
    # 3. Example Query Patterns
    print("3. Example Query Patterns:")
    print("-" * 40)
    
    # Basic filtering
    print("Basic filtering:")
    print("  people = await graph.nodes(Person).where(lambda p: p.city == 'Boston').to_list()")
    print()
    
    # Complex filtering
    print("Complex filtering:")
    print("  senior_devs = await graph.nodes(Person).where(")
    print("      lambda p: p.age > 30 and p.city == 'San Francisco'")
    print("  ).to_list()")
    print()
    
    # Sorting
    print("Sorting:")
    print("  people_by_age = await graph.nodes(Person).order_by(lambda p: p.age).to_list()")
    print("  people_by_salary = await graph.nodes(Person).order_by_descending(lambda p: p.age).to_list()")
    print()
    
    # Pagination
    print("Pagination:")
    print("  page_1 = await graph.nodes(Person).take(10).to_list()")
    print("  page_2 = await graph.nodes(Person).skip(10).take(10).to_list()")
    print()
    
    # Projection
    print("Projection:")
    print("  name_age = await graph.nodes(Person).select(")
    print("      lambda p: {'name': p.name, 'age': p.age}")
    print("  ).to_list()")
    print()
    
    # Traversal
    print("Traversal:")
    print("  tech_workers = await graph.nodes(Person).traverse('WORKS_FOR', Company).where(")
    print("      lambda target: target.industry == 'Technology'")
    print("  ).to_list()")
    print()
    
    # Chaining
    print("Chaining:")
    print("  result = await (graph.nodes(Person)")
    print("      .where(lambda p: p.age > 25)")
    print("      .order_by(lambda p: p.name)")
    print("      .take(5)")
    print("      .to_list())")
    print()
    
    # 4. Relationship Queries
    print("4. Relationship Queries:")
    print("-" * 40)
    print("  high_paying_jobs = await graph.relationships(WorksFor).where(")
    print("      lambda r: r.salary > 100000")
    print("  ).to_list()")
    print()
    print("  jobs_by_salary = await graph.relationships(WorksFor).order_by_descending(")
    print("      lambda r: r.salary")
    print("  ).take(10).to_list()")
    print()
    
    # 5. Error Handling
    print("5. Error Handling:")
    print("-" * 40)
    print("  # Safe - returns None if not found")
    print("  person = await graph.nodes(Person).where(lambda p: p.name == 'John').first_or_none()")
    print()
    print("  # Throws exception if not found")
    print("  try:")
    print("      person = await graph.nodes(Person).where(lambda p: p.name == 'John').first()")
    print("  except Exception as e:")
    print("      print(f'Person not found: {e}')")
    print()
    
    # 6. Advanced Patterns
    print("6. Advanced Patterns:")
    print("-" * 40)
    
    # Nested traversal
    print("Nested traversal:")
    print("  python_devs = await graph.nodes(Person)")
    print("      .traverse('WORKS_FOR', Company)")
    print("      .where(lambda target: target.industry == 'Technology')")
    print("      .traverse('HAS_SKILL', Skill)")
    print("      .where(lambda skill: skill.name == 'Python')")
    print("      .to_list()")
    print()
    
    # Complex projection
    print("Complex projection:")
    print("  summary = await graph.nodes(Person).select(")
    print("      lambda p: {")
    print("          'name': p.name,")
    print("          'age_group': 'senior' if p.age > 30 else 'junior',")
    print("          'location': p.city or 'Unknown'")
    print("      }")
    print("  ).to_list()")
    print()
    
    # Relationship projection
    print("Relationship projection:")
    print("  job_summary = await graph.relationships(WorksFor).select(")
    print("      lambda r: {")
    print("          'position': r.position,")
    print("          'salary_range': 'high' if r.salary > 100000 else 'medium',")
    print("          'level': 'senior' if r.salary > 80000 else 'junior'")
    print("      }")
    print("  ).to_list()")
    print()
    
    print("=== Demo Complete ===")
    print("\nThis demonstrates the fluent, LINQ-style API for querying graph data.")
    print("The API provides type-safe, chainable methods for filtering, sorting,")
    print("pagination, projection, and traversal operations.")


def show_api_comparison():
    """Show comparison with traditional approaches"""
    
    print("\n=== API Comparison ===")
    print()
    
    print("Traditional Cypher:")
    print("  MATCH (p:Person)")
    print("  WHERE p.age > 30 AND p.city = 'Boston'")
    print("  ORDER BY p.name")
    print("  RETURN p")
    print("  LIMIT 10")
    print()
    
    print("GraphModel LINQ-style:")
    print("  await graph.nodes(Person)")
    print("      .where(lambda p: p.age > 30 and p.city == 'Boston')")
    print("      .order_by(lambda p: p.name)")
    print("      .take(10)")
    print("      .to_list()")
    print()
    
    print("Benefits:")
    print("  ✓ Type-safe lambda expressions")
    print("  ✓ IntelliSense support")
    print("  ✓ Compile-time error checking")
    print("  ✓ Fluent, readable syntax")
    print("  ✓ Reusable query patterns")
    print("  ✓ .NET compatibility")


if __name__ == "__main__":
    demonstrate_queryable_api_structure()
    show_api_comparison() 