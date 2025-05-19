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

using Cvoya.Graph.Provider.Model;
using Microsoft.Extensions.Logging;

namespace Cvoya.Graph.Provider.Neo4j.Tests;

internal class Neo4jTestInfrastructureWithDbInstance : ITestInfrastructure
{
    private static ILogger logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<Neo4jGraphProvider>();
    private const string Endpoint = "neo4j://localhost:7687";
    private readonly IGraphProvider provider = new Neo4jGraphProvider(Endpoint, username: "neo4j", password: "password", logger: logger);

    public Task<IGraphProvider> CreateProvider()
    {
        return Task.FromResult<IGraphProvider>(provider);
    }

    public async Task ResetDatabase()
    {
        await EnsureReady();
        await provider.ExecuteCypher("CREATE OR REPLACE DATABASE tests");
    }

    public Task<string> EnsureReady()
    {
        return Task.FromResult(Endpoint);
    }

    public async ValueTask DisposeAsync()
    {
        await provider.ExecuteCypher("DROP DATABASE tests");
    }
}
