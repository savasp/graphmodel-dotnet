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

namespace Cvoya.Graph.Model.Age.Tests;

using Cvoya.Graph.Model.Age.Tests.Infrastructure;
using Cvoya.Graph.Model.Age.Core;
using Microsoft.Extensions.Logging;
using Xunit;

/// <summary>
/// Base class for AGE provider conformance tests. It mirrors the Neo4j test harness but keeps
/// the implementation lightweight while the provider is still under construction. Tests are
/// skipped by default until the "AGE_PROVIDER_ENABLE_TESTS" environment variable is set to true.
/// </summary>
public class AgeTest : IAsyncLifetime, IClassFixture<TestInfrastructureFixture>
{
    private readonly TestInfrastructureFixture fixture;
    private readonly bool getNewGraph;
    private IGraph? graph => ageGraphStore?.Graph;
    private ILogger<AgeTest>? logger;
    private AgeGraphStore? ageGraphStore;

    public AgeTest(TestInfrastructureFixture fixture, bool getNewGraph = false)
    {
        this.fixture = fixture;
        this.getNewGraph = getNewGraph;
    }

    public IGraph Graph => graph ?? throw new InvalidOperationException("Graph has not been initialized yet");

    public async ValueTask InitializeAsync()
    {
        logger = fixture.LoggerFactory.CreateLogger<AgeTest>();
        logger.LogInformation("Initializing AGE test graph (newGraph: {NewGraph})", getNewGraph);

        ageGraphStore = await fixture.GetGraphAsync(getNewGraph).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        (logger as IDisposable)?.Dispose();
        if(ageGraphStore != null)
        {
            await fixture.ReturnGraphAsync(ageGraphStore);
            ageGraphStore = null;
        }
    }
}
