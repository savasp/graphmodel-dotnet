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

using Neo4j.Driver;

public class TestDatabase
{
    private readonly string databaseName = "GraphModelTests" + Guid.NewGuid().ToString("N");
    private readonly IDriver driver;

    public TestDatabase(string endpoint, string? username = null, string? password = null)
    {
        this.driver = username != null && password != null
            ? GraphDatabase.Driver(endpoint, AuthTokens.Basic(username, password))
            : GraphDatabase.Driver(endpoint);
    }

    public string DatabaseName => this.databaseName;

    public async Task Setup()
    {
        using var session = driver.AsyncSession(builder => builder.WithDatabase("system"));
        await session.RunAsync($"CREATE OR REPLACE DATABASE {this.databaseName}");
        await WaitForDatabaseOnline();
    }

    public async Task Reset()
    {
        using var session = driver.AsyncSession(builder => builder.WithDatabase(this.databaseName));
        await session.RunAsync("MATCH (n) DETACH DELETE n");
        // Schema cleanup removed for now to avoid APOC dependency
        // If needed, can be added back with proper APOC plugin configuration
    }

    public async ValueTask DisposeAsync()
    {
        using var session = driver.AsyncSession(builder => builder.WithDatabase("system"));
        {
            await session.RunAsync($"DROP DATABASE {this.databaseName}");
        }
    }

    private async Task WaitForDatabaseOnline(int maxAttempts = 30, int initialDelayMs = 100)
    {
        // First, wait for SHOW DATABASES to report online
        int delayMs = initialDelayMs;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                await using var session = driver.AsyncSession(builder => builder.WithDatabase("system"));
                var result = await session.RunAsync($"SHOW DATABASES YIELD name, currentStatus WHERE name = '{this.databaseName}' RETURN currentStatus");
                
                if (await result.FetchAsync())
                {
                    var record = result.Current;
                    var status = record["currentStatus"].As<string>();
                    if (status == "online")
                    {
                        break;
                    }
                }
                await session.CloseAsync();
            }
            catch (Exception ex) when (ex.Message.Contains("not found") || ex.Message.Contains("does not exist") || ex.Message.Contains("empty"))
            {
                // Database not yet available or query returned no results
            }
            
            // Use exponential backoff with a maximum delay of 2 seconds
            await Task.Delay(delayMs);
            delayMs = Math.Min(delayMs * 2, 2000);
        }

        // Second, wait until the driver can actually connect to the database
        delayMs = initialDelayMs; // Reset delay for second phase
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                await using (var session = driver.AsyncSession(builder => builder.WithDatabase(this.databaseName)))
                {
                    var result = await session.RunAsync("RETURN 1");
                    await result.ConsumeAsync();
                    return;
                }
            }
            catch (Neo4jException ex) when (ex.Message.Contains("not found") || ex.Message.Contains("does not exist"))
            {
                // Database not yet available for driver
            }
            
            // Use exponential backoff with a maximum delay of 2 seconds
            await Task.Delay(delayMs);
            delayMs = Math.Min(delayMs * 2, 2000);
        }
        throw new Exception($"Database '{this.databaseName}' did not become available for driver connections in time.");
    }
}
