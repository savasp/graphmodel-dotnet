// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph;
using Cvoya.Graph.Neo4j;
using Neo4j.Driver;

// Example 3: Transaction Management
// Demonstrates transaction support, rollback, and data consistency

Console.WriteLine("=== Example 3: Transaction Management ===\n");

const string databaseName = "example3";

// ==== SETUP a new database ====
Console.WriteLine("0. Setting up a new database...");
var driver = GraphDatabase.Driver("bolt://localhost:7687", AuthTokens.Basic("neo4j", "password"));
await using (var session = driver.AsyncSession(sc => sc.WithDatabase("system")))
{
    await session.RunAsync($"CREATE OR REPLACE DATABASE {databaseName}");
}

Console.WriteLine($"✓ Created database: {databaseName}");

// We start with the Neo4j Graph Provider here
// Create graph instance with Neo4j provider
var store = new Neo4jGraphStore("bolt://localhost:7687", "neo4j", "password", databaseName, null);
var graph = store.Graph;


try
{
    // ==== SETUP: Create initial data ====
    Console.WriteLine("1. Setting up bank accounts...");

    var bank = new Bank { Name = "GraphBank", Country = "USA" };
    var alice = new Account { AccountNumber = "ACC-001", Owner = "Alice", Balance = 1000 };
    var bob = new Account { AccountNumber = "ACC-002", Owner = "Bob", Balance = 500 };

    await graph.CreateNodeAsync(bank);
    await graph.CreateNodeAsync(alice);
    await graph.CreateNodeAsync(bob);

    var bankSelection = graph.Nodes<Bank>().Where(candidate => candidate.Name == bank.Name);
    await graph.CreateRelationshipAsync(
        graph.Nodes<Account>().Where(account => account.AccountNumber == alice.AccountNumber),
        new BankAccount(),
        bankSelection);
    await graph.CreateRelationshipAsync(
        graph.Nodes<Account>().Where(account => account.AccountNumber == bob.AccountNumber),
        new BankAccount(),
        bankSelection);

    Console.WriteLine($"✓ Created bank: {bank.Name}");
    Console.WriteLine($"✓ Created account for {alice.Owner}: ${alice.Balance}");
    Console.WriteLine($"✓ Created account for {bob.Owner}: ${bob.Balance}\n");

    // ==== SUCCESSFUL TRANSACTION ====
    Console.WriteLine("2. Successful money transfer...");

    await using (var transaction = await graph.GetTransactionAsync())
    {
        try
        {
            // Get fresh copies within transaction
            var aliceSelection = graph.Nodes<Account>(transaction)
                .Where(account => account.AccountNumber == alice.AccountNumber);
            var bobSelection = graph.Nodes<Account>(transaction)
                .Where(account => account.AccountNumber == bob.AccountNumber);
            var aliceAccount = await aliceSelection.SingleAsync();
            var bobAccount = await bobSelection.SingleAsync();

            var transferAmount = 200m;
            Console.WriteLine($"Transferring ${transferAmount} from Alice to Bob...");

            // Update balances
            await aliceSelection.UpdateAsync(
                setters => setters.SetProperty(account => account.Balance, aliceAccount.Balance - transferAmount));
            await bobSelection.UpdateAsync(
                setters => setters.SetProperty(account => account.Balance, bobAccount.Balance + transferAmount));

            // Record transfer
            var transfer = new Transfer
            {
                Amount = transferAmount,
                Timestamp = DateTime.UtcNow,
                Description = "Payment for services"
            };
            await graph.CreateRelationshipAsync(aliceSelection, transfer, bobSelection);

            // Commit transaction
            await transaction.CommitAsync();
            Console.WriteLine("✓ Transaction committed successfully");
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // Verify balances after successful transaction
    var aliceAfter = await graph.Nodes<Account>()
        .Where(account => account.AccountNumber == alice.AccountNumber)
        .SingleAsync();
    var bobAfter = await graph.Nodes<Account>()
        .Where(account => account.AccountNumber == bob.AccountNumber)
        .SingleAsync();
    Console.WriteLine($"✓ Alice's balance: ${aliceAfter.Balance} (was $1000)");
    Console.WriteLine($"✓ Bob's balance: ${bobAfter.Balance} (was $500)\n");

    // ==== FAILED TRANSACTION (ROLLBACK) ====
    Console.WriteLine("3. Failed transaction with rollback...");

    await using (var transaction = await graph.GetTransactionAsync())
    {
        try
        {
            var aliceSelection = graph.Nodes<Account>(transaction)
                .Where(account => account.AccountNumber == alice.AccountNumber);
            var bobSelection = graph.Nodes<Account>(transaction)
                .Where(account => account.AccountNumber == bob.AccountNumber);
            var aliceAccount = await aliceSelection.SingleAsync();
            var bobAccount = await bobSelection.SingleAsync();

            var transferAmount = 1000m; // More than Alice has
            Console.WriteLine($"Attempting to transfer ${transferAmount} from Alice to Bob...");

            // Check balance
            if (aliceAccount.Balance < transferAmount)
            {
                throw new InvalidOperationException($"Insufficient funds. Alice has ${aliceAccount.Balance}, needs ${transferAmount}");
            }

            // This won't be reached due to exception above
            await aliceSelection.UpdateAsync(
                setters => setters.SetProperty(account => account.Balance, aliceAccount.Balance - transferAmount));
            await bobSelection.UpdateAsync(
                setters => setters.SetProperty(account => account.Balance, bobAccount.Balance + transferAmount));
            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Transaction failed: {ex.Message}");
            await transaction.RollbackAsync();
            Console.WriteLine("✓ Transaction rolled back");
        }
    }

    // Verify balances remain unchanged after rollback
    var aliceAfterFailed = await graph.Nodes<Account>()
        .Where(account => account.AccountNumber == alice.AccountNumber)
        .SingleAsync();
    var bobAfterFailed = await graph.Nodes<Account>()
        .Where(account => account.AccountNumber == bob.AccountNumber)
        .SingleAsync();
    Console.WriteLine($"✓ Alice's balance: ${aliceAfterFailed.Balance} (unchanged)");
    Console.WriteLine($"✓ Bob's balance: ${bobAfterFailed.Balance} (unchanged)\n");

    // ==== COMPLEX TRANSACTION ====
    Console.WriteLine("4. Complex transaction with multiple operations...");

    await using (var transaction = await graph.GetTransactionAsync())
    {
        try
        {
            // Create a new account
            var charlie = new Account
            {
                AccountNumber = "ACC-003",
                Owner = "Charlie",
                Balance = 0
            };
            await graph.CreateNodeAsync(charlie, transaction: transaction);
            var charlieSelection = graph.Nodes<Account>(transaction)
                .Where(account => account.AccountNumber == charlie.AccountNumber);
            await graph.CreateRelationshipAsync(
                charlieSelection,
                new BankAccount(),
                graph.Nodes<Bank>(transaction).Where(candidate => candidate.Name == bank.Name));

            // Transfer from multiple sources to Charlie
            var aliceSelection = graph.Nodes<Account>(transaction)
                .Where(account => account.AccountNumber == alice.AccountNumber);
            var bobSelection = graph.Nodes<Account>(transaction)
                .Where(account => account.AccountNumber == bob.AccountNumber);
            var aliceAccount = await aliceSelection.SingleAsync();
            var bobAccount = await bobSelection.SingleAsync();

            var aliceContribution = 50m;
            var bobContribution = 50m;

            charlie.Balance = aliceContribution + bobContribution;

            await aliceSelection.UpdateAsync(
                setters => setters.SetProperty(account => account.Balance, aliceAccount.Balance - aliceContribution));
            await bobSelection.UpdateAsync(
                setters => setters.SetProperty(account => account.Balance, bobAccount.Balance - bobContribution));
            await charlieSelection.UpdateAsync(
                setters => setters.SetProperty(account => account.Balance, charlie.Balance));

            // Record transfers
            await graph.CreateRelationshipAsync(aliceSelection, new Transfer
            {
                Amount = aliceContribution,
                Timestamp = DateTime.UtcNow,
                Description = "Welcome gift"
            }, charlieSelection);

            await graph.CreateRelationshipAsync(bobSelection, new Transfer
            {
                Amount = bobContribution,
                Timestamp = DateTime.UtcNow,
                Description = "Welcome gift"
            }, charlieSelection);

            await transaction.CommitAsync();
            Console.WriteLine("✓ Complex transaction completed successfully");
            Console.WriteLine($"✓ Created new account for Charlie with balance: ${charlie.Balance}");
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // ==== TRANSACTION HISTORY ====
    Console.WriteLine("\n5. Transaction history...");

    var transfers = await graph.Nodes<Account>()
        .PathSegments<Account, Transfer, Account>()
        .ToListAsync();

    Console.WriteLine($"Total transfers: {transfers.Count}");
    foreach (var rel in transfers.OrderBy(t => t.Relationship.Timestamp))
    {
        Console.WriteLine($"  - {rel.StartNode.Owner} → {rel.EndNode.Owner}: ${rel.Relationship.Amount} ({rel.Relationship.Description}) at {rel.Relationship.Timestamp}");
    }
    Console.WriteLine("\n=== Transaction History Complete ===");

    Console.WriteLine("\n=== Example 3 Complete ===");
    Console.WriteLine("This example demonstrated:");
    Console.WriteLine("• Creating and managing transactions");
    Console.WriteLine("• Committing successful transactions");
    Console.WriteLine("• Rolling back failed transactions");
    Console.WriteLine("• Complex multi-operation transactions");
    Console.WriteLine("• Maintaining data consistency");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
    if (ex.InnerException != null)
    {
        Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
        Console.WriteLine(ex.InnerException.StackTrace);
    }
    Console.WriteLine("Make sure Neo4j is running on localhost:7687 with username 'neo4j' and password 'password'");
}
finally
{
    await store.DisposeAsync();
    await driver.DisposeAsync();
}
