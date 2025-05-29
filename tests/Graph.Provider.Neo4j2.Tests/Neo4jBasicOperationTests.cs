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

using Cvoya.Graph.Model.Tests;

namespace Cvoya.Graph.Provider.Neo4j.Tests;

/// <summary>
/// Basic CRUD operation tests for Neo4j provider
/// </summary>
public class Neo4jBasicOperationTests : BasicOperationTestsBase, IAsyncLifetime
{
    private readonly Neo4jTestBase _testBase = new();

    public override IGraph Graph => _testBase.Graph;

    public async Task InitializeAsync() => await _testBase.InitializeAsync();

    public async Task DisposeAsync() => await _testBase.DisposeAsync();
}
