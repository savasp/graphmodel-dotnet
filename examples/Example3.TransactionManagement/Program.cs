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
using Cvoya.Graph.Model.Neo4j.Linq;
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

    await graph.CreateRelationshipAsync(new BankAccount(alice.Id, bank.Id));
    await graph.CreateRelationshipAsync(new BankAccount(bob.Id, bank.Id));

    Console.WriteLine($"✓ Created bank: {bank.Name}");
    Console.WriteLine($"✓ Created account for {alice.Owner}: ${alice.Balance}");
    Console.WriteLine($"✓ Created account for {bob.Owner}: ${bob.Balance}\n");

    // ==== SUCCESSFUL TRANSACTION ====
    Console.WriteLine("2. Successful money transfer...");

    using (var transaction = await graph.GetTransactionAsync())
    {
        try
        {
            // Get fresh copies within transaction
            var aliceAccount = await graph.GetNodeAsync<Account>(alice.Id, transaction: transaction);
            var bobAccount = await graph.GetNodeAsync<Account>(bob.Id, transaction: transaction);

            var transferAmount = 200m;
            Console.WriteLine($"Transferring ${transferAmount} from Alice to Bob...");

            // Update balances
            aliceAccount.Balance -= transferAmount;
            bobAccount.Balance += transferAmount;

            await graph.UpdateNodeAsync(aliceAccount, transaction: transaction);
            await graph.UpdateNodeAsync(bobAccount, transaction: transaction);

            // Record transfer
            var transfer = new Transfer(aliceAccount.Id, bobAccount.Id)
            {
                Amount = transferAmount,
                Timestamp = DateTime.UtcNow,
                Description = "Payment for services"
            };
            await graph.CreateRelationshipAsync(transfer, transaction: transaction);

            // Commit transaction
            await transaction.CommitAsync();
            Console.WriteLine("✓ Transaction committed successfully");
        }
        catch
        {
            await transaction.Rollback();
            throw;
        }
    }

    // Verify balances after successful transaction
    var aliceAfter = await graph.GetNodeAsync<Account>(alice.Id);
    var bobAfter = await graph.GetNodeAsync<Account>(bob.Id);
    Console.WriteLine($"✓ Alice's balance: ${aliceAfter.Balance} (was $1000)");
    Console.WriteLine($"✓ Bob's balance: ${bobAfter.Balance} (was $500)\n");

    // ==== FAILED TRANSACTION (ROLLBACK) ====
    Console.WriteLine("3. Failed transaction with rollback...");

    using (var transaction = await graph.GetTransactionAsync())
    {
        try
        {
            var aliceAccount = await graph.GetNodeAsync<Account>(alice.Id, transaction: transaction);
            var bobAccount = await graph.GetNodeAsync<Account>(bob.Id, transaction: transaction);

            var transferAmount = 1000m; // More than Alice has
            Console.WriteLine($"Attempting to transfer ${transferAmount} from Alice to Bob...");

            // Check balance
            if (aliceAccount.Balance < transferAmount)
            {
                throw new InvalidOperationException($"Insufficient funds. Alice has ${aliceAccount.Balance}, needs ${transferAmount}");
            }

            // This won't be reached due to exception above
            aliceAccount.Balance -= transferAmount;
            bobAccount.Balance += transferAmount;

            await graph.UpdateNodeAsync(aliceAccount, transaction: transaction);
            await graph.UpdateNodeAsync(bobAccount, transaction: transaction);
            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Transaction failed: {ex.Message}");
            await transaction.Rollback();
            Console.WriteLine("✓ Transaction rolled back");
        }
    }

    // Verify balances remain unchanged after rollback
    var aliceAfterFailed = await graph.GetNodeAsync<Account>(alice.Id);
    var bobAfterFailed = await graph.GetNodeAsync<Account>(bob.Id);
    Console.WriteLine($"✓ Alice's balance: ${aliceAfterFailed.Balance} (unchanged)");
    Console.WriteLine($"✓ Bob's balance: ${bobAfterFailed.Balance} (unchanged)\n");

    // ==== COMPLEX TRANSACTION ====
    Console.WriteLine("4. Complex transaction with multiple operations...");

    using (var transaction = await graph.GetTransactionAsync())
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
            await graph.CreateRelationshipAsync(new BankAccount(charlie.Id, bank.Id), transaction: transaction);

            // Transfer from multiple sources to Charlie
            var aliceAccount = await graph.GetNodeAsync<Account>(alice.Id, transaction: transaction);
            var bobAccount = await graph.GetNodeAsync<Account>(bob.Id, transaction: transaction);

            var aliceContribution = 50m;
            var bobContribution = 50m;

            aliceAccount.Balance -= aliceContribution;
            bobAccount.Balance -= bobContribution;
            charlie.Balance = aliceContribution + bobContribution;

            await graph.UpdateNodeAsync(aliceAccount, transaction: transaction);
            await graph.UpdateNodeAsync(bobAccount, transaction: transaction);
            await graph.UpdateNodeAsync(charlie, transaction: transaction);

            // Record transfers
            await graph.CreateRelationshipAsync(new Transfer(aliceAccount.Id, charlie.Id)
            {
                Amount = aliceContribution,
                Timestamp = DateTime.UtcNow,
                Description = "Welcome gift"
            }, transaction: transaction);

            await graph.CreateRelationshipAsync(new Transfer(bobAccount.Id, charlie.Id)
            {
                Amount = bobContribution,
                Timestamp = DateTime.UtcNow,
                Description = "Welcome gift"
            }, transaction: transaction);

            await transaction.CommitAsync();
            Console.WriteLine("✓ Complex transaction completed successfully");
            Console.WriteLine($"✓ Created new account for Charlie with balance: ${charlie.Balance}");
        }
        catch
        {
            await transaction.Rollback();
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
    await graph.DisposeAsync();
    await using (var session = driver.AsyncSession())
    {
        //        await session.RunAsync($"DROP DATABASE {databaseName}");
    }
    await driver.DisposeAsync();
}
