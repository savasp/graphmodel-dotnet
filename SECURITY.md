# Security Policy

## 🛡️ Supported Versions

We actively support the following versions with security updates:

| Version | Supported         |
| ------- | ----------------- |
| 1.x.x   | ✅ Active support |
| 0.x.x   | ❌ End of life    |

## 🚨 Reporting a Vulnerability

**Please do not report security vulnerabilities through public GitHub issues.**

### How to Report

1. **Email**: Send details to [savas@parastatidis.name]
2. **Include**:
   - Description of the vulnerability
   - Steps to reproduce
   - Potential impact assessment
   - Suggested fix (if any)

### What to Expect

This is not a supported product but we will do our best to provide:

- **Acknowledgment**: Within a week
- **Initial Assessment**: Within 2 weeks
- **Regular Updates**: Every 1-2 weeks until resolved
- **Resolution Timeline**: Varies by severity (see below)

## 🔒 Security Considerations

### Database Connections

- Always use encrypted connections to Neo4j
- Implement proper authentication and authorization
- Use connection pooling securely
- Validate connection strings

### Query Security

- CVOYA graph automatically parameterizes queries to prevent injection
- Be cautious with dynamic query building
- Validate user inputs before querying
- Use strongly-typed queries when possible

### Data Serialization

- Complex properties are serialized securely
- Be aware of deserialization vulnerabilities
- Validate serialized data sources
- Consider encryption for sensitive data

### Configuration

- Store connection strings securely (Azure Key Vault, etc.)
- Use environment variables for sensitive configuration
- Follow least-privilege principles
- Regularly rotate credentials

## 🔧 Best Practices for Users

### Connection Security

```csharp
// ✅ Good - encrypted connection
var store = new Neo4jGraphStore("neo4j+s://your-server:7687",
    "username", "password", "neo4j");
var graph = store.Graph;

// ❌ Avoid - unencrypted connection
var insecureStore = new Neo4jGraphStore("neo4j://your-server:7687",
    "username", "password", "neo4j");
```

### Input Validation

```csharp
// ✅ Good - parameterized queries
var users = await (await graph.NodesAsync<User>())
    .Where(u => u.Email == email)  // Automatically parameterized
    .ToListAsync();

// ✅ Good - validate inputs
if (string.IsNullOrWhiteSpace(email) || !IsValidEmail(email))
    throw new ArgumentException("Invalid email");
```

### Configuration Security

```csharp
// ✅ Good - environment variables
var connectionString = Environment.GetEnvironmentVariable("NEO4J_CONNECTION");
var username = Environment.GetEnvironmentVariable("NEO4J_USERNAME");
var password = Environment.GetEnvironmentVariable("NEO4J_PASSWORD");

// ❌ Avoid - hardcoded credentials
var store = new Neo4jGraphStore("neo4j://localhost:7687", "neo4j", "password123");
```

## 🚀 Security Updates

Security updates are released as:

- **Patch releases** for supported versions
- **Out-of-band releases** for critical vulnerabilities
- **Security advisories** on GitHub

## 📝 Security Checklist

Before deploying to production:

- [ ] Use encrypted database connections
- [ ] Implement proper authentication
- [ ] Validate all user inputs
- [ ] Store credentials securely
- [ ] Enable logging and monitoring
- [ ] Keep dependencies updated
- [ ] Review custom serializers
- [ ] Test with security scanning tools
- [ ] Follow principle of least privilege
- [ ] Implement rate limiting if applicable

## 🔗 Related Resources

- [OWASP Top 10](https://owasp.org/www-project-top-ten/)
- [Neo4j Security Guidelines](https://neo4j.com/docs/operations-manual/current/security/)
- [.NET Security Best Practices](https://docs.microsoft.com/en-us/dotnet/standard/security/)

## 📞 Contact

For security-related questions or concerns:

- **Security Email**: [savas@parastatidis.name]
- **General Issues**: [GitHub Issues](https://github.com/cvoya-com/graph/issues)
- **Documentation**: [GitHub Discussions](https://github.com/cvoya-com/graph/discussions/)

---

**Thank you for helping keep CVOYA graph secure!** 🙏
