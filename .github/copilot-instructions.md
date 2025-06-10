Answer all questions in the style of a friendly colleague, using informal language.

If you need to explain something, keep it simple and straightforward. Use examples if they help clarify things.

When writing code:
- Ground your recommendations on the existing codebase whenever possible.
- Use C#13, C#14, and .NET 10 features, like records, pattern matching, and nullable reference types.
- Follow the C# coding conventions, like using PascalCase for class names and camelCase for method parameters.
- Use meaningful variable names and keep your code organized.
- Promote modularity, good code organization, readability, and reusability.
- Break down complex problems into smaller, manageable pieces.
- Each class, interface, struct, or record declaration should be in its own file.
- If you need to use comments, keep them concise and relevant. They should help explain the "why" behind your code, not just the "what".
- If you're writing tests, make sure they're clear and cover the important parts of your code. Use descriptive names for your test methods.

The repository is for a C# project using .NET 10, so focus on C# best practices. Use the latest features and libraries where appropriate.

The repository is about a Graph abstraction layer, so keep that in mind when writing code or explanations. Make sure to consider performance and scalability.

The code is organized as follows:
- `src/`: Contains the main source code.
  - `src/Graph.Model`: Project for the graph model abstraction.
  - `src/Graph.Provider.Neo4j`: Project for the Neo4j graph provider, which is an implementation of the graph model abstraction.
  - `src/Graph.Model.Analyzers`: Contains Roslyn analyzers for the graph model abstraction.
- `tests/Graph.Model.Tests`: Tests on top of the abstraction layer. These tests cannot be run without a providers. It is expected that provider-specific tests will inherit these tests. When writing tests, favor this project to ensure that the tests aren't provider-specific.
- `tests/Graph.Provider.Neo4j.Tests`: Tests for the Neo4j provider. Most of the tests inherit from the tests in `tests/Graph.Model.Tests`. It may also contain provider-specific tests.
- `docs/`: Documentation for the project, including architecture and design decisions.
- `examples`: Usage examples and sample code to help users understand how to use the graph abstraction layer and its providers.
- `possible-futures`: Contains code that is not yet supported but may be in the future. This is more of a playground for ideas and concepts that might be implemented later.
