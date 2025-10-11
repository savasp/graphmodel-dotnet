// Copyright 2025 Savas Parastatidis
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      https://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Cvoya.Graph.Model;
using Cvoya.Graph.Model.Neo4j;
using Microsoft.Extensions.Logging;
using Neo4j.Driver;

// Example 6: Full Text Search
// Demonstrates full text search capabilities across nodes and relationships

Console.WriteLine("=== Example 6: Full Text Search ===\n");

const string databaseName = "example6";

// ==== SETUP a new database ====
Console.WriteLine("0. Setting up a new database...");
var driver = GraphDatabase.Driver("bolt://localhost:7687", AuthTokens.Basic("neo4j", "password"));
await using (var session = driver.AsyncSession(sc => sc.WithDatabase("system")))
{
    await session.RunAsync($"CREATE OR REPLACE DATABASE {databaseName}");
}

Console.WriteLine($"✓ Created database: {databaseName}");

// Create graph instance
var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole().SetMinimumLevel(LogLevel.Information);
});

var store = new Neo4jGraphStore("bolt://localhost:7687", "neo4j", "password", databaseName, null, loggerFactory);
var graph = store.Graph;

try
{
    // ==== CREATE SAMPLE DATA ====
    Console.WriteLine("\n1. Creating sample data...");

    // Create authors
    var tolkien = new Author
    {
        Name = "J.R.R. Tolkien",
        Bio = "British author known for fantasy literature including The Lord of the Rings",
        Nationality = "British",
        PersonalNotes = "Secret information not searchable"
    };

    var asimov = new Author
    {
        Name = "Isaac Asimov",
        Bio = "American science fiction writer famous for robot stories and Foundation series",
        Nationality = "American",
        PersonalNotes = "Private notes about the author"
    };

    var dahl = new Author
    {
        Name = "Roald Dahl",
        Bio = "British novelist known for children's books and dark humor",
        Nationality = "British",
        PersonalNotes = "Confidential author information"
    };

    await graph.CreateNodeAsync(tolkien);
    await graph.CreateNodeAsync(asimov);
    await graph.CreateNodeAsync(dahl);

    // Create books
    var hobbit = new Book
    {
        Title = "The Hobbit",
        Genre = "Fantasy",
        Summary = "A reluctant hobbit goes on an adventure with dwarves to reclaim their mountain home",
        PublicationYear = 1937,
        Price = 12.99m
    };

    var foundation = new Book
    {
        Title = "Foundation",
        Genre = "Science Fiction",
        Summary = "Mathematical psychohistory predicts the fall of a galactic empire",
        PublicationYear = 1951,
        Price = 14.99m
    };

    var charlie = new Book
    {
        Title = "Charlie and the Chocolate Factory",
        Genre = "Children's Literature",
        Summary = "A poor boy wins a golden ticket to tour a magical chocolate factory",
        PublicationYear = 1964,
        Price = 9.99m
    };

    await graph.CreateNodeAsync(hobbit);
    await graph.CreateNodeAsync(foundation);
    await graph.CreateNodeAsync(charlie);

    // Create publishers
    var allen = new Publisher
    {
        Name = "Allen & Unwin",
        Country = "United Kingdom",
        Description = "British publisher known for academic and literary works"
    };

    var gnome = new Publisher
    {
        Name = "Gnome Press",
        Country = "United States",
        Description = "American science fiction specialty publisher from the golden age"
    };

    await graph.CreateNodeAsync(allen);
    await graph.CreateNodeAsync(gnome);

    // Create relationships
    var tolkienWroteHobbit = new Wrote(tolkien.Id, hobbit.Id)
    {
        WrittenDate = new DateTime(1937, 9, 21),
        WritingStyle = "Narrative prose with detailed world-building and mythology"
    };

    var asimovWroteFoundation = new Wrote(asimov.Id, foundation.Id)
    {
        WrittenDate = new DateTime(1951, 5, 1),
        WritingStyle = "Hard science fiction with mathematical concepts"
    };

    var dahlWroteCharlie = new Wrote(dahl.Id, charlie.Id)
    {
        WrittenDate = new DateTime(1964, 1, 17),
        WritingStyle = "Whimsical children's literature with dark undertones"
    };

    var allenPublishedHobbit = new Published(allen.Id, hobbit.Id)
    {
        PublishedDate = new DateTime(1937, 9, 21),
        Edition = "First Edition",
        MarketingCampaign = "Literary fiction targeting adult readers"
    };

    var gnomePublishedFoundation = new Published(gnome.Id, foundation.Id)
    {
        PublishedDate = new DateTime(1951, 5, 1),
        Edition = "Hardcover First Edition",
        MarketingCampaign = "Science fiction specialty market campaign"
    };

    var authorsCollaboration = new Collaborated(tolkien.Id, asimov.Id)
    {
        ProjectType = "Academic discussion",
        Description = "Imaginary collaboration on fantasy versus science fiction themes"
    };

    await graph.CreateRelationshipAsync(tolkienWroteHobbit);
    await graph.CreateRelationshipAsync(asimovWroteFoundation);
    await graph.CreateRelationshipAsync(dahlWroteCharlie);
    await graph.CreateRelationshipAsync(allenPublishedHobbit);
    await graph.CreateRelationshipAsync(gnomePublishedFoundation);
    await graph.CreateRelationshipAsync(authorsCollaboration);

    Console.WriteLine("✓ Created authors, books, publishers and their relationships");

    // ==== DEMONSTRATE FULL TEXT SEARCH FEATURES ====

    Console.WriteLine("\n2. Demonstrating Full Text Search Features...\n");

    // ==== Search across all entities ====
    Console.WriteLine("🔍 Searching across ALL entities for 'British':");
    var allBritishResults = await graph.Search("British").ToListAsync();
    Console.WriteLine($"Found {allBritishResults.Count} entities containing 'British':");
    foreach (var result in allBritishResults)
    {
        if (result is Author author)
            Console.WriteLine($"  - Author: {author.Name} ({author.Nationality})");
        else if (result is Book book)
            Console.WriteLine($"  - Book: {book.Title}");
        else if (result is Publisher publisher)
            Console.WriteLine($"  - Publisher: {publisher.Name}");
        else
            Console.WriteLine($"  - {result.GetType().Name}: {result}");
    }

    Console.WriteLine();

    // ==== Search specific node types ====
    Console.WriteLine("🔍 Searching for Authors containing 'science':");
    var scienceAuthors = await graph.SearchNodes<Author>("science").ToListAsync();
    Console.WriteLine($"Found {scienceAuthors.Count} authors:");
    foreach (var author in scienceAuthors)
    {
        Console.WriteLine($"  - {author.Name}: {author.Bio}");
    }

    Console.WriteLine();

    Console.WriteLine("🔍 Searching for Books containing 'adventure':");
    var adventureBooks = await graph.SearchNodes<Book>("adventure").ToListAsync();
    Console.WriteLine($"Found {adventureBooks.Count} books:");
    foreach (var book in adventureBooks)
    {
        Console.WriteLine($"  - {book.Title}: {book.Summary}");
    }

    Console.WriteLine();

    // ==== Search relationships ====
    Console.WriteLine("🔍 Searching relationships for 'mathematical':");
    var mathRelationships = await graph.SearchRelationships("mathematical").ToListAsync();
    Console.WriteLine($"Found {mathRelationships.Count} relationships containing 'mathematical':");
    foreach (var rel in mathRelationships)
    {
        if (rel is Wrote wrote)
            Console.WriteLine($"  - Wrote relationship: {wrote.WritingStyle}");
        else if (rel is Published published)
            Console.WriteLine($"  - Published relationship: {published.MarketingCampaign}");
        else if (rel is Collaborated collaborated)
            Console.WriteLine($"  - Collaborated relationship: {collaborated.Description}");
    }

    Console.WriteLine();

    // ==== Search specific relationship types ====
    Console.WriteLine("🔍 Searching Wrote relationships for 'world-building':");
    var worldBuildingWrites = await graph.SearchRelationships<Wrote>("world-building").ToListAsync();
    Console.WriteLine($"Found {worldBuildingWrites.Count} writing relationships:");
    foreach (var wrote in worldBuildingWrites)
    {
        Console.WriteLine($"  - Writing style: {wrote.WritingStyle}");
    }

    Console.WriteLine();

    // ==== Search using generic interfaces ====
    Console.WriteLine("🔍 Searching all nodes for 'fantasy':");
    var fantasyNodes = await graph.SearchNodes("fantasy").ToListAsync();
    Console.WriteLine($"Found {fantasyNodes.Count} nodes:");
    foreach (var node in fantasyNodes)
    {
        if (node is Author author)
            Console.WriteLine($"  - Author: {author.Name}");
        else if (node is Book book)
            Console.WriteLine($"  - Book: {book.Title} ({book.Genre})");
        else if (node is Publisher publisher)
            Console.WriteLine($"  - Publisher: {publisher.Name}");
    }

    Console.WriteLine();

    // ==== Demonstrate case insensitivity ====
    Console.WriteLine("🔍 Demonstrating case-insensitive search for 'CHOCOLATE' (uppercase):");
    var chocolateResults = await graph.SearchNodes<Book>("CHOCOLATE").ToListAsync();
    Console.WriteLine($"Found {chocolateResults.Count} books:");
    foreach (var book in chocolateResults)
    {
        Console.WriteLine($"  - {book.Title}: {book.Summary}");
    }

    Console.WriteLine();

    // ==== Demonstrate property exclusion ====
    Console.WriteLine("🔍 Searching for 'Secret' (should NOT find anything because PersonalNotes is excluded):");
    var secretResults = await graph.Search("Secret").ToListAsync();
    Console.WriteLine($"Found {secretResults.Count} entities containing 'Secret'");
    Console.WriteLine("(PersonalNotes with 'Secret information' is excluded from search using [Property(IncludeInFullTextSearch = false)])");

    Console.WriteLine();

    Console.WriteLine("🔍 Searching for 'British' in author name/bio (should find results):");
    var britishResults = await graph.SearchNodes<Author>("British").ToListAsync();
    Console.WriteLine($"Found {britishResults.Count} authors:");
    foreach (var author in britishResults)
    {
        Console.WriteLine($"  - {author.Name}: {author.Bio}");
    }

    Console.WriteLine();

    // ==== Search with no results ====
    Console.WriteLine("🔍 Searching for non-existent term 'Klingon':");
    var klingonResults = await graph.Search("Klingon").ToListAsync();
    Console.WriteLine($"Found {klingonResults.Count} entities containing 'Klingon'");

    Console.WriteLine();

    // ==== Complex search terms ====
    Console.WriteLine("🔍 Searching for 'golden ticket' (multi-word search):");
    var goldenTicketResults = await graph.SearchNodes<Book>("golden ticket").ToListAsync();
    Console.WriteLine($"Found {goldenTicketResults.Count} books:");
    foreach (var book in goldenTicketResults)
    {
        Console.WriteLine($"  - {book.Title}: {book.Summary}");
    }

    Console.WriteLine("\n=== Full Text Search Demo Complete! ===");

    Console.WriteLine("\n📋 Summary of demonstrated features:");
    Console.WriteLine("✓ Search across all entity types with Search(query)");
    Console.WriteLine("✓ Search specific node types with SearchNodes<T>(query)");
    Console.WriteLine("✓ Search specific relationship types with SearchRelationships<T>(query)");
    Console.WriteLine("✓ Search using generic interfaces (SearchNodes, SearchRelationships)");
    Console.WriteLine("✓ Case-insensitive searching");
    Console.WriteLine("✓ Property-level search exclusion with [Property(IncludeInFullTextSearch = false)]");
    Console.WriteLine("✓ Multi-word search terms");
    Console.WriteLine("✓ Automatic full text index creation and management");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
}
finally
{
    await store.DisposeAsync();
}