// Example domain models
public class Person : INode
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public int Age { get; init; }
}

public class Company : INode
{
    public required string Id { get; init; }
    public required string Name { get; init; }
}

public class WorksFor : IRelationship
{
    public required string Id { get; init; }
    public required string StartNodeId { get; init; }
    public required string EndNodeId { get; init; }
    public DateTime Since { get; init; }
    public string? Title { get; init; }
}

public class Knows : IRelationship
{
    public required string Id { get; init; }
    public required string StartNodeId { get; init; }
    public required string EndNodeId { get; init; }
}

// Example queries using the new API
public class GraphQueryExamples
{
    private readonly IGraph _graph;

    public GraphQueryExamples(IGraph graph)
    {
        _graph = graph;
    }

    // Find all companies where people named "John" work
    public async Task<List<Company>> FindCompaniesOfJohns()
    {
        return await _graph.Nodes<Person>()
            .Where(p => p.Name == "John")
            .ConnectedBy<Person, WorksFor, Company>()
            .ToListAsync();
    }

    // Find colleagues of a person (people working at the same company)
    public async Task<List<Person>> FindColleagues(string personId)
    {
        return await _graph.Nodes<Person>()
            .Where(p => p.Id == personId)
            .Traverse<Person, WorksFor>()
            .To<Company>()
            .TraverseFrom<Company, WorksFor>()
            .To<Person>(p => p.Id != personId)
            .Distinct()
            .ToListAsync();
    }

    // Find people within N degrees of separation
    public async Task<List<Person>> FindNetwork(string personId, int degrees)
    {
        return await _graph.Nodes<Person>()
            .Where(p => p.Id == personId)
            .Traverse<Person, Knows>()
            .WithDepth(1, degrees)
            .To<Person>()
            .Distinct()
            .ToListAsync();
    }

    // Find shortest path between two people
    public async Task<GraphPath<Person>?> FindShortestPath(string fromId, string toId)
    {
        var paths = await _graph.Nodes<Person>()
            .Where(p => p.Id == fromId)
            .ShortestPath(p => p.Id == toId)
            .ToListAsync();

        return paths.FirstOrDefault();
    }

    // Complex traversal with filters
    public async Task<List<Company>> FindCompaniesWithSeniorEmployees()
    {
        return await _graph.Nodes<Person>()
            .Where(p => p.Age > 40)
            .Traverse<Person, WorksFor>()
            .Where(w => w.Title != null && w.Title.Contains("Senior"))
            .To<Company>()
            .Distinct()
            .ToListAsync();
    }

    // Pattern matching example
    public async Task<List<Person>> FindTriangularRelationships()
    {
        return await _graph.Nodes<Person>()
            .Match("(p1:Person)-[:KNOWS]->(p2:Person)-[:KNOWS]->(p3:Person)-[:KNOWS]->(p1)")
            .Bind<Person>("p1")
            .Bind<Person>("p2")
            .Bind<Person>("p3")
            .Where("p1.Id < p2.Id AND p2.Id < p3.Id") // Avoid duplicates
            .Return<Person>()
            .ToListAsync();
    }

    // Expansion example
    public async Task<GraphResult<Person>> GetPersonWithRelatedData(string personId)
    {
        var results = await _graph.Nodes<Person>()
            .Where(p => p.Id == personId)
            .Expand()
            .Include<WorksFor, Company>()
            .Include<Knows, Person>()
            .Execute()
            .SingleAsync();

        return results;
    }
}