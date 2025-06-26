# Contributing to GraphModel

Thank you for your interest in contributing to GraphModel! This document provides guidelines for contributing to this project.

## 🚀 Getting Started

### Prerequisites

- .NET 8.0 SDK or later
- Docker (for running Neo4j tests)
- Git

### Development Setup

1. **Clone the repository**

   ```bash
   git clone https://github.com/[your-org]/GraphModel.git
   cd GraphModel
   ```

2. **Restore dependencies**

   ```bash
   dotnet restore
   ```

3. **Build the solution**

   ```bash
   dotnet build
   ```

4. **Run tests**

   ```bash
   # Unit tests
   dotnet test tests/Graph.Model.Tests

   # Integration tests (requires Docker)
   dotnet test tests/Graph.Model.Neo4j.Tests
   ```

## 🐛 Reporting Issues

When reporting issues, please include:

- **Description**: Clear description of the problem
- **Reproduction Steps**: Minimal code to reproduce the issue
- **Expected Behavior**: What you expected to happen
- **Actual Behavior**: What actually happened
- **Environment**: .NET version, OS, Neo4j version (if applicable)
- **Code Sample**: Minimal working example demonstrating the issue

## 🔧 Development Guidelines

### Code Style

- Follow standard C# coding conventions
- Use meaningful variable and method names
- Add XML documentation for public APIs
- Keep methods focused and single-purpose

### Testing

- Write unit tests for new functionality
- Ensure all tests pass before submitting PR
- Add integration tests for provider-specific features
- Maintain or improve code coverage

### Documentation

- Update relevant README files
- Add code examples for new features
- Update API documentation
- Include migration notes for breaking changes

## 📝 Pull Request Process

1. **Fork and Branch**

   ```bash
   git checkout -b feature/your-feature-name
   ```

2. **Make Changes**

   - Follow coding guidelines
   - Add tests for new functionality
   - Update documentation

3. **Test Thoroughly**

   ```bash
   dotnet test
   ```

4. **Commit with Clear Messages**

   ```bash
   git commit -m "feat: add graph traversal depth limiting"
   ```

5. **Push and Create PR**
   - Push to your fork
   - Create pull request with clear description
   - Reference related issues

### Commit Message Format

We follow conventional commits:

- `feat:` New features
- `fix:` Bug fixes
- `docs:` Documentation changes
- `refactor:` Code refactoring
- `test:` Test additions/changes
- `chore:` Build/tooling changes

## 🏗️ Project Structure

```
src/
├── Graph.Model/                 # Core abstractions
├── Graph.Model.Neo4j/          # Neo4j provider
├── Graph.Model.Serialization/  # Object serialization
└── Graph.Model.Analyzers/      # Roslyn analyzers

tests/
├── Graph.Model.Tests/          # Unit tests
└── Graph.Model.Neo4j.Tests/   # Integration tests

examples/                       # Usage examples
docs/                          # Documentation
```

## 🎯 Areas for Contribution

### High Priority

- Performance optimizations
- Additional graph database providers
- Enhanced LINQ query capabilities
- Improved error handling and diagnostics

### Medium Priority

- Additional examples and tutorials
- Schema migration tools
- Query optimization hints
- Bulk operations support

### Documentation

- API reference improvements
- More usage examples
- Performance tuning guides
- Migration documentation

## 🔍 Code Review Guidelines

All submissions require review. We look for:

- **Correctness**: Code works as intended
- **Performance**: No unnecessary performance degradation
- **Maintainability**: Code is readable and well-structured
- **Testing**: Adequate test coverage
- **Documentation**: Public APIs are documented

## 📊 Performance Considerations

- Profile performance-critical changes
- Avoid allocations in hot paths
- Consider async/await best practices
- Test with realistic data volumes

## 🛡️ Security

- Report security vulnerabilities privately
- Follow secure coding practices
- Validate user inputs appropriately
- Be mindful of injection attacks in query building

## 📄 License

By contributing, you agree that your contributions will be licensed under the Apache License 2.0.

## 🤝 Community

- Be respectful and inclusive
- Help others learn and grow
- Share knowledge and experiences
- Provide constructive feedback

## ❓ Questions?

- Check existing issues and documentation
- Ask in discussions or create an issue
- Be specific about your use case
- Provide context and examples

Thank you for contributing to GraphModel! 🎉
