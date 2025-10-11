namespace SimpleMovieExample;

using Cvoya.Graph.Model;
using Cvoya.Graph.Model.Neo4j;
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

        var aliceWatchedInception = new Watched(personAlice.Id, movieInception.Id) { Date = DateTime.UtcNow };
        var aliceWatchedTheMatrix = new Watched(personAlice.Id, movieTheMatrix.Id) { Date = DateTime.UtcNow };
        var aliceWatchedInterstellar = new Watched(personAlice.Id, movieInterstellar.Id) { Date = DateTime.UtcNow };
        var bobWatchedTheMatrix = new Watched(personBob.Id, movieTheMatrix.Id) { Date = DateTime.UtcNow };
        var charlieWatchedInterstellar = new Watched(personCharlie.Id, movieInterstellar.Id) { Date = DateTime.UtcNow };

        var alicePaidForInception = new Paid(personAlice.Id, creditCardAlice.Id) { Amount = 15.99m, Date = DateTime.UtcNow, MovieName = "Inception" };

        await graph.CreateNodeAsync(movieInception);
        await graph.CreateNodeAsync(movieTheMatrix);
        await graph.CreateNodeAsync(movieInterstellar);
        await graph.CreateNodeAsync(personAlice);
        await graph.CreateNodeAsync(personBob);
        await graph.CreateNodeAsync(personCharlie);
        await graph.CreateNodeAsync(creditCardAlice);
        await graph.CreateNodeAsync(creditCardBob);
        await graph.CreateNodeAsync(creditCardCharlie);

        await graph.CreateRelationshipAsync(alicePaidForInception);
        await graph.CreateRelationshipAsync(aliceWatchedInception);
        await graph.CreateRelationshipAsync(aliceWatchedTheMatrix);
        await graph.CreateRelationshipAsync(aliceWatchedInterstellar);
        await graph.CreateRelationshipAsync(bobWatchedTheMatrix);
        await graph.CreateRelationshipAsync(charlieWatchedInterstellar);


        var moviesAliceWatched = graph.Nodes<Person>()
            .Where(p => p.Name == "Alice")
            .Traverse<Person, Watched, Movie>()
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