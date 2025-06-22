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

namespace Cvoya.Graph.Model.Neo4j.Tests;

public class TestInfrastructureFixture : IAsyncLifetime
{
    private ITestInfrastructure testInfrastructure;

    public TestInfrastructureFixture()
    {
        // Check for environment variable to determine which infrastructure to use
        var useContainers = Environment.GetEnvironmentVariable("USE_NEO4J_CONTAINERS");

        // Default to containers in CI environments (GitHub Actions sets CI=true)
        if (string.IsNullOrEmpty(useContainers))
        {
            useContainers = Environment.GetEnvironmentVariable("CI") ?? "false";
        }

        this.testInfrastructure = bool.Parse(useContainers)
            ? new Neo4jTestInfrastructureWithContainer()
            : new Neo4jTestInfrastructureWithDbInstance();
    }

    public ITestInfrastructure TestInfrastructure => this.testInfrastructure;

    public async ValueTask DisposeAsync()
    {
        await testInfrastructure.DisposeAsync();
    }

    public async ValueTask InitializeAsync()
    {
        await testInfrastructure.Setup();
    }
}