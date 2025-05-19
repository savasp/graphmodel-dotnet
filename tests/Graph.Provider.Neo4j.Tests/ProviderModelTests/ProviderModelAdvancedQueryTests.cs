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
using DotNet.Testcontainers.Containers;

namespace Cvoya.Graph.Provider.Neo4j.Tests;

public class ProviderModelAdvancedQueryTests : Model.Tests.GraphProviderAdvancedQueryTestsBase, IAsyncLifetime
{
    private readonly TestInfrastructureFixture fixture;
    private IGraphProvider? provider;
    public ProviderModelAdvancedQueryTests(TestInfrastructureFixture fixture)
    {
        this.fixture = fixture;
    }

    protected override IGraphProvider Provider => this.provider ?? throw new InvalidOperationException("Provider not initialized");

    protected override Task ResetDatabase()
    {
        return fixture.TestInfrastructure.ResetDatabase();
    }

    public override async Task InitializeAsync()
    {
        await fixture.TestInfrastructure.EnsureReady();
        this.provider = await fixture.TestInfrastructure.CreateProvider();
        await base.InitializeAsync();
    }

    public override async Task DisposeAsync()
    {
        fixture.Dispose();
        await base.DisposeAsync();
    }
}
