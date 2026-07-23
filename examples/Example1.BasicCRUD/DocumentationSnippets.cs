// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph;

internal static class DocumentationSnippets
{
    private static async Task CreateRelationshipAsync(
        IGraph graph,
        Person alice,
        Company techCorp)
    {
        // snippet-start: create-relationship
        await graph.CreateRelationshipAsync(
            graph.Nodes<Person>().Where(person => person.Email == alice.Email),
            new WorksFor { Position = "Senior Developer", Salary = 95_000m },
            graph.Nodes<Company>().Where(company => company.Name == techCorp.Name));
        // snippet-end: create-relationship
    }

    private static async Task QueryAsync(IGraph graph)
    {
        // snippet-start: query
        var engineers = await graph.Nodes<Person>()
            .Where(person => person.Department == "Engineering")
            .ToListAsync();
        // snippet-end: query
        Console.WriteLine($"Found {engineers.Count} engineer(s).");
    }

    private static async Task UpdateAndDeleteAsync(IGraph graph, Person alice)
    {
        // snippet-start: update-and-delete
        await graph.Nodes<Person>()
            .Where(person => person.Email == alice.Email)
            .UpdateAsync(setters => setters
                .SetProperty(person => person.Age, 31)
                .SetProperty(person => person.Department, "Engineering"));

        await graph.Nodes<Person>()
            .Where(person => person.Email == "temporary@example.com")
            .DeleteAsync();
        // snippet-end: update-and-delete
    }
}
