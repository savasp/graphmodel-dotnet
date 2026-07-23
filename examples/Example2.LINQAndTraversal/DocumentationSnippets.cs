// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph;

internal static class DocumentationSnippets
{
    private static async Task QueryRootsAsync(IGraph graph)
    {
        // snippet-start: query-roots
        var newYorkers = await graph.Nodes<Person>()
            .Where(person => person.City == "New York")
            .OrderBy(person => person.Name)
            .ToListAsync();

        var ageRange = await graph.Nodes<Person>()
            .Where(person => person.Age >= 28 && person.Age <= 32)
            .ToListAsync();
        // snippet-end: query-roots
        Console.WriteLine($"Found {newYorkers.Count} New Yorker(s) and {ageRange.Count} person(s) in the age range.");
    }

    private static async Task TraverseAsync(IGraph graph)
    {
        // snippet-start: traversal
        var directConnections = await graph.Nodes<Person>()
            .Where(person => person.Name == "Alice")
            .Traverse<Knows, Person>()
            .ToListAsync();

        var twoHopConnections = await graph.Nodes<Person>()
            .Where(person => person.Name == "Alice")
            .Traverse<Knows, Person>(2)
            .ToListAsync();
        // snippet-end: traversal
        Console.WriteLine(
            $"Found {directConnections.Count} direct connection(s) and {twoHopConnections.Count} two-hop connection(s).");
    }

    private static async Task PathSegmentsAsync(IGraph graph)
    {
        // snippet-start: path-segments
        var connections = await graph.Nodes<Person>()
            .Where(person => person.Name == "Alice")
            .PathSegments<Person, Knows, Person>()
            .ToListAsync();
        // snippet-end: path-segments
        Console.WriteLine($"Found {connections.Count} path segment(s).");
    }
}
