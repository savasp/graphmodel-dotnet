// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

﻿namespace SimpleMovieExample;

using Cvoya.Graph;
using Cvoya.Graph.Neo4j;
using Neo4j.Driver;

static class Program
{
    static string databaseName = "SimpleMovieExample";

    async static Task Main()
    {
        var driver = GraphDatabase.Driver("bolt://localhost:7687", AuthTokens.Basic("neo4j", "password"));
        await using (var session = driver.AsyncSession(sc => sc.WithDatabase("system")))
        {
            await session.RunAsync($"CREATE OR REPLACE DATABASE {databaseName}");
        }

        var store = new Neo4jGraphStore("bolt://localhost:7687", "neo4j", "password", databaseName);
        var graph = store.Graph;

        var movieInception = new Movie { Title = "Inception", ReleaseYear = 2010 };
        var movieTheMatrix = new Movie { Title = "The Matrix", ReleaseYear = 1999 };
        var movieInterstellar = new Movie { Title = "Interstellar", ReleaseYear = 2014 };

        var personAlice = new Person { Name = "Alice", Age = 30 };
        var personBob = new Person { Name = "Bob", Age = 25 };
        var personCharlie = new Person { Name = "Charlie", Age = 28 };

        var creditCardAlice = new CreditCard { Number = "1234-5678-9012-3456", Expiry = "12/25" };
        var creditCardBob = new CreditCard { Number = "2345-6789-0123-4567", Expiry = "11/24" };
        var creditCardCharlie = new CreditCard { Number = "3456-7890-1234-5678", Expiry = "10/23" };

        var aliceWatchedInception = new Watched { Date = DateTime.UtcNow };
        var aliceWatchedTheMatrix = new Watched { Date = DateTime.UtcNow };
        var aliceWatchedInterstellar = new Watched { Date = DateTime.UtcNow };
        var bobWatchedTheMatrix = new Watched { Date = DateTime.UtcNow };
        var charlieWatchedInterstellar = new Watched { Date = DateTime.UtcNow };

        var alicePaidForInception = new Paid { Amount = 15.99m, Date = DateTime.UtcNow, MovieName = "Inception" };

        await graph.CreateNodeAsync(movieInception);
        await graph.CreateNodeAsync(movieTheMatrix);
        await graph.CreateNodeAsync(movieInterstellar);
        await graph.CreateNodeAsync(personAlice);
        await graph.CreateNodeAsync(personBob);
        await graph.CreateNodeAsync(personCharlie);
        await graph.CreateNodeAsync(creditCardAlice);
        await graph.CreateNodeAsync(creditCardBob);
        await graph.CreateNodeAsync(creditCardCharlie);

        var aliceSelection = graph.Nodes<Person>().Where(person => person.Name == personAlice.Name);
        var bobSelection = graph.Nodes<Person>().Where(person => person.Name == personBob.Name);
        var charlieSelection = graph.Nodes<Person>().Where(person => person.Name == personCharlie.Name);
        var inceptionSelection = graph.Nodes<Movie>().Where(movie => movie.Title == movieInception.Title);
        var matrixSelection = graph.Nodes<Movie>().Where(movie => movie.Title == movieTheMatrix.Title);
        var interstellarSelection = graph.Nodes<Movie>().Where(movie => movie.Title == movieInterstellar.Title);
        var aliceCardSelection = graph.Nodes<CreditCard>().Where(card => card.Number == creditCardAlice.Number);

        await graph.CreateRelationshipAsync(aliceSelection, alicePaidForInception, aliceCardSelection);
        await graph.CreateRelationshipAsync(aliceSelection, aliceWatchedInception, inceptionSelection);
        await graph.CreateRelationshipAsync(aliceSelection, aliceWatchedTheMatrix, matrixSelection);
        await graph.CreateRelationshipAsync(aliceSelection, aliceWatchedInterstellar, interstellarSelection);
        await graph.CreateRelationshipAsync(bobSelection, bobWatchedTheMatrix, matrixSelection);
        await graph.CreateRelationshipAsync(charlieSelection, charlieWatchedInterstellar, interstellarSelection);


        var moviesAliceWatched = graph.Nodes<Person>()
            .Where(p => p.Name == "Alice")
            .Traverse<Watched, Movie>()
            .Distinct();

        foreach (var movie in moviesAliceWatched)
        {
            Console.WriteLine($"Alice watched: {movie.Title} ({movie.ReleaseYear})");
        }

        var moviesAlicePaidFor = graph.Nodes<Person>()
            .Where(p => p.Name == "Alice")
            .PathSegments<Person, Paid, CreditCard>()
            .Select(s => new { Movie = s.Relationship.MovieName, CreditCard = s.EndNode });


        foreach (var creditCard in moviesAlicePaidFor)
        {
            Console.WriteLine($"Alice paid for '{creditCard.Movie}' with credit card: {creditCard.CreditCard.Number}, Expiry: {creditCard.CreditCard.Expiry}");
        }
    }
}
